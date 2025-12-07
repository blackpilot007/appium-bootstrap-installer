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
$appiumExe = "$AppiumHome\node_modules\.bin\appium.cmd"

if (-not (Test-Path $appiumExe)) {
    Write-Log "Appium not found at $appiumExe" "ERROR"
    exit 1
}

try {
    Write-Log "Appium version:"
    & $appiumExe --version
    
    Write-Log "`nInstalled drivers:"
    & $appiumExe driver list --installed
    
    Write-Log "`nAvailable drivers:"
    & $appiumExe driver list
    
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
