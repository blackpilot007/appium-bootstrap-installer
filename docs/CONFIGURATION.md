# Configuration Guide

Complete reference for configuring Appium Bootstrap Installer.

## Configuration File Location

The application searches for configuration files in this order:

1. **Specified path** (via `--config` argument)
2. **Current directory** (`./config.json`)
3. **Home directory** (`~/.appium-bootstrap/config.json`)

## Complete Schema

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
      "Name": "string - Driver name (uiautomator2, xcuitest, espresso, flutter, safari, gecko, etc.)",
      "Version": "string - Driver version (e.g., 3.8.3)",
      "Enabled": "boolean - Enable/disable driver installation"
    }
  ],

  // ============================================
  // PLUGINS CONFIGURATION
  // ============================================
  "Plugins": [
    {
      "Name": "string - Plugin name (device-farm, appium-dashboard, images, relaxed-caps, etc.)",
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

## Environment Variable Expansion

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

## Configuration Examples

### Example 1: Android Testing (Windows)
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

### Example 2: iOS Testing (macOS)
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

### Example 3: Multi-Platform Testing
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

### Example 4: CI/CD Pipeline
```json
{
  "InstallFolder": "/opt/ci-appium",
  "EnableDeviceListener": false,
  "Drivers": [
    {
      "Name": "uiautomator2",
      "Version": "3.8.3",
      "Enabled": true
    }
  ]
}
```

### Example 5: Device Farm
```json
{
  "InstallFolder": "/opt/device-farm",
  "EnableDeviceListener": true,
  "AutoStartAppium": true,
  "DeviceListenerPollInterval": 3,
  "DeviceRegistry": {
    "Enabled": true,
    "AutoSaveInterval": 30
  },
  "PortRanges": {
    "AppiumStart": 4723,
    "AppiumEnd": 5000,
    "SystemPortStart": 8200,
    "SystemPortEnd": 8500
  },
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

## Configuration Tips

### 1. Port Management
- Allocate enough ports for expected devices: `(AppiumEnd - AppiumStart) + 1`
- Leave buffer space for future expansion
- Avoid conflicts with other services

### 2. Device Listener
- Use shorter poll intervals (3-5s) for interactive environments
- Use longer intervals (10-15s) for production to reduce overhead
- Enable registry for audit trails and troubleshooting

### 3. Platform-Specific Settings
- Use `PlatformSpecific` to override defaults per OS
- Useful for cross-platform configuration files

### 4. CI/CD Optimization
- Set `EnableDeviceListener: false` for one-time installations
- Minimize driver/plugin installation to speed up pipelines

### 5. Development vs Production
- Development: Enable all logging, shorter intervals
- Production: Optimize intervals, enable metrics collection
