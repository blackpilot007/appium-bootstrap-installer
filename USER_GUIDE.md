# Appium Bootstrap Installer - User Guide

## Overview

The Appium Bootstrap Installer is a configuration-driven service that automates the installation of Appium and all its dependencies across Windows, macOS, and Linux.

## Installation Process

### What Gets Installed

The installer will set up the following components in your specified installation folder:

1. **NVM (Node Version Manager)**
   - Windows: nvm-windows
   - macOS/Linux: nvm

2. **Node.js**
   - Specific version as configured
   - Managed by NVM for easy version switching

3. **Appium Server**
   - Specific version as configured
   - Installed in a local directory (not globally)

4. **Appium Drivers** (if enabled)
   - **XCUITest**: For iOS automation
   - **UiAutomator2**: For Android automation

5. **Appium Plugins** (if enabled)
   - **device-farm**: Device management and parallel execution
   - **appium-dashboard**: Web-based dashboard for monitoring

6. **Platform-Specific Tools**
   - **macOS**: libimobiledevice for iOS device support
   - **Windows**: Optional iTunes drivers for iOS support
   - **Linux**: Optional libimobiledevice for iOS support
   - **All**: Android SDK Platform Tools (ADB)

## Configuration Guide

### Basic Configuration

Create a `config.json` file with minimal settings:

```json
{
  "installFolder": "C:\\appium",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1"
}
```

### Full Configuration

For complete control, specify all options:

```json
{
  "installFolder": "${HOME}/.local/appium",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "nvmVersion": "0.40.2",
  "drivers": [
    {
      "name": "xcuitest",
      "version": "7.24.3",
      "enabled": true
    },
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
    },
    {
      "name": "appium-dashboard",
      "version": "2.0.3",
      "enabled": false
    }
  ],
  "platformSpecific": {
    "macOS": {
      "libimobiledeviceVersion": "latest",
      "installXCUITest": true,
      "installUiAutomator": true,
      "installDeviceFarm": true
    },
    "windows": {
      "installIOSSupport": false,
      "installAndroidSupport": true,
      "installXCUITest": false,
      "installUiAutomator": true,
      "installDeviceFarm": true
    },
    "linux": {
      "installIOSSupport": false,
      "installAndroidSupport": true
    }
  }
}
```

### Configuration Options Explained

#### Core Settings

- **installFolder**: Where to install all components
  - Supports environment variables: `${HOME}`, `%USERPROFILE%`
  - Will be **completely deleted** before installation starts
  - Example: `C:\appium`, `/Users/Shared/tmp/appiumagent`

- **nodeVersion**: Node.js version to install
  - Example: `"22"`, `"20.18.1"`

- **appiumVersion**: Appium server version
  - Example: `"2.17.1"`, `"3.0.0"`

- **nvmVersion**: NVM version
  - Windows: nvm-windows version (e.g., `"1.1.12"`)
  - macOS/Linux: nvm version (e.g., `"0.40.2"`)

#### Drivers

Each driver can be individually configured:

```json
{
  "name": "uiautomator2",
  "version": "3.8.3",
  "enabled": true
}
```

- **name**: Driver identifier (`xcuitest`, `uiautomator2`)
- **version**: Specific version or empty for latest
- **enabled**: Set to `false` to skip installation

#### Plugins

Similar to drivers:

```json
{
  "name": "device-farm",
  "version": "8.3.5",
  "enabled": true
}
```

#### Platform-Specific Settings

Override behavior for specific operating systems:

**macOS:**
- `libimobiledeviceVersion`: Version of iOS device tools
- `installXCUITest`: Install XCUITest driver
- `installUiAutomator`: Install UiAutomator2 driver
- `installDeviceFarm`: Install device-farm plugin

**Windows:**
- `installIOSSupport`: Install iTunes drivers for iOS
- `installAndroidSupport`: Install Android SDK tools
- `installXCUITest`: Install XCUITest driver
- `installUiAutomator`: Install UiAutomator2 driver
- `installDeviceFarm`: Install device-farm plugin

**Linux:**
- `installIOSSupport`: Install libimobiledevice
- `installAndroidSupport`: Install Android tools

## Usage Examples

### Example 1: Android-Only Setup on Windows

```json
{
  "installFolder": "C:\\appium-android",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    {
      "name": "uiautomator2",
      "version": "3.8.3",
      "enabled": true
    }
  ],
  "platformSpecific": {
    "windows": {
      "installAndroidSupport": true,
      "installIOSSupport": false
    }
  }
}
```

Run:
```powershell
AppiumBootstrapInstaller.exe --config android-config.json
```

### Example 2: iOS-Only Setup on macOS

```json
{
  "installFolder": "/Users/Shared/tmp/appium-ios",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    {
      "name": "xcuitest",
      "version": "7.24.3",
      "enabled": true
    }
  ],
  "platformSpecific": {
    "macOS": {
      "libimobiledeviceVersion": "latest",
      "installXCUITest": true,
      "installUiAutomator": false
    }
  }
}
```

Run:
```bash
./AppiumBootstrapInstaller --config ios-config.json
```

### Example 3: Full Setup with Device Farm

```json
{
  "installFolder": "${HOME}/.local/appium-full",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "drivers": [
    {
      "name": "xcuitest",
      "version": "7.24.3",
      "enabled": true
    },
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
    },
    {
      "name": "appium-dashboard",
      "version": "2.0.3",
      "enabled": true
    }
  ]
}
```

## After Installation

### Windows

1. **Set up environment:**
   ```powershell
   C:\appium\appium-env.bat
   ```

2. **Start Appium:**
   ```powershell
   C:\appium\bin\appium.bat
   ```

### macOS/Linux

1. **Load NVM:**
   ```bash
   source ~/.local/appium/.nvm/nvm.sh
   ```

2. **Use Node version:**
   ```bash
   nvm use 22
   ```

3. **Start Appium:**
   ```bash
   ~/.local/appium/bin/appium
   ```

### Verify Installation

Check installed drivers:
```bash
appium driver list --installed
```

Check installed plugins:
```bash
appium plugin list --installed
```

## Important Notes

### Clean Installation

⚠️ **WARNING**: The installer **ALWAYS deletes** the installation folder before starting. This ensures a clean, consistent installation every time.

**Before running:**
- Backup any important data in the installation folder
- Ensure you have the correct path in your configuration
- Use `--dry-run` to preview what will happen

### Permissions

- **macOS/Linux**: The installer automatically sets execute permissions on scripts
- **Windows**: May require Administrator privileges for some operations
- **All platforms**: Ensure you have write permissions to the installation folder

### Network Requirements

The installation process downloads components from the internet:
- Node.js binaries
- Appium npm packages
- Driver packages
- Plugin packages
- Platform-specific tools

Ensure you have a stable internet connection during installation.

## Troubleshooting

### Installation Fails

1. **Check logs**: The installer streams all output in real-time
2. **Verify configuration**: Use `--dry-run` to preview
3. **Check permissions**: Ensure write access to installation folder
4. **Check network**: Verify internet connectivity
5. **Check disk space**: Ensure sufficient space (recommend 2GB+)

### Script Not Found

```
Error: Installation script not found
```

**Solution**: Ensure the `Platform` folder exists in the repository root with the correct structure.

### Permission Denied

**macOS/Linux:**
```bash
chmod +x AppiumBootstrapInstaller
./AppiumBootstrapInstaller --config config.json
```

**Windows:**
Run PowerShell as Administrator

## Advanced Usage

### Multiple Configurations

Maintain different configurations for different environments:

```
configs/
├── dev-android.json
├── dev-ios.json
├── ci-full.json
└── test-minimal.json
```

Run with specific config:
```bash
AppiumBootstrapInstaller --config configs/dev-android.json
```

### Environment-Specific Paths

Use environment variables for portability:

```json
{
  "installFolder": "${CI_WORKSPACE}/appium"
}
```

### Dry Run Before Installation

Always preview before running:

```bash
AppiumBootstrapInstaller --config config.json --dry-run
```

This shows exactly what will be executed without making any changes.
