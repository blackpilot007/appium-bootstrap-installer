# check_appium_drivers.ps1
# Script to check Appium drivers installation on Windows

param(
    [string]$AppiumHome = "$env:USERPROFILE\.local\appium-home"
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp $Level] $Message"
}

Write-Log "Checking Appium drivers installation..."
Write-Log "APPIUM_HOME: $AppiumHome"

$env:APPIUM_HOME = $AppiumHome
# Prefer node + main.js to avoid relying on appium.cmd wrapper which requires 'node' on PATH
$nodeExe = $null
try {
    $nodeExe = (Get-Command node.exe -ErrorAction SilentlyContinue).Source
} catch {
    $nodeExe = $null
}

if (-not $nodeExe) {
    # Try locating node under a user-local nvm installation near the Appium home
    $installFolder = Split-Path $AppiumHome -Parent
    $nvmDir = Join-Path $installFolder "nvm"
    if (Test-Path $nvmDir) {
        $versioned = Get-ChildItem -Path $nvmDir -Directory -Filter "v*" | Sort-Object Name -Descending | Select-Object -First 1
        if ($versioned) {
            $nodeCandidate = Join-Path $versioned.FullName "node.exe"
            if (Test-Path $nodeCandidate) { $nodeExe = $nodeCandidate }
        }
    }
    
    # Try locating node under fnm
    if (-not $nodeExe) {
        $fnmDir = Join-Path $installFolder "fnm\node-versions"
        if (Test-Path $fnmDir) {
            $versioned = Get-ChildItem -Path $fnmDir -Directory -Filter "v*" | Sort-Object Name -Descending | Select-Object -First 1
            if ($versioned) {
                $nodeCandidate = Join-Path $versioned.FullName "node.exe"
                if (Test-Path $nodeCandidate) { $nodeExe = $nodeCandidate }
            }
        }
    }
}

$appiumCmd = Join-Path $AppiumHome "..\bin\appium.cmd"
$appiumScript = Join-Path $AppiumHome "node_modules\appium\build\lib\main.js"

if (-not $nodeExe -and -not (Test-Path $appiumCmd)) {
    Write-Log "Appium not found (no node or wrapper found)" "ERROR"
    exit 1
}

try {
    Write-Log "Appium version:"
    if ($nodeExe -and (Test-Path $appiumScript)) {
        & $nodeExe $appiumScript --version
    } else {
        & $appiumCmd --version
    }

    Write-Log "`nInstalled drivers:"
    if ($nodeExe -and (Test-Path $appiumScript)) {
        & $nodeExe $appiumScript driver list --installed
    } else {
        & $appiumCmd driver list --installed
    }

    Write-Log "`nAvailable drivers:"
    if ($nodeExe -and (Test-Path $appiumScript)) {
        & $nodeExe $appiumScript driver list
    } else {
        & $appiumCmd driver list
    }
    
    Write-Log "`nDriver directories:"
    $driversPath = "$AppiumHome\node_modules"
    if (Test-Path $driversPath) {
        Get-ChildItem -Path $driversPath -Filter "appium-*-driver" -Directory | ForEach-Object {
            Write-Log "  - $($_.Name)"
            $packageJson = Join-Path $_.FullName "package.json"
            if (Test-Path $packageJson) {
                $package = Get-Content $packageJson -Raw | ConvertFrom-Json
                Write-Log "    Version: $($package.version)"
            }
        }
    }
    
    Write-Log "`nDriver check completed successfully"
}
catch {
    Write-Log "Error checking drivers: $_" "ERROR"
    exit 1
}
