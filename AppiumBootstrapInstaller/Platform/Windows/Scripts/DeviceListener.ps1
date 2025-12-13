# DeviceListener.ps1
# Monitors Android and iOS device connections/disconnections
# Runs as a Windows service via NSSM

param(
    [string]$InstallDir = (Resolve-Path "$PSScriptRoot\..\..\..").Path,
    [int]$PollIntervalSeconds = 5,
    [string]$LogFile = ""
)

$ErrorActionPreference = "Continue"

# Check for go-ios installation
$script:GoIosPath = "$InstallDir\.cache\appium-device-farm\goIOS\ios\ios.exe"
$script:UseGoIosForDevices = $false

# Initialize log file
if (-not $LogFile) {
    # Use logs/ folder relative to executable location (same as Serilog installer logs)
    $executableDir = Split-Path -Parent $PSCommandPath
    # Navigate to publish folder if we're in Platform/Windows/Scripts
    if ($executableDir -match "Platform\\Windows\\Scripts") {
        $executableDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $executableDir))
    }
    $LogFile = Join-Path $executableDir "logs\DeviceListener_$(Get-Date -Format 'yyyyMMdd').log"
}

$logDir = Split-Path $LogFile -Parent
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Logging function
function Write-DeviceLog {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Write to console and log file
    Write-Host $logMessage
    Add-Content -Path $LogFile -Value $logMessage -ErrorAction SilentlyContinue
}

# Store current device states
$script:androidDevices = @{}
$script:iosDevices = @{}

# Get Android device list via ADB
function Get-AndroidDevices {
    try {
        $adbPath = Get-Command adb -ErrorAction SilentlyContinue
        if (-not $adbPath) {
            return @()
        }
        
        $output = & adb devices 2>&1 | Out-String
        $devices = @()
        
        # Parse ADB output (skip header line)
        $lines = $output -split "`n" | Select-Object -Skip 1
        foreach ($line in $lines) {
            $line = $line.Trim()
            if ($line -and $line -notmatch "^List of devices" -and $line -notmatch "^\*") {
                # Format: <serial>\t<state>
                $parts = $line -split "\s+"
                if ($parts.Count -ge 2) {
                    $serial = $parts[0]
                    $state = $parts[1]
                    
                    if ($state -in @("device", "emulator")) {
                        $devices += @{
                            Serial = $serial
                            State = $state
                            Type = if ($serial -match "emulator") { "Emulator" } else { "Device" }
                        }
                    }
                }
            }
        }
        
        return $devices
    }
    catch {
        Write-DeviceLog "Error getting Android devices: $_" "ERROR"
        return @()
    }
}

# Get iOS device list via libimobiledevice or go-ios (fallback)
function Get-iOSDevices {
    try {
        # Try libimobiledevice first
        $idevicePath = Get-Command idevice_id -ErrorAction SilentlyContinue
        
        if (-not $idevicePath -and (Test-Path $script:GoIosPath)) {
            # Fallback to go-ios
            if (-not $script:UseGoIosForDevices) {
                Write-DeviceLog "libimobiledevice not available, using go-ios as fallback" "INFO"
                $script:UseGoIosForDevices = $true
            }
            
            $output = & $script:GoIosPath list --details 2>&1 | Out-String
            $devices = @()
            
            # Check for device trust/pairing issues
            if ($output -match "could not retrieve PairRecord" -or $output -match "ReadPair failed") {
                Write-DeviceLog "═══════════════════════════════════════════════════════════════" "WARN"
                Write-DeviceLog "  iOS DEVICE DETECTED BUT NOT TRUSTED" "WARN"
                Write-DeviceLog "═══════════════════════════════════════════════════════════════" "WARN"
                Write-DeviceLog "Action Required:" "WARN"
                Write-DeviceLog "  1. Unlock your iPhone/iPad" "WARN"
                Write-DeviceLog "  2. Look for 'Trust This Computer?' dialog on device" "WARN"
                Write-DeviceLog "  3. Tap 'Trust' and enter device passcode" "WARN"
                Write-DeviceLog "  4. Device will appear automatically once trusted" "WARN"
                Write-DeviceLog "═══════════════════════════════════════════════════════════════" "WARN"
                return @()
            }
            
            # Parse go-ios list --details output
            # Format: UDID ModelName ProductVersion ProductName
            # Example: 00008030-001234567890001E iPhone15,2 17.0.1 iPhone 14 Pro
            $lines = $output -split "`n"
            foreach ($line in $lines) {
                $line = $line.Trim()
                if ($line -and $line -notmatch "^(DeviceList|Connected)") {
                    $parts = $line -split "\s+", 4
                    if ($parts.Count -ge 2) {
                        $udid = $parts[0]
                        $deviceName = if ($parts.Count -ge 4) { $parts[3] } else { "Unknown iOS Device" }
                        
                        $devices += @{
                            UDID = $udid
                            Name = $deviceName
                            Type = "Device"
                            Tool = "go-ios"
                        }
                    }
                }
            }
            
            return $devices
        }
        elseif (-not $idevicePath) {
            return @()
        }
        
        # Use libimobiledevice
        $output = & idevice_id -l 2>&1 | Out-String
        $devices = @()
        
        # Parse idevice_id output (one UDID per line)
        $lines = $output -split "`n"
        foreach ($line in $lines) {
            $line = $line.Trim()
            if ($line -and $line -match "^[a-fA-F0-9\-]{25,}$") {
                $udid = $line
                
                # Try to get device name
                $deviceName = "Unknown"
                try {
                    $nameOutput = & ideviceinfo -u $udid -k DeviceName 2>&1
                    if ($LASTEXITCODE -eq 0 -and $nameOutput) {
                        $deviceName = $nameOutput.Trim()
                    }
                }
                catch {
                    # Ignore errors, keep "Unknown"
                }
                
                $devices += @{
                    UDID = $udid
                    Name = $deviceName
                    Type = "Device"
                    Tool = "libimobiledevice"
                }
            }
        }
        
        return $devices
    }
    catch {
        Write-DeviceLog "Error getting iOS devices: $_" "ERROR"
        return @()
    }
}

# Handle device connection
function On-DeviceConnected {
    param(
        [string]$Platform,
        [hashtable]$Device
    )
    
    if ($Platform -eq "Android") {
        $serial = $Device.Serial
        $type = $Device.Type
        Write-DeviceLog "Android $type connected: $serial" "INFO"
        
        # Log where to find Appium service logs
        $executableDir = Split-Path -Parent $PSCommandPath
        if ($executableDir -match "Platform\\Windows\\Scripts") {
            $executableDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $executableDir))
        }
        $logDir = Join-Path $executableDir "logs"
        Write-DeviceLog "Service logs will be at: $logDir\Appium-$serial`_stdout.log" "INFO"
    }
    elseif ($Platform -eq "iOS") {
        $udid = $Device.UDID
        $name = $Device.Name
        Write-DeviceLog "iOS Device connected: $name ($udid)" "INFO"
        
        # Log where to find Appium service logs
        $executableDir = Split-Path -Parent $PSCommandPath
        if ($executableDir -match "Platform\\Windows\\Scripts") {
            $executableDir = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $executableDir))
        }
        $logDir = Join-Path $executableDir "logs"
        Write-DeviceLog "Service logs will be at: $logDir\Appium-$udid`_stdout.log" "INFO"
    }
}

# Handle device disconnection
function On-DeviceDisconnected {
    param(
        [string]$Platform,
        [string]$DeviceId
    )
    
    if ($Platform -eq "Android") {
        Write-DeviceLog "Android device disconnected: $DeviceId" "INFO"
    }
    elseif ($Platform -eq "iOS") {
        Write-DeviceLog "iOS device disconnected: $DeviceId" "INFO"
    }
}

# Monitor devices
function Monitor-Devices {
    Write-DeviceLog "Device listener started" "INFO"
    Write-DeviceLog "Install Directory: $InstallDir" "INFO"
    Write-DeviceLog "Poll Interval: $PollIntervalSeconds seconds" "INFO"
    Write-DeviceLog "Log File: $LogFile" "INFO"
    
    # Check if tools are available
    $adbAvailable = $null -ne (Get-Command adb -ErrorAction SilentlyContinue)
    $ideviceAvailable = $null -ne (Get-Command idevice_id -ErrorAction SilentlyContinue)
    
    # Check for go-ios fallback
    if (-not $ideviceAvailable -and (Test-Path $script:GoIosPath)) {
        Write-DeviceLog "libimobiledevice not available, will use go-ios as fallback" "INFO"
        $script:UseGoIosForDevices = $true
        $ideviceAvailable = $true
    }
    
    Write-DeviceLog "ADB Available: $adbAvailable" "INFO"
    Write-DeviceLog "iOS tools Available: $ideviceAvailable (using $(if ($script:UseGoIosForDevices) { 'go-ios' } else { 'libimobiledevice' }))" "INFO"
    
    if (-not $adbAvailable -and -not $ideviceAvailable) {
        Write-DeviceLog "No device monitoring tools available. Exiting." "ERROR"
        return
    }
    
    Write-DeviceLog "Monitoring for device connections..." "INFO"
    
    while ($true) {
        try {
            # Monitor Android devices
            if ($adbAvailable) {
                $currentAndroid = Get-AndroidDevices
                $currentSerials = @($currentAndroid | ForEach-Object { $_.Serial })
                $previousSerials = @($script:androidDevices.Keys)
                
                # Detect new devices
                foreach ($device in $currentAndroid) {
                    $serial = $device.Serial
                    if (-not $script:androidDevices.ContainsKey($serial)) {
                        $script:androidDevices[$serial] = $device
                        On-DeviceConnected -Platform "Android" -Device $device
                    }
                }
                
                # Detect disconnected devices
                foreach ($serial in $previousSerials) {
                    if ($serial -notin $currentSerials) {
                        $script:androidDevices.Remove($serial)
                        On-DeviceDisconnected -Platform "Android" -DeviceId $serial
                    }
                }
            }
            
            # Monitor iOS devices
            if ($ideviceAvailable) {
                $currentIOS = Get-iOSDevices
                $currentUDIDs = @($currentIOS | ForEach-Object { $_.UDID })
                $previousUDIDs = @($script:iosDevices.Keys)
                
                # Detect new devices
                foreach ($device in $currentIOS) {
                    $udid = $device.UDID
                    if (-not $script:iosDevices.ContainsKey($udid)) {
                        $script:iosDevices[$udid] = $device
                        On-DeviceConnected -Platform "iOS" -Device $device
                    }
                }
                
                # Detect disconnected devices
                foreach ($udid in $previousUDIDs) {
                    if ($udid -notin $currentUDIDs) {
                        $script:iosDevices.Remove($udid)
                        On-DeviceDisconnected -Platform "iOS" -DeviceId $udid
                    }
                }
            }
            
            # Sleep before next poll
            Start-Sleep -Seconds $PollIntervalSeconds
        }
        catch {
            Write-DeviceLog "Error in monitoring loop: $_" "ERROR"
            Start-Sleep -Seconds $PollIntervalSeconds
        }
    }
}

# Handle Ctrl+C gracefully
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
    Write-DeviceLog "Device listener stopping..." "INFO"
}

# Start monitoring
Monitor-Devices
