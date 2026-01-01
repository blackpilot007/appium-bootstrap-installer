# Release Process Guide

This guide explains how to create new releases of Appium Bootstrap Installer for Windows, macOS, and Linux.

## Overview

The release process is automated through GitHub Actions and can be triggered in three ways:

1. **Automated Tag Release** - Push a version tag to trigger builds
2. **Manual Release Script** - Use helper scripts for convenience
3. **GitHub UI Release** - Trigger from GitHub Actions web interface

## Quick Start

### Option 1: Using the Release Script (Recommended)

**Windows (PowerShell):**
```powershell
# Create a patch release (0.10.1 -> 0.10.2)
.\create-release.ps1 -ReleaseType patch

# Create a minor release (0.10.1 -> 0.11.0)
.\create-release.ps1 -ReleaseType minor

# Create a major release (0.10.1 -> 1.0.0)
.\create-release.ps1 -ReleaseType major

# Create a specific version
.\create-release.ps1 -Version 1.5.0

# Preview changes without committing
.\create-release.ps1 -ReleaseType patch -DryRun
```

**macOS/Linux (Bash):**
```bash
# Make script executable
chmod +x create-release.sh

# Create a patch release
./create-release.sh --type patch

# Create a minor release
./create-release.sh --type minor

# Create a specific version
./create-release.sh --version 1.5.0

# Preview changes
./create-release.sh --type patch --dry-run
```

### Option 2: Manual Tag Creation

```bash
# Update VERSION file
echo "0.10.2" > publish/VERSION

# Commit and create tag
git add publish/VERSION
git commit -m "chore: bump version to 0.10.2"
git tag -a v0.10.2 -m "Release v0.10.2"

# Push to trigger release
git push origin HEAD
git push origin v0.10.2
```

### Option 3: GitHub Web Interface

1. Go to **Actions** tab in your repository
2. Select **Create Release** workflow
3. Click **Run workflow**
4. Choose release type or enter version
5. Click **Run workflow** button

## What Happens During Release

When you trigger a release, GitHub Actions automatically:

1. **Builds for all platforms:**
   - Windows x64
   - Windows ARM64
   - macOS Intel (x64)
   - macOS Apple Silicon (ARM64)
   - Linux x64
   - Linux ARM64

2. **Packages each build with:**
   - Executable binary
   - Platform-specific scripts
   - Configuration samples
   - Documentation (README, USER_GUIDE, LICENSE)
   - VERSION file

3. **Creates release artifacts:**
   - ZIP archives for each platform
   - SHA256 checksums for verification

4. **Publishes to GitHub:**
   - Creates a GitHub Release
   - Uploads all platform builds
   - Includes release notes
   - Marks as pre-release (if specified)

## Release Workflow Details

### Build Matrix

The workflow builds on native runners for optimal compatibility:

| Platform | Runner | Runtime ID | Artifact Name |
|----------|--------|------------|---------------|
| Windows x64 | windows-latest | win-x64 | AppiumBootstrapInstaller-v{version}-win-x64.zip |
| Windows ARM64 | windows-latest | win-arm64 | AppiumBootstrapInstaller-v{version}-win-arm64.zip |
| macOS Intel | macos-13 | osx-x64 | AppiumBootstrapInstaller-v{version}-osx-x64.zip |
| macOS Apple Silicon | macos-latest | osx-arm64 | AppiumBootstrapInstaller-v{version}-osx-arm64.zip |
| Linux x64 | ubuntu-latest | linux-x64 | AppiumBootstrapInstaller-v{version}-linux-x64.zip |
| Linux ARM64 | ubuntu-latest | linux-arm64 | AppiumBootstrapInstaller-v{version}-linux-arm64.zip |

### Version Numbering

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR.MINOR.PATCH** (e.g., 1.2.3)
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Types

- **Patch** (0.10.1 → 0.10.2): Bug fixes, minor improvements
- **Minor** (0.10.1 → 0.11.0): New features, enhancements
- **Major** (0.10.1 → 1.0.0): Breaking changes, major milestones

## Pre-Release Checklist

Before creating a release:

- [ ] All tests pass locally
- [ ] RELEASE_NOTES.md is updated with changes
- [ ] No uncommitted changes in working directory
- [ ] Version number follows semantic versioning
- [ ] Breaking changes are documented
- [ ] README and USER_GUIDE are up to date

## Post-Release Checklist

After release is published:

- [ ] Verify all platform builds succeeded
- [ ] Download and test at least one platform
- [ ] Check release notes are correct
- [ ] Update documentation if needed
- [ ] Announce release (if applicable)

## Testing a Release Locally

Test builds before pushing to production:

```bash
# Build all platforms locally
./build-all.sh  # macOS/Linux
.\build-all.ps1  # Windows

# Builds will be in ./publish/{runtime}/ directories
```

## Troubleshooting

### Release Script Issues

**Problem:** "Not a git repository"
```bash
# Solution: Ensure you're in the repository root
cd /path/to/appium-bootstrap-installer
```

**Problem:** "Uncommitted changes"
```bash
# Solution: Commit or stash changes
git add .
git commit -m "description"
# or
git stash
```

### GitHub Actions Issues

**Problem:** Workflow not triggering
```bash
# Check tag was pushed correctly
git ls-remote --tags origin

# Re-push tag if needed
git push origin v0.10.2
```

**Problem:** Build fails on specific platform
- Check Actions logs for detailed error
- Verify .NET SDK compatibility
- Ensure platform-specific scripts are valid

### Version File Issues

**Problem:** VERSION file not updated
```bash
# Manually update
echo "0.10.2" > publish/VERSION
git add publish/VERSION
git commit -m "chore: update VERSION file"
git push
```

## Advanced Usage

### Creating Pre-Releases

Pre-releases are useful for testing before official release:

```bash
# Using GitHub UI
1. Go to Actions → Create Release
2. Enter version (e.g., 1.0.0-rc.1)
3. Check "Mark as pre-release"
4. Run workflow

# Manual approach
git tag -a v1.0.0-rc.1 -m "Release candidate 1"
git push origin v1.0.0-rc.1
# Then edit release in GitHub UI to mark as pre-release
```

### Hotfix Releases

For urgent fixes on old versions:

```bash
# Create hotfix branch from tag
git checkout -b hotfix/0.10.2 v0.10.1
# Make fixes
git commit -m "fix: critical bug"
# Create new tag
git tag -a v0.10.2 -m "Hotfix release"
git push origin hotfix/0.10.2
git push origin v0.10.2
```

### Release Notes Format

Keep RELEASE_NOTES.md structured:

```markdown
## [0.10.2] - 2025-01-15

### Added
- New feature description

### Changed
- Modified behavior description

### Fixed
- Bug fix description

### Deprecated
- Features marked for removal

### Removed
- Removed features

### Security
- Security fixes
```

## Monitoring Releases

### Check Release Status

1. **GitHub Actions**: `https://github.com/{owner}/{repo}/actions`
2. **Releases Page**: `https://github.com/{owner}/{repo}/releases`
3. **Download Stats**: Available in release page

### Artifacts Retention

- **Build artifacts**: 90 days
- **Release assets**: Permanent (until manually deleted)
- **Logs**: 90 days

## Best Practices

1. **Regular Releases**: Release frequently with smaller changes
2. **Version Tags**: Always use annotated tags (`git tag -a`)
3. **Release Notes**: Document all user-facing changes
4. **Testing**: Test on all platforms before marking stable
5. **Communication**: Announce breaking changes clearly
6. **Backups**: GitHub automatically maintains release history

## Rollback Procedure

If a release has critical issues:

1. **Mark as Draft/Pre-release**:
   - Edit release in GitHub UI
   - Check "Set as pre-release"
   - Add warning to description

2. **Create Hotfix**:
   ```bash
   # Fix the issue
   ./create-release.sh --type patch
   ```

3. **Delete Bad Release** (last resort):
   ```bash
   # Delete tag locally and remotely
   git tag -d v0.10.2
   git push origin :refs/tags/v0.10.2
   # Delete release in GitHub UI
   ```

## Support

For issues with the release process:
- Check [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)
- Review GitHub Actions logs
- Open an issue with `[release]` prefix

## License

Copyright 2025 Appium Bootstrap Installer Contributors

Licensed under the Apache License, Version 2.0
