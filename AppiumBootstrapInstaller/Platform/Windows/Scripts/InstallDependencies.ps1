# InstallDependencies.ps1
# PowerShell script to install Node.js, NVM, Appium, and drivers on Windows
# Equivalent to InstallDependencies.sh for macOS

param(
    [string]$InstallFolder = "$env:USERPROFILE\.local",
    [string]$NodeVersion = "22",
    [string]$AppiumVersion = "2.17.1",
    [string]$DriverName = "uiautomator2",
    [string]$DriverVersion = "",
    [string]$XCUITestVersion = "",
    [string]$NvmVersion = "1.1.12",
    [switch]$InstallIOSSupport,
    [switch]$InstallAndroidSupport = $true,
    [switch]$InstallXCUITest = $true,
    [switch]$InstallUiAutomator = $true,
    [switch]$InstallDeviceFarm = $true
)

# Set error action preference
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

# Note: Admin privileges are NOT required for user-directory installations
# Installing to user-writable directories like $env:USERPROFILE\.local

Write-Log "Starting prerequisites installation on Windows"
Write-Log "Installation Folder: $InstallFolder"
Write-Log "Node Version: $NodeVersion"
Write-Log "Appium Version: $AppiumVersion"
Write-Log "Driver: $DriverName@$DriverVersion"
Write-Log "NVM Version: $NvmVersion"

# Create installation directory
if (-not (Test-Path $InstallFolder)) {
    New-Item -ItemType Directory -Path $InstallFolder -Force | Out-Null
    Write-Log "Created installation directory: $InstallFolder"
}

# Install Chocolatey if not present
function Install-Chocolatey {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING CHOCOLATEY INSTALLATION                 " "INF"
    Write-Log "================================================================" "INF"
    
    if (Get-Command choco -ErrorAction SilentlyContinue) {
        Write-Log "Chocolatey is already installed"
        choco --version
        Write-Success "CHOCOLATEY ALREADY INSTALLED"
        return
    }
    
    Write-Log "Installing Chocolatey package manager locally..."
    $env:ChocolateyInstall = "$InstallFolder\chocolatey"
    $env:Path = "$env:ChocolateyInstall\bin;$env:Path"
    
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    
    try {
        Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        Write-Success "CHOCOLATEY INSTALLATION"
    }
    catch {
        # Check if local choco exists despite error (common in non-admin if global exists)
        if (Test-Path "$env:ChocolateyInstall\bin\choco.exe") {
            Write-Log "Warning: Chocolatey installer script reported an error (likely access to global ProgramData), but local binary was created." "WARN"
            Write-Success "CHOCOLATEY INSTALLATION (With Warnings)"
        }
        else {
            Write-ErrorMessage "CHOCOLATEY INSTALLATION"
            Write-Log "Error: $_" "ERR"
            throw
        }
    }
}

# Install NVM for Windows
function Install-NVM {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING NVM INSTALLATION                        " "INF"
    Write-Log "================================================================" "INF"
    
    $nvmPath = "$InstallFolder\nvm"
    $env:NVM_HOME = $nvmPath
    $env:NVM_SYMLINK = "$InstallFolder\nodejs"
    $Script:ActiveNodePath = "$InstallFolder\nodejs"

    # Create directories if they don't exist already installed
    if (Test-Path "$nvmPath\nvm.exe") {
        Write-Log "NVM is already installed at $nvmPath"
        & "$nvmPath\nvm.exe" version
        Write-Success "NVM ALREADY INSTALLED"
        return
    }
    
    Write-Log "Installing NVM for Windows version $NvmVersion..."
    
    try {
        # Download NVM for Windows
        $nvmInstallerUrl = "https://github.com/coreybutler/nvm-windows/releases/download/$NvmVersion/nvm-noinstall.zip"
        $nvmZipPath = "$env:TEMP\nvm.zip"
        
        Write-Log "Downloading NVM from $nvmInstallerUrl..."
        Invoke-WebRequest -Uri $nvmInstallerUrl -OutFile $nvmZipPath -UseBasicParsing
        
        # Extract NVM
        Write-Log "Extracting NVM to $nvmPath..."
        Expand-Archive -Path $nvmZipPath -DestinationPath $nvmPath -Force
        
        # Create settings file
        $settingsContent = @"
root: $nvmPath
path: $($env:NVM_SYMLINK)
"@
        Set-Content -Path "$nvmPath\settings.txt" -Value $settingsContent
        
        # Add to PATH
        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if ($userPath -notlike "*$nvmPath*") {
            [Environment]::SetEnvironmentVariable("Path", "$userPath;$nvmPath;$($env:NVM_SYMLINK)", "User")
        }
        
        # Update current session PATH and env vars
        $env:NVM_HOME = $nvmPath
        $env:NVM_SYMLINK = $env:NVM_SYMLINK
        $env:Path += ";$nvmPath;$($env:NVM_SYMLINK)"
        
        Write-Log "NVM installed successfully at $nvmPath"
        Write-Success "NVM INSTALLATION"
    }
    catch {
        Write-ErrorMessage "NVM INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install Node.js using NVM
function Install-NodeJS {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING NODE.JS INSTALLATION                    " "INF"
    Write-Log "================================================================" "INF"
    
    $nvmPath = "$InstallFolder\nvm"
    $nvmExe = "$nvmPath\nvm.exe"
    
    if (-not (Test-Path $nvmExe)) {
        Write-Log "NVM not found. Installing NVM first..." "WARN"
        Install-NVM
    }
    
    # Check if Node.js version is already installed
    # Use cmd /c to bypass potential terminal checks in nvm.exe
    $output = & cmd /c "$nvmExe" list
    $nodeInstalled = $output | Select-String -Pattern $NodeVersion -Quiet
    
    if ($nodeInstalled) {
        Write-Log "Node.js version $NodeVersion is already installed"
        & $nvmExe use $NodeVersion
        Write-Success "NODE.JS ALREADY INSTALLED"
        return
    }
    
    Write-Log "Installing Node.js version $NodeVersion..."
    
    try {
        # Check if version directory exists first to avoid re-download
        $existingVer = Get-ChildItem -Path $nvmPath -Directory -Filter "v$NodeVersion*" | Select-Object -First 1
        
        if (-not $existingVer) {
            # MANUAL DOWNLOAD LOGIC (Primary Strategy to avoid NVM popups)
            Write-Log "Resolving latest v$NodeVersion version from nodejs.org..."
            # Note: Using basic parsing for compatibility
            $releasesJson = Invoke-WebRequest "https://nodejs.org/dist/index.json" -UseBasicParsing
            $releases = $releasesJson.Content | ConvertFrom-Json
            $latest = $releases | Where-Object { $_.version -match "^v$NodeVersion\." } | Select-Object -First 1
             
            if (-not $latest) { throw "Could not find Node.js version $NodeVersion" }
             
            $fullVersion = $latest.version # e.g. v22.12.0
            $downloadUrl = "https://nodejs.org/dist/$fullVersion/node-$fullVersion-win-x64.zip"
            $zipPath = "$env:TEMP\node_manual.zip"
             
            Write-Log "Downloading Node.js $fullVersion from $downloadUrl..."
            Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
             
            # Target directory compatible with NVM standard
            $destDir = "$nvmPath\$fullVersion"
            if (Test-Path $destDir) { Remove-Item -Recurse -Force $destDir -ErrorAction SilentlyContinue }
             
            Write-Log "Extracting to $destDir..."
            $extractTmp = "$nvmPath\temp_extract_$((Get-Date).Ticks)"
            New-Item -ItemType Directory -Path $extractTmp -Force | Out-Null
             
            try {
                Expand-Archive -Path $zipPath -DestinationPath $extractTmp -Force
                
                # Move content (Node zip contains a root folder)
                $subFolder = Get-ChildItem $extractTmp | Select-Object -First 1
                Move-Item "$($subFolder.FullName)" $destDir
            }
            finally {
                if (Test-Path $extractTmp) { Remove-Item -Recurse -Force $extractTmp -ErrorAction SilentlyContinue }
                if (Test-Path $zipPath) { Remove-Item -Force $zipPath -ErrorAction SilentlyContinue }
            }
             
            Write-Log "Manual installation of Node.js $fullVersion completed."
        }
        
        # Verify Install Success
        $verCheck = Get-ChildItem -Path $nvmPath -Directory -Filter "v$NodeVersion*" | Select-Object -First 1
        if (-not $verCheck -or -not (Test-Path "$($verCheck.FullName)\node.exe")) { 
            throw "Node.js install failed: directory missing or empty" 
        }
        
        Write-Log "Using Node.js version $NodeVersion..."
        & cmd /c "$nvmExe" use $NodeVersion
        
        $nodePath = "$InstallFolder\nodejs"
        
        # Fallback detection if symlink creation failed (common in non-admin)
        if (-not (Test-Path "$nodePath\node.exe")) {
            Write-Log "Node.js symlink not found (likely needs Admin rights). Attempting to find versioned directory..." "WARN"
            
            # Find the actual version directory (e.g., v22.11.0)
            # nvm-windows installs versions in subdirectories like vX.Y.Z
            $versionDir = Get-ChildItem -Path $nvmPath -Directory -Filter "v$NodeVersion*" | Select-Object -First 1
            
            if ($versionDir -and (Test-Path "$($versionDir.FullName)\node.exe")) {
                $nodePath = $versionDir.FullName
                $Script:ActiveNodePath = $nodePath
                Write-Log "Found Node.js in version directory: $nodePath" "INFO"
                
                # Update PATH for this session so npm works
                $env:Path = "$nodePath;$env:Path"
            }
        }
        
        if (Test-Path "$nodePath\node.exe") {
            $nodeVersionOutput = & "$nodePath\node.exe" --version
            Write-Log "Node.js version installed: $nodeVersionOutput"
            Write-Success "NODE.JS INSTALLATION"
        }
        else {
            throw "Node.js binary not found after installation"
        }
    }
    catch {
        Write-ErrorMessage "NODE.JS INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install Appium
function Install-Appium {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING APPIUM INSTALLATION                     " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $env:APPIUM_HOME = $appiumHome
    
    # Ensure APPIUM_HOME directory exists
    if (-not (Test-Path $appiumHome)) {
        New-Item -ItemType Directory -Path $appiumHome -Force | Out-Null
        Write-Log "Created APPIUM_HOME directory at $appiumHome"
    }
    
    # Get Node and NPM paths
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    $npmPath = "$nodePath\npm.cmd"
    
    if (-not (Test-Path $npmPath)) {
        Write-Log "npm not found. Ensure Node.js is installed properly." "ERR"
        throw "npm not found"
    }
    
    Write-Log "Installing Appium $AppiumVersion..."
    
    try {
        # Initialize package.json in APPIUM_HOME
        Push-Location $appiumHome
        
        if (-not (Test-Path "package.json")) {
            & $npmPath init -y
        }
        
        # Install Appium
        Write-Log "Running: npm install appium@$AppiumVersion --prefix $appiumHome"
        & $npmPath install "appium@$AppiumVersion" --prefix $appiumHome --legacy-peer-deps
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "First attempt failed, retrying with increased timeout..." "WARN"
            & $npmPath install "appium@$AppiumVersion" --prefix $appiumHome --legacy-peer-deps --network-timeout 100000
        }
        
        Pop-Location
        
        # Verify Appium installation
        $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
        if (Test-Path $appiumExe) {
            $appiumVersionOutput = & $appiumExe --version
            Write-Log "Appium version installed: $appiumVersionOutput"
            
            # Detect major version for configuration
            $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
            
            # Create proper configuration based on Appium version
            if ($appiumMajorVersion -eq "3") {
                Write-Log "Creating Appium 3.x configuration..."
                $appiumConfigDir = "$env:USERPROFILE\.appium"
                if (-not (Test-Path $appiumConfigDir)) {
                    New-Item -ItemType Directory -Path $appiumConfigDir -Force | Out-Null
                }
                # For Appium 3.x: use extensionPaths.base (not appium_home)
                $config = @{
                    extensionPaths = @{
                        base = $appiumHome
                    }
                } | ConvertTo-Json -Depth 10
                $config | Out-File -FilePath "$appiumConfigDir\config.json" -Encoding utf8
                Write-Log "Created Appium 3.x config with extensionPaths.base"
            }
            else {
                Write-Log "Appium 2.x detected - using default configuration"
            }
            
            Write-Success "APPIUM INSTALLATION"
        }
        else {
            throw "Appium binary not found after installation"
        }
    }
    catch {
        Write-ErrorMessage "APPIUM INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install Appium Driver
function Install-AppiumDriver {
    Write-Log "================================================================" "INF"
    Write-Log "      STARTING $DriverName DRIVER $DriverVersion INSTALLATION  " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
    
    if (-not (Test-Path $appiumExe)) {
        Write-Log "Appium not found. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    
    # Detect Appium version
    $appiumVersionOutput = & $appiumExe --version
    $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
    Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
    
    Write-Log "Installing $DriverName driver version $DriverVersion..."
    
    try {
        # Uninstall existing driver
        Write-Log "Uninstalling any existing $DriverName driver..."
        & $appiumExe driver uninstall $DriverName 2>$null
        
        # Install driver using appropriate command based on version
        if ($appiumMajorVersion -eq "3") {
            Write-Log "Using 'driver add' command for Appium 3.x"
            Write-Log "Running: appium driver add $DriverName@$DriverVersion"
            & $appiumExe driver add "$DriverName@$DriverVersion"
        }
        else {
            Write-Log "Using 'driver install' command for Appium 2.x"
            Write-Log "Running: appium driver install $DriverName@$DriverVersion"
            & $appiumExe driver install "$DriverName@$DriverVersion"
        }
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Driver installation via Appium command failed, trying npm install..." "WARN"
            $npmPath = "$InstallFolder\nodejs\npm.cmd"
            $driverPackage = "appium-${DriverName}-driver"
            Push-Location $appiumHome
            & $npmPath install "${driverPackage}@$DriverVersion" --save --legacy-peer-deps
            Pop-Location
        }
        
        # Verify driver installation
        Write-Log "Verifying $DriverName driver installation..."
        $driverList = & $appiumExe driver list --installed
        Write-Log $driverList
        
        if ($driverList -match $DriverName) {
            Write-Log "$DriverName driver appears in installed drivers list"
            Write-Success "$DriverName DRIVER INSTALLATION"
        }
        else {
            # Check directory existence
            $driverDir = "$appiumHome\node_modules\appium-${DriverName}-driver"
            if (Test-Path $driverDir) {
                Write-Log "$DriverName driver directory exists at $driverDir"
                Write-Success "$DriverName DRIVER INSTALLATION"
            }
            else {
                throw "$DriverName driver not found after installation"
            }
        }
    }
    catch {
        Write-ErrorMessage "$DriverName DRIVER INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install libimobiledevice for iOS device support on Windows
function Install-LibimobileDevice {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING LIBIMOBILEDEVICE INSTALLATION               " "INF"
    Write-Log "================================================================" "INF"
    
    # Check if libimobiledevice tools are already available
    $ideviceInfoPath = Get-Command ideviceinfo -ErrorAction SilentlyContinue
    if ($ideviceInfoPath) {
        Write-Log "libimobiledevice tools are already installed and available in PATH"
        $ideviceVersion = & ideviceinfo --version 2>&1
        Write-Log $ideviceVersion
        Write-Success "LIBIMOBILEDEVICE ALREADY INSTALLED"
        return
    }
    
    Write-Log "Installing libimobiledevice using Chocolatey..."
    
    try {
        choco install libimobiledevice -y
        
        # Refresh environment variables
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        
        # Verify installation
        $ideviceInfoPath = Get-Command ideviceinfo -ErrorAction SilentlyContinue
        if ($ideviceInfoPath) {
            Write-Log "libimobiledevice installed successfully"
            $ideviceVersion = & ideviceinfo --version 2>&1
            Write-Log $ideviceVersion
            Write-Success "LIBIMOBILEDEVICE INSTALLATION"
        }
        else {
            Write-Log "libimobiledevice not found in PATH after installation. You may need to restart your terminal." "WARN"
            Write-Log "Installed location is typically: C:\ProgramData\chocolatey\lib\libimobiledevice\tools" "WARN"
        }
    }
    catch {
        Write-ErrorMessage "LIBIMOBILEDEVICE INSTALLATION"
        Write-Log "Error: $_" "ERR"
        Write-Log "You can manually install libimobiledevice from:" "WARN"
        Write-Log "https://github.com/libimobiledevice-win32/imobiledevice-net" "WARN"
    }
}

# Install iTunes drivers for iOS device connectivity on Windows
function Install-ITunesDrivers {
    Write-Log "================================================================" "INF"
    Write-Log "           CHECKING ITUNES DRIVERS FOR iOS SUPPORT              " "INF"
    Write-Log "================================================================" "INF"
    
    Write-Log "Checking for Apple Mobile Device Support..."
    
    # Check if iTunes or Apple Mobile Device Support is installed
    $itunesPath = Get-ItemProperty -Path "HKLM:\SOFTWARE\Apple Inc.\Apple Mobile Device Support" -ErrorAction SilentlyContinue
    $appleMobileDevice = Get-Service -Name "Apple Mobile Device Service" -ErrorAction SilentlyContinue
    
    if ($appleMobileDevice) {
        Write-Log "Apple Mobile Device Support is already installed"
        Write-Log "Service Status: $($appleMobileDevice.Status)"
        
        if ($appleMobileDevice.Status -ne "Running") {
            Write-Log "Starting Apple Mobile Device Service..." "WARN"
            Start-Service "Apple Mobile Device Service" -ErrorAction SilentlyContinue
        }
        
        Write-Success "ITUNES DRIVERS ALREADY INSTALLED"
        return
    }
    
    Write-Log "Apple Mobile Device Support not found." "WARN"
    Write-Log "For iOS device support on Windows, you need to install:" "WARN"
    Write-Log "1. iTunes from Microsoft Store (recommended), OR" "WARN"
    Write-Log "2. iTunes from Apple (https://www.apple.com/itunes/download/), OR" "WARN"
    Write-Log "3. Apple Mobile Device Support drivers separately" "WARN"
    Write-Log "" "WARN"
    Write-Log "Attempting to install iTunes via Chocolatey..." "INF"
    
    try {
        choco install itunes -y
        Write-Log "iTunes installation completed. Please restart your computer for drivers to take effect." "INF"
        Write-Success "ITUNES INSTALLATION"
    }
    catch {
        Write-Log "Automated iTunes installation failed." "WARN"
        Write-Log "Please install iTunes manually from Microsoft Store or Apple website." "WARN"
    }
}

# Install XCUITest driver for iOS automation
function Install-XCUITestDriver {
    Write-Log "================================================================" "INF"
    Write-Log "      STARTING XCUITEST DRIVER $XCUITestVersion INSTALLATION   " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
    
    if (-not (Test-Path $appiumExe)) {
        Write-Log "Appium not found. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    
    # Detect Appium version
    $appiumVersionOutput = & $appiumExe --version
    $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
    Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
    
    Write-Log "Installing XCUITest driver version $XCUITestVersion..."
    
    try {
        # Uninstall existing driver
        Write-Log "Uninstalling any existing XCUITest driver..."
        & $appiumExe driver uninstall xcuitest 2>$null
        
        # Install driver using appropriate command based on version
        if ($appiumMajorVersion -eq "3") {
            Write-Log "Using 'driver add' command for Appium 3.x"
            Write-Log "Running: appium driver add xcuitest@$XCUITestVersion"
            & $appiumExe driver add "xcuitest@$XCUITestVersion"
        }
        else {
            Write-Log "Using 'driver install' command for Appium 2.x"
            Write-Log "Running: appium driver install xcuitest@$XCUITestVersion"
            & $appiumExe driver install "xcuitest@$XCUITestVersion"
        }
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Plugin installation via Appium command had issues, trying npm..." "WARN"
            $npmPath = "$InstallFolder\nodejs\npm.cmd"
            Push-Location $appiumHome
            & $npmPath install "appium-xcuitest-driver@$XCUITestVersion" --save --legacy-peer-deps
            Pop-Location
        }
        
        # Verify driver installation
        Write-Log "Verifying XCUITest driver installation..."
        $driverList = & $appiumExe plugin list --installed
        Write-Log $driverList
        
        if ($driverList -match "xcuitest") {
            Write-Log "XCUITest driver appears in installed drivers list"
            Write-Success "XCUITEST DRIVER INSTALLATION"
        }
        else {
            # Check directory existence
            $driverDir = "$appiumHome\node_modules\appium-xcuitest-driver"
            if (Test-Path $driverDir) {
                Write-Log "XCUITest driver directory exists at $driverDir"
                Write-Success "XCUITEST DRIVER INSTALLATION"
            }
            else {
                throw "XCUITest driver not found after installation"
            }
        }
    }
    catch {
        Write-ErrorMessage "XCUITEST DRIVER INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install Android SDK Platform Tools (includes ADB)
function Install-AndroidSDK {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING ANDROID SDK PLATFORM TOOLS INSTALLATION    " "INF"
    Write-Log "================================================================" "INF"
    
    # Check if ADB is already available
    $adbPath = Get-Command adb -ErrorAction SilentlyContinue
    if ($adbPath) {
        Write-Log "ADB is already installed and available in PATH"
        $adbVersion = & adb version
        Write-Log $adbVersion
        Write-Success "ANDROID SDK ALREADY INSTALLED"
        return
    }
    
    Write-Log "Installing Android SDK Platform Tools using Chocolatey..."
    
    try {
        choco install adb -y
        
        # Refresh environment variables
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        
        # Verify installation
        $adbPath = Get-Command adb -ErrorAction SilentlyContinue
        if ($adbPath) {
            Write-Log "ADB installed successfully"
            $adbVersion = & adb version
            Write-Log $adbVersion
            Write-Success "ANDROID SDK INSTALLATION"
        }
        else {
            Write-Log "ADB not found in PATH after installation. You may need to restart your terminal." "WARN"
        }
    }
    catch {
        Write-ErrorMessage "ANDROID SDK INSTALLATION"
        Write-Log "Error: $_" "ERR"
        Write-Log "You can manually install Android SDK Platform Tools from:" "WARN"
        Write-Log "https://developer.android.com/studio/releases/platform-tools" "WARN"
    }
}

# Install go-ios for iOS real device support
function Install-GoIos {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING GO-IOS INSTALLATION                         " "INF"
    Write-Log "================================================================" "INF"
    
    $goIosVersion = "v1.0.182"
    $goIosDir = "$InstallFolder\.cache\appium-device-farm\goIOS"
    $goIosBin = "$goIosDir\ios\ios.exe"
    
    # Check if go-ios is already installed
    if (Test-Path $goIosBin) {
        Write-Log "go-ios is already installed at $goIosBin"
        try {
            $version = & $goIosBin version 2>&1
            Write-Log "go-ios version: $version"
            Write-Success "GO-IOS ALREADY INSTALLED"
            return
        }
        catch {
            Write-Log "Existing go-ios binary appears corrupt, reinstalling..." "WARN"
        }
    }
    
    Write-Log "Installing go-ios $goIosVersion for Windows..."
    
    try {
        # Create directory structure
        if (-not (Test-Path $goIosDir)) {
            New-Item -ItemType Directory -Path $goIosDir -Force | Out-Null
            Write-Log "Created directory: $goIosDir"
        }
        
        $iosDir = "$goIosDir\ios"
        if (-not (Test-Path $iosDir)) {
            New-Item -ItemType Directory -Path $iosDir -Force | Out-Null
        }
        
        # Download go-ios Windows binary
        $downloadUrl = "https://github.com/danielpaulus/go-ios/releases/download/$goIosVersion/go-ios-windows.zip"
        $zipPath = "$env:TEMP\go-ios-windows.zip"
        
        Write-Log "Downloading go-ios from $downloadUrl..."
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
        
        # Extract zip
        Write-Log "Extracting go-ios to $iosDir..."
        Expand-Archive -Path $zipPath -DestinationPath $iosDir -Force
        
        # Verify installation
        if (Test-Path $goIosBin) {
            $version = & $goIosBin version 2>&1
            Write-Log "go-ios installed successfully: $version"
            
            # Create environment file for device-farm
            $envFile = "$InstallFolder\.go_ios_env"
            $envContent = "GO_IOS=$goIosBin"
            Set-Content -Path $envFile -Value $envContent
            Write-Log "Created go-ios environment file at $envFile"
            
            Write-Success "GO-IOS INSTALLATION"
        }
        else {
            throw "go-ios binary not found after extraction"
        }
        
        # Clean up
        Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-ErrorMessage "GO-IOS INSTALLATION"
        Write-Log "Error: $_" "ERR"
        Write-Log "go-ios installation failed, but this is not critical." "WARN"
        Write-Log "Device-farm will work with simulators only." "WARN"
    }
}

# Install Appium Device Farm plugin and related plugins
function Install-DeviceFarm {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING DEVICE FARM PLUGIN INSTALLATION             " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
    
    if (-not (Test-Path $appiumExe)) {
        Write-Log "Appium not found. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    $env:HOME = $InstallFolder
    $env:XDG_CACHE_HOME = "$InstallFolder\.cache"
    
    Write-Log "Installing device-farm, appium-dashboard, and inspector plugins..."
    Write-Log "Using APPIUM_HOME: $appiumHome"
    Write-Log "Using HOME: $InstallFolder"
    Write-Log "Using XDG_CACHE_HOME: $env:XDG_CACHE_HOME"
    
    try {
        # Detect Appium version
        $appiumVersionOutput = & $appiumExe --version
        $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
        Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
        
        $plugins = @(
            @{Name = "device-farm"; Version = "8.3.5" },
            @{Name = "appium-dashboard"; Version = "2.0.3" },
            @{Name = "inspector"; Version = "2025.3.1" }
        )
        
        foreach ($plugin in $plugins) {
            $pluginName = $plugin.Name
            $pluginVersion = $plugin.Version
            
            Write-Log "Installing $pluginName@$pluginVersion..."
            
            # Uninstall existing plugin
            & $appiumExe plugin uninstall $pluginName 2>$null
            
            # Install plugin using appropriate command based on version
            if ($appiumMajorVersion -eq "3") {
                Write-Log "Using 'plugin add' command for Appium 3.x"
                & $appiumExe plugin add "$pluginName@$pluginVersion"
            }
            else {
                Write-Log "Using 'plugin install' command for Appium 2.x"
                & $appiumExe plugin install "$pluginName@$pluginVersion"
            }
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Plugin installation via Appium command had issues, trying npm..." "WARN"
                $npmPath = "$InstallFolder\nodejs\npm.cmd"
                Push-Location $appiumHome
                & $npmPath install "$pluginName@$pluginVersion" --save --legacy-peer-deps
                Pop-Location
            }
            
            Write-Log "$pluginName@$pluginVersion installed"
        }
        
        # Verify plugin installations
        Write-Log "Verifying plugin installations..."
        $pluginList = & $appiumExe plugin list --installed
        Write-Log $pluginList
        
        # Install go-ios for real device support
        Write-Log "Installing go-ios for iOS real device support..."
        Install-GoIos
        
        Write-Success "DEVICE FARM PLUGIN INSTALLATION"
    }
    catch {
        Write-ErrorMessage "DEVICE FARM PLUGIN INSTALLATION"
        Write-Log "Error: $_" "ERR"
        Write-Log "Device-farm plugin installation failed" "WARN"
    }
}

# Create wrapper scripts
function Create-WrapperScripts {
    Write-Log "================================================================" "INF"
    Write-Log "           CREATING ENVIRONMENT SCRIPTS                         " "INF"
    Write-Log "================================================================" "INF"
    
    $binDir = "$InstallFolder\bin"
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumHome = "$InstallFolder\appium-home"
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    
    # Create appium.bat wrapper
    $appiumBatContent = @"
@echo off
REM Appium wrapper script for Windows
set APPIUM_HOME=$appiumHome
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    
    # Determine Appium main script path
    $appiumPkgJson = "$appiumHome\node_modules\appium\package.json"
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js" # Default
    if (Test-Path $appiumPkgJson) {
        try {
            $pkg = Get-Content $appiumPkgJson -Raw | ConvertFrom-Json
            if ($pkg.bin.appium) {
                $appiumScript = Join-Path "$appiumHome\node_modules\appium" $pkg.bin.appium
            }
        } catch { Write-Log "Warning: could not parse appium package.json" "WARN" }
    }

    # appium.bat - Main wrapper
    # We use explicit node path to support isolated/portable mode
    $appiumContent = @"
@ECHO OFF
SETLOCAL
SET APPIUM_HOME=$appiumHome
"$nodePath\node.exe" "$appiumScript" %*
"@
    
    Set-Content -Path "$InstallFolder\appium.bat" -Value $appiumContent
    Write-Log "Created appium.bat wrapper at $InstallFolder\appium.bat"
    
    # appium-env.bat - Setup environment
    $appiumEnvContent = @"
@ECHO OFF
SET APPIUM_HOME=$appiumHome
SET PATH=$nodePath;$binDir;%PATH%

echo Appium environment activated:
echo APPIUM_HOME=%APPIUM_HOME%
echo NODE_VERSION=
"$nodePath\node.exe" --version
echo NPM_VERSION=
"$nodePath\npm.cmd" --version
echo APPIUM_VERSION=
"$nodePath\node.exe" "$appiumScript" --version
"@
    
    Set-Content -Path "$InstallFolder\appium-env.bat" -Value $appiumEnvContent
    Write-Log "Created appium-env.bat at $InstallFolder\appium-env.bat"
    
    Write-Success "ENVIRONMENT SCRIPTS CREATION"
}

# Verify installations
function Verify-Installations {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING INSTALLATION VERIFICATION                   " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $nodePath = "$InstallFolder\nodejs"
    $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
    
    Write-Log "Environment variables:"
    Write-Log "- APPIUM_HOME: $appiumHome"
    Write-Log "- Node Path: $($Script:ActiveNodePath)"
    
    # Check Node.js
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    if (Test-Path "$nodePath\node.exe") {
        $nodeVer = & "$nodePath\node.exe" --version
        $npmVer = & "$nodePath\npm.cmd" --version
        Write-Log "Node.js version: $nodeVer"
        Write-Log "NPM version: $npmVer"
    }
    else {
        Write-Log "Warning: Node.js not found at expected path" "WARN"
    }
    
    # Check Appium
    # Check Appium
    # Determine Appium main script path again for verification
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    if (Test-Path "$appiumHome\node_modules\appium\package.json") {
        try {
            $pkg = Get-Content "$appiumHome\node_modules\appium\package.json" -Raw | ConvertFrom-Json
            if ($pkg.bin.appium) {
                $appiumScript = Join-Path "$appiumHome\node_modules\appium" $pkg.bin.appium
            }
        }
        catch {}
    }

    if (Test-Path $appiumScript) {
        $env:APPIUM_HOME = $appiumHome
        # Call node directly with the script
        $appiumVer = & "$($Script:ActiveNodePath)\node.exe" "$appiumScript" --version
        Write-Log "Appium version: $appiumVer"
        
        # List installed drivers
        Write-Log "Installed drivers:"
        & "$($Script:ActiveNodePath)\node.exe" "$appiumScript" driver list --installed
    }
    else {
        Write-Log "Warning: Appium not found at expected path" "WARN"
    }
    
    # Check driver
    $driverDir = "$appiumHome\node_modules\appium-${DriverName}-driver"
    if (Test-Path $driverDir) {
        Write-Log "$DriverName driver directory exists"
        $packageJson = "$driverDir\package.json"
        if (Test-Path $packageJson) {
            $packageContent = Get-Content $packageJson -Raw | ConvertFrom-Json
            Write-Log "$DriverName driver version: $($packageContent.version)"
        }
    }
    else {
        Write-Log "Warning: $DriverName driver directory not found" "WARN"
    }
    
    # Check platform-specific tools
    if ($InstallAndroidSupport) {
        # Check ADB
        $adbPath = Get-Command adb -ErrorAction SilentlyContinue
        if ($adbPath) {
            $adbVer = & adb version
            Write-Log "ADB version:"
            Write-Log $adbVer
        }
        else {
            Write-Log "Warning: ADB not found in PATH" "WARN"
        }
    }
    
    if ($InstallIOSSupport) {
        # Check libimobiledevice
        $idevicePath = Get-Command ideviceinfo -ErrorAction SilentlyContinue
        if ($idevicePath) {
            Write-Log "libimobiledevice tools available"
            $ideviceVer = & ideviceinfo --version 2>&1
            Write-Log "ideviceinfo version: $ideviceVer"
        }
        else {
            Write-Log "Warning: libimobiledevice tools not found in PATH" "WARN"
        }
        
        # Check iTunes/Apple Mobile Device Service
        $appleMobileDevice = Get-Service -Name "Apple Mobile Device Service" -ErrorAction SilentlyContinue
        if ($appleMobileDevice) {
            Write-Log "Apple Mobile Device Service: $($appleMobileDevice.Status)"
        }
        else {
            Write-Log "Warning: Apple Mobile Device Service not found" "WARN"
        }
    }
    
    Write-Success "INSTALLATION VERIFICATION"
}

# Main execution
try {
    Write-Log "================================================================" "INF"
    Write-Log "     STARTING PREREQUISITES INSTALLATION ON WINDOWS             " "INF"
    Write-Log "================================================================" "INF"
    
    Install-Chocolatey
    Install-NVM
    Install-NodeJS
    Install-Appium
    
    # Install platform-specific components
    if ($InstallIOSSupport) {
        Write-Log "Installing iOS support components..."
        Install-ITunesDrivers
        Install-LibimobileDevice
        Install-XCUITestDriver
    }
    
    if ($InstallAndroidSupport) {
        Write-Log "Installing Android support components..."
        Install-AppiumDriver
        Install-AndroidSDK
    }
    
    # Install Device Farm plugin if enabled
    if ($InstallDeviceFarm) {
        Write-Log "Installing Device Farm plugin and dependencies..."
        Install-DeviceFarm
    }
    
    Create-WrapperScripts
    Verify-Installations
    
    Write-Log "================================================================" "INF"
    Write-Log "           PREREQUISITES SETUP COMPLETED SUCCESSFULLY           " "INF"
    Write-Log "================================================================" "INF"
    Write-Log ""
    Write-Log "To use Appium from this installation:"
    Write-Log ""
    Write-Log "1. Run the environment setup script:"
    Write-Log "   $InstallFolder\appium-env.bat"
    Write-Log ""
    Write-Log "2. Or use the appium wrapper directly:"
    Write-Log "   $InstallFolder\bin\appium.bat --address 127.0.0.1 --port 4723"
    Write-Log ""
    Write-Log "3. To add to your PATH permanently, run:"
    Write-Log "   setx PATH `"%PATH%;$InstallFolder\bin;$InstallFolder\nodejs`""
    Write-Log ""
    Write-Log "4. Test your installation:"
    Write-Log "   $InstallFolder\bin\appium.bat driver list"
    Write-Log "================================================================" "INF"
}
catch {
    Write-Log "Installation failed with error: $_" "ERR"
    exit 1
}
