# Quick Release Guide

## TL;DR

### Create a Release

**Windows:**
```powershell
.\create-release.ps1 -ReleaseType patch
```

**macOS/Linux:**
```bash
./create-release.sh --type patch
```

**GitHub Web:**
1. Go to Actions → Create Release
2. Click "Run workflow"
3. Select release type
4. Click "Run workflow"

## Release Types

| Type | Example | When to Use |
|------|---------|-------------|
| **patch** | 0.10.1 → 0.10.2 | Bug fixes, minor improvements |
| **minor** | 0.10.1 → 0.11.0 | New features, enhancements |
| **major** | 0.10.1 → 1.0.0 | Breaking changes, major milestones |

## What Gets Built

✅ Windows x64 & ARM64  
✅ macOS Intel (x64) & Apple Silicon (ARM64)  
✅ Linux x64 & ARM64  

All with:
- Single-file executable
- Platform scripts
- Config samples
- Documentation
- SHA256 checksums

## Quick Commands

```powershell
# Preview what will happen
.\create-release.ps1 -ReleaseType patch -DryRun

# Create specific version
.\create-release.ps1 -Version 1.0.0

# Create minor release
.\create-release.ps1 -ReleaseType minor
```

## After Release

1. Check [Actions](https://github.com/blackpilot007/appium-bootstrap-installer/actions) for build status
2. View [Releases](https://github.com/blackpilot007/appium-bootstrap-installer/releases) when complete
3. Download and test one platform

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Uncommitted changes | `git add . && git commit -m "message"` |
| Script not executable | `chmod +x create-release.sh` |
| Workflow not triggering | Verify tag pushed: `git push origin v0.10.2` |

## Full Documentation

See [RELEASE_PROCESS.md](RELEASE_PROCESS.md) for complete details.
