#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create a new release of Appium Bootstrap Installer

.DESCRIPTION
    This script helps create a new release by:
    1. Incrementing the version number
    2. Updating VERSION file
    3. Creating and pushing a git tag
    4. Triggering the GitHub Actions release workflow

.PARAMETER ReleaseType
    Type of release: major, minor, or patch (default: patch)

.PARAMETER Version
    Specific version to use (overrides ReleaseType)

.PARAMETER DryRun
    Show what would be done without making changes

.EXAMPLE
    .\create-release.ps1 -ReleaseType patch
    Creates a patch release (e.g., 0.10.1 -> 0.10.2)

.EXAMPLE
    .\create-release.ps1 -Version 1.0.0
    Creates a specific version release

.EXAMPLE
    .\create-release.ps1 -ReleaseType minor -DryRun
    Shows what a minor release would do without making changes
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('major', 'minor', 'patch')]
    [string]$ReleaseType = 'patch',

    [Parameter()]
    [string]$Version,

    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    Write-Host $Message -ForegroundColor $Color
}

function Get-CurrentVersion {
    $versionFile = "VERSION"
    if (-not (Test-Path $versionFile)) {
        throw "VERSION file not found at: $versionFile"
    }
    
    $currentVersion = Get-Content $versionFile -Raw | ForEach-Object { $_.Trim() }
    return $currentVersion
}

function Get-NextVersion {
    param(
        [string]$CurrentVersion,
        [string]$ReleaseType
    )
    
    $parts = $CurrentVersion -split '\.'
    if ($parts.Count -ne 3) {
        throw "Invalid version format: $CurrentVersion. Expected format: major.minor.patch"
    }
    
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    
    switch ($ReleaseType) {
        'major' {
            $major++
            $minor = 0
            $patch = 0
        }
        'minor' {
            $minor++
            $patch = 0
        }
        'patch' {
            $patch++
        }
    }
    
    return "$major.$minor.$patch"
}

function Test-GitRepository {
    try {
        git rev-parse --git-dir 2>&1 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Test-CleanWorkingDirectory {
    $status = git status --porcelain
    return [string]::IsNullOrWhiteSpace($status)
}

function Update-VersionFile {
    param(
        [string]$NewVersion,
        [bool]$DryRun
    )
    
    $versionFile = "VERSION"
    
    if ($DryRun) {
        Write-ColorOutput "  [DRY RUN] Would update $versionFile to: $NewVersion" "Yellow"
    }
    else {
        Set-Content -Path $versionFile -Value $NewVersion -NoNewline
        Write-ColorOutput "  ✓ Updated $versionFile to: $NewVersion" "Green"
    }
}

function Create-GitTag {
    param(
        [string]$Version,
        [bool]$DryRun
    )
    
    $tag = "v$Version"
    
    if ($DryRun) {
        Write-ColorOutput "  [DRY RUN] Would create and push tag: $tag" "Yellow"
    }
    else {
        # Commit VERSION file change
        git add VERSION
        git commit -m "chore: bump version to $Version"
        
        # Create annotated tag
        git tag -a $tag -m "Release $tag"
        
        # Push commit and tag
        git push origin HEAD
        git push origin $tag
        
        Write-ColorOutput "  ✓ Created and pushed tag: $tag" "Green"
    }
}

# Main script
Write-ColorOutput "`n🚀 Appium Bootstrap Installer - Release Creator`n" "Cyan"

# Verify git repository
if (-not (Test-GitRepository)) {
    Write-ColorOutput "❌ Error: Not a git repository" "Red"
    exit 1
}

# Check for uncommitted changes
if (-not (Test-CleanWorkingDirectory)) {
    Write-ColorOutput "⚠️  Warning: You have uncommitted changes" "Yellow"
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne 'y' -and $continue -ne 'Y') {
        Write-ColorOutput "Aborted." "Yellow"
        exit 0
    }
}

# Get current version
$currentVersion = Get-CurrentVersion
Write-ColorOutput "📦 Current version: $currentVersion" "White"

# Determine new version
if ($Version) {
    $newVersion = $Version
    Write-ColorOutput "🎯 Target version: $newVersion (manual)" "Cyan"
}
else {
    $newVersion = Get-NextVersion -CurrentVersion $currentVersion -ReleaseType $ReleaseType
    Write-ColorOutput "🎯 Target version: $newVersion ($ReleaseType release)" "Cyan"
}

# Validate version format
if ($newVersion -notmatch '^\d+\.\d+\.\d+$') {
    Write-ColorOutput "❌ Error: Invalid version format: $newVersion" "Red"
    Write-ColorOutput "   Expected format: major.minor.patch (e.g., 1.0.0)" "Red"
    exit 1
}

Write-ColorOutput "`nRelease plan:" "White"
Write-ColorOutput "  Current:  $currentVersion" "Gray"
Write-ColorOutput "  New:      $newVersion" "Green"
Write-ColorOutput "  Tag:      v$newVersion" "Green"

if (-not $DryRun) {
    Write-ColorOutput "`n⚠️  This will:" "Yellow"
    Write-ColorOutput "   1. Update publish/VERSION" "Yellow"
    Write-ColorOutput "   2. Commit the change" "Yellow"
    Write-ColorOutput "   3. Create and push tag v$newVersion" "Yellow"
    Write-ColorOutput "   4. Trigger GitHub Actions release workflow" "Yellow"
    
    $confirm = Read-Host "`nProceed with release? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-ColorOutput "Aborted." "Yellow"
        exit 0
    }
}

Write-ColorOutput "`n📝 Creating release...`n" "Cyan"

try {
    # Update VERSION file
    Update-VersionFile -NewVersion $newVersion -DryRun $DryRun

    # Create and push git tag
    Create-GitTag -Version $newVersion -DryRun $DryRun

    if ($DryRun) {
        Write-ColorOutput "`n✅ Dry run completed successfully!" "Green"
        Write-ColorOutput "`nTo create the release for real, run:" "Cyan"
        if ($Version) {
            Write-ColorOutput "  .\create-release.ps1 -Version $Version" "White"
        }
        else {
            Write-ColorOutput "  .\create-release.ps1 -ReleaseType $ReleaseType" "White"
        }
    }
    else {
        Write-ColorOutput "`n✅ Release v$newVersion created successfully!" "Green"
        Write-ColorOutput "`n📋 Next steps:" "Cyan"
        Write-ColorOutput "  1. GitHub Actions will build for all platforms" "White"
        Write-ColorOutput "  2. Release will be published at: https://github.com/$((git config --get remote.origin.url) -replace '.*github\.com[:/]', '' -replace '\.git$', '')/releases/tag/v$newVersion" "White"
        Write-ColorOutput "  3. Monitor the workflow at: https://github.com/$((git config --get remote.origin.url) -replace '.*github\.com[:/]', '' -replace '\.git$', '')/actions" "White"
    }
}
catch {
    Write-ColorOutput "`n❌ Error: $($_.Exception.Message)" "Red"
    exit 1
}
