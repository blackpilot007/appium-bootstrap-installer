# ServiceSetup.ps1
# Optional: Creates a Windows Startup shortcut for the device listener agent.
# The agent runs as part of AppiumBootstrapInstaller.exe with --listen flag.
# This is OPTIONAL - you can also run the installer manually with --listen mode.

param(
    [string]$InstallDir = (Resolve-Path "$PSScriptRoot\..\..\..").Path,
    [string]$ExeSource = ""
)

$ErrorActionPreference = "Continue"

# Logging functions
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    if ($Level -in "WARN", "WARNING") {
        Write-Host "WARN: $Message"
    }
    elseif ($Level -in "ERR", "ERROR") {
        Write-Host "ERROR: $Message"
    }
    else {
        Write-Host "$Message"
    }
}

function Write-Success {
    param([string]$Message)
    Write-Log "================================================================" "INF"
    Write-Log "             $Message COMPLETED SUCCESSFULLY                    " "INF"
    Write-Log "================================================================" "INF"
}

Write-Log "Starting optional startup configuration on Windows"
Write-Log "Installation Directory: $InstallDir"
Write-Log "System: $(Get-WmiObject Win32_OperatingSystem | Select-Object -ExpandProperty Caption)"
Write-Log "User: $env:USERNAME"

# Stop any existing AppiumBootstrap services
function Stop-ExistingServices {
    Write-Log "Checking for existing AppiumBootstrap services..."
    
    $services = Get-Service | Where-Object { 
        $_.Name -like "AppiumBootstrap_*" -or $_.Name -like "Appium_*" 
    }
    
    if ($services) {
        foreach ($service in $services) {
            try {
                Write-Log "Stopping service: $($service.Name)"
                Stop-Service -Name $service.Name -Force -ErrorAction SilentlyContinue
                Write-Log "Stopped service: $($service.Name)"
            }
            catch {
                Write-Log "Could not stop service $($service.Name): $_" "WARN"
            }
        }
    }
}

# Configure Device Listener for Startup (Optional)
function Setup-DeviceListenerStartup {
    Write-Log "================================================================" "INF"
    Write-Log "         CONFIGURING DEVICE LISTENER FOR AUTO-START             " "INF"
    Write-Log "================================================================" "INF"
    
    $serviceName = "AppiumBootstrapAgent"
    $exePath = "$InstallDir\AppiumBootstrapInstaller.exe"
    $configPath = "$InstallDir\config.json"
    
    # Check if config file exists before setting up auto-start
    if (-not (Test-Path $configPath)) {
        Write-Log "Config file not found at $configPath" "WARN"
        Write-Log "Skipping auto-start setup. You can manually start the agent with:" "WARN"
        Write-Log "  $exePath --listen --config <path-to-config>" "WARN"
        Write-Success "AGENT SETUP (MANUAL START REQUIRED)"
        return
    }
    
    if (-not (Test-Path $exePath)) {
            Write-Log "Executable not found at $exePath, attempting to copy from ExeSource: $ExeSource" "WARN"
            if ($ExeSource -and (Test-Path $ExeSource)) {
                try {
                    Copy-Item -Path $ExeSource -Destination $exePath -Force
                    Write-Log "Copied executable from $ExeSource to $exePath"
                }
                catch {
                    $errorMsg = $_.Exception.Message
                    Write-Log "ERROR: Could not copy executable from $ExeSource to ${exePath}: $errorMsg" "ERR"
                    throw "AppiumBootstrapInstaller.exe not found and could not be copied"
                }
            }
            else {
                Write-Log "ERROR: Executable not found at $exePath and no valid ExeSource provided" "ERR"
                throw "AppiumBootstrapInstaller.exe not found"
            }
    }
    
    # Create VBScript wrapper to run hidden (no console window)
    $vbsPath = "$InstallDir\AppiumAgent.vbs"
    $vbsContent = @"
Set WshShell = CreateObject("WScript.Shell")
WshShell.Run """$exePath"" --listen --config ""$configPath""", 0, False
"@
    Set-Content -Path $vbsPath -Value $vbsContent -Encoding ASCII
    Write-Log "Created hidden startup wrapper at $vbsPath"
    
    # Create Startup Shortcut
    $startupDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
    $shortcutPath = "$startupDir\$serviceName.lnk"
    
    try {
        $wshShell = New-Object -ComObject WScript.Shell
        $shortcut = $wshShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $vbsPath
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.Description = "Appium Bootstrap Agent - Device Listener"
        $shortcut.Save()
        
        Write-Log "Created startup shortcut at $shortcutPath"
        Write-Success "AGENT SETUP (STARTUP FOLDER)"
        
        # Don't auto-start the agent during setup - it will start on next login
        # or can be started manually
        Write-Log ""
        Write-Log "Device monitoring agent has been configured for auto-start"
        Write-Log "The agent will start automatically on next system login"
        Write-Log ""
        Write-Log "To manually start the agent now:"
        Write-Log "  wscript.exe `"$vbsPath`""
        Write-Log ""
        Write-Log "To stop the agent:"
        Write-Log "  Stop the AppiumBootstrapInstaller process from Task Manager"
        Write-Log ""
    }
    catch {
        Write-Log "Failed to create startup shortcut: $_" "ERR"
        Write-Log "You can manually run the agent: $exePath --listen" "WARN"
        throw
    }
}

# Main execution
try {
    Write-Log "================================================================" "INF"
    Write-Log "     OPTIONAL: CONFIGURING AUTO-START FOR DEVICE LISTENER       " "INF"
    Write-Log "================================================================" "INF"
    
    Stop-ExistingServices
    Setup-DeviceListenerStartup
    
    Write-Log "================================================================" "INF"
    Write-Log "           STARTUP CONFIGURATION COMPLETED SUCCESSFULLY         " "INF"
    Write-Log "================================================================" "INF"
    
    exit 0
}
catch {
    Write-Log "Startup configuration failed with error: $_" "ERR"
    exit 1
}
