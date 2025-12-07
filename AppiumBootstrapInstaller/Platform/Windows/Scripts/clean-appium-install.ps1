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
        Remove-ItemWithRetries -Path $appiumHome -Recurse -Force
        Write-Log "Removed Appium home directory"
    }
    else {
        Write-Log "Appium home directory not found"
    }
    
    # Remove global Appium config
    $appiumConfigDir = "$env:USERPROFILE\.appium"
    if (Test-Path $appiumConfigDir) {
        Write-Log "Removing global Appium config: $appiumConfigDir"
        Remove-ItemWithRetries -Path $appiumConfigDir -Recurse -Force
        Write-Log "Removed global Appium config"
    }
    
    # Remove wrapper scripts
    $binDir = "$InstallFolder\bin"
    if (Test-Path "$binDir\appium.bat") {
        Write-Log "Removing Appium wrapper scripts"
        Remove-ItemWithRetries -Path "$binDir\appium.bat" -Force
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
