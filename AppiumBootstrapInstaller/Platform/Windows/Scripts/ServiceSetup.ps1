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
    # Timestamp handled by C# host logger
    Write-Host "[$Level] $Message"
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
