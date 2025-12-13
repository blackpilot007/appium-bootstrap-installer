# GitHub Copilot Instructions for Appium Bootstrap Installer

## Project Overview
**Appium Bootstrap Installer** is a cross-platform automation tool for installing and managing Appium infrastructure on Windows, macOS, and Linux. It runs as a portable, non-admin service that automatically detects connected iOS/Android devices and spins up dedicated Appium server instances.

## Core Architecture

### Execution Model
- **Portable Process Mode (Default)**: Appium servers run as child processes managed by the C# application
- **No System Services Required**: Designed to run without NSSM, Servy, or Systemd by default
- **Optional Service Managers**: Systemd (Linux) and Supervisor (macOS) support available for advanced users

### Technology Stack
- **Language**: C# (.NET 8)
- **Logging**: Serilog with structured logging
- **Config Format**: JSON with JSON serialization context
- **Node.js Management**:
  - **Windows**: `nvm-windows` (portable, no-install version) - no admin required
  - **macOS/Linux**: `nvm` (Node Version Manager) - standard shell-based

### Key Components
1. **ConfigurationReader**: Loads and validates `config.json`
2. **ScriptExecutor**: Executes platform-specific PowerShell/Bash scripts
3. **DeviceListenerService**: Monitors USB device connections (adb/idevice_id)
4. **AppiumSessionManager**: Manages Appium server processes per device
5. **DeviceRegistry**: Tracks device state and session mappings
6. **DeviceMetrics**: Prometheus-style metrics for monitoring

## Code Style & Standards

### General Guidelines
- **No Admin Required**: All operations must work without elevation
- **Portable by Default**: Avoid system-wide installations
- **Cross-Platform**: Test on Windows, macOS (Intel/ARM), and Linux
- **Structured Logging**: Use Serilog with context properties
- **Error Handling**: Implement retry logic with exponential backoff

### C# Conventions
```csharp
// Use logger with context
_logger.LogInformation("Starting session for {DeviceId} on port {Port}", device.Id, port);

// Guard clauses for validation
if (string.IsNullOrWhiteSpace(config.InstallFolder))
{
    throw new ArgumentException("InstallFolder cannot be empty");
}

// Use CancellationToken for async operations
public async Task<bool> StartAsync(CancellationToken cancellationToken)

// Dispose patterns for processes
using var process = new Process { StartInfo = psi };
```

### PowerShell Script Standards
```powershell
# Use proper error handling
try {
    # Operation
    Write-Log "Success message"
}
catch {
    Write-Log "Error: $($_.Exception.Message)" "ERR"
    throw
}

# Avoid $_ in double-quoted strings (use ${} or capture first)
$errorMsg = $_.Exception.Message
Write-Log "Error: $errorMsg" "ERR"

# Initialize environment for nvm-windows (no-admin approach)
$env:NVM_HOME = $nvmPath
$env:Path = "$nodejsPath;$nvmPath;$env:Path"

# Copy Node.js instead of using 'nvm use' (avoids admin-required symlinks)
Copy-Item -Path "$nvmPath\v$NodeVersion.*" -Destination $nodejsPath -Recurse -Force
```

### Bash Script Standards
```bash
# Exit on error with trap
set -e
trap 'handle_error ${LINENO}' ERR

# Use portable shebang
#!/bin/bash

# Load nvm properly
export NVM_DIR="$INSTALL_FOLDER/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
```

## Configuration Management

### Config Schema
- **installFolder**: Local installation path (non-admin writable)
- **cleanInstallFolder**: Delete before install (default: false for incremental updates)
- **nodeVersion**: Major version number (e.g., "22")
- **appiumVersion**: Semantic version (e.g., "2.17.1")
- **drivers/plugins**: Arrays with name, version, enabled flags
- **enableDeviceListener**: Auto-start device monitoring
- **portRanges**: Dynamic port allocation for sessions

### Environment Variables
- **APPIUM_HOME**: Points to appium-home directory for drivers/plugins
- **NVM_HOME**: (Windows) nvm installation directory
- **NVM_DIR**: (macOS/Linux) nvm installation directory
- **Path**: Must include nodejs directory (copied files, not symlink)

## Common Tasks

### Adding New Features
1. Update `InstallConfig.cs` model with new properties
2. Add JSON property name attribute: `[JsonPropertyName("propertyName")]`
3. Implement logic in service layer (e.g., `AppiumSessionManager.cs`)
4. Update platform scripts if installation changes needed
5. Document in `README.md` and `USER_GUIDE.md`

### Fixing Installation Issues
1. Check logs in `logs/installer-YYYYMMDD.log`
2. Verify script paths in `ScriptExecutor.cs`
3. Test script manually: `powershell -ExecutionPolicy Bypass -File script.ps1 -Param value`
4. Ensure environment variables are set before commands run
5. Add retry logic for transient failures

### Device Listener Debugging
- Poll interval: `deviceListenerPollInterval` (default: 5 seconds)
- Check `adb devices` and `idevice_id -l` work standalone
- Review `DeviceListenerService.cs` for regex patterns
- Verify port allocation in `AppiumSessionManager.AllocateConsecutivePortsAsync`

### Appium Binary Detection
When using `--no-bin-links` npm flag (required for no-admin installation):
- **Primary Path**: `$appiumHome\node_modules\appium\build\lib\main.js`
- **Fallback Path**: `$appiumHome\node_modules\.bin\appium.cmd` (only if bin-links enabled)
- **Execution Pattern**: `& "$nodePath\node.exe" "$appiumScript" --version`
- **Why**: npm `--no-bin-links` prevents creation of `.bin` directory and wrapper scripts
- **All Functions Must Use**: Check `main.js` first, then fallback to `.bin\appium.cmd`

### Cross-Platform Testing
- **Windows**: Test with PowerShell 5.1 (not Core)
- **macOS**: Test on both Intel and Apple Silicon
- **Linux**: Test on Ubuntu/Debian and RHEL/CentOS
- Use `RuntimeInformation.IsOSPlatform()` for OS detection
- Test `nvm-windows` on Windows, `nvm` on Unix

## File Structure

```
AppiumBootstrapInstaller/
├── Program.cs                      # Entry point with DI setup
├── Models/
│   ├── InstallConfig.cs            # Configuration schema
│   ├── DeviceModels.cs             # Device/Session models
│   └── JsonSerializerContext.cs    # AOT JSON context
├── Services/
│   ├── ConfigurationReader.cs      # Config loading
│   ├── ScriptExecutor.cs           # Cross-platform script execution
│   ├── DeviceListenerService.cs    # USB device monitoring
│   ├── AppiumSessionManager.cs     # Process management
│   ├── DeviceRegistry.cs           # State persistence
│   └── DeviceMetrics.cs            # Metrics collection
└── Platform/
    ├── Windows/Scripts/            # PowerShell (.ps1)
    ├── MacOS/Scripts/              # Bash (.sh)
    └── Linux/Scripts/              # Bash (.sh)
```

## Testing Checklist

### Before Committing
- [ ] Code compiles without warnings (`dotnet build`)
- [ ] Runs on target platform(s)
- [ ] Logs are structured and informative
- [ ] Error handling covers edge cases
- [ ] No hardcoded paths or admin requirements
- [ ] Configuration changes documented
- [ ] Scripts tested with fresh install (`cleanInstallFolder: true`)
- [ ] Scripts tested with incremental update (`cleanInstallFolder: false`)

### Device Listener Tests
- [ ] Android device connect/disconnect detected
- [ ] iOS device connect/disconnect detected
- [ ] Multiple devices handled correctly
- [ ] Port exhaustion handled gracefully
- [ ] Process cleanup on disconnect

## Troubleshooting Guide

### Windows NVM Issues
**Problem**: "ERROR open \settings.txt: The system cannot find the file specified"
```powershell
# Solution: Ensure settings.txt exists in NVM_HOME and environment variable is set
$settingsContent = @"
root: $nvmPath
path: $nodejsPath
arch: 64
proxy: none
"@
Set-Content -Path "$nvmPath\settings.txt" -Value $settingsContent -Encoding ASCII
$env:NVM_HOME = $nvmPath
```

**Problem**: "nvm use" prompts for UAC/admin access
```powershell
# Solution: Copy Node.js files instead of using symlinks (no admin required)
$sourceDir = Get-ChildItem -Path $nvmPath -Directory | Where-Object { $_.Name -match "^v$NodeVersion\." } | Select-Object -First 1
if (Test-Path $nodejsPath) { Remove-ItemWithRetries -Path $nodejsPath -Recurse -Force }
Copy-Item -Path $sourceDir.FullName -Destination $nodejsPath -Recurse -Force
# This avoids symlink/junction creation which requires admin privileges on Windows
```

**Problem**: "Appium binary not found after installation"
```powershell
# Solution: When using --no-bin-links, check main.js instead of appium.cmd
$appiumScript = "$appiumHome\node_modules\appium\build\lib\main.js"
if (Test-Path $appiumScript) {
    $appiumVersion = & "$nodePath\node.exe" $appiumScript --version
}
# The .bin directory is not created when using --no-bin-links flag
```

### macOS/Linux nvm Issues
**Problem**: "nvm: command not found"
```bash
# Solution: Source nvm.sh explicitly
export NVM_DIR="$INSTALL_FOLDER/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
nvm use $NODE_VERSION
```

### NPM Configuration Issues
**Problem**: "The 'global-style' option is deprecated"
```powershell
# Solution: Remove deprecated global-style config (npm 10+)
& $npmPath config set prefix "$appiumHome" --location user
& $npmPath config set bin-links false --location user
# Do NOT set global-style - it's deprecated in favor of --install-strategy
```

**Problem**: npm trying to create symlinks requiring admin access
```powershell
# Solution: Use --no-bin-links flag for all npm install operations
npm install appium@$version --prefix $appiumHome --no-bin-links --legacy-peer-deps
# This prevents symlink creation in .bin directory
```

### Port Allocation Failures
**Problem**: "No available consecutive ports"
- Check `portRanges` in config (default: 4723-4823)
- Verify no zombie processes holding ports
- Increase range or implement port recycling

### Process Management
**Problem**: Appium servers not stopping
- Check `_runningProcesses` dictionary in `AppiumSessionManager`
- Verify `StopLocalProcessAsync` is called on disconnect
- Add process.Kill() as fallback if graceful stop fails

## Version Control

### Commit Messages
```
feat: Add support for Appium 3.x configuration
fix: Resolve nvm environment initialization on Windows
docs: Update installation guide for nvm-windows migration
refactor: Remove legacy NSSM service code
test: Add device listener integration tests
```

### Branch Strategy
- `main`: Stable release branch
- `develop`: Active development
- Feature branches: `feature/<name>`
- Bugfix branches: `fix/<issue-number>`

## Documentation Standards

### Code Comments
- Use XML documentation comments for public APIs
- Explain "why" not "what" in inline comments
- Document non-obvious workarounds or platform quirks

### User Documentation
- `README.md`: Quick start and overview
- `USER_GUIDE.md`: Detailed usage instructions
- `ARCHITECTURE.md`: System design and flow
- `TROUBLESHOOTING.md`: Common issues and solutions
- `CONFIGURATION.md`: Configuration reference

## Important Notes

### Do NOT
- ❌ Use system services (NSSM/Servy/Systemd) in default flow
- ❌ Require administrator/sudo privileges
- ❌ Use `nvm use` on Windows (creates admin-requiring symlinks)
- ❌ Use deprecated npm configs like `global-style`
- ❌ Expect `.bin` directory when using `--no-bin-links`
- ❌ Install globally unless explicitly configured
- ❌ Hardcode paths or platform-specific logic in C#
- ❌ Use deprecated APIs without migration plan

### DO
- ✅ Support portable, non-admin installation
- ✅ Copy Node.js files instead of creating symlinks (Windows)
- ✅ Use `--no-bin-links` flag for npm install operations
- ✅ Check for `main.js` when locating Appium binary
- ✅ Use child processes for Appium servers
- ✅ Implement proper cleanup and disposal
- ✅ Log structured data for debugging
- ✅ Test on all target platforms
- ✅ Handle transient failures with retry logic
- ✅ Document configuration options
- ✅ Use `nvm-windows` (portable) on Windows, `nvm` on Unix
- ✅ Provide detailed diagnostic output when installations fail

## Questions?
Refer to existing code patterns in:
- `AppiumSessionManager.cs` - Process management patterns
- `ScriptExecutor.cs` - Cross-platform script execution
- `DeviceListenerService.cs` - Background service patterns
- `InstallDependencies.ps1` / `.sh` - Installation script structure
