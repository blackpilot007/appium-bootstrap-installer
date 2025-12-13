# InstallDependencies.ps1
# PowerShell script to install Node.js, NVM, Appium, drivers and plugins on Windows

param(
    [string]$InstallFolder = "$env:USERPROFILE\.local",
    [string]$NodeVersion = "22",
    [string]$AppiumVersion = "2.17.1",
    [string]$DriversJson = "[]",
    [string]$PluginsJson = "[]",
    [string]$GoIosVersion = "v1.0.189",
    [switch]$InstallIOSSupport,
    [switch]$InstallAndroidSupport = $true
)

# Set error action preference
$ErrorActionPreference = "Continue"

# ================================================================
# ISOLATION: Remove global installations from PATH to avoid conflicts
# ================================================================
function Initialize-IsolatedEnvironment {
    # Split PATH and filter out global installations
    $pathEntries = $env:Path -split ';'
    $filteredPath = $pathEntries | Where-Object {
        $_ -notmatch '\\nvm|\\node|\\chocolatey' -and
        $_ -notmatch 'ProgramData\\chocolatey' -and
        $_ -notmatch '\.local\\appium'
    }
    # Rebuild PATH without global installations
    $env:Path = ($filteredPath -join ';')
    Write-Host "Initialized isolated environment (removed global nvm/node/chocolatey from PATH)"
}

# Initialize isolated environment at script start
Initialize-IsolatedEnvironment

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

# Helper: Resolve npm.cmd given a node installation path or PATH
function Resolve-NpmPath {
    param([string]$nodePath)

    # If a direct npm.cmd next to node.exe, prefer it
    if ($nodePath) {
        $direct = Join-Path $nodePath 'npm.cmd'
        if (Test-Path $direct) { return $direct }

        # Search recursively under nodePath for npm.cmd (handles nested extraction layouts)
        try {
            $found = Get-ChildItem -Path $nodePath -Filter 'npm.cmd' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($found) { return $found.FullName }
        } catch {
            # ignore
        }
    }

    # Fall back to resolving from PATH
    $cmd = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $cmd2 = Get-Command npm -ErrorAction SilentlyContinue
    if ($cmd2) { return $cmd2.Source }

    # As a last resort, if node is present but npm.cmd is missing, try to locate npm-cli.js
    if ($nodePath -and (Test-Path "$nodePath\node.exe")) {
        $npmCli = Join-Path $nodePath 'node_modules\npm\bin\npm-cli.js'
        if (Test-Path $npmCli) {
            # Create a small wrapper npm.cmd next to node.exe so callers can invoke npm normally
            $wrapper = Join-Path $nodePath 'npm.cmd'
            try {
                $nodeExePath = Join-Path $nodePath 'node.exe'
                $content = "@echo off`r`n`"$nodeExePath`" `"$npmCli`" %*"
                Set-Content -Path $wrapper -Value $content -Encoding ASCII -Force
                return $wrapper
            }
            catch {
                # ignore wrapper creation errors and fall through to returning null
            }
        }
    }

    return $null
}

# Helper: invoke a scriptblock with retries (useful for transient EPERM / file locks)
function Invoke-WithRetries {
    param(
        [scriptblock]$ScriptBlock,
        [int]$MaxAttempts = 3,
        [int]$DelayMs = 300
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            & $ScriptBlock
            return $true
        }
        catch {
            if ($attempt -lt $MaxAttempts) {
                Write-Log "This is try $attempt/$MaxAttempts. Retrying after $DelayMs milliseconds." "INF"
                Start-Sleep -Milliseconds $DelayMs
            }
            else {
                Write-Log "Maximum tries of $MaxAttempts reached. Throwing error." "ERR"
                throw
            }
        }
    }
}

# Helper: Remove-Item with retries to mitigate transient EPERM / file-lock errors
function Remove-ItemWithRetries {
    param(
        [Parameter(Mandatory=$true)] [string]$Path,
        [switch]$Recurse,
        [switch]$Force,
        [int]$MaxAttempts = 3,
        [int]$DelayMs = 300
    )

    $splat = @{}
    $splat.Path = $Path
    if ($Recurse) { $splat.Recurse = $true }
    if ($Force) { $splat.Force = $true }
    $splat.ErrorAction = 'Stop'

    for ($i=1; $i -le $MaxAttempts; $i++) {
        try {
            Remove-Item @splat
            return $true
        }
        catch {
            if ($i -lt $MaxAttempts) {
                Write-Log "This is try $i/$MaxAttempts for Remove-Item '$Path'. Retrying after $DelayMs ms." "INF"
                Start-Sleep -Milliseconds $DelayMs
                continue
            }
            else {
                Write-Log "Remove-Item failed for '$Path' after $MaxAttempts attempts: $_" "WARN"
                return $false
            }
        }
    }
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
    # PREPEND to PATH to use local Chocolatey, not global
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

# Install NVM for Windows (portable, no-install version) - no GUI, no admin required
# Install Node.js Portable (direct download - no NVM, no admin required)
function Install-NodeJS {
    Write-Log "================================================================" "INF"
    Write-Log "               STARTING NODE.JS INSTALLATION                    " "INF"
    Write-Log "================================================================" "INF"
    
    $nodejsPath = "$InstallFolder\nodejs"
    $Script:ActiveNodePath = $nodejsPath

    # Check if Node.js is already installed
    if (Test-Path "$nodejsPath\node.exe") {
        $existingVersion = & "$nodejsPath\node.exe" --version 2>&1
        Write-Log "Node.js is already installed at $nodejsPath"
        Write-Log "Existing version: $existingVersion"
        
        # Check if it matches the requested major version
        if ($existingVersion -match "^v$NodeVersion\.") {
            Write-Log "Version matches requested Node.js $NodeVersion"
            $env:Path = "$nodejsPath;$env:Path"
            Write-Success "NODE.JS ALREADY INSTALLED"
            return
        }
        else {
            Write-Log "Version mismatch. Removing existing installation..." "WARN"
            Remove-ItemWithRetries -Path $nodejsPath -Recurse -Force
        }
    }
    
    Write-Log "Downloading Node.js $NodeVersion portable (no NVM, no admin required)..."
    
    try {
        # Fetch latest version for the major release
        Write-Log "Fetching Node.js release information from nodejs.org..."
        $indexUrl = "https://nodejs.org/dist/index.json"
        $releases = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing
        
        # Find the latest version matching the major version
        $targetRelease = $releases | Where-Object { $_.version -match "^v$NodeVersion\." } | Select-Object -First 1
        
        if (-not $targetRelease) {
            throw "Could not find Node.js version matching v$NodeVersion.*"
        }
        
        $nodeVersion = $targetRelease.version
        Write-Log "Found Node.js version: $nodeVersion"
        
        # Download Node.js portable (win-x64 zip)
        $downloadUrl = "https://nodejs.org/dist/$nodeVersion/node-$nodeVersion-win-x64.zip"
        $zipPath = "$env:TEMP\node-$nodeVersion-win-x64.zip"
        
        Write-Log "Downloading from: $downloadUrl"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing
        
        # Extract Node.js
        Write-Log "Extracting Node.js to $nodejsPath..."
        $tempExtractPath = "$env:TEMP\node-extract-$([guid]::NewGuid())"
        Expand-Archive -Path $zipPath -DestinationPath $tempExtractPath -Force
        
        # Move extracted folder to target location
        # The zip contains a folder like "node-v22.21.1-win-x64"
        $extractedFolder = Get-ChildItem -Path $tempExtractPath -Directory | Select-Object -First 1
        
        if (-not $extractedFolder) {
            throw "Could not find extracted Node.js folder"
        }
        
        # Create parent directory if needed
        $parentDir = Split-Path -Parent $nodejsPath
        if (-not (Test-Path $parentDir)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }
        
        # Move to final location
        Move-Item -Path $extractedFolder.FullName -Destination $nodejsPath -Force
        
        # Verify node.exe exists
        if (-not (Test-Path "$nodejsPath\node.exe")) {
            throw "node.exe not found after extraction"
        }
        
        # Verify npm exists
        if (-not (Test-Path "$nodejsPath\npm.cmd")) {
            Write-Log "npm.cmd not found, checking for npm-cli.js..." "WARN"
            $npmCli = "$nodejsPath\node_modules\npm\bin\npm-cli.js"
            if (Test-Path $npmCli) {
                Write-Log "Creating npm.cmd wrapper..."
                $npmCmd = "$nodejsPath\npm.cmd"
                $wrapperContent = "@echo off`r`n`"$nodejsPath\node.exe`" `"$npmCli`" %*"
                Set-Content -Path $npmCmd -Value $wrapperContent -Encoding ASCII -Force
            }
        }
        
        # Set environment variables
        $env:Path = "$nodejsPath;$env:Path"
        
        # Verify installation
        $installedVersion = & "$nodejsPath\node.exe" --version
        Write-Log "Node.js version installed: $installedVersion"
        
        $npmPath = Resolve-NpmPath -nodePath $nodejsPath
        if ($npmPath) {
            $npmVersion = & $npmPath --version
            Write-Log "npm version: $npmVersion"
        }
        
        # Clean up
        if (Test-Path $zipPath) {
            Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $tempExtractPath) {
            Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        Write-Log "Node.js installed successfully - portable, no NVM, no admin required"
        Write-Success "NODE.JS INSTALLATION"
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

    # Ensure node path is available in this session so tools can be resolved
    if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }

    # Resolve npm executable robustly (search node folder and PATH)
    $npmPath = Resolve-NpmPath -nodePath $nodePath
    if (-not $npmPath) {
        Write-Log "npm not found. Ensure Node.js is installed properly." "ERR"
        throw "npm not found"
    }
    
    # Configure npm to prevent admin prompts (no symlinks, user-level operations)
    Write-Log "Configuring npm for non-admin operation..."
    & $npmPath config set prefix "$appiumHome" --location user
    & $npmPath config set bin-links false --location user
    # Note: global-style is deprecated in npm 10+, no longer configuring it
    
    Write-Log "Installing Appium $AppiumVersion..."
    
    try {
        # Initialize package.json in APPIUM_HOME
        Push-Location $appiumHome
        
        if (-not (Test-Path "package.json")) {
            & $npmPath init -y
        }
        
        # Install Appium
        Write-Log "Running: npm install appium@$AppiumVersion --prefix $appiumHome"
        Invoke-WithRetries { & $npmPath install "appium@$AppiumVersion" --prefix $appiumHome --legacy-peer-deps --no-bin-links } -MaxAttempts 3 -DelayMs 500

        if ($LASTEXITCODE -ne 0) {
            Write-Log "First attempt failed, retrying with increased timeout..." "WARN"
            Invoke-WithRetries { & $npmPath install "appium@$AppiumVersion" --prefix $appiumHome --legacy-peer-deps --no-bin-links --network-timeout 100000 } -MaxAttempts 2 -DelayMs 800
        }
        
        Pop-Location
        
        # Verify Appium installation
        # With --no-bin-links, .bin directory is not created, use main.js directly
        $appiumMainJs = "$appiumHome\node_modules\appium\build\lib\main.js"
        $appiumExe = "$appiumHome\node_modules\.bin\appium.cmd"
        
        # Check both locations (bin links might exist in some scenarios)
        if (Test-Path $appiumMainJs) {
            Write-Log "Found Appium at: $appiumMainJs"
            $appiumVersionOutput = & "$nodePath\node.exe" $appiumMainJs --version
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
        elseif (Test-Path $appiumExe) {
            Write-Log "Found Appium at: $appiumExe"
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
            $checkPaths = @(
                "$appiumHome\node_modules\appium\build\lib\main.js",
                "$appiumHome\node_modules\.bin\appium.cmd",
                "$appiumHome\node_modules\appium"
            )
            Write-Log "Appium binary not found. Checked paths:" "ERR"
            foreach ($path in $checkPaths) {
                $exists = Test-Path $path
                Write-Log "  - $path : $exists" "ERR"
            }
            throw "Appium binary not found after installation"
        }
    }
    catch {
        Write-ErrorMessage "APPIUM INSTALLATION"
        Write-Log "Error: $_" "ERR"
        throw
    }
}

# Install Appium Drivers from JSON configuration
function Install-AppiumDrivers {
    param(
        [Parameter(Mandatory=$true)]
        [string]$DriversJsonString
    )
    
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING APPIUM DRIVERS INSTALLATION                 " "INF"
    Write-Log "================================================================" "INF"
    
    # Decode Base64 and parse JSON
    try {
        $decodedJson = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($DriversJsonString))
        $drivers = $decodedJson | ConvertFrom-Json
        if ($drivers.Count -eq 0) {
            Write-Log "No drivers configured for installation" "WARN"
            return
        }
        
        Write-Log "Installing $($drivers.Count) Appium driver(s)..."
    }
    catch {
        Write-Log "Failed to parse drivers JSON: $_" "ERR"
        throw
    }
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    
    if (-not (Test-Path $appiumScript)) {
        Write-Log "Appium not found at $appiumScript. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    
    # Get Node.js path
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    
    # Ensure node path exists
    if (-not (Test-Path "$nodePath\node.exe")) {
        Write-Log "Node.js symlink not found, checking versioned directory..." "WARN"
        $versionedPath = Get-ChildItem -Path "$InstallFolder\nvm" -Directory -Filter "v*" | Select-Object -First 1
        if ($versionedPath) {
            $nodePath = $versionedPath.FullName
            Write-Log "Using Node.js from versioned directory: $nodePath" "INF"
        } else {
            throw "Node.js not found in expected locations"
        }
    }
    
    $appiumVersionOutput = & "$nodePath\node.exe" $appiumScript --version
    $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
    Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
    
    # Install each driver
    foreach ($driver in $drivers) {
        $driverName = $driver.name
        $driverVersion = $driver.version
        
        Write-Log "Installing driver: $driverName@$driverVersion"
        
        try {
            # Check if driver already exists and get version
            Write-Log "Checking if $driverName driver is already installed..."
            $driverList = & "$nodePath\node.exe" $appiumScript driver list --installed 2>&1 | Out-String
            # Strip ANSI color codes for reliable matching
            $driverListClean = $driverList -replace '\x1b\[[0-9;]*m', ''
            
            # Extract installed version using regex
            $installedVersion = $null
            if ($driverListClean -match "$driverName@([\d\.]+)") {
                $installedVersion = $matches[1]
            }
            
            if ($installedVersion) {
                if ($installedVersion -eq $driverVersion) {
                    Write-Log "Driver $driverName@$driverVersion already installed with correct version, skipping..."
                    # Set success exit code to skip fallback
                    $LASTEXITCODE = 0
                } else {
                    Write-Log "Driver $driverName installed with version $installedVersion, updating to $driverVersion..."
                    & "$nodePath\node.exe" $appiumScript driver update "$driverName@$driverVersion"
                }
            } else {
                Write-Log "Installing new driver $driverName@$driverVersion..."
                & "$nodePath\node.exe" $appiumScript driver install "$driverName@$driverVersion"
            }
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Driver installation via Appium command failed, trying npm install..." "WARN"
                if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }
                $npmPath = Resolve-NpmPath -nodePath $nodePath
                if (-not $npmPath) { throw "npm not found for fallback installation" }
                $driverPackage = "appium-${driverName}-driver"
                Push-Location $appiumHome
                Invoke-WithRetries { & $npmPath install "${driverPackage}@$driverVersion" --prefix $appiumHome --legacy-peer-deps --no-bin-links } -MaxAttempts 3 -DelayMs 400
                Pop-Location
            }
            
            # Verify driver installation
            Write-Log "Verifying $driverName driver installation..."
            $driverList = & "$nodePath\node.exe" $appiumScript driver list --installed 2>&1 | Out-String
            # Strip ANSI color codes for reliable matching
            $driverListClean = $driverList -replace '\x1b\[[0-9;]*m', ''
            
            if ($driverListClean -match $driverName) {
                Write-Log "✅ $driverName driver installed successfully"
            } else {
                Write-Log "⚠️ $driverName driver not found in list, checking directory..." "WARN"
                $driverDir = "$appiumHome\node_modules\appium-${driverName}-driver"
                if (Test-Path $driverDir) {
                    Write-Log "✅ $driverName driver directory exists at $driverDir"
                } else {
                    Write-Log "❌ $driverName driver installation may have failed" "WARN"
                }
            }
        }
        catch {
            Write-Log "Error installing driver $driverName : $_" "ERR"
            # Continue with next driver instead of failing completely
        }
    }
    
    Write-Success "APPIUM DRIVERS INSTALLATION"
}

# Install Appium Plugins from JSON configuration
function Install-AppiumPlugins {
    param(
        [Parameter(Mandatory=$true)]
        [string]$PluginsJsonString
    )
    
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING APPIUM PLUGINS INSTALLATION                 " "INF"
    Write-Log "================================================================" "INF"
    
    # Decode Base64 and parse JSON
    try {
        $decodedJson = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($PluginsJsonString))
        $plugins = $decodedJson | ConvertFrom-Json
        if ($plugins.Count -eq 0) {
            Write-Log "No plugins configured for installation" "WARN"
            return
        }
        
        Write-Log "Installing $($plugins.Count) Appium plugin(s)..."
    }
    catch {
        Write-Log "Failed to parse plugins JSON: $_" "ERR"
        throw
    }
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    
    if (-not (Test-Path $appiumScript)) {
        Write-Log "Appium not found at $appiumScript. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    
    # Get Node.js path
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    
    # Ensure node path exists
    if (-not (Test-Path "$nodePath\node.exe")) {
        Write-Log "Node.js symlink not found, checking versioned directory..." "WARN"
        $versionedPath = Get-ChildItem -Path "$InstallFolder\nvm" -Directory -Filter "v*" | Select-Object -First 1
        if ($versionedPath) {
            $nodePath = $versionedPath.FullName
            Write-Log "Using Node.js from versioned directory: $nodePath" "INF"
        } else {
            throw "Node.js not found in expected locations"
        }
    }
    
    $appiumVersionOutput = & "$nodePath\node.exe" $appiumScript --version
    $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
    Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
    
    # Install each plugin
    foreach ($plugin in $plugins) {
        $pluginName = $plugin.name
        $pluginVersion = $plugin.version
        
        Write-Log "Installing plugin: $pluginName@$pluginVersion"
        
        try {
            # Check if plugin already exists and get version
            Write-Log "Checking if $pluginName plugin is already installed..."
            $pluginList = & "$nodePath\node.exe" $appiumScript plugin list --installed 2>&1 | Out-String
            # Strip ANSI color codes for reliable matching
            $pluginListClean = $pluginList -replace '\x1b\[[0-9;]*m', ''
            
            # Extract installed version using regex
            $installedVersion = $null
            if ($pluginListClean -match "$pluginName@([\d\.]+)") {
                $installedVersion = $matches[1]
            }
            
            if ($installedVersion) {
                if ($installedVersion -eq $pluginVersion) {
                    Write-Log "Plugin $pluginName@$pluginVersion already installed with correct version, skipping..."
                    # Set success exit code to skip fallback
                    $LASTEXITCODE = 0
                } else {
                    Write-Log "Plugin $pluginName installed with version $installedVersion, updating to $pluginVersion..."
                    & "$nodePath\node.exe" $appiumScript plugin update "$pluginName@$pluginVersion"
                }
            } else {
                Write-Log "Installing new plugin $pluginName@$pluginVersion..."
                & "$nodePath\node.exe" $appiumScript plugin install "$pluginName@$pluginVersion"
            }
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Plugin installation via Appium command failed, trying npm install..." "WARN"
                if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }
                $npmPath = Resolve-NpmPath -nodePath $nodePath
                if (-not $npmPath) { throw "npm not found for fallback installation" }
                Push-Location $appiumHome
                Invoke-WithRetries { & $npmPath install "${pluginName}@$pluginVersion" --prefix $appiumHome --legacy-peer-deps --no-bin-links } -MaxAttempts 3 -DelayMs 400
                Pop-Location
            }
            
            # Verify plugin installation
            Write-Log "Verifying $pluginName plugin installation..."
            $pluginList = & "$nodePath\node.exe" $appiumScript plugin list --installed 2>&1 | Out-String
            # Strip ANSI color codes for reliable matching
            $pluginListClean = $pluginList -replace '\x1b\[[0-9;]*m', ''
            
            if ($pluginListClean -match $pluginName) {
                Write-Log "✅ $pluginName plugin installed successfully"
            } else {
                Write-Log "⚠️ $pluginName plugin not found in list, checking directory..." "WARN"
                $pluginDir = "$appiumHome\node_modules\$pluginName"
                if (Test-Path $pluginDir) {
                    Write-Log "✅ $pluginName plugin directory exists at $pluginDir"
                } else {
                    Write-Log "❌ $pluginName plugin installation may have failed" "WARN"
                }
            }
        }
        catch {
            Write-Log "Error installing plugin $pluginName : $_" "ERR"
            # Continue with next plugin instead of failing completely
        }
    }
    
    Write-Success "APPIUM PLUGINS INSTALLATION"
}

# Install go-ios for iOS real device support  
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
    
    Write-Log "Installing libimobiledevice using Chocolatey (best-effort, optional on Windows)..."
    
    try {
        Invoke-WithRetries { choco install libimobiledevice -y --no-progress --force } -MaxAttempts 3 -DelayMs 400
        
        # Refresh environment variables
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        
        # Verify installation (best-effort)
        $ideviceInfoPath = Get-Command ideviceinfo -ErrorAction SilentlyContinue
        if ($ideviceInfoPath) {
            Write-Log "libimobiledevice installed successfully"
            $ideviceVersion = & ideviceinfo --version 2>&1
            Write-Log $ideviceVersion
            Write-Success "LIBIMOBILEDEVICE INSTALLATION (BEST-EFFORT)"
        }
        else {
            Write-Log "libimobiledevice not found in PATH after installation. It may not be available on this system or may require a terminal restart." "WARN"
            Write-Log "Installed location is typically: C:\ProgramData\chocolatey\lib\libimobiledevice\tools" "WARN"
        }
    }
    catch {
        Write-Log "libimobiledevice installation via Chocolatey failed or package not found. This tool is optional on Windows; iOS automation will still work if iTunes drivers are present." "WARN"
        Write-Log "If you need libimobiledevice, please install an appropriate tool manually for your environment." "WARN"
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
    
    # Check if running as administrator
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    
    if (-not $isAdmin) {
        Write-Log "Apple Mobile Device Support not found." "WARN"
        Write-Log "iTunes installation requires administrator privileges." "WARN"
        Write-Log "Since this is a user-directory installation, iOS support will be skipped." "WARN"
        Write-Log "To enable iOS device support, run this installer as administrator or install iTunes manually." "WARN"
        Write-Log "Manual installation options:" "WARN"
        Write-Log "1. iTunes from Microsoft Store (recommended), OR" "WARN"
        Write-Log "2. iTunes from Apple (https://www.apple.com/itunes/download/)" "WARN"
        Write-Success "ITUNES DRIVERS SKIPPED (NON-ADMIN)"
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
        Invoke-WithRetries { choco install itunes -y --no-progress --force } -MaxAttempts 3 -DelayMs 400
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
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    
    if (-not (Test-Path $appiumScript)) {
        Write-Log "Appium not found at $appiumScript. Installing Appium first..." "WARN"
        Install-Appium
    }
    
    $env:APPIUM_HOME = $appiumHome
    
    # Get Node.js path
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    
    # Ensure node path exists, fallback to versioned directory if symlink not created
    if (-not (Test-Path "$nodePath\node.exe")) {
        Write-Log "Node.js symlink not found, checking versioned directory..." "WARN"
        $versionedPath = Get-ChildItem -Path "$InstallFolder\nvm" -Directory -Filter "v*" | Select-Object -First 1
        if ($versionedPath) {
            $nodePath = $versionedPath.FullName
            Write-Log "Using Node.js from versioned directory: $nodePath" "INF"
        } else {
            Write-ErrorMessage "XCUITEST DRIVER INSTALLATION"
            Write-Log "Node.js not found in expected locations." "ERR"
            throw "Node.js installation issue"
        }
    }
    
    $appiumVersionOutput = & "$nodePath\node.exe" $appiumScript --version
    $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
    Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"
    
    Write-Log "Installing XCUITest driver version $XCUITestVersion..."
    
    try {
        # Uninstall existing driver
        Write-Log "Uninstalling any existing XCUITest driver..."
        & "$nodePath\node.exe" $appiumScript driver uninstall xcuitest 2>$null
        
        # Install driver using appropriate command (use 'driver install' for Appium 3+)
        if ($appiumMajorVersion -ge "3") {
            Write-Log "Using 'driver install' command for Appium $appiumMajorVersion.x"
            Write-Log "Running: appium driver install xcuitest@$XCUITestVersion"
            & "$nodePath\node.exe" $appiumScript driver install "xcuitest@$XCUITestVersion"
        }
        else {
            Write-Log "Using 'driver install' command for Appium 2.x"
            Write-Log "Running: appium driver install xcuitest@$XCUITestVersion"
            & "$nodePath\node.exe" $appiumScript driver install "xcuitest@$XCUITestVersion"
        }
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Plugin installation via Appium command had issues, trying npm..." "WARN"
            if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }
            $npmPath = Resolve-NpmPath -nodePath $nodePath
            if (-not $npmPath) { throw "npm not found for fallback installation" }
            Push-Location $appiumHome
            Invoke-WithRetries { & $npmPath install "appium-xcuitest-driver@$XCUITestVersion" --save --legacy-peer-deps } -MaxAttempts 3 -DelayMs 400
            Pop-Location
        }
        
        # Verify driver installation
        Write-Log "Verifying XCUITest driver installation..."
        # Use driver list to enumerate installed drivers/plugins
        $driverList = & "$nodePath\node.exe" $appiumScript driver list --installed 2>&1
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
        Invoke-WithRetries { choco install adb -y --no-progress --force } -MaxAttempts 3 -DelayMs 400
        
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
    
    Write-Log "Installing go-ios $GoIosVersion for Windows..."
    Write-Log "This is required for iOS real device support with device-farm plugin"
    
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
        
        # Download go-ios Windows binary (note: filename is go-ios-win.zip, not go-ios-windows.zip)
        $downloadUrl = "https://github.com/danielpaulus/go-ios/releases/download/$GoIosVersion/go-ios-win.zip"
        $zipPath = "$env:TEMP\go-ios-win.zip"
        
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
            throw "go-ios binary not found after extraction at $goIosBin"
        }
        
        # Clean up (use retry wrapper to reduce transient EPERM errors)
        Remove-ItemWithRetries -Path $zipPath -Force
    }
    catch {
        Write-ErrorMessage "GO-IOS INSTALLATION"
        Write-Log "Error: $_" "ERR"
        Write-Log "go-ios installation is CRITICAL for iOS real device support." "ERR"
        Write-Log "Without go-ios, device-farm plugin will NOT work with iOS real devices." "ERR"
        Write-Log "You can manually download from: https://github.com/danielpaulus/go-ios/releases" "WARN"
        throw
    }
}

# Install Appium Device Farm plugin and related plugins
function Install-DeviceFarm {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING DEVICE FARM PLUGIN INSTALLATION             " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    
    if (-not (Test-Path $appiumScript)) {
        Write-Log "Appium not found at $appiumScript. Installing Appium first..." "WARN"
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
        # Detect Appium version and ensure we call Appium via node where possible
        $nodePath = "$InstallFolder\nodejs"
        if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }

        # Ensure node path exists, fallback to versioned directory if symlink not created
        if (-not (Test-Path "$nodePath\node.exe")) {
            Write-Log "Node.js symlink not found, checking versioned directory..." "WARN"
            $versionedPath = Get-ChildItem -Path "$InstallFolder\nvm" -Directory -Filter "v*" | Select-Object -First 1
            if ($versionedPath) {
                $nodePath = $versionedPath.FullName
                Write-Log "Using Node.js from versioned directory: $nodePath" "INF"
            } else {
                Write-ErrorMessage "DEVICE FARM PLUGIN INSTALLATION"
                Write-Log "Node.js not found in expected locations." "ERR"
                throw "Node.js installation issue"
            }
        }

        # Make sure node dir is on PATH for child processes that may call node internally
        if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }

        $nodeExe = Join-Path $nodePath 'node.exe'
        $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
        $appiumVersionOutput = & $nodeExe $appiumScript --version 2>&1
        $appiumMajorVersion = $appiumVersionOutput.Split('.')[0]
        Write-Log "Detected Appium version: $appiumVersionOutput (Major: $appiumMajorVersion)"

        # Resolve npm once for fallback use and for explicit npm installs
        $npmPath = Resolve-NpmPath -nodePath $nodePath
        if (-not $npmPath) { Write-Log "npm not found; plugin npm fallbacks may fail" "WARN" }

        # Map plugin names to npm packages (device-farm and others aren't in Appium 2.x CLI)
        $plugins = @(
            @{Name = "device-farm"; Package = "appium-device-farm"; Version = "8.3.5" },
            @{Name = "appium-dashboard"; Package = "appium-dashboard"; Version = "2.0.3" },
            @{Name = "inspector"; Package = "appium-inspector"; Version = "2025.3.1" }
        )

        foreach ($plugin in $plugins) {
            $pluginName = $plugin.Name
            $pluginPackage = $plugin.Package
            $pluginVersion = $plugin.Version

            Write-Log "Installing $pluginName@$pluginVersion as npm package $pluginPackage..."

            # For Appium 2.x, device-farm and related plugins must be installed via npm
            # (they're not in the built-in plugin list)
            if ($appiumMajorVersion -lt 3) {
                Write-Log "Appium 2.x detected - installing $pluginName via npm directly"
                if (-not $npmPath) { $npmPath = Resolve-NpmPath -nodePath $nodePath }
                if (-not $npmPath) { Write-Log "npm not found for plugin installation" "ERR"; throw }

                Push-Location $appiumHome
                try {
                    Invoke-WithRetries { & $npmPath install "$pluginPackage@$pluginVersion" --save --legacy-peer-deps } -MaxAttempts 3 -DelayMs 400
                }
                finally { Pop-Location }
            }
            else {
                # Appium 3.x - try CLI first, fallback to npm
                try {
                    Invoke-WithRetries { & $nodeExe $appiumScript plugin uninstall $pluginName 2>$null } -MaxAttempts 2 -DelayMs 200
                } catch { }

                try {
                    Invoke-WithRetries { & $nodeExe $appiumScript plugin install "$pluginName@$pluginVersion" } -MaxAttempts 3 -DelayMs 300
                }
                catch {
                    Write-Log "Plugin installation via Appium CLI had issues, trying npm..." "WARN"
                    if (-not $npmPath) { $npmPath = Resolve-NpmPath -nodePath $nodePath }
                    if (-not $npmPath) { Write-Log "npm not found for plugin fallback" "ERR"; throw }

                    Push-Location $appiumHome
                    try {
                        Invoke-WithRetries { & $npmPath install "$pluginPackage@$pluginVersion" --save --legacy-peer-deps } -MaxAttempts 3 -DelayMs 400
                    }
                    finally { Pop-Location }
                }
            }

            Write-Log "$pluginName@$pluginVersion installed"
        }

        # Verify plugin installations
        Write-Log "Verifying plugin installations..."
        # Prefer node+main.js for plugin listing to avoid wrapper/path issues
        try {
            $pluginList = & $nodeExe $appiumScript plugin list --installed
            Write-Log $pluginList
        } catch {
            Write-Log "Failed to list plugins via node+main.js; attempting npm-based check" "WARN"
            if ($npmPath) {
                Push-Location $appiumHome
                try { & $npmPath ls --depth=0 } catch {}
                Pop-Location
            }
        }

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

# Copy Platform scripts to install folder
function Copy-PlatformScripts {
    Write-Log "================================================================" "INF"
    Write-Log "           COPYING PLATFORM SCRIPTS                             " "INF"
    Write-Log "================================================================" "INF"
    
    $platformSourceDir = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Platform"
    $platformDestDir = Join-Path $InstallFolder "Platform"
    
    # Check if source Platform directory exists (from publish folder)
    if (Test-Path $platformSourceDir) {
        Write-Log "Copying Platform scripts from $platformSourceDir to $platformDestDir..."
        
        # Create destination directory
        if (-not (Test-Path $platformDestDir)) {
            New-Item -ItemType Directory -Path $platformDestDir -Force | Out-Null
        }
        
        # Copy entire Platform directory structure
        try {
            Copy-Item -Path $platformSourceDir -Destination $InstallFolder -Recurse -Force
            Write-Log "Platform scripts copied successfully"
            
            # Verify critical scripts were copied
            $deviceListenerScript = Join-Path $platformDestDir "Windows\Scripts\DeviceListener.ps1"
            $startAppiumScript = Join-Path $platformDestDir "Windows\Scripts\StartAppiumServer.ps1"
            
            if (Test-Path $deviceListenerScript) {
                Write-Log "✓ DeviceListener.ps1 copied successfully"
            } else {
                Write-Log "✗ DeviceListener.ps1 not found after copy" "WARN"
            }
            
            if (Test-Path $startAppiumScript) {
                Write-Log "✓ StartAppiumServer.ps1 copied successfully"
            } else {
                Write-Log "✗ StartAppiumServer.ps1 not found after copy" "WARN"
            }
        }
        catch {
            Write-Log "Warning: Failed to copy Platform scripts: $_" "WARN"
            Write-Log "Device listener may not function correctly" "WARN"
        }
    }
    else {
        Write-Log "Warning: Platform source directory not found at $platformSourceDir" "WARN"
        Write-Log "Looking for alternative location..." "WARN"
        
        # Try finding Platform in the same directory as this script
        $altPlatformSource = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Platform"
        if (Test-Path $altPlatformSource) {
            Write-Log "Found Platform at $altPlatformSource"
            try {
                Copy-Item -Path $altPlatformSource -Destination $InstallFolder -Recurse -Force
                Write-Log "Platform scripts copied from alternative location"
            }
            catch {
                Write-Log "Failed to copy Platform scripts from alternative location: $_" "WARN"
            }
        }
        else {
            Write-Log "Platform scripts not found. Device listener will not be available." "WARN"
        }
    }
    
    Write-Success "PLATFORM SCRIPTS COPY"
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
    # Resolve npm path so we can write it into wrapper env files
    $npmPath = Resolve-NpmPath -nodePath $nodePath
    
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
    
        # appium-env.bat - Setup environment (include explicit node and npm paths)
        $nodeExe = "";
        $npmCmd = "";
        if (Test-Path "$nodePath\node.exe") { $nodeExe = "$nodePath\node.exe" }
        if ($npmPath) { $npmCmd = $npmPath }

        $appiumEnvContent = @"
@ECHO OFF
REM Appium environment wrapper
SET APPIUM_HOME=$appiumHome
SET NODE_EXE=$nodeExe
SET NPM_CMD=$npmCmd
SET PATH=$InstallFolder;$nodePath;$binDir;%PATH%

echo Appium environment activated:
echo APPIUM_HOME=%APPIUM_HOME%
echo NODE_EXE=%NODE_EXE%
%NODE_EXE% --version
echo NPM_CMD=%NPM_CMD%
if "%NPM_CMD%"=="" (
    npm --version
) else (
    "%NPM_CMD%" --version
)
echo APPIUM_VERSION=
"%NODE_EXE%" "$appiumScript" --version
echo.
echo You can now use 'appium' command directly
"@

        Set-Content -Path "$InstallFolder\appium-env.bat" -Value $appiumEnvContent
        Write-Log "Created appium-env.bat at $InstallFolder\appium-env.bat"

        # appium-env.sh for Unix-like shells (optional for WSL/mac)
        # Build a bash env file using placeholders then replace them to avoid PowerShell variable expansion issues
        $appiumEnvSh = @'
#!/usr/bin/env bash
export APPIUM_HOME="__APPIUMHOME__"
export NODE_EXE="__NODEEXE__"
export NPM_CMD="__NPMCMD__"
export PATH="__NODEPATH__:__BINDIR__: $PATH"
echo "Appium environment activated:"
echo "APPIUM_HOME=$APPIUM_HOME"
if [ -n "$NODE_EXE" ]; then
    "$NODE_EXE" --version
fi
if [ -n "$NPM_CMD" ]; then
    "$NPM_CMD" --version
else
    npm --version
fi
'@

        # Replace placeholders with actual values (escape backslashes for bash where appropriate)
        $nodePathForSh = $nodePath -replace '\\','/'
        $binDirForSh = $binDir -replace '\\','/'
        if ($nodeExe -eq "") { $nodeExeForSh = "" } else { $nodeExeForSh = $nodeExe -replace '\\','/' }
        if ($npmCmd -eq "") { $npmCmdForSh = "" } else { $npmCmdForSh = $npmCmd -replace '\\','/' }

        $appiumEnvSh = $appiumEnvSh -replace '__APPIUMHOME__', ($appiumHome -replace '"','\"')
        $appiumEnvSh = $appiumEnvSh -replace '__NODEEXE__', ($nodeExeForSh)
        $appiumEnvSh = $appiumEnvSh -replace '__NPMCMD__', ($npmCmdForSh)
        $appiumEnvSh = $appiumEnvSh -replace '__NODEPATH__', ($nodePathForSh)
        $appiumEnvSh = $appiumEnvSh -replace '__BINDIR__', ($binDirForSh)

        Set-Content -Path "$InstallFolder\appium-env.sh" -Value $appiumEnvSh
        Write-Log "Created appium-env.sh at $InstallFolder\appium-env.sh"
    
    # Add InstallFolder to User PATH so appium.bat and other wrappers are accessible
    try {
        $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
        if ($userPath -notlike "*$InstallFolder*") {
            Write-Log "Adding $InstallFolder to User PATH..."
            [Environment]::SetEnvironmentVariable("Path", "$userPath;$InstallFolder", "User")
            $env:Path = "$InstallFolder;$env:Path"  # Update current session
            Write-Log "Added $InstallFolder to PATH for current and future sessions"
        } else {
            Write-Log "InstallFolder already in User PATH"
        }
    }
    catch {
        Write-Log "Could not update User PATH (may require elevated privileges): $_" "WARN"
        Write-Log "You can manually add $InstallFolder to your PATH or use full paths to wrappers" "WARN"
    }
    
    Write-Success "ENVIRONMENT SCRIPTS CREATION"
}

# Verify installations
function Verify-Installations {
    Write-Log "================================================================" "INF"
    Write-Log "           STARTING INSTALLATION VERIFICATION                   " "INF"
    Write-Log "================================================================" "INF"
    
    $appiumHome = "$InstallFolder\appium-home"
    $nodePath = "$InstallFolder\nodejs"
    $appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
    
    Write-Log "Environment variables:"
    Write-Log "- APPIUM_HOME: $appiumHome"
    Write-Log "- Node Path: $($Script:ActiveNodePath)"
    
    # Check Node.js
    $nodePath = "$InstallFolder\nodejs"
    if ($Script:ActiveNodePath) { $nodePath = $Script:ActiveNodePath }
    if (Test-Path "$nodePath\node.exe") {
        $nodeVer = & "$nodePath\node.exe" --version
        if (Test-Path "$nodePath\node.exe") { $env:Path = "$nodePath;$env:Path" }
        $npmPath = Resolve-NpmPath -nodePath $nodePath
        if ($npmPath) { $npmVer = & $npmPath --version } else { $npmVer = "(npm not found)" }
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
    Install-NodeJS
    Install-Appium
    
    # Check if iOS drivers are configured and auto-enable iOS support
    $needsIOSSupport = $InstallIOSSupport
    if (-not $needsIOSSupport -and $DriversJson -and $DriversJson -ne "[]") {
        try {
            $decodedJson = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($DriversJson))
            $drivers = $decodedJson | ConvertFrom-Json
            $iosDrivers = @("xcuitest", "safari", "mac2")
            foreach ($driver in $drivers) {
                if ($iosDrivers -contains $driver.name) {
                    Write-Log "Detected iOS driver '$($driver.name)' - automatically enabling iOS support" "INF"
                    $needsIOSSupport = $true
                    break
                }
            }
        }
        catch {
            Write-Log "Could not parse drivers JSON for iOS detection: $_" "WARN"
        }
    }
    
    # Install platform-specific components
    if ($needsIOSSupport) {
        Write-Log "Installing iOS support components..."
        Install-ITunesDrivers
        # Note: libimobiledevice not available via Chocolatey on Windows
        # iTunes drivers + go-ios provide iOS device support
        Install-GoIos
    }
    
    if ($InstallAndroidSupport) {
        Write-Log "Installing Android support components..."
        Install-AndroidSDK
    }
    
    # Install drivers dynamically from JSON configuration
    if ($DriversJson -and $DriversJson -ne "[]") {
        Install-AppiumDrivers -DriversJsonString $DriversJson
    }
    
    # Install plugins dynamically from JSON configuration
    if ($PluginsJson -and $PluginsJson -ne "[]") {
        Install-AppiumPlugins -PluginsJsonString $PluginsJson
    }
    
    # Copy Platform scripts before creating wrappers
    Copy-PlatformScripts
    
    Create-WrapperScripts
    
    # Setup Device Listener Service
    Write-Log "Setting up device listener service..."
    $serviceSetupScript = Join-Path $InstallFolder "Platform\Windows\Scripts\ServiceSetup.ps1"
    if (Test-Path $serviceSetupScript) {
        try {
            & $serviceSetupScript -InstallDir $InstallFolder
            Write-Log "Device listener service setup completed"
        }
        catch {
            Write-Log "Warning: Device listener service setup failed: $_" "WARN"
            Write-Log "You can set it up manually later by running ServiceSetup.ps1" "WARN"
        }
    }
    else {
        Write-Log "Warning: ServiceSetup.ps1 not found at $serviceSetupScript" "WARN"
    }
    
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
