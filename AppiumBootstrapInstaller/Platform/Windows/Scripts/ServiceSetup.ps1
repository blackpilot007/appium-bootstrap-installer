# ServiceSetup.ps1
# Portable/non-admin setup for the Appium Bootstrap Agent on Windows.
# Creates a Startup shortcut that runs the agent in listen mode via VBScript wrapper.

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

Write-Log "Starting service manager setup on Windows"
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

# Setup Device Listener Agent (Non-Admin Mode)
function Setup-DeviceListenerAgent {
    Write-Log "================================================================" "INF"
    Write-Log "         SETTING UP DEVICE LISTENER AGENT (NON-ADMIN)            " "INF"
    Write-Log "================================================================" "INF"
    
    $serviceName = "AppiumBootstrapAgent"
    $exePath = "$InstallDir\AppiumBootstrapInstaller.exe"
    $configPath = "$InstallDir\config.json"
    
    if (-not (Test-Path $exePath)) {
            Write-Log "Executable not found at $exePath, attempting to copy from ExeSource: $ExeSource" "WARN"
            if ($ExeSource -and (Test-Path $ExeSource)) {
                try {
                    Copy-Item -Path $ExeSource -Destination $exePath -Force
                    Write-Log "Copied executable from $ExeSource to $exePath"
                }
                catch {
                    Write-Log "ERROR: Could not copy executable from $ExeSource to $exePath: $_" "ERR"
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
        
        # Start the agent now
        Write-Log "Starting agent now..."
        Start-Process "wscript.exe" -ArgumentList "`"$vbsPath`"" -WindowStyle Hidden
        Write-Log "Agent started successfully"
        Write-Log ""
        Write-Log "Device monitoring is now active via AppiumBootstrapInstaller.exe"
        Write-Log "The agent will automatically start on system login"
        Write-Log ""
        Write-Log "To manually start/stop the agent:"
        Write-Log "  Start: wscript.exe `"$vbsPath`""
        Write-Log "  Stop:  Stop the AppiumBootstrapInstaller process from Task Manager"
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
    Write-Log "     STARTING SERVICE MANAGER SETUP ON WINDOWS                  " "INF"
    Write-Log "================================================================" "INF"
    
    Stop-ExistingServices
    Setup-DeviceListenerAgent
    
    Write-Log "================================================================" "INF"
    Write-Log "           SERVICE MANAGER SETUP COMPLETED SUCCESSFULLY         " "INF"
    Write-Log "================================================================" "INF"
    
    exit 0
}
catch {
    Write-Log "Service manager setup failed with error: $_" "ERR"
    exit 1
}
