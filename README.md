# Appium Bootstrap Installer

A cross-platform .NET application that automates the complete setup of Appium across Windows, macOS, and Linux. It installs all prerequisites, dependencies, drivers, and configurations required to run Appium seamlessly, eliminating manual setup hassles.

## ✨ Key Highlights

- **Configuration-Driven** – Define installation parameters in JSON files
- **Universal Support** – Works across Windows, macOS, and Linux
- **Self-Contained** – Single executable with no .NET runtime required on target machines
- **Cross-Platform Build** – Build for all platforms from any OS
- **Automated Setup** – Installs Node.js, Appium server, platform drivers, and required dependencies
- **Driver Management** – Handles iOS, Android, and cross-platform drivers automatically
- **Clean Installation** – Always performs fresh installation by cleaning the target folder first
- **Dry-Run Mode** – Preview what will be executed without making changes

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later (only needed for building)

## Quick Start

### 1. Generate Sample Configuration

```bash
cd AppiumBootstrapInstaller
dotnet run -- --generate-config
```

This creates `config.sample.json` with all available options.

### 2. Customize Configuration

Edit `config.sample.json` or create your own `config.json`:

```json
{
  "installFolder": "C:\\appium-test",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "nvmVersion": "0.40.2",
  "drivers": [
    {
      "name": "uiautomator2",
      "version": "3.8.3",
      "enabled": true
    }
  ],
  "plugins": [
    {
      "name": "device-farm",
      "version": "8.3.5",
      "enabled": true
    }
  ]
}
```

### 3. Run Installation

**Dry run (preview only):**
```bash
dotnet run -- --config config.json --dry-run
```

**Actual installation:**
```bash
dotnet run -- --config config.json
```

## Configuration File

### Location Priority

The service searches for configuration files in this order:

1. **Specified path** (via `--config` argument)
2. **Current directory** (`./config.json`)
3. **Home directory** (`~/.appium-bootstrap/config.json`)

### Configuration Schema

```json
{
  "installFolder": "string (required) - Installation directory path",
  "nodeVersion": "string (default: 22) - Node.js version",
  "appiumVersion": "string (default: 2.17.1) - Appium version",
  "nvmVersion": "string (default: 0.40.2) - NVM version",
  "drivers": [
    {
      "name": "string - Driver name (xcuitest, uiautomator2)",
      "version": "string - Driver version",
      "enabled": "boolean - Enable/disable driver"
    }
  ],
  "plugins": [
    {
      "name": "string - Plugin name",
      "version": "string - Plugin version",
      "enabled": "boolean - Enable/disable plugin"
    }
  ],
  "platformSpecific": {
    "macOS": {
      "libimobiledeviceVersion": "string",
      "installXCUITest": "boolean",
      "installUiAutomator": "boolean",
      "installDeviceFarm": "boolean"
    },
    "windows": {
      "installIOSSupport": "boolean",
      "installAndroidSupport": "boolean",
      "installXCUITest": "boolean",
      "installUiAutomator": "boolean",
      "installDeviceFarm": "boolean"
    },
    "linux": {
      "installIOSSupport": "boolean",
      "installAndroidSupport": "boolean"
    }
  }
}
```

### Environment Variables

Configuration values support environment variable expansion:

- **Windows style**: `%USERPROFILE%\appium`
- **Unix style**: `${HOME}/appium`

Example:
```json
{
  "installFolder": "${HOME}/.local/appium"
}
```

## Command-Line Options

```
USAGE:
  AppiumBootstrapInstaller [options]

OPTIONS:
  --config, -c <path>       Path to configuration file (JSON)
  --dry-run, -d             Show what would be executed without running
  --generate-config, -g     Generate a sample configuration file
  --help, -h                Show help message

EXAMPLES:
  # Generate sample config
  AppiumBootstrapInstaller --generate-config

  # Run with custom config
  AppiumBootstrapInstaller --config my-config.json

  # Dry run to preview execution
  AppiumBootstrapInstaller --config my-config.json --dry-run

  # Use default config location
  AppiumBootstrapInstaller
```

## Building the Application

### Build for Current Platform

```bash
cd AppiumBootstrapInstaller
dotnet build -c Release
```

### Build Self-Contained Executables

**Windows x64:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ./publish/win-x64
```

**Linux x64:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ./publish/linux-x64
```

**macOS x64:**
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ./publish/osx-x64
```

**macOS ARM64 (Apple Silicon):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ./publish/osx-arm64
```

### Build All Platforms at Once

**On Windows (PowerShell):**
```powershell
cd AppiumBootstrapInstaller
.\build-all.ps1
```

**On Linux/macOS (Bash):**
```bash
cd AppiumBootstrapInstaller
chmod +x build-all.sh
./build-all.sh
```

## How It Works

1. **Configuration Loading**: Reads JSON configuration from file with priority-based location search
2. **OS Detection**: Automatically detects Windows, macOS, or Linux
3. **Script Selection**: Selects appropriate installation script from `Platform/{OS}/Scripts/`
4. **Folder Cleanup**: Deletes the installation folder for a fresh start
5. **Permission Setup**: Auto-sets execute permissions on scripts (Unix-like systems)
6. **Script Execution**: Runs platform-specific script with parameters from configuration
7. **Real-time Output**: Streams installation progress to console

## Platform-Specific Scripts

The service uses existing battle-tested installation scripts:

- **Windows**: `Platform/Windows/Scripts/InstallDependencies.ps1`
- **macOS**: `Platform/MacOS/Scripts/InstallDependencies.sh`
- **Linux**: `Platform/Linux/Scripts/InstallDependencies.sh`

Additional utility scripts available:
- **Runtime**: `StartAppiumServer.ps1` / `StartAppiumServer.sh` - Start Appium server
- **Setup**: `InitialSetup.bat` / `InitialSetup.command` / `InitialSetup.sh` - Initial environment setup
- **Utilities**: `check_appium_drivers.*`, `clean-appium-install.*`

## Sample Configurations

### Windows (Android Only)

See [`config.windows.json`](file:///d:/Code_repos/Github/appium-bootstrap-installer/config.windows.json)

### Cross-Platform (Full Setup)

See [`config.sample.json`](file:///d:/Code_repos/Github/appium-bootstrap-installer/config.sample.json)

## Troubleshooting

### Configuration Not Found

```
Error: Configuration file not found
```

**Solution**: Specify config path explicitly:
```bash
AppiumBootstrapInstaller --config /path/to/config.json
```

### Platform Scripts Not Found

```
Error: Platform scripts directory not found
```

**Solution**: Ensure the `Platform` folder exists in the repository root alongside `AppiumBootstrapInstaller` folder.

### Permission Denied (Linux/macOS)

```
Error: Permission denied
```

**Solution**: The service auto-sets execute permissions, but you may need to run with appropriate user permissions for the installation folder.

## Project Structure

```
appium-bootstrap-installer/
├── AppiumBootstrapInstaller/
│   ├── Models/
│   │   └── InstallConfig.cs           # Configuration models
│   ├── Services/
│   │   ├── ConfigurationReader.cs     # Config file reader
│   │   └── ScriptExecutor.cs          # Script execution engine
│   ├── Program.cs                      # Main application
│   ├── AppiumBootstrapInstaller.csproj
│   ├── build-all.ps1                   # Build script (Windows)
│   └── build-all.sh                    # Build script (Linux/macOS)
├── Platform/
│   ├── Windows/Scripts/                # Windows installation scripts
│   ├── MacOS/Scripts/                  # macOS installation scripts
│   └── Linux/Scripts/                  # Linux installation scripts
├── config.sample.json                  # Sample configuration
├── config.windows.json                 # Windows-specific example
└── README.md
```

## License

[Add your license here]
