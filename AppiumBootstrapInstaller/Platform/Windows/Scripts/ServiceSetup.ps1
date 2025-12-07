# ServiceSetup.ps1
# Sets up NSSM (Non-Sucking Service Manager) for managing Appium services on Windows
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

# Note: NSSM service installation typically requires admin privileges
# However, for user-directory installations, we can skip service setup
# Users can manually set up services if needed with admin privileges later

Write-Log "Starting service manager setup on Windows"
Write-Log "Installation Directory: $InstallDir"
Write-Log "System: $(Get-WmiObject Win32_OperatingSystem | Select-Object -ExpandProperty Caption)"
Write-Log "User: $env:USERNAME"

# Create directories
$serviceDir = "$InstallDir\services"
$activeDir = "$serviceDir\Active"
$logsDir = "$serviceDir\logs"
$nssmDir = "$InstallDir\nssm"

foreach ($dir in @($InstallDir, $serviceDir, $activeDir, $logsDir, $nssmDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Log "Created directory: $dir"
    }
}

# Install NSSM
function Install-NSSM {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING NSSM INSTALLATION                       " "INF"
    Write-Log "================================================================" "INF"
    
    $nssmExe = "$nssmDir\nssm.exe"
    
    if (Test-Path $nssmExe) {
        Write-Log "NSSM is already installed at $nssmDir"
        # Skip version check to avoid potential popups
        # $nssmVersion = & $nssmExe version
        # Write-Log "NSSM version: $nssmVersion"
        Write-Success "NSSM ALREADY INSTALLED"
        return
    }
    
    Write-Log "Downloading NSSM (Non-Sucking Service Manager)..."
    
    try {
        # Download NSSM
        $nssmVersion = "2.24"
        $nssmUrl = "https://nssm.cc/release/nssm-$nssmVersion.zip"
        $nssmZip = "$env:TEMP\nssm.zip"
        
        Write-Log "Downloading from $nssmUrl..."
        Invoke-WebRequest -Uri $nssmUrl -OutFile $nssmZip -UseBasicParsing
        
        # Extract NSSM
        Write-Log "Extracting NSSM..."
        $extractPath = "$env:TEMP\nssm-extract"
        Expand-Archive -Path $nssmZip -DestinationPath $extractPath -Force
        
        # Determine architecture
        $arch = if ([Environment]::Is64BitOperatingSystem) { "win64" } else { "win32" }
        $nssmSourcePath = "$extractPath\nssm-$nssmVersion\$arch\nssm.exe"
        
        if (Test-Path $nssmSourcePath) {
            Copy-Item -Path $nssmSourcePath -Destination $nssmExe -Force
            Write-Log "NSSM installed successfully at $nssmExe"
        }
        else {
            throw "NSSM executable not found in extracted archive"
        }
        
        # Add to system PATH
        # Add to current session PATH only (Isolated/Portable mode)
        if ($env:Path -notlike "*$nssmDir*") {
            $env:Path += ";$nssmDir"
            Write-Log "Added NSSM directory to current session PATH"
        }
        
        # Clean up
        Remove-Item -Path $nssmZip -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $extractPath -Recurse -Force -ErrorAction SilentlyContinue
        
        Write-Success "NSSM INSTALLATION"
    }
    catch {
        Write-ErrorMessage "NSSM INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
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
    
    $serviceName = "AppiumBootstrap_DeviceListener"
    $deviceListenerScript = "$InstallDir\Platform\Windows\Scripts\DeviceListener.ps1"
    $nssmExe = "$nssmDir\nssm.exe"
    
    if (-not (Test-Path $deviceListenerScript)) {
        Write-Log "Device listener script not found at $deviceListenerScript" "WARN"
        Write-Log "Skipping device listener service setup" "WARN"
        return
    }
    
    if (-not (Test-Path $nssmExe)) {
        Write-Log "NSSM not found at $nssmExe" "ERR"
        Write-Log "Cannot setup device listener service" "ERR"
        return
    }
    
    # Check if service already exists
    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Log "Service $serviceName already exists. Removing..." "WARN"
        try {
            if ($existingService.Status -eq "Running") {
                & $nssmExe stop $serviceName
                Start-Sleep -Seconds 2
            }
            & $nssmExe remove $serviceName confirm
            Write-Log "Removed existing service"
        }
        catch {
            Write-Log "Error removing existing service: $_" "ERR"
        }
    }
    
    try {
        # Install service
        Write-Log "Installing device listener service..."
        
        $powershellExe = "powershell.exe"
        $arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$deviceListenerScript`" -InstallDir `"$InstallDir`" -PollIntervalSeconds 5"
        
        # Install the service
        & $nssmExe install $serviceName $powershellExe $arguments
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install service. NSSM exit code: $LASTEXITCODE"
        }
        
        Write-Log "Service installed successfully"
        
        # Configure service
        Write-Log "Configuring service parameters..."
        
        # Set display name
        & $nssmExe set $serviceName DisplayName "Appium Bootstrap Device Listener"
        
        # Set description
        & $nssmExe set $serviceName Description "Monitors Android and iOS device connections for Appium testing"
        
        # Set startup type to automatic
        & $nssmExe set $serviceName Start SERVICE_AUTO_START
        
        # Set working directory
        & $nssmExe set $serviceName AppDirectory "$InstallDir"
        
        # Set log output
        $logDir = "$InstallDir\services\logs"
        & $nssmExe set $serviceName AppStdout "$logDir\DeviceListener_stdout.log"
        & $nssmExe set $serviceName AppStderr "$logDir\DeviceListener_stderr.log"
        
        # Set restart behavior (restart on failure)
        & $nssmExe set $serviceName AppExit Default Restart
        & $nssmExe set $serviceName AppRestartDelay 5000
        
        Write-Log "Service configuration completed"
        
        # Start the service
        Write-Log "Starting device listener service..."
        & $nssmExe start $serviceName
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "DEVICE LISTENER SERVICE SETUP"
            Write-Log ""
            Write-Log "Service Name: $serviceName"
            Write-Log "Service Status: Running"
            Write-Log "Log Files:"
            Write-Log "  - $logDir\DeviceListener_$(Get-Date -Format 'yyyyMMdd').log"
            Write-Log "  - $logDir\DeviceListener_stdout.log"
            Write-Log "  - $logDir\DeviceListener_stderr.log"
            Write-Log ""
            Write-Log "Service Management Commands:"
            Write-Log "  Start:   nssm start $serviceName"
            Write-Log "  Stop:    nssm stop $serviceName"
            Write-Log "  Restart: nssm restart $serviceName"
            Write-Log "  Status:  nssm status $serviceName"
            Write-Log "  Remove:  nssm remove $serviceName confirm"
        }
        else {
            Write-Log "Warning: Service installed but failed to start (exit code: $LASTEXITCODE)" "WARN"
            Write-Log "You may need administrator privileges to start the service" "WARN"
            Write-Log "Try manually: nssm start $serviceName" "WARN"
        }
    }
    catch {
        Write-ErrorMessage "DEVICE LISTENER SERVICE SETUP"
        Write-Log "Error: $_" "ERR"
        Write-Log "The device listener service could not be set up" "ERR"
        Write-Log "You can manually set it up later using NSSM" "ERR"
    }
}

# Verify NSSM setup
function Test-NSSMSetup {
    Write-Log "================================================================" "INF"
    Write-Log "           VERIFYING NSSM SETUP                                 " "INF"
    Write-Log "================================================================" "INF"
    
    $nssmExe = "$nssmDir\nssm.exe"
    
    if (Test-Path $nssmExe) {
        Write-Log "NSSM executable found at $nssmExe"
        
        try {
            # Skip version check to avoid potential popups
            # $version = & $nssmExe version
            # Write-Log "NSSM version: $version"
            Write-Log "NSSM is ready to manage Windows services"
            Write-Success "NSSM SETUP VERIFICATION"
            return $true
        }
        catch {
            Write-Log "Error executing NSSM: $_" "ERR"
            return $false
        }
    }
    else {
        Write-Log "NSSM executable not found" "ERR"
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
    Install-NSSM
    New-PlaceholderConfig
    Setup-DeviceListenerService
    
    if (Test-NSSMSetup) {
        Write-Log "================================================================" "INF"
        Write-Log "           SERVICE MANAGER SETUP COMPLETED SUCCESSFULLY         " "INF"
        Write-Log "================================================================" "INF"
        Write-Log ""
        Write-Log "NSSM has been installed and configured successfully."
        Write-Log ""
        Write-Log "To create a new service, use:"
        Write-Log "  nssm install ServiceName `"C:\path\to\executable.exe`""
        Write-Log ""
        Write-Log "To manage services:"
        Write-Log "  nssm start ServiceName"
        Write-Log "  nssm stop ServiceName"
        Write-Log "  nssm restart ServiceName"
        Write-Log "  nssm remove ServiceName confirm"
        Write-Log ""
        Write-Log "Service configuration directory: $serviceDir"
        Write-Log "NSSM executable: $nssmDir\nssm.exe"
        Write-Log "================================================================" "INF"
    }
    else {
        throw "Service manager setup verification failed"
    }
}
catch {
    Write-Log "Service manager setup failed with error: $_" "ERR"
    exit 1
}
