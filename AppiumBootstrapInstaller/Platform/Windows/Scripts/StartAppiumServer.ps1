# appium.ps1 - Windows version
# Starts Appium server with specified ports and DeviceFarm plugin configuration
# Uses explicit fully qualified paths for complete isolation from global installations

param(
    [Parameter(Mandatory=$true)]
    [string]$AppiumHomePath,
    
    [Parameter(Mandatory=$true)]
    [string]$AppiumBinPath,
    
    [Parameter(Mandatory=$true)]
    [string]$NodePath,
    
    [Parameter(Mandatory=$true)]
    [string]$InstallFolder,
    
    [Parameter(Mandatory=$true)]
    [int]$AppiumPort,
    
    [Parameter(Mandatory=$true)]
    [int]$WdaLocalPort,
    
    [Parameter(Mandatory=$true)]
    [int]$MpegLocalPort
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Starting Appium Server (Windows)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Appium Home: $AppiumHomePath"
Write-Host "Node.js Path: $NodePath"
Write-Host "Install Folder: $InstallFolder"
Write-Host "Appium Port: $AppiumPort"
Write-Host "WDA Local Port: $WdaLocalPort"
Write-Host "MPEG Local Port: $MpegLocalPort"
Write-Host "========================================" -ForegroundColor Cyan

# Use explicit fully qualified paths - no PATH manipulation needed
$nodeExe = Join-Path $NodePath "node.exe"

if (-not (Test-Path $nodeExe)) {
    Write-Host "ERROR: Node.js executable not found at $nodeExe" -ForegroundColor Red
    exit 1
}

Write-Host "Using local Node.js: $nodeExe" -ForegroundColor Green

# Set APPIUM_HOME explicitly for this process only (not system-wide)
$env:APPIUM_HOME = $AppiumHomePath

# Prepare fully qualified paths
$appiumScript = Join-Path $AppiumHomePath "node_modules\appium\build\lib\main.js"

# Try node+main.js first if available
if ($nodeExe -and (Test-Path $appiumScript)) {
    $AppiumVersionOutput = & $nodeExe $appiumScript --version 2>&1
} elseif (Test-Path $appiumCmd) {
    $AppiumVersionOutput = & $appiumCmd --version 2>&1
} else {
    $AppiumVersionOutput = ""
}

if ($AppiumVersionOutput -and $LASTEXITCODE -eq 0) {
    $AppiumVersion = $AppiumVersionOutput.Trim()
    Write-Host "Detected Appium version: $AppiumVersion" -ForegroundColor Green
} else {
    Write-Host "Failed to detect Appium version, assuming 2.x" -ForegroundColor Yellow
    $AppiumVersion = "2.0.0"
}

$AppiumMajorVersion = $AppiumVersion.Split('.')[0]

# Check for DeviceFarm plugin
Write-Host "Checking for DeviceFarm plugin..." -ForegroundColor Cyan
$DeviceFarmInstalled = $false
$DeviceFarmPath = Join-Path $AppiumHomePath "node_modules\appium-device-farm"
if (Test-Path $DeviceFarmPath) {
    Write-Host "✅ DeviceFarm plugin is installed" -ForegroundColor Green
    $DeviceFarmInstalled = $true
} else {
    Write-Host "ℹ️ DeviceFarm plugin is not installed" -ForegroundColor Yellow
}

# Build plugin list dynamically
$PluginList = "inspector"
$PluginOptions = ""

if ($DeviceFarmInstalled) {
    $PluginList = "device-farm,appium-dashboard,inspector"
    
    # Detect installed drivers
    Write-Host "Detecting installed Appium drivers..." -ForegroundColor Cyan
    $XcuitestInstalled = $false
    $Uiautomator2Installed = $false
    
    # Check driver list first. Prefer node+main.js if available to avoid wrapper PATH issues.
    $DriverListOutput = ""
    if ($nodeExe -and (Test-Path $appiumScript)) {
        $DriverListOutput = & $nodeExe $appiumScript driver list --installed 2>&1
    } elseif (Test-Path $appiumCmd) {
        $DriverListOutput = & $appiumCmd driver list --installed 2>&1
    }

    if ($DriverListOutput) {
        Write-Host "Driver list output:" -ForegroundColor Gray
        Write-Host $DriverListOutput -ForegroundColor Gray

        if ($DriverListOutput -match "xcuitest|XCUITest") {
            $XcuitestInstalled = $true
            Write-Host "✅ XCUITest driver found in driver list" -ForegroundColor Green
        }

        if ($DriverListOutput -match "uiautomator2|UiAutomator2") {
            $Uiautomator2Installed = $true
            Write-Host "✅ UiAutomator2 driver found in driver list" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️ Could not query driver list, checking filesystem" -ForegroundColor Yellow
    }
    
    # Fallback to filesystem check
    if (-not $XcuitestInstalled) {
        $XcuitestPath = Join-Path $AppiumHomePath "node_modules\appium-xcuitest-driver"
        if (Test-Path $XcuitestPath) {
            Write-Host "✅ XCUITest driver directory found at $XcuitestPath" -ForegroundColor Green
            $XcuitestInstalled = $true
        }
    }
    
    if (-not $Uiautomator2Installed) {
        $Uiautomator2Path = Join-Path $AppiumHomePath "node_modules\appium-uiautomator2-driver"
        if (Test-Path $Uiautomator2Path) {
            Write-Host "✅ UiAutomator2 driver directory found at $Uiautomator2Path" -ForegroundColor Green
            $Uiautomator2Installed = $true
        }
    }
    
    # Determine platform based on installed drivers
    $DeviceFarmPlatform = "both"
    if ($XcuitestInstalled -and $Uiautomator2Installed) {
        $DeviceFarmPlatform = "both"
        Write-Host "✅ Both iOS and Android drivers detected - platform set to 'both'" -ForegroundColor Green
    } elseif ($XcuitestInstalled) {
        $DeviceFarmPlatform = "ios"
        Write-Host "✅ Only iOS driver detected - platform set to 'ios'" -ForegroundColor Green
    } elseif ($Uiautomator2Installed) {
        $DeviceFarmPlatform = "android"
        Write-Host "✅ Only Android driver detected - platform set to 'android'" -ForegroundColor Green
    } else {
        $DeviceFarmPlatform = "both"
        Write-Host "⚠️ No drivers detected - defaulting platform to 'both'" -ForegroundColor Yellow
    }
    
    # Device Farm plugin configuration
    # Reference: https://github.com/AppiumTestDistribution/appium-device-farm/blob/main/server-config.json
    # All plugin options must use = format (not spaces), except boolean flags
    $PluginOptions = "--plugin-device-farm-platform=$DeviceFarmPlatform"
    $PluginOptions += " --plugin-device-farm-max-sessions=1"
    
    # Boolean flags - do NOT use =true
    $PluginOptions += " --plugin-device-farm-skip-chrome-download"
    
    # Add iOS-specific options only if iOS is supported
    if ($DeviceFarmPlatform -eq "ios" -or $DeviceFarmPlatform -eq "both") {
        $PluginOptions += " --plugin-device-farm-ios-device-type=real"
    }
    
    Write-Host "DeviceFarm enabled - plugins: $PluginList" -ForegroundColor Green
    Write-Host "DeviceFarm platform: $DeviceFarmPlatform" -ForegroundColor Green
    Write-Host "DeviceFarm options: $PluginOptions" -ForegroundColor Green
} else {
    Write-Host "DeviceFarm not installed - plugins: $PluginList" -ForegroundColor Yellow
}

# Build Appium command
$DefaultCapabilities = "{`"appium:wdaLocalPort`": $WdaLocalPort,`"appium:mjpegServerPort`": $MpegLocalPort}"

if ($nodeExe -and (Test-Path $appiumScript)) {
    if ($AppiumMajorVersion -eq "3") {
        # Appium 3.x via node
        $AppiumCommand = "& `"$nodeExe`" `"$appiumScript`" server -p $AppiumPort --allow-cors --allow-insecure=xcuitest:get_server_logs --default-capabilities '$DefaultCapabilities' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PluginList $PluginOptions"
    } else {
        # Appium 2.x via node
        $AppiumCommand = "& `"$nodeExe`" `"$appiumScript`" -p $AppiumPort --allow-cors --allow-insecure=get_server_logs --default-capabilities '$DefaultCapabilities' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PluginList $PluginOptions"
    }
} else {
    if ($AppiumMajorVersion -eq "3") {
        # Appium 3.x fallback to wrapper
        $AppiumCommand = "& `"$AppiumBinPath\appium.cmd`" server -p $AppiumPort --allow-cors --allow-insecure=xcuitest:get_server_logs --default-capabilities '$DefaultCapabilities' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PluginList $PluginOptions"
    } else {
        # Appium 2.x fallback to wrapper
        $AppiumCommand = "& `"$AppiumBinPath\appium.cmd`" -p $AppiumPort --allow-cors --allow-insecure=get_server_logs --default-capabilities '$DefaultCapabilities' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PluginList $PluginOptions"
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Executing Appium Command:" -ForegroundColor Cyan
Write-Host $AppiumCommand -ForegroundColor White
Write-Host "========================================" -ForegroundColor Cyan

# Execute Appium
Invoke-Expression $AppiumCommand
