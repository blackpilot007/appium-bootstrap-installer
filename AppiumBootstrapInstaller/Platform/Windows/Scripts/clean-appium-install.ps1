# clean-appium-install.ps1
# Script to clean and reinstall Appium on Windows

param(
    [string]$InstallFolder = "$env:USERPROFILE\.local",
    [switch]$Force
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp $Level] $Message"
}

Write-Log "Clean Appium Installation Script for Windows"
Write-Log "Installation Folder: $InstallFolder"

if (-not $Force) {
    $response = Read-Host "This will delete the existing Appium installation. Continue? (y/n)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Log "Operation cancelled by user"
        exit 0
    }
}

try {
    # Stop any running Appium processes
    Write-Log "Stopping any running Appium processes..."
    Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -like "*appium*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    
    # Remove Appium home directory
    $appiumHome = "$InstallFolder\appium-home"
    if (Test-Path $appiumHome) {
        Write-Log "Removing Appium home directory: $appiumHome"
        Remove-Item -Path $appiumHome -Recurse -Force -ErrorAction SilentlyContinue
        Write-Log "Removed Appium home directory"
    }
    else {
        Write-Log "Appium home directory not found"
    }
    
    # Remove global Appium config
    $appiumConfigDir = "$env:USERPROFILE\.appium"
    if (Test-Path $appiumConfigDir) {
        Write-Log "Removing global Appium config: $appiumConfigDir"
        Remove-Item -Path $appiumConfigDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Log "Removed global Appium config"
    }
    
    # Remove wrapper scripts
    $binDir = "$InstallFolder\bin"
    if (Test-Path "$binDir\appium.bat") {
        Write-Log "Removing Appium wrapper scripts"
        Remove-Item -Path "$binDir\appium.bat" -Force -ErrorAction SilentlyContinue
    }
    
    Write-Log "================================================================"
    Write-Log "           APPIUM CLEANUP COMPLETED SUCCESSFULLY                "
    Write-Log "================================================================"
    Write-Log ""
    Write-Log "To reinstall Appium, run:"
    Write-Log "  .\InstallDependencies.ps1 -InstallFolder '$InstallFolder'"
}
catch {
    Write-Log "Error during cleanup: $_" "ERROR"
    exit 1
}
