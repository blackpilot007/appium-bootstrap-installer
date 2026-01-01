# ScriptPlugin Runtime Support Enhancements

## Overview
ScriptPlugin has been enhanced to support multiple scripting runtimes with automatic detection and appropriate wrapper execution. This provides a unified API for running scripts across different languages and platforms.

## Supported Runtimes

### 1. PowerShell (.ps1)
**Auto-detection**: Files ending with `.ps1`  
**Runtime hint**: `"powershell"`  
**Wrapper**: `powershell.exe -ExecutionPolicy Bypass -File "script.ps1" args...`  
**Platform**: Windows (primary), Cross-platform with PowerShell Core

**Example Configuration**:
```json
{
  "name": "powershell-monitor",
  "type": "script",
  "executable": "scripts/monitor.ps1",
  "arguments": ["--verbose"],
  "enabled": true
}
```

### 2. Bash (.sh)
**Auto-detection**: Files ending with `.sh`  
**Runtime hint**: `"bash"`  
**Wrapper**: `bash script.sh args...`  
**Platform**: Linux, macOS, Windows (with Git Bash/WSL)

**Example Configuration**:
```json
{
  "name": "bash-watcher",
  "type": "script",
  "executable": "scripts/watcher.sh",
  "runtime": "bash",
  "enabled": true
}
```

### 3. Python (.py) **[NEW]**
**Auto-detection**: Files ending with `.py`  
**Runtime hint**: `"python"` or `"python3"`  
**Wrapper**: 
- Windows: `python "script.py" args...`
- Linux/macOS: `python3 "script.py" args...`
**Platform**: Cross-platform (requires Python installed)

**Example Configuration**:
```json
{
  "name": "python-analyzer",
  "type": "script",
  "executable": "scripts/analyzer.py",
  "arguments": ["--mode", "realtime"],
  "environmentVariables": {
    "PYTHONUNBUFFERED": "1"
  },
  "enabled": true
}
```

### 4. Node.js (.js) **[NEW]**
**Auto-detection**: Files ending with `.js`  
**Runtime hint**: `"node"` or `"nodejs"`  
**Wrapper**: `node "script.js" args...`  
**Platform**: Cross-platform (requires Node.js installed)

**Example Configuration**:
```json
{
  "name": "node-metrics",
  "type": "script",
  "executable": "scripts/metrics.js",
  "arguments": ["--port", "3000"],
  "enabled": true
}
```

### 5. Batch/CMD (.bat, .cmd) **[NEW]**
**Auto-detection**: Files ending with `.bat` or `.cmd`  
**Runtime hint**: `"batch"` or `"cmd"`  
**Wrapper**: `cmd.exe /c "script.bat" args...`  
**Platform**: Windows only

**Example Configuration**:
```json
{
  "name": "batch-cleanup",
  "type": "script",
  "executable": "scripts/cleanup.bat",
  "runtime": "batch",
  "enabled": true
}
```

## Runtime Detection Logic

The plugin uses the following priority for determining the runtime:

1. **Explicit `runtime` configuration property**
   ```json
   {
     "executable": "myscript.txt",
     "runtime": "python"
   }
   ```

2. **`runtime` environment variable** (legacy support)
   ```json
   {
     "executable": "myscript",
     "environmentVariables": {
       "runtime": "node"
     }
   }
   ```

3. **Automatic file extension detection**
   - `.ps1` → PowerShell
   - `.sh` → Bash
   - `.py` → Python
   - `.js` → Node.js
   - `.bat` / `.cmd` → Batch

4. **Default fallback**: PowerShell (if no detection matches)

## Advanced Usage

### Custom Runtime Executable Paths
Use environment variables or working directory configuration to specify custom interpreter paths:

```json
{
  "name": "custom-python",
  "type": "script",
  "executable": "analyzer.py",
  "runtime": "python",
  "environmentVariables": {
    "PATH": "/opt/python3.11/bin:$PATH"
  }
}
```

### Cross-Platform Scripts
For scripts that should work across platforms, use runtime hints with platform-specific executables:

```json
{
  "name": "cross-platform-monitor",
  "type": "script",
  "executable": "{{INSTALL_FOLDER}}/scripts/monitor.sh",
  "runtime": "bash",
  "enabled": true
}
```

On Windows with Git Bash or WSL, this will execute using `bash`.

### Template Variable Expansion
All runtime wrappers support template variable expansion:

```json
{
  "name": "device-handler",
  "type": "script",
  "executable": "handlers/device_handler.py",
  "arguments": [
    "--device", "{{DEVICE_ID}}",
    "--port", "{{APPIUM_PORT}}"
  ],
  "runtime": "python"
}
```

## Testing

Comprehensive tests validate:
- ✅ PowerShell script execution (.ps1)
- ✅ Bash script execution (.sh)
- ✅ Python script execution (.py)
- ✅ Node.js script execution (.js)
- ✅ Batch script execution (.bat, .cmd)
- ✅ Custom runtime hints
- ✅ Template variable expansion
- ✅ Environment variable propagation
- ✅ Error handling and logging
- ✅ Health checks
- ✅ Cross-platform compatibility

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~ScriptPluginComprehensiveTests"
```

## Migration Guide

### From ProcessPlugin to ScriptPlugin

**Before (ProcessPlugin)**:
```json
{
  "name": "python-script",
  "type": "process",
  "executable": "python",
  "arguments": ["scripts/monitor.py", "--verbose"]
}
```

**After (ScriptPlugin with auto-detection)**:
```json
{
  "name": "python-script",
  "type": "script",
  "executable": "scripts/monitor.py",
  "arguments": ["--verbose"]
}
```

The runtime is now automatically detected from the `.py` extension!

## Requirements

For runtime support to work, ensure the following are installed:

| Runtime | Windows | Linux/macOS |
|---------|---------|-------------|
| PowerShell | Built-in (5.1+) or PowerShell Core | PowerShell Core |
| Bash | Git Bash, WSL, or Cygwin | Built-in |
| Python | Python 3.x (in PATH) | Python 3.x (python3) |
| Node.js | Node.js (in PATH) | Node.js (node) |
| Batch/CMD | Built-in | N/A |

## Troubleshooting

### Runtime Not Found
**Error**: `The system cannot find the file specified`

**Solution**: Ensure the runtime executable is in the system PATH:
- Python: `python --version` or `python3 --version`
- Node.js: `node --version`
- Bash: `bash --version`

### Script Execution Permissions
**Error**: Permission denied (Linux/macOS)

**Solution**: Make scripts executable:
```bash
chmod +x scripts/monitor.sh
chmod +x scripts/analyzer.py
```

### Wrong Runtime Detected
**Error**: Script runs with wrong interpreter

**Solution**: Explicitly specify the runtime:
```json
{
  "executable": "myscript.custom",
  "runtime": "python"
}
```

## Architecture Benefits

1. **Unified API**: Single plugin type for all script types
2. **Auto-detection**: Less configuration needed
3. **Cross-platform**: Consistent behavior across OS
4. **Template Support**: Full variable expansion
5. **Error Handling**: Graceful failures with logging
6. **Health Checks**: Monitor script lifecycle
7. **Process Management**: Proper cleanup and disposal

## Future Enhancements

Potential future runtime support:
- Ruby (`.rb`)
- Perl (`.pl`)
- PowerShell Core explicit targeting
- Custom interpreter paths in configuration
- Shebang line detection for Unix scripts
- WSL-specific runtime targeting on Windows

## Related Documentation

- [Plugin Architecture](PLUGIN_ARCHITECTURE.md)
- [Plugin Quick Start](PLUGIN_QUICK_START.md)
- [Configuration Reference](CONFIGURATION.md)
- [Troubleshooting Guide](TROUBLESHOOTING.md)

---
**Last Updated**: January 1, 2026  
**Version**: 1.0.0
