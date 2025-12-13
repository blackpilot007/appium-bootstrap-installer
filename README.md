# Appium Bootstrap Installer

[![License](https://img.shields.io/github/license/blackpilot007/appium-bootstrap-installer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-0.10.1-blue)](RELEASE_NOTES.md)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)](https://github.com/blackpilot007/appium-bootstrap-installer)

**Configuration-driven Appium infrastructure with dynamic driver/plugin installation and intelligent device monitoring.**

##  Features

-  **Dynamic Configuration** – Install any Appium driver or plugin via JSON configuration
-  **Automated Installation** – One-command setup of Node.js, Appium, drivers, and plugins
-  **Smart Device Monitoring** – Auto-detects Android/iOS device connections with detailed info
-  **Dual iOS Detection** – libimobiledevice (primary) with go-ios fallback
-  **Health Monitoring** – Automatic service health checks with restart on failure
-  **Dynamic Port Allocation** – Automatically finds and assigns consecutive ports
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
    { "name": "device-farm", "version": "8.3.5", "enabled": true }
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
    { "name": "device-farm", "version": "8.3.5", "enabled": true },
    { "name": "appium-dashboard", "version": "2.0.3", "enabled": true },
    { "name": "images", "version": "2.1.7", "enabled": false },
    { "name": "relaxed-caps", "version": "2.0.0", "enabled": false },
    { "name": "element-wait", "version": "3.0.2", "enabled": false },
    { "name": "execute-driver", "version": "2.1.4", "enabled": false }
  ]
}
```

### Device Listener (Optional)
```json
{
  "enableDeviceListener": true,        // Enable device monitoring
  "deviceListenerPollInterval": 5,     // Poll every 5 seconds
  "autoStartAppium": true,             // Auto-start Appium per device
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
| [Architecture Assessment](docs/ARCHITECTURE_ASSESSMENT.md) | Complete architecture analysis |
| [Long-Running Service Analysis](docs/LONG_RUNNING_SERVICE_ANALYSIS.md) | Performance & memory analysis |
| [Configuration Guide](docs/CONFIGURATION.md) | Detailed configuration options |
| [User Guide](USER_GUIDE.md) | Step-by-step usage instructions |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Building from Source](docs/BUILDING.md) | Build and development guide |
| [Release Notes](RELEASE_NOTES.md) | Version history and changes |

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
    { "name": "device-farm", "version": "8.3.5", "enabled": true }
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
    { "name": "appium-dashboard", "version": "2.0.3", "enabled": true }
  ],
  "enableDeviceListener": true,
  "autoStartAppium": true
}
```

### Multi-Platform Lab
```json
{
  "installFolder": "${HOME}/appium-lab",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true },
    { "name": "xcuitest", "version": "7.24.3", "enabled": true },
    { "name": "flutter", "version": "2.8.1", "enabled": true }
  ],
  "plugins": [
    { "name": "device-farm", "version": "8.3.5", "enabled": true },
    { "name": "appium-dashboard", "version": "2.0.3", "enabled": true },
    { "name": "images", "version": "2.1.7", "enabled": true }
  ],
  "enableDeviceListener": true,
  "autoStartAppium": true
}
```

##  Platform Support

| Platform | Service Manager | iOS Support | Android Support | Status |
|----------|----------------|-------------|-----------------|--------|
| Windows 10/11 | Servy | go-ios (fallback: libimobiledevice) | ADB |  Fully Supported |
| macOS 10.15+ | Supervisor | Native (Xcode) | ADB |  Fully Supported |
| Ubuntu/Debian | systemd | go-ios | ADB |  Fully Supported |

##  How It Works

1. **Configuration Loading** – Reads JSON config with driver/plugin specifications
2. **Installation** – Installs Node.js, Appium, and all enabled drivers/plugins
3. **Service Setup** – Configures Servy/Supervisor/systemd for process management
4. **Device Monitoring** – Polls for device connections (configurable interval)
5. **Port Allocation** – Dynamically finds available consecutive ports with retry logic
6. **Session Management** – Auto-starts/stops Appium servers per device with exponential backoff
7. **Health Monitoring** – Continuous service health checks with automatic recovery

### Service Management Features (Servy)
- **Health Monitoring**: Checks every 30 seconds, auto-restart on failure
- **Log Rotation**: 10 MB per file, keeps 5 rotated files
- **Auto-Recovery**: Max 5 restart attempts on service failure
- **No GUI Prompts**: Fully scriptable for CI/CD pipelines
- **Consolidated Logs**: All logs in executable's `logs/` directory

### iOS Device Detection
- **Primary**: libimobiledevice (native tools)
- **Fallback**: go-ios (JSON output parsed correctly)
- **Trust Detection**: Clear prompts for untrusted devices
- **Device Details**: Logs UDID, model, iOS version, device name

### Port Allocation Strategy
- **iOS**: 3 consecutive ports (Appium + WDA + MJPEG)
- **Android**: 2 consecutive ports (Appium + SystemPort)
- Automatically finds available 4-digit ports (1000-65535)
- Thread-safe allocation with semaphore locking

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

✅ **Native AOT Compilation** – Fast startup, low memory footprint
✅ **Zero Hardcoding** – All drivers/plugins configured via JSON
✅ **Robust Error Handling** – Specific exception types with retry strategies
✅ **Resource Safety** – Proper disposal patterns, no memory leaks
✅ **Atomic Operations** – File writes use temp-file-then-rename pattern
✅ **Thread-Safe** – ConcurrentDictionary + SemaphoreSlim for synchronization

##  Troubleshooting

**Installation fails with permission errors:**
```bash
# Windows: Run as Administrator
# Linux/macOS: Check folder permissions
sudo chown -R $USER:$USER /path/to/installFolder
```

**Configuration not found:**
```bash
AppiumBootstrapInstaller --config /full/path/to/config.json
```

**Service management (Windows only):**
```powershell
# Check device listener service status
servy-cli status --name="AppiumBootstrap_DeviceListener"

# Restart device listener
servy-cli restart --quiet --name="AppiumBootstrap_DeviceListener"

# View service logs in real-time
Get-Content logs\AppiumBootstrap_DeviceListener_stdout.log -Tail 50 -Wait

# Check all Appium services
servy-cli list | Select-String "AppiumBootstrap"
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

##  License

Apache License 2.0 - see [LICENSE](LICENSE) file.

##  Support

- **Issues**: [GitHub Issues](https://github.com/blackpilot007/appium-bootstrap-installer/issues)
- **Discussions**: [GitHub Discussions](https://github.com/blackpilot007/appium-bootstrap-installer/discussions)

---

**Quick Links**: [Config Guide](docs/CONFIGURATION.md) | [User Guide](USER_GUIDE.md) | [Troubleshooting](docs/TROUBLESHOOTING.md)
