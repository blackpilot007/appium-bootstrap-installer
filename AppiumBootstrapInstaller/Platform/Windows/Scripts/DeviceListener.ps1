# DeviceListener.ps1
# Monitors Android and iOS device connections/disconnections
# Runs as a Windows service via NSSM

param(
    [string]$InstallDir = (Resolve-Path "$PSScriptRoot\..\..\..").Path,
    [int]$PollIntervalSeconds = 5,
    [string]$LogFile = ""
)

$ErrorActionPreference = "Continue"

# Initialize log file
if (-not $LogFile) {
    $LogFile = "$InstallDir\services\logs\DeviceListener_$(Get-Date -Format 'yyyyMMdd').log"
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

# Get iOS device list via idevice_id
function Get-iOSDevices {
    try {
        $idevicePath = Get-Command idevice_id -ErrorAction SilentlyContinue
        if (-not $idevicePath) {
            return @()
        }
        
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
        
        # Optional: Trigger custom actions here
        # Example: Start Appium session, configure device, etc.
    }
    elseif ($Platform -eq "iOS") {
        $udid = $Device.UDID
        $name = $Device.Name
        Write-DeviceLog "iOS Device connected: $name ($udid)" "INFO"
        
        # Optional: Trigger custom actions here
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
    
    Write-DeviceLog "ADB Available: $adbAvailable" "INFO"
    Write-DeviceLog "idevice_id Available: $ideviceAvailable" "INFO"
    
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
