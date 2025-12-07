# Appium Bootstrap Installer

[![License](https://img.shields.io/github/license/blackpilot007/appium-bootstrap-installer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue)](https://github.com/blackpilot007/appium-bootstrap-installer)

A comprehensive cross-platform automation solution that installs Appium infrastructure, monitors device connections, and automatically manages Appium server sessions—eliminating manual setup and runtime management.

## ✨ Key Features

- **🔧 Automated Installation** – One-command setup of Node.js, Appium, and drivers
- **📱 Smart Device Monitoring** – Auto-detects Android/iOS device connections
- **🚀 Auto-Start Appium** – Launches Appium servers per device with intelligent port allocation
- **📊 Built-in Observability** – Metrics, correlation IDs, and structured logging
- **⚙️ Configuration-Driven** – JSON-based configuration with environment variables
- **🌐 Cross-Platform** – Windows, macOS, and Linux support
- **📦 Zero Dependencies** – Self-contained executables, no runtime installation needed
- **🔄 Service Integration** – Works with NSSM, Supervisor, and systemd

## 🚀 Quick Start

### 1. Download

Get the latest release for your platform:
- **Windows:** [AppiumBootstrapInstaller-win-x64.zip](../../releases)
- **macOS:** [AppiumBootstrapInstaller-osx-x64.tar.gz](../../releases) (Intel) or [osx-arm64](../../releases) (Apple Silicon)
- **Linux:** [AppiumBootstrapInstaller-linux-x64.tar.gz](../../releases)

### 2. Configure

```bash
# Copy sample configuration
cp config.sample.json config.json

# Edit with your settings
nano config.json
```

**Minimal configuration:**
```json
{
  "InstallFolder": "/path/to/appium-home",
  "EnableDeviceListener": true,
  "AutoStartAppium": true
}
```

### 3. Run

**Windows (as Administrator):**
```powershell
.\AppiumBootstrapInstaller.exe
```

**Linux/macOS:**
```bash
chmod +x AppiumBootstrapInstaller
./AppiumBootstrapInstaller
```

**That's it!** The application will:
1. Install Node.js, Appium, and drivers
2. Set up service manager (NSSM/Supervisor/systemd)
3. Start monitoring devices and auto-launch Appium sessions

## 📖 Documentation

| Document | Description |
|----------|-------------|
| **[Configuration Guide](docs/CONFIGURATION.md)** | Complete configuration reference and examples |
| **[Installation Guide](docs/INSTALLATION.md)** | Detailed installation instructions per platform |
| **[Usage Guide](docs/USAGE.md)** | Command-line options and usage modes |
| **[Architecture](docs/ARCHITECTURE.md)** | System design and how it works |
| **[Troubleshooting](docs/TROUBLESHOOTING.md)** | Common issues and solutions |
| **[Building from Source](docs/BUILDING.md)** | Developer guide for building the project |
| **[API Reference](docs/API.md)** | Service APIs and integration |

## 💡 Common Use Cases

### Local Development
```bash
# Quick setup for testing
AppiumBootstrapInstaller --config dev-config.json
```

### CI/CD Pipeline
```yaml
# GitHub Actions example
- name: Setup Appium
  run: |
    ./AppiumBootstrapInstaller --config ci-config.json
```

### Device Farm
```json
{
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "PortRanges": { "AppiumStart": 4723, "AppiumEnd": 4923 }
}
```

## 🔧 Command-Line Options

```bash
# Full installation with device monitoring
AppiumBootstrapInstaller

# Custom configuration file
AppiumBootstrapInstaller --config custom-config.json

# Skip installation, run device listener only
AppiumBootstrapInstaller --listen

# Generate sample configuration
AppiumBootstrapInstaller --generate-config

# Preview without executing
AppiumBootstrapInstaller --dry-run

# Show help
AppiumBootstrapInstaller --help
```

## 📊 Monitoring

View real-time metrics and logs:

```bash
# Tail logs
tail -f logs/device-listener-*.log

# View metrics (logged every 5 minutes)
grep "METRICS" logs/device-listener-*.log
```

**Sample metrics:**
```
[METRICS] Connected=3, Active=2, Sessions(Success=15, Failed=1), PortUsage=23%
```

## 🛠️ Platform Support

| Platform | Service Manager | Status |
|----------|----------------|--------|
| Windows 10/11 | NSSM | ✅ Fully Supported |
| macOS 10.15+ | Supervisor | ✅ Fully Supported |
| Ubuntu/Debian | systemd | ✅ Fully Supported |
| RHEL/CentOS | systemd | ✅ Fully Supported |

## 🤝 Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

```bash
# Development setup
git clone https://github.com/blackpilot007/appium-bootstrap-installer.git
cd appium-bootstrap-installer
dotnet build
```

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- **Issues:** [GitHub Issues](https://github.com/blackpilot007/appium-bootstrap-installer/issues)
- **Discussions:** [GitHub Discussions](https://github.com/blackpilot007/appium-bootstrap-installer/discussions)
- **Documentation:** [docs/](docs/)

## 🙏 Acknowledgments

Built with [Appium](https://appium.io/), [.NET](https://dotnet.microsoft.com/), and [Serilog](https://serilog.net/).

---

**Quick Links:** [Configuration](docs/CONFIGURATION.md) | [Installation](docs/INSTALLATION.md) | [Troubleshooting](docs/TROUBLESHOOTING.md) | [Building](docs/BUILDING.md)

## ⚙️ Configuration Reference

### Configuration File Location Priority

The application searches for configuration files in this order:

1. **Specified path** (via `--config` argument)
2. **Current directory** (`./config.json`)
3. **Home directory** (`~/.appium-bootstrap/config.json`)

### Complete Configuration Schema

```json
{
  // ============================================
  // INSTALLATION SETTINGS
  // ============================================
  "InstallFolder": "string (required) - Installation directory path",
  "NodeVersion": "string (default: 22) - Node.js version to install",
  "AppiumVersion": "string (default: 2.17.1) - Appium version",
  "NvmVersion": "string (default: 0.40.2) - NVM version",

  // ============================================
  // DEVICE LISTENER SETTINGS
  // ============================================
  "EnableDeviceListener": "boolean (default: false) - Enable automatic device monitoring",
  "AutoStartAppium": "boolean (default: true) - Auto-start Appium for detected devices",
  "DeviceListenerPollInterval": "number (default: 5) - Device check interval in seconds",

  // ============================================
  // PORT CONFIGURATION
  // ============================================
  "PortRanges": {
    "AppiumStart": "number (default: 4723) - Starting port for Appium servers",
    "AppiumEnd": "number (default: 4823) - Ending port for Appium servers",
    "SystemPortStart": "number (default: 8200) - Android system port start",
    "SystemPortEnd": "number (default: 8300) - Android system port end",
    "MjpegServerStart": "number (default: 9100) - MJPEG server port start",
    "MjpegServerEnd": "number (default: 9200) - MJPEG server port end",
    "WdaLocalStart": "number (default: 8100) - iOS WDA local port start",
    "WdaLocalEnd": "number (default: 8200) - iOS WDA local port end"
  },

  // ============================================
  // DEVICE REGISTRY SETTINGS
  // ============================================
  "DeviceRegistry": {
    "Enabled": "boolean (default: true) - Enable device session registry",
    "RegistryPath": "string (optional) - Custom registry file path",
    "AutoSaveInterval": "number (default: 60) - Auto-save interval in seconds"
  },

  // ============================================
  // DRIVERS CONFIGURATION
  // ============================================
  "Drivers": [
    {
      "Name": "string - Driver name (uiautomator2, xcuitest, etc.)",
      "Version": "string - Driver version (e.g., 3.8.3)",
      "Enabled": "boolean - Enable/disable driver installation"
    }
  ],

  // ============================================
  // PLUGINS CONFIGURATION
  // ============================================
  "Plugins": [
    {
      "Name": "string - Plugin name (device-farm, images, etc.)",
      "Version": "string - Plugin version",
      "Enabled": "boolean - Enable/disable plugin installation"
    }
  ],

  // ============================================
  // PLATFORM-SPECIFIC SETTINGS
  // ============================================
  "PlatformSpecific": {
    "MacOS": {
      "NvmVersion": "string - macOS-specific NVM version",
      "LibimobiledeviceVersion": "string - libimobiledevice version",
      "InstallXCUITest": "boolean - Install XCUITest driver",
      "InstallUiAutomator": "boolean - Install UiAutomator2 driver",
      "InstallDeviceFarm": "boolean - Install Device Farm plugin"
    },
    "Windows": {
      "NvmVersion": "string - Windows-specific NVM version",
      "InstallIOSSupport": "boolean - Install iOS support tools",
      "InstallAndroidSupport": "boolean - Install Android support",
      "InstallXCUITest": "boolean - Install XCUITest driver",
      "InstallUiAutomator": "boolean - Install UiAutomator2 driver",
      "InstallDeviceFarm": "boolean - Install Device Farm plugin"
    },
    "Linux": {
      "NvmVersion": "string - Linux-specific NVM version",
      "InstallIOSSupport": "boolean - Install iOS support tools",
      "InstallAndroidSupport": "boolean - Install Android support"
    }
  }
}
```

### Environment Variable Expansion

Configuration values support environment variable expansion:

**Windows:**
```json
{
  "InstallFolder": "%USERPROFILE%\\appium-home"
}
```

**Linux/macOS:**
```json
{
  "InstallFolder": "${HOME}/.local/appium"
}
```

### Configuration Examples

#### Example 1: Android Testing (Windows)
```json
{
  "InstallFolder": "C:\\appium-home",
  "NodeVersion": "22",
  "AppiumVersion": "2.17.1",
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "DeviceListenerPollInterval": 5,
  "Drivers": [
    {
      "Name": "uiautomator2",
      "Version": "3.8.3",
      "Enabled": true
    }
  ],
  "PortRanges": {
    "AppiumStart": 4723,
    "AppiumEnd": 4823
  }
}
```

#### Example 2: iOS Testing (macOS)
```json
{
  "InstallFolder": "/Users/username/.appium",
  "NodeVersion": "22",
  "AppiumVersion": "2.17.1",
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "Drivers": [
    {
      "Name": "xcuitest",
      "Version": "7.26.5",
      "Enabled": true
    }
  ],
  "PlatformSpecific": {
    "MacOS": {
      "LibimobiledeviceVersion": "latest",
      "InstallXCUITest": true
    }
  }
}
```

#### Example 3: Multi-Platform Testing
```json
{
  "InstallFolder": "${HOME}/appium-lab",
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "DeviceListenerPollInterval": 3,
  "Drivers": [
    {
      "Name": "uiautomator2",
      "Version": "3.8.3",
      "Enabled": true
    },
    {
      "Name": "xcuitest",
      "Version": "7.26.5",
      "Enabled": true
    }
  ],
  "Plugins": [
    {
      "Name": "device-farm",
      "Version": "8.3.5",
      "Enabled": true
    }
  ]
}
```

## 🔧 Command-Line Reference

### Syntax
```bash
AppiumBootstrapInstaller [options]
```

### Options

| Option | Short | Description |
|--------|-------|-------------|
| `--config <path>` | `-c` | Path to configuration file (JSON) |
| `--listen` | `-l` | Skip installation, run device listener only |
| `--dry-run` | `-d` | Preview execution without making changes |
| `--generate-config [path]` | `-g` | Generate sample configuration file |
| `--help` | `-h` | Show help message |

### Examples

**Generate sample configuration:**
```bash
AppiumBootstrapInstaller --generate-config
AppiumBootstrapInstaller --generate-config my-config.json
```

**Full installation with device monitoring:**
```bash
# Uses config.json from current directory
AppiumBootstrapInstaller

# Uses custom config file
AppiumBootstrapInstaller --config /path/to/config.json
```

**Preview without executing:**
```bash
AppiumBootstrapInstaller --config config.json --dry-run
```

**Listen-only mode (skip installation):**
```bash
AppiumBootstrapInstaller --listen
```

**Listen-only with custom config:**
```bash
AppiumBootstrapInstaller --listen --config config.json
```

## 🏗️ Architecture & How It Works

### Installation Flow

```
1. Load Configuration
   ├─ Check --config argument
   ├─ Check ./config.json
   └─ Check ~/.appium-bootstrap/config.json

2. Detect Operating System
   ├─ Windows (via RuntimeInformation)
   ├─ macOS (via RuntimeInformation)
   └─ Linux (via RuntimeInformation)

3. STEP 1: Install Dependencies
   ├─ Execute Platform/{OS}/Scripts/InstallDependencies.{ext}
   ├─ Install Node.js via NVM
   ├─ Install Appium via npm
   ├─ Install configured drivers
   └─ Install configured plugins

4. STEP 2: Setup Service Manager
   ├─ Windows: Install NSSM
   ├─ macOS: Setup Supervisor
   └─ Linux: Setup systemd

5. STEP 3: Start Device Listener (if enabled)
   ├─ Monitor device connections (polling)
   ├─ Detect Android devices (adb devices)
   ├─ Detect iOS devices (idevice_id -l)
   ├─ Allocate ports from pool
   ├─ Start Appium server per device
   └─ Log metrics and events
```

### Device Listener Architecture

```
DeviceListenerService (BackgroundService)
├─ Polls every N seconds (configurable)
├─ Detects connected devices
│  ├─ Android: adb devices
│  └─ iOS: idevice_id -l
├─ Generates correlation ID per device
├─ Uses AppiumSessionManager
│  ├─ Allocates ports from pool
│  ├─ Starts Appium via NSSM/Supervisor
│  └─ Monitors port usage (70%/90% warnings)
├─ Updates DeviceRegistry
│  ├─ Persistent device history
│  └─ Session tracking
└─ Records DeviceMetrics
   ├─ Connection/disconnection counts
   ├─ Session success/failure rates
   └─ Port allocation statistics
```

### Observability Features

**Correlation IDs:**
- Each device operation gets a unique correlation ID
- Enables end-to-end tracing through logs
- Format: `CORR-{timestamp}-{random}`

**Structured Logging:**
- Uses Serilog with structured events
- Context scoping with BeginScope
- File logging with daily rotation
- Console output with colored levels

**Metrics Collection:**
```csharp
DeviceMetrics tracks:
- Total devices connected/disconnected
- Active sessions count
- Session success/failure rates
- Port allocation failures
- Port pool utilization percentage
- Periodic summary logging (every 5 minutes)
```

**Port Pool Monitoring:**
- Tracks available ports in real-time
- Warns at 70% utilization
- Alerts at 90% utilization
- Prevents port exhaustion

## 🔨 Building from Source

### Prerequisites
- .NET 8 SDK or later
- Git

### Clone Repository
```bash
git clone https://github.com/yourusername/appium-bootstrap-installer.git
cd appium-bootstrap-installer
```

### Build for Current Platform
```bash
cd AppiumBootstrapInstaller
dotnet build -c Release
```

### Run from Source
```bash
dotnet run --project AppiumBootstrapInstaller -- --help
```

### Build Self-Contained Executables

**Windows x64:**
```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64
```

**Linux x64:**
```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/linux-x64
```

**macOS x64 (Intel):**
```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/osx-x64
```

**macOS ARM64 (Apple Silicon):**
```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/osx-arm64
```

### Build All Platforms

**Windows (PowerShell):**
```powershell
powershell -ExecutionPolicy Bypass -File .\build-all.ps1
```

**Linux/macOS:**
```bash
chmod +x build-all.sh
./build-all.sh
```

This creates executables for:
- Windows x64/ARM64
- Linux x64/ARM64
- macOS x64/ARM64

## 🖥️ Platform-Specific Details

### Windows

**Service Manager:** NSSM (Non-Sucking Service Manager)

**Installation Scripts:**
- `Platform/Windows/Scripts/InstallDependencies.ps1` - Main installer
- `Platform/Windows/Scripts/ServiceSetup.ps1` - NSSM setup
- `Platform/Windows/Scripts/InitialSetup.bat` - Quick start script
- `Platform/Windows/Scripts/StartAppiumServer.ps1` - Appium launcher
- `Platform/Windows/Scripts/DeviceListener.ps1` - Legacy device monitor
- `Platform/Windows/Scripts/check_appium_drivers.ps1` - Driver verification
- `Platform/Windows/Scripts/clean-appium-install.ps1` - Cleanup utility

**Requirements:**
- Windows 10/11 (64-bit recommended)
- PowerShell 5.1 or later
- Administrator privileges
- Execution policy allowing scripts

**Device Detection Tools:**
- **Android:** [Android SDK Platform Tools](https://developer.android.com/studio/releases/platform-tools)
- **iOS:** iTunes or [Apple Mobile Device Support](https://support.apple.com/downloads)

**Service Management:**
```powershell
# Install device listener as service
nssm install AppiumDeviceListener "C:\path\to\AppiumBootstrapInstaller.exe" --listen
nssm set AppiumDeviceListener Start SERVICE_AUTO_START
nssm start AppiumDeviceListener

# Check service status
nssm status AppiumDeviceListener

# Stop service
nssm stop AppiumDeviceListener

# Remove service
nssm remove AppiumDeviceListener confirm
```

### macOS

**Service Manager:** Supervisor

**Installation Scripts:**
- `Platform/MacOS/Scripts/InstallDependencies.sh` - Main installer
- `Platform/MacOS/Scripts/SupervisorSetup.sh` - Supervisor configuration
- `Platform/MacOS/Scripts/InitialSetup.command` - Quick start (double-click)
- `Platform/MacOS/Scripts/check_appium_drivers.sh` - Driver verification
- `Platform/MacOS/Scripts/clean-appium-install.sh` - Cleanup utility

**Requirements:**
- macOS 10.15 (Catalina) or later
- Xcode Command Line Tools: `xcode-select --install`
- Homebrew (auto-installed if missing)
- Admin privileges for certain operations

**Device Detection Tools:**
- **Android:** [Android SDK Platform Tools](https://developer.android.com/studio/releases/platform-tools)
- **iOS:** `libimobiledevice` (auto-installed via Homebrew)

**Service Management:**
```bash
# Check supervisor status
supervisorctl status

# Start service
supervisorctl start appium-device-listener

# Stop service
supervisorctl stop appium-device-listener

# Restart service
supervisorctl restart appium-device-listener
```

### Linux

**Service Manager:** systemd

**Installation Scripts:**
- `Platform/Linux/Scripts/InstallDependencies.sh` - Main installer
- `Platform/Linux/Scripts/SystemdSetup.sh` - systemd service configuration
- `Platform/Linux/Scripts/InitialSetup.sh` - Quick start script
- `Platform/Linux/Scripts/StartAppiumServer.sh` - Appium launcher
- `Platform/Linux/Scripts/portforward.sh` - Port forwarding utility

**Requirements:**
- Ubuntu 20.04+ / Debian 11+ / RHEL 8+ (or equivalent)
- systemd
- bash, curl
- sudo privileges

**Device Detection Tools:**
- **Android:** [Android SDK Platform Tools](https://developer.android.com/studio/releases/platform-tools)
- **iOS:** `libimobiledevice` and `usbmuxd` (auto-installed)

**Service Management:**
```bash
# Enable service
sudo systemctl enable appium-device-listener

# Start service
sudo systemctl start appium-device-listener

# Check status
sudo systemctl status appium-device-listener

# Stop service
sudo systemctl stop appium-device-listener

# View logs
sudo journalctl -u appium-device-listener -f
```

## 📊 Monitoring & Logging

### Log Files

**Default Location:** `{InstallFolder}/logs/`

**Log Files:**
- `installer-YYYYMMDD.log` - Installation logs
- `device-listener-YYYYMMDD.log` - Device monitoring logs
- `appium-{deviceId}-YYYYMMDD.log` - Per-device Appium logs

### Log Levels

- **Debug** - Detailed diagnostic information
- **Information** - General informational messages
- **Warning** - Warning messages (port usage, etc.)
- **Error** - Error events
- **Fatal** - Critical failures

### Metrics Dashboard

View real-time metrics in logs (every 5 minutes):
```
[METRICS] Summary: Connected=3, Disconnected=1, Active=2, Sessions(Success=15, Failed=1), Ports(Failed=0)
```

### Correlation ID Tracking

Search logs by correlation ID to trace device lifecycle:
```bash
# Linux/macOS
grep "CORR-20251207-abc123" logs/device-listener-*.log

# Windows PowerShell
Select-String -Path "logs\device-listener-*.log" -Pattern "CORR-20251207-abc123"
```

## 🔍 Troubleshooting

### Installation Issues

**Problem:** Configuration file not found
```
Error: Configuration file not found
```
**Solution:**
```bash
# Specify config explicitly
AppiumBootstrapInstaller --config /full/path/to/config.json

# Or copy to expected location
cp config.sample.json config.json
```

**Problem:** Platform scripts not found
```
Error: Platform scripts directory not found
```
**Solution:** Ensure `Platform/` folder exists alongside the executable:
```
your-app-folder/
├── AppiumBootstrapInstaller.exe
└── Platform/
    ├── Windows/
    ├── MacOS/
    └── Linux/
```

**Problem:** Permission denied (Linux/macOS)
```
Error: Permission denied
```
**Solution:**
```bash
chmod +x AppiumBootstrapInstaller
sudo ./AppiumBootstrapInstaller  # If system directories involved
```

### Device Detection Issues

**Problem:** Android devices not detected
```bash
# Check ADB installation
adb version

# Check connected devices
adb devices

# Restart ADB server
adb kill-server
adb start-server
```

**Problem:** iOS devices not detected (macOS/Linux)
```bash
# Check libimobiledevice installation
idevice_id --version

# List connected iOS devices
idevice_id -l

# Check usbmuxd status (Linux)
sudo systemctl status usbmuxd
```

**Problem:** Devices detected but Appium not starting
- Check logs in `logs/` directory
- Verify port ranges in config.json
- Check NSSM/Supervisor service status
- Ensure Appium is installed: `{InstallFolder}/node_modules/.bin/appium --version`

### Service Issues

**Windows - NSSM Service Won't Start:**
```powershell
# Check NSSM installation
Test-Path "C:\path\to\nssm\nssm.exe"

# Check service configuration
nssm status AppiumDeviceListener

# View service logs
Get-Content "C:\path\to\logs\device-listener.log" -Tail 50
```

**macOS - Supervisor Issues:**
```bash
# Check supervisor installation
which supervisord

# Reload configuration
supervisorctl reread
supervisorctl update

# Check logs
tail -f /path/to/logs/supervisord.log
```

**Linux - systemd Issues:**
```bash
# Check service status
sudo systemctl status appium-device-listener

# View detailed logs
sudo journalctl -u appium-device-listener --no-pager -n 100

# Reload systemd
sudo systemctl daemon-reload
```

### Port Conflicts

**Problem:** Port already in use
```
Warning: Port 4723 is already in use
```
**Solution:** Adjust port ranges in config.json:
```json
{
  "PortRanges": {
    "AppiumStart": 5000,
    "AppiumEnd": 5100
  }
}
```

### Build Issues

**Problem:** PowerShell script execution disabled
```
.\build-all.ps1 : running scripts is disabled
```
**Solution:**
```powershell
# Temporary bypass
powershell -ExecutionPolicy Bypass -File .\build-all.ps1

# Or set execution policy (requires admin)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## 📁 Project Structure

```
appium-bootstrap-installer/
├── AppiumBootstrapInstaller/
│   ├── Models/
│   │   ├── DeviceModels.cs              # Device and session models
│   │   └── InstallConfig.cs             # Configuration models
│   ├── Services/
│   │   ├── AppiumSessionManager.cs      # Appium instance management
│   │   ├── ConfigurationReader.cs       # Config file reader
│   │   ├── DeviceListenerService.cs     # Device monitoring service
│   │   ├── DeviceMetrics.cs             # Metrics collection
│   │   ├── DeviceRegistry.cs            # Device session registry
│   │   └── ScriptExecutor.cs            # Script execution engine
│   ├── Platform/
│   │   ├── Windows/Scripts/             # Windows installation scripts
│   │   │   ├── InstallDependencies.ps1
│   │   │   ├── ServiceSetup.ps1
│   │   │   ├── StartAppiumServer.ps1
│   │   │   ├── DeviceListener.ps1
│   │   │   └── ...
│   │   ├── MacOS/Scripts/               # macOS installation scripts
│   │   │   ├── InstallDependencies.sh
│   │   │   ├── SupervisorSetup.sh
│   │   │   └── ...
│   │   └── Linux/Scripts/               # Linux installation scripts
│   │       ├── InstallDependencies.sh
│   │       ├── SystemdSetup.sh
│   │       └── ...
│   ├── Program.cs                        # Main application entry point
│   ├── AppiumBootstrapInstaller.csproj   # Project file
│   └── bin/Release/net8.0/               # Build output
├── publish/                              # Published releases
│   ├── AppiumBootstrapInstaller.exe      # Windows executable
│   ├── config.json                       # User configuration
│   ├── config.sample.json                # Sample configuration
│   ├── INSTALL.md                        # Installation guide
│   ├── USER_GUIDE.md                     # User guide
│   ├── setup-service.bat                 # Windows setup script
│   ├── logs/                             # Log directory
│   └── Platform/                         # Platform scripts (copied)
├── examples/                             # Example configurations
├── build-all.ps1                         # Build script (Windows)
├── build-all.sh                          # Build script (Linux/macOS)
├── config.sample.json                    # Root sample config
├── config.windows.json                   # Windows example
├── config.appium3.json                   # Appium 3.x example
├── README.md                             # This file
├── USER_GUIDE.md                         # Detailed user guide
├── DEVICE_LISTENER.md                    # Device listener documentation
├── OBSERVABILITY_PLAN.md                 # Observability architecture
├── LICENSE                               # Apache 2.0 license
└── AppiumBootstrapInstaller.sln          # Visual Studio solution
```

## 🎯 Use Cases

### 1. CI/CD Pipeline Integration

Automate Appium setup in continuous integration:

```yaml
# GitHub Actions example
- name: Setup Appium
  run: |
    curl -L https://github.com/yourusername/appium-bootstrap-installer/releases/latest/download/AppiumBootstrapInstaller-linux-x64.tar.gz | tar xz
    ./AppiumBootstrapInstaller --config ci-config.json
```

### 2. Device Farm Setup

Configure automatic Appium session management for device farm:

```json
{
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "DeviceListenerPollInterval": 3,
  "PortRanges": {
    "AppiumStart": 4723,
    "AppiumEnd": 4923
  }
}
```

### 3. Development Environment

Quick setup for local development:

```bash
# Install Appium and dependencies
AppiumBootstrapInstaller --config dev-config.json

# Start monitoring devices
AppiumBootstrapInstaller --listen
```

### 4. Testing Lab

Enterprise testing lab with multiple devices:

```json
{
  "InstallFolder": "/opt/appium-lab",
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "DeviceRegistry": {
    "Enabled": true,
    "AutoSaveInterval": 30
  },
  "PortRanges": {
    "AppiumStart": 4723,
    "AppiumEnd": 5000
  }
}
```

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Setup

```bash
# Clone repository
git clone https://github.com/yourusername/appium-bootstrap-installer.git
cd appium-bootstrap-installer

# Build project
dotnet build

# Run tests
dotnet test

# Run locally
dotnet run --project AppiumBootstrapInstaller -- --help
```

## 📄 License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [Appium](https://appium.io/) - Mobile automation framework
- [NVM](https://github.com/nvm-sh/nvm) - Node Version Manager
- [NSSM](https://nssm.cc/) - Windows service manager
- [Supervisor](http://supervisord.org/) - Process control system
- [Serilog](https://serilog.net/) - Structured logging library

## 📚 Additional Documentation

- [USER_GUIDE.md](USER_GUIDE.md) - Comprehensive user guide
- [DEVICE_LISTENER.md](DEVICE_LISTENER.md) - Device listener details
- [OBSERVABILITY_PLAN.md](OBSERVABILITY_PLAN.md) - Observability architecture
- [INSTALL.md](publish/INSTALL.md) - Installation instructions (in publish folder)

## 🆘 Support

- **Issues:** [GitHub Issues](https://github.com/yourusername/appium-bootstrap-installer/issues)
- **Discussions:** [GitHub Discussions](https://github.com/yourusername/appium-bootstrap-installer/discussions)
- **Documentation:** Check the docs folder and inline help

## 🗺️ Roadmap

- [ ] Web UI for monitoring and management
- [ ] REST API for remote control
- [ ] Docker container support
- [ ] Kubernetes operator
- [ ] Cloud provider integrations (AWS, Azure, GCP)
- [ ] Advanced load balancing
- [ ] Device health monitoring
- [ ] Test execution scheduling
- [ ] Multi-tenant support
- [ ] Performance analytics dashboard

## 📊 Statistics

![GitHub stars](https://img.shields.io/github/stars/yourusername/appium-bootstrap-installer?style=social)
![GitHub forks](https://img.shields.io/github/forks/yourusername/appium-bootstrap-installer?style=social)
![GitHub issues](https://img.shields.io/github/issues/yourusername/appium-bootstrap-installer)
![GitHub pull requests](https://img.shields.io/github/issues-pr/yourusername/appium-bootstrap-installer)
![License](https://img.shields.io/github/license/yourusername/appium-bootstrap-installer)

---

**Made with ❤️ for the Appium Community**
