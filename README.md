# Appium Bootstrap Installer

[![CI/CD Pipeline](https://github.com/blackpilot007/appium-bootstrap-installer/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/blackpilot007/appium-bootstrap-installer/actions/workflows/ci-cd.yml)
[![Release](https://github.com/blackpilot007/appium-bootstrap-installer/actions/workflows/release.yml/badge.svg)](https://github.com/blackpilot007/appium-bootstrap-installer/actions/workflows/release.yml)
[![License](https://img.shields.io/github/license/blackpilot007/appium-bootstrap-installer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-0.10.1-blue)](RELEASE_NOTES.md)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)](https://github.com/blackpilot007/appium-bootstrap-installer)

**Configuration-driven Appium infrastructure with dynamic driver/plugin installation and intelligent device monitoring.**

##  Features

-  **Dynamic Configuration** – Install any Appium driver or plugin via JSON configuration
-  **Automated Installation** – One-command setup of Node.js, Appium, drivers, and plugins
-  **Plugin System** – Extend functionality with scripts (Python/Node.js/Bash/PowerShell/Batch), processes, and custom plugins
-  **Smart Device Monitoring** – Auto-detects Android/iOS device connections with detailed info
-  **Event-Driven Architecture** – Pub/sub event bus for device and session lifecycle events
-  **Dual iOS Detection** – libimobiledevice (primary) with go-ios fallback
-  **Health Monitoring** – Programmatic health checks with component status tracking
-  **Centralized Port Management** – Dedicated port manager service with thread-safe allocation
-  **Dependency Injection** – Full DI container with service interfaces for testability
-  **Retry Logic** – Robust error handling with exponential backoff
-  **Log Rotation** – Automatic log management (10 MB, keep 5 files)
-  **Cross-Platform** – Native AOT support for Windows, macOS, and Linux
-  **Zero Hardcoding** – Add new drivers/plugins by editing config.json only

##  Quick Start

### Download
```bash
# Windows
curl -L https://github.com/blackpilot007/appium-bootstrap-installer/releases/latest/download/AppiumBootstrapInstaller-win-x64.zip -o installer.zip
unzip installer.zip

# Linux
curl -L https://github.com/blackpilot007/appium-bootstrap-installer/releases/latest/download/AppiumBootstrapInstaller-linux-x64.tar.gz | tar xz

# macOS
curl -L https://github.com/blackpilot007/appium-bootstrap-installer/releases/latest/download/AppiumBootstrapInstaller-osx-x64.tar.gz | tar xz
```

### Configure
```bash
cp config.sample.json config.json
```

**Minimal configuration:**
```json
{
  "installFolder": "${USERPROFILE}/AppiumBootstrap",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true }
  ],
  "plugins": [
    { "id": "device-farm", "version": "8.3.5", "enabled": true }
  ]
}
```

### Run
```bash
# Windows (as Administrator for system components)
.\AppiumBootstrapInstaller.exe

# Linux/macOS
chmod +x AppiumBootstrapInstaller
./AppiumBootstrapInstaller
```

##  Configuration Reference

### Core Settings
```json
{
  "installFolder": "${USERPROFILE}/AppiumBootstrap",  // Installation directory
  "nodeVersion": "22",                                 // Node.js version
  "appiumVersion": "2.17.1",                          // Appium version
  "nvmVersion": "0.40.1",                             // NVM version (optional)
  "cleanInstallFolder": false                          // Delete folder before install
}
```

### Drivers (Dynamic - Add Any Appium Driver)
```json
{
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true },
    { "name": "xcuitest", "version": "7.24.3", "enabled": true },
    { "name": "espresso", "version": "2.44.0", "enabled": false },
    { "name": "flutter", "version": "2.8.1", "enabled": false },
    { "name": "safari", "version": "3.7.3", "enabled": false },
    { "name": "gecko", "version": "0.33.0", "enabled": false }
  ]
}
```

### Plugins (Dynamic - Add Any Appium Plugin)
```json
{
  "plugins": [
    { "id": "device-farm", "version": "8.3.5", "enabled": true },
    { "id": "appium-dashboard", "version": "2.0.3", "enabled": true },
    { "id": "images", "version": "2.1.7", "enabled": false },
    { "id": "relaxed-caps", "version": "2.0.0", "enabled": false },
    { "id": "element-wait", "version": "3.0.2", "enabled": false },
    { "id": "execute-driver", "version": "2.1.4", "enabled": false }
  ]
}
```

### Device Listener (Optional)
```json
{
  "enableDeviceListener": true,        // Enable device monitoring
  "deviceListenerPollInterval": 5,     // Poll every 5 seconds
  "autoStartAppium": true,             // Auto-start Appium per device
  "prebuiltWdaPath": "string (optional) - For iOS on macOS: path or URL to signed/prebuilt WebDriverAgent bundle.\n  On Windows/Linux, Appium will NOT auto-start iOS sessions unless this is provided."
  "deviceRegistry": {
    "enabled": true,
    "filePath": "device-registry.json",
    "autoSave": true,
    "saveIntervalSeconds": 30
  }
}
```

### Platform-Specific Settings
```json
{
  "platformSpecific": {
    "windows": {
      "installIOSSupport": true,
      "installAndroidSupport": true,
      "nvmVersion": "1.1.12"
    },
    "linux": {
      "installIOSSupport": true,
      "installAndroidSupport": true
    },
    "macOS": {
      "libimobiledeviceVersion": "latest"
    }
  }
}
```

##  Documentation

| Guide | Description |
|-------|-------------|
| [**Plugin System Guide**](docs/SCRIPTPLUGIN_ENHANCEMENTS.md) | **NEW!** Extend with Python/Node.js/Bash scripts |
| [Plugin Architecture](docs/PLUGIN_ARCHITECTURE.md) | Complete plugin system architecture |
| [Plugin Quick Start](docs/PLUGIN_QUICK_START.md) | Step-by-step plugin implementation |
| [Architecture Assessment](docs/ARCHITECTURE_ASSESSMENT.md) | Complete architecture analysis |
| [Configuration Guide](docs/CONFIGURATION.md) | Detailed configuration options |
| [User Guide](USER_GUIDE.md) | Step-by-step usage instructions |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Building from Source](docs/BUILDING.md) | Build and development guide |
| [Release Notes](RELEASE_NOTES.md) | Version history and changes |

##  Plugin System (NEW!)

Extend Appium Bootstrap Installer with custom automation using the built-in plugin system. Plugins can run scripts, processes, or custom code triggered by device events.

```
┌─────────────────────────────────────────────────────────────────┐
│                    Device Event Flow                             │
│                                                                  │
│  Device Connected  ──►  EventBus  ──►  Plugin Trigger          │
│       ▼                                      ▼                   │
│  USB Detection          Pub/Sub          Python Script          │
│  (adb/idevice)         Events            Node.js Script         │
│                                          PowerShell Script       │
│                                          Bash Script             │
│                                          Any Process             │
│                                                                  │
│  Your scripts get called with device info as template variables │
└─────────────────────────────────────────────────────────────────┘
```

### Quick Example

**Run a Python script when any device connects:**
```json
{
  "pluginSystem": {
    "enabled": true,
    "plugins": [
      {
        "name": "device-notifier",
        "type": "script",
        "executable": "scripts/notify.py",
        "arguments": ["--device", "{{DEVICE_ID}}", "--platform", "{{DEVICE_PLATFORM}}"],
        "triggerOn": "device-connected",
        "enabled": true
      }
    ]
  }
}
```

### Supported Script Types

| Type | Extensions | Example |
|------|------------|---------|
| **Python** | `.py` | `monitor.py` |
| **Node.js** | `.js` | `metrics.js` |
| **PowerShell** | `.ps1` | `setup.ps1` |
| **Bash** | `.sh` | `cleanup.sh` |
| **Batch** | `.bat`, `.cmd` | `backup.bat` |

**No runtime configuration needed** – file extensions are auto-detected!

### Plugin Triggers

Plugins can be triggered by:
- `startup` - Run when installer starts
- `device-connected` - Run when device connects
- `device-disconnected` - Run when device disconnects  
- `manual` - Start/stop via API

### Template Variables

Use these variables in your plugin configurations:

| Variable | Example Value | Description |
|----------|---------------|-------------|
| `{{DEVICE_ID}}` | `emulator-5554` | Device UDID/serial |
| `{{DEVICE_PLATFORM}}` | `android` | Platform type |
| `{{DEVICE_NAME}}` | `Pixel 6` | Device model name |
| `{{APPIUM_PORT}}` | `4723` | Appium server port |
| `{{INSTALL_FOLDER}}` | `C:\Appium` | Installation directory |

### Common Use Cases

**1. Post-Installation Tasks (Sequential Execution)**
```json
{
  "pluginSystem": {
    "enabled": true,
    "plugins": [
      {
        "id": "unzip-artifacts",
        "name": "Unzip Artifacts",
        "type": "script",
        "runtime": "powershell",
        "script": "Expand-Archive -Path '${INSTALL_FOLDER}/artifacts.zip' -DestinationPath '${INSTALL_FOLDER}/extracted' -Force",
        "triggerOn": "startup",
        "enabled": true,
        "dependsOn": []
      },
      {
        "id": "copy-files",
        "name": "Copy Files",
        "type": "script",
        "runtime": "powershell",
        "script": "Copy-Item -Path '${INSTALL_FOLDER}/extracted/*' -Destination 'C:/target/' -Recurse -Force",
        "triggerOn": "startup",
        "enabled": true,
        "dependsOn": ["unzip-artifacts"]
      },
      {
        "id": "custom-service",
        "name": "Run Custom Service",
        "type": "process",
        "executable": "${INSTALL_FOLDER}/myapp.exe",
        "arguments": ["--config", "${INSTALL_FOLDER}/config.json"],
        "workingDirectory": "${INSTALL_FOLDER}",
        "triggerOn": "startup",
        "enabled": true,
        "restartPolicy": "always",
        "healthCheck": {
          "type": "port",
          "port": 8080,
          "intervalSeconds": 30
        },
        "dependsOn": ["copy-files"]
      }
    ]
  }
}
```
> **Note:** Plugins execute sequentially based on `dependsOn` - unzip runs first, then copy, then exe starts with auto-restart and health monitoring.

**2. Slack Notifications**
```json
{
  "name": "slack-notify",
  "type": "script",
  "executable": "notify.py",
  "arguments": ["--message", "Device {{DEVICE_ID}} connected"],
  "triggerOn": "device-connected"
}
```

**3. Device Setup Automation**
```json
{
  "name": "auto-provision",
  "type": "script",
  "executable": "provision.sh",
  "arguments": ["{{DEVICE_ID}}"],
  "triggerOn": "device-connected",
  "stopOnDisconnect": true
}
```

**4. Metrics Collection**
```json
{
  "name": "metrics-collector",
  "type": "script",
  "executable": "metrics.js",
  "arguments": ["--port", "3000"],
  "triggerOn": "startup"
}
```

**5. Log Forwarding**
```json
{
  "name": "log-forwarder",
  "type": "process",
  "executable": "fluent-bit",
  "arguments": ["-c", "config/fluent-bit.conf"],
  "triggerOn": "startup"
}
```

### Full Plugin Configuration Example

```json
{
  "pluginSystem": {
    "enabled": true,
    "plugins": [
      {
        "name": "device-monitor",
        "type": "script",
        "executable": "scripts/monitor.py",
        "arguments": [
          "--device", "{{DEVICE_ID}}",
          "--port", "{{APPIUM_PORT}}"
        ],
        "triggerOn": "device-connected",
        "stopOnDisconnect": true,
        "enabled": true,
        "environmentVariables": {
          "PYTHONUNBUFFERED": "1",
          "LOG_LEVEL": "info"
        },
        "healthCheck": {
          "command": "python scripts/health.py",
          "intervalSeconds": 30,
          "timeoutSeconds": 5
        }
      }
    ]
  }
}
```

**Learn more:** [ScriptPlugin Enhancements Guide](docs/SCRIPTPLUGIN_ENHANCEMENTS.md) | [Plugin Architecture](docs/PLUGIN_ARCHITECTURE.md)

##  Command-Line Options

```bash
# Full installation and monitoring
AppiumBootstrapInstaller

# Custom configuration
AppiumBootstrapInstaller --config my-config.json

# Device monitoring only (skip installation)
AppiumBootstrapInstaller --listen

# Generate sample configuration
AppiumBootstrapInstaller --generate-config

# Show help
AppiumBootstrapInstaller --help
```

##  Real-World Examples

### Android Only
```json
{
  "installFolder": "C:\\AppiumAndroid",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true }
  ],
  "plugins": [
    { "id": "device-farm", "version": "8.3.5", "enabled": true }
  ],
  "platformSpecific": {
    "windows": {
      "installIOSSupport": false,
      "installAndroidSupport": true
    }
  }
}
```

### iOS Only (macOS)
```json
{
  "installFolder": "/Users/username/AppiumiOS",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    { "name": "xcuitest", "version": "7.24.3", "enabled": true }
  ],
  "plugins": [
    { "id": "appium-dashboard", "version": "2.0.3", "enabled": true }
  ],
  "enableDeviceListener": true,
  "autoStartAppium": true
}
```

### Multi-Platform Lab with Custom Plugins
```json
{
  "installFolder": "${HOME}/appium-lab",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true },
    { "name": "xcuitest", "version": "7.24.3", "enabled": true }
  ],
  "plugins": [
    { "id": "device-farm", "version": "8.3.5", "enabled": true },
    { "id": "appium-dashboard", "version": "2.0.3", "enabled": true }
  ],
  "enableDeviceListener": true,
  "autoStartAppium": true,
  "pluginSystem": {
    "enabled": true,
    "plugins": [
      {
        "name": "slack-notifier",
        "type": "script",
        "executable": "scripts/notify.py",
        "arguments": ["--device", "{{DEVICE_ID}}"],
        "triggerOn": "device-connected",
        "enabled": true
      },
      {
        "name": "metrics-dashboard",
        "type": "script",
        "executable": "metrics/server.js",
        "arguments": ["--port", "3000"],
        "triggerOn": "startup",
        "enabled": true
      }
    ]
  }
}
```

##  Platform Support

| Platform | Management Mode (Default) | iOS Support | Android Support | Status |
|----------|----------------------------|-------------|-----------------|--------|
| Windows 10/11 | Portable process-mode (child processes) | go-ios (fallback: libimobiledevice) | ADB | Fully Supported |
| macOS 10.15+ | Portable process-mode (child processes) | Native (Xcode) | ADB | Fully Supported |
| Ubuntu/Debian | Portable process-mode (child processes) | go-ios | ADB | Fully Supported |

##  How It Works

1. **Configuration Loading** – Validates and loads JSON config with driver/plugin specifications
2. **Dependency Injection** – Builds DI container with all services (EventBus, PortManager, DeviceRegistry, etc.)
3. **Installation** – Orchestrator manages Node.js, Appium, and all enabled drivers/plugins
4. **Portable Setup** – Prepares optional startup helpers (no admin/system services)
5. **Device Monitoring** – Polls for device connections and publishes events (DeviceConnected/Disconnected)
6. **Port Allocation** – PortManager finds available consecutive ports with thread-safe allocation
7. **Session Management** – Auto-starts/stops Appium servers per device, publishes session events
8. **Event Propagation** – Event bus notifies subscribers of all device and session lifecycle changes
9. **Health Monitoring** – HealthCheckService provides programmatic health status queries

### Management Features (Portable Process Mode)
- **Health Monitoring**: Checks every 30 seconds, auto-restart on failure (in-process)
- **Log Rotation**: 10 MB per file, keeps 5 rotated files
- **Auto-Recovery**: Max 5 restart attempts on service failure
- **No GUI Prompts**: Fully scriptable for CI/CD pipelines
- **Consolidated Logs**: All logs in executable's `logs/` directory

### iOS Device Detection
- **Primary**: libimobiledevice (native tools)
- **Fallback**: go-ios (JSON output parsed correctly)
- **Trust Detection**: Clear prompts for untrusted devices
- **Device Details**: Logs UDID, model, iOS version, device name

### Port Allocation Strategy (PortManager Service)
- **iOS**: 3 consecutive ports (Appium + WDA + MJPEG)
- **Android**: 2 consecutive ports (Appium + SystemPort)
- **Range**: Configurable port range (default: 4723-5000)
- **Thread-Safe**: Dedicated PortManager with SemaphoreSlim locking
- **Availability Check**: TCP listener verification before allocation
- **Extensible**: Plugins can request ports via IPortManager interface

### Retry Logic
- **Session Start**: 3 attempts with exponential backoff (1s, 2s, 4s)
- **Session Stop**: 3 attempts with 10-second timeout each
- **File I/O**: 3 attempts for registry save/load operations
- **Transient vs Permanent**: Distinguishes retryable vs fatal errors

### Log Management
- **Daily Rotation**: New log file created at midnight
- **Size Limit**: 10 MB per file
- **Retention**: 30 days (automatic cleanup)
- **Max Storage**: ~300 MB total

##  Architecture Highlights

✅ **Service Interfaces** – Clean abstraction layer (IDeviceRegistry, IAppiumSessionManager, IPortManager, etc.)
✅ **Event-Driven** – Pub/sub event bus for loose coupling and extensibility
✅ **Dependency Injection** – Microsoft.Extensions.DependencyInjection with proper service lifetimes
✅ **Orchestrator Pattern** – Centralized workflow management separate from main entry point
✅ **Native AOT Compilation** – Fast startup, low memory footprint
✅ **Zero Hardcoding** – All drivers/plugins configured via JSON
✅ **Robust Error Handling** – Specific exception types with retry strategies
✅ **Resource Safety** – Proper disposal patterns, no memory leaks
✅ **Atomic Operations** – File writes use temp-file-then-rename pattern
✅ **Thread-Safe** – ConcurrentDictionary + SemaphoreSlim for synchronization

##  Troubleshooting

**Installation fails with permission errors:**
```bash
# Choose a user-writable install folder (portable mode)
# Example:
#   --install_folder="$HOME/appium-bootstrap"
```

**Configuration not found:**
```bash
AppiumBootstrapInstaller --config /full/path/to/config.json
```

**Agent (listen mode) management (Windows):**
```powershell
# Run the agent in the foreground
./AppiumBootstrapInstaller.exe --listen --config .\config.json

# Tail logs
Get-Content .\logs\installer-*.log -Tail 50 -Wait
```

**Android devices not detected:**
```bash
adb devices
adb kill-server && adb start-server
```

**iOS devices not detected (macOS/Linux):**
```bash
idevice_id -l
# Install if missing: brew install libimobiledevice
```

**Port allocation failures:**
- Check firewall settings
- Ensure no other services using port range 1000-65535
- Review logs: `logs/installer-*.log`

**Check logs:**
```bash
# Windows PowerShell
Get-Content logs\installer-*.log -Tail 50 -Wait
Get-Content logs\device-listener-*.log -Tail 50 -Wait
Get-Content logs\AppiumBootstrap_*_stdout.log -Tail 50 -Wait  # Per-device Appium logs

# Linux/macOS
tail -f logs/installer-*.log
tail -f logs/device-listener-*.log
```

**Drivers not installing:**
- Verify JSON syntax in config.json
- Check driver versions at: https://appium.io/docs/en/latest/ecosystem/
- Review installation logs for specific errors

**For more help**, see [Troubleshooting Guide](docs/TROUBLESHOOTING.md)

##  Performance & Reliability

### Memory Usage (Long-Running Service)
- **Baseline**: ~150-200 MB
- **Per Device**: +2 MB
- **Growth**: Bounded (no memory leaks)
- **Log Storage**: Max ~300 MB (auto-rotation)

### Retry Strategy
| Operation | Retries | Timeout | Backoff |
|-----------|---------|---------|---------|
| Session Start | 3 | Per operation | Exponential |
| Session Stop | 3 | 10s each | Linear |
| File I/O | 3 | None | Linear |

### Production-Ready
✅ Process handle cleanup
✅ Port leak prevention  
✅ Corrupted file recovery  
✅ Atomic file writes  
✅ Thread-safe operations  
✅ Graceful degradation

##  Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

For maintainers creating releases, see [RELEASE_PROCESS.md](RELEASE_PROCESS.md) for detailed release workflow.

##  License

Apache License 2.0 - see [LICENSE](LICENSE) file.

##  Support

- **Issues**: [GitHub Issues](https://github.com/blackpilot007/appium-bootstrap-installer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/blackpilot007/appium-bootstrap-installer/discussions)

---

**Quick Links**: [Config Guide](docs/CONFIGURATION.md) | [User Guide](USER_GUIDE.md) | [Troubleshooting](docs/TROUBLESHOOTING.md)
