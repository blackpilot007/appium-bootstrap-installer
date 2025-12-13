# ServiceSetup.ps1
# Sets up Servy for managing Appium services on Windows
# Equivalent to SupervisorSetup.sh for macOS

param(
    [string]$InstallDir = (Resolve-Path "$PSScriptRoot\..\..\..").Path
)

$ErrorActionPreference = "Continue"

# Logging functions
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    # Timestamp and Level (for INFO) handled by C# host logger
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

function Write-ErrorMessage {
    param([string]$Message)
    Write-Log "================================================================" "ERR"
    Write-Log "                 $Message FAILED                                " "ERR"
    Write-Log "================================================================" "ERR"
}

# Note: Servy service installation typically requires admin privileges
# However, for user-directory installations, we can skip service setup
# Users can manually set up services if needed with admin privileges later

Write-Log "Starting service manager setup on Windows"
Write-Log "Installation Directory: $InstallDir"
Write-Log "System: $(Get-WmiObject Win32_OperatingSystem | Select-Object -ExpandProperty Caption)"
Write-Log "User: $env:USERNAME"

# Create directories
$serviceDir = "$InstallDir\services"
$activeDir = "$serviceDir\Active"
# Note: We do NOT create logs dir here - it's managed by the executable in its own directory
$servyDir = "$InstallDir\servy"

foreach ($dir in @($InstallDir, $serviceDir, $activeDir, $servyDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Log "Created directory: $dir"
    }
}

# Install Servy
function Install-Servy {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING SERVY INSTALLATION                       " "INF"
    Write-Log "================================================================" "INF"
    
    # Check if running as administrator
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    
    if (-not $isAdmin) {
        Write-Log "Running in non-admin mode. Skipping Servy installation." "WARN"
        Write-Log "Windows services require administrator privileges." "WARN"
        Write-Log "Run installer as Administrator to enable service management." "WARN"
        return
    }
    
    # Check if Servy is already installed globally
    $globalServyPath = "C:\Program Files\Servy\servy-cli.exe"
    if (Test-Path $globalServyPath) {
        Write-Log "Servy is already installed at $globalServyPath"
        Write-Success "SERVY ALREADY INSTALLED (GLOBAL)"
        return
    }
    
    $servyExe = "$servyDir\servy-cli.exe"
    
    if (Test-Path $servyExe) {
        Write-Log "Servy is already installed at $servyDir"
        Write-Success "SERVY ALREADY INSTALLED (LOCAL)"
        return
    }
    
    Write-Log "Installing Servy using Chocolatey..."
    Write-Log "Note: Servy provides health monitoring, log rotation, and no GUI prompts"
    
    try {
        # Try to install Servy via Chocolatey
        $chocoPath = "$InstallDir\chocolatey\bin\choco.exe"
        if (Test-Path $chocoPath) {
            Write-Log "Installing Servy via Chocolatey..."
            & $chocoPath install servy -y --no-progress --force 2>&1 | ForEach-Object { Write-Log $_ }
            
            # Check if installed globally
            if (Test-Path $globalServyPath) {
                Write-Log "Servy installed successfully at $globalServyPath"
                Write-Success "SERVY INSTALLATION"
                return
            }
        }
        
        # Fallback: Download portable version
        Write-Log "Downloading Servy portable version..."
        $servyRelease = "https://api.github.com/repos/aelassas/servy/releases/latest"
        
        $release = Invoke-RestMethod -Uri $servyRelease -UseBasicParsing
        $asset = $release.assets | Where-Object { $_.name -like "*servy-cli*.zip" } | Select-Object -First 1
        
        if ($asset) {
            $servyZip = "$env:TEMP\servy-cli.zip"
            Write-Log "Downloading from $($asset.browser_download_url)..."
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $servyZip -UseBasicParsing
            
            # Extract Servy
            Write-Log "Extracting Servy..."
            Expand-Archive -Path $servyZip -DestinationPath $servyDir -Force
            
            if (Test-Path $servyExe) {
                Write-Log "Servy installed successfully at $servyExe"
            }
            else {
                throw "Servy executable not found after extraction"
            }
            
            # Clean up
            Remove-Item -Path $servyZip -Force -ErrorAction SilentlyContinue
            
            Write-Success "SERVY INSTALLATION"
        }
        else {
            throw "Could not find Servy CLI download"
        }
    }
    catch {
        Write-Log "WARN: Servy installation failed: $_" "WARN"
        Write-Log "WARN: Services will use direct process execution instead" "WARN"
        Write-Log "WARN: For best results, install Servy manually from: https://github.com/aelassas/servy" "WARN"
    }
}

# Create placeholder service configuration
function New-PlaceholderConfig {
    Write-Log "Creating placeholder service configuration..."
    
    $placeholderScript = "$activeDir\placeholder.ps1"
    $placeholderContent = @"
# Placeholder service script
# This is a dummy script that does nothing
# Replace with actual service scripts

Write-Host "Placeholder service running"
Start-Sleep -Seconds 60
"@
    
    Set-Content -Path $placeholderScript -Value $placeholderContent
    Write-Log "Created placeholder script at $placeholderScript"
}

# Setup Device Listener Service
function Setup-DeviceListenerService {
    Write-Log "================================================================" "INF"
    Write-Log "         SETTING UP DEVICE LISTENER SERVICE                      " "INF"
    Write-Log "================================================================" "INF"
    
    # Check if running as administrator
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    $isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    
    if (-not $isAdmin) {
        Write-Log "================================================================" "WARN"
        Write-Log "    WINDOWS SERVICES REQUIRE ADMINISTRATOR PRIVILEGES           " "WARN"
        Write-Log "================================================================" "WARN"
        Write-Log "The device listener service cannot be installed without admin rights." "WARN"
        Write-Log "Two options:" "WARN"
        Write-Log "  1. Run this installer as Administrator (right-click -> Run as Administrator)" "WARN"
        Write-Log "  2. Continue without service - you can manually start device monitoring later" "WARN"
        Write-Log "" "WARN"
        Write-Log "Skipping service installation (non-admin mode)" "WARN"
        Write-Log "================================================================" "WARN"
        return
    }
    
    $serviceName = "AppiumBootstrap_DeviceListener"
    $deviceListenerScript = "$InstallDir\Platform\Windows\Scripts\DeviceListener.ps1"
    
    # Find Servy CLI
    $servyCli = "C:\Program Files\Servy\servy-cli.exe"
    if (-not (Test-Path $servyCli)) {
        $servyCli = "$servyDir\servy-cli.exe"
        if (-not (Test-Path $servyCli)) {
            Write-Log "Servy CLI not found. Skipping service setup." "WARN"
            Write-Log "Services will be managed by the installer executable directly" "WARN"
            return
        }
    }
    
    if (-not (Test-Path $deviceListenerScript)) {
        Write-Log "Device listener script not found at $deviceListenerScript" "WARN"
        Write-Log "Skipping device listener service setup" "WARN"
        return
    }
    
    # Check if service already exists
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Log "Service $serviceName already exists. Removing..." "WARN"
        try {
            & $servyCli stop --quiet --name="$serviceName" 2>&1 | Out-Null
            Start-Sleep -Seconds 2
            & $servyCli uninstall --quiet --name="$serviceName" 2>&1 | Out-Null
            Write-Log "Removed existing service"
        }
        catch {
            Write-Log "Error removing existing service: $_" "ERR"
        }
    }
    
    try {
        # Configure service for logging to executable's logs folder (same as Serilog installer logs)
        $executablePath = Get-Process -Id $PID | Select-Object -ExpandProperty Path
        $executableDir = Split-Path -Parent $executablePath
        $logsDir = Join-Path $executableDir "logs"
        if (-not (Test-Path $logsDir)) {
            New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        }
        
        # Install service with Servy (includes health monitoring and log rotation)
        Write-Log "Installing device listener service with Servy..."
        Write-Log "Features: Health monitoring, automatic restart, log rotation"
        
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$deviceListenerScript`" -InstallDir `"$InstallDir`" -PollIntervalSeconds 5"
        
        & $servyCli install --quiet `
            --name="$serviceName" `
            --displayName="Appium Bootstrap Device Listener" `
            --description="Monitors Android and iOS device connections for Appium testing" `
            --path="powershell.exe" `
            --startupDir="$InstallDir" `
            --params="$arguments" `
            --startupType=Automatic `
            --priority=Normal `
            --stdout="$logsDir\DeviceListener_stdout.log" `
            --stderr="$logsDir\DeviceListener_stderr.log" `
            --enableSizeRotation `
            --rotationSize=10 `
            --maxRotations=5 `
            --enableHealth `
            --heartbeatInterval=60 `
            --maxFailedChecks=3 `
            --recoveryAction=RestartProcess `
            --maxRestartAttempts=5
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install service. Servy CLI exit code: $LASTEXITCODE"
        }
        
        Write-Log "Service installed successfully with health monitoring"
        
        # Start the service
        Write-Log "Starting device listener service..."
        & $servyCli start --quiet --name="$serviceName"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "DEVICE LISTENER SERVICE SETUP"
            Write-Log ""
            Write-Log "Service Name: $serviceName"
            Write-Log "Service Status: Running"
            Write-Log "Health Monitoring: Enabled (checks every 60s)"
            Write-Log "Auto-Restart: Enabled (max 5 attempts)"
            Write-Log "Log Rotation: Enabled (10 MB, keep 5 files)"
            Write-Log "Log Files:"
            Write-Log "  - $logsDir\DeviceListener_stdout.log"
            Write-Log "  - $logsDir\DeviceListener_stderr.log"
            Write-Log ""
            Write-Log "Service Management Commands:"
            Write-Log "  Start:   servy-cli start --name=`"$serviceName`""
            Write-Log "  Stop:    servy-cli stop --name=`"$serviceName`""
            Write-Log "  Restart: servy-cli restart --name=`"$serviceName`""
            Write-Log "  Status:  servy-cli status --name=`"$serviceName`""
            Write-Log "  Remove:  servy-cli uninstall --name=`"$serviceName`""
        }
        else {
            Write-Log "Warning: Service installed but failed to start (exit code: $LASTEXITCODE)" "WARN"
            Write-Log "You may need administrator privileges to start the service" "WARN"
            Write-Log "Try manually: servy-cli start --name=`"$serviceName`"" "WARN"
        }
    }
    catch {
        Write-ErrorMessage "DEVICE LISTENER SERVICE SETUP"
        Write-Log "Error: $_" "ERR"
        Write-Log "The device listener service could not be set up" "ERR"
        Write-Log "You can manually set it up later using Servy CLI" "ERR"
    }
}

# Verify Servy setup
function Test-ServySetup {
    Write-Log "================================================================" "INF"
    Write-Log "           VERIFYING SERVY SETUP                                 " "INF"
    Write-Log "================================================================" "INF"
    
    $servyCli = "C:\Program Files\Servy\servy-cli.exe"
    if (-not (Test-Path $servyCli)) {
        $servyCli = "$servyDir\servy-cli.exe"
    }
    
    if (Test-Path $servyCli) {
        Write-Log "Servy CLI found at $servyCli"
        
        try {
            $version = & $servyCli version 2>&1 | Select-Object -First 1
            Write-Log "Servy version: $version"
            Write-Log "Servy is ready to manage Windows services"
            Write-Success "SERVY SETUP VERIFICATION"
            return $true
        }
        catch {
            Write-Log "Error executing Servy CLI: $_" "ERR"
            return $false
        }
    }
    else {
        Write-Log "Servy CLI executable not found" "ERR"
        Write-Log "Service management will be handled by the installer executable" "WARN"
        return $false
    }
}

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
    else {
        Write-Log "No existing AppiumBootstrap services found"
    }
}

# Main execution
try {
    Write-Log "================================================================" "INF"
    Write-Log "     STARTING SERVICE MANAGER SETUP ON WINDOWS                  " "INF"
    Write-Log "================================================================" "INF"
    
    Stop-ExistingServices
    Install-Servy
    New-PlaceholderConfig
    Setup-DeviceListenerService
    
    if (Test-ServySetup) {
        Write-Log "================================================================" "INF"
        Write-Log "           SERVICE MANAGER SETUP COMPLETED SUCCESSFULLY         " "INF"
        Write-Log "================================================================" "INF"
        Write-Log ""
        Write-Log "Servy has been installed and configured successfully."
        Write-Log ""
        Write-Log "Features enabled:"
        Write-Log "  - Health monitoring with automatic restart"
        Write-Log "  - Log rotation (10 MB, keep 5 files)"
        Write-Log "  - No GUI prompts (fully scriptable)"
        Write-Log ""
        Write-Log "To create a new service, use:"
        Write-Log "  servy-cli install --name=ServiceName --path=`"C:\path\to\executable.exe`""
        Write-Log ""
        Write-Log "To manage services:"
        Write-Log "  servy-cli start --name=ServiceName"
        Write-Log "  servy-cli stop --name=ServiceName"
        Write-Log "  servy-cli restart --name=ServiceName"
        Write-Log "  servy-cli status --name=ServiceName"
        Write-Log "  servy-cli uninstall --name=ServiceName"
        Write-Log ""
        Write-Log "Service configuration directory: $serviceDir"
        Write-Log "================================================================" "INF"
    }
    else {
        Write-Log "Service manager setup completed (Servy not available)" "WARN"
        Write-Log "Services will be managed by the installer executable directly" "WARN"
    }
}
catch {
    Write-Log "Service manager setup failed with error: $_" "ERR"
    exit 1
}
