# Appium Bootstrap Installer

[![License](https://img.shields.io/github/license/blackpilot007/appium-bootstrap-installer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Version](https://img.shields.io/badge/version-0.10.1-blue)](RELEASE_NOTES.md)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)](https://github.com/blackpilot007/appium-bootstrap-installer)

Automated Appium infrastructure setup with intelligent device monitoring and session management.

##  Features

-  **Automated Installation** – One-command setup of Node.js, Appium, and drivers
-  **Smart Device Monitoring** – Auto-detects Android/iOS connections
-  **Dynamic Port Allocation** – Finds and assigns consecutive ports automatically
-  **Configuration-Driven** – Simple JSON configuration
-  **Cross-Platform** – Windows, macOS, and Linux support
-  **Self-Contained** – No dependencies or runtime installation needed

##  Quick Start

### Download
```bash
# Windows
curl -L https://github.com/blackpilot007/appium-bootstrap-installer/releases/download/v0.10.1/AppiumBootstrapInstaller-v0.10.1.zip -o installer.zip
unzip installer.zip
```

### Configure
```bash
cp config.sample.json config.json
```

Minimal configuration:
```json
{
  "installFolder": "${USERPROFILE}/AppiumBootstrap",
  "enableDeviceListener": true,
  "autoStartAppium": true
}
```

### Run
```bash
# Windows (as Administrator)
.\AppiumBootstrapInstaller.exe

# Linux/macOS
chmod +x AppiumBootstrapInstaller
./AppiumBootstrapInstaller
```

##  Documentation

| Guide | Description |
|-------|-------------|
| [Configuration](docs/CONFIGURATION.md) | Configuration options and examples |
| [User Guide](USER_GUIDE.md) | Detailed usage instructions |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Building](docs/BUILDING.md) | Build from source instructions |
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

# Preview execution
AppiumBootstrapInstaller --dry-run

# Show help
AppiumBootstrapInstaller --help
```

##  Configuration Examples

### Android Testing
```json
{
  "installFolder": "C:\\appium-home",
  "enableDeviceListener": true,
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true }
  ]
}
```

### iOS Testing
```json
{
  "installFolder": "/Users/username/.appium",
  "enableDeviceListener": true,
  "drivers": [
    { "name": "xcuitest", "version": "7.24.3", "enabled": true }
  ]
}
```

### Multi-Platform
```json
{
  "installFolder": "${HOME}/appium-lab",
  "enableDeviceListener": true,
  "drivers": [
    { "name": "uiautomator2", "version": "3.8.3", "enabled": true },
    { "name": "xcuitest", "version": "7.24.3", "enabled": true }
  ]
}
```

##  Platform Support

| Platform | Service Manager | Status |
|----------|----------------|--------|
| Windows 10/11 | NSSM |  Supported |
| macOS 10.15+ | Supervisor |  Supported |
| Ubuntu/Debian | systemd |  Supported |

##  How It Works

1. **Installation** – Installs Node.js, Appium, drivers, and plugins
2. **Service Setup** – Configures NSSM/Supervisor/systemd for process management
3. **Device Monitoring** – Polls for device connections (Android/iOS)
4. **Port Allocation** – Dynamically finds available consecutive ports
5. **Session Management** – Auto-starts/stops Appium servers per device

**Port allocation strategy:**
- **iOS**: 3 consecutive ports (Appium  WDA  MJPEG)
- **Android**: 2 consecutive ports (Appium  SystemPort)
- Automatically finds available 4-digit ports (1000-65535)

##  Troubleshooting

**Configuration not found:**
```bash
AppiumBootstrapInstaller --config /full/path/to/config.json
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

**Check logs:**
```bash
# View device listener logs
tail -f logs/device-listener-*.log

# View Appium logs
tail -f logs/installer-*.log
```

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
