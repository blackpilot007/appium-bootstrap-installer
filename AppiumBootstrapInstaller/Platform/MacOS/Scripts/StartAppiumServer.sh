#!/bin/bash

# StartAppiumServer.sh - MacOS version
# Starts Appium server with specified ports for iOS devices

# Check for minimum number of arguments
if [ "$#" -lt 3 ]; then
    echo "Usage: $0 <appium_port> <wda_local_port> <mpeg_local_port>"
    echo "or: $0 <appium_home_path> <appium_bin_path> <appium_port> <wda_local_port> <mpeg_local_port>"
    exit 1
fi

# Debug: Print all arguments
echo "DEBUG: Total arguments: $#"
for i in $(seq 1 $#); do
    echo "DEBUG: Argument $i: ${!i}"
done

# Parse arguments
if [ "$#" -eq 3 ]; then
    APPIUM_PORT=$1
    WDA_LOCAL_PORT=$2
    MPEG_LOCAL_PORT=$3
elif [ "$#" -eq 5 ]; then
    CUSTOM_APPIUM_HOME=$1
    CUSTOM_APPIUM_BIN=$2
    APPIUM_PORT=$3
    WDA_LOCAL_PORT=$4
    MPEG_LOCAL_PORT=$5
else
    echo "Error: Unexpected number of arguments: $#"
    exit 1
fi

# Detect APPIUM_HOME
if [ -n "$CUSTOM_APPIUM_HOME" ] && [ -d "$CUSTOM_APPIUM_HOME" ]; then
    echo "Using custom Appium home: $CUSTOM_APPIUM_HOME"
    APPIUM_HOME="$CUSTOM_APPIUM_HOME"
elif [ -z "$APPIUM_HOME" ]; then
    # Try common locations for MacOS
    if [ -d "$HOME/AppiumBootstrap/appium-home" ]; then
        APPIUM_HOME="$HOME/AppiumBootstrap/appium-home"
    elif [ -d "$HOME/.local/appium-home" ]; then
        APPIUM_HOME="$HOME/.local/appium-home"
    elif [ -d "$HOME/.appium-bootstrap/appium-home" ]; then
        APPIUM_HOME="$HOME/.appium-bootstrap/appium-home"
    elif [ -d "$HOME/.appium" ]; then
        APPIUM_HOME="$HOME/.appium"
    else
        echo "Error: APPIUM_HOME not found"
        exit 1
    fi
fi

# Find appium executable
if [ -n "$CUSTOM_APPIUM_BIN" ] && [ -f "$CUSTOM_APPIUM_BIN/appium" ]; then
    echo "Using custom Appium binary: $CUSTOM_APPIUM_BIN"
    APPIUM_PATH="$CUSTOM_APPIUM_BIN"
elif [ -f "$APPIUM_HOME/node_modules/.bin/appium" ]; then
    APPIUM_PATH="$APPIUM_HOME/node_modules/.bin"
elif command -v appium &>/dev/null; then
    APPIUM_PATH=$(dirname $(command -v appium))
else
    echo "Error: Appium executable not found"
    exit 1
fi

# Detect Appium version
APPIUM_VERSION=$($APPIUM_PATH/appium --version 2>/dev/null)
APPIUM_MAJOR_VERSION=$(echo $APPIUM_VERSION | cut -d'.' -f1)
echo "Detected Appium version: $APPIUM_VERSION (Major: $APPIUM_MAJOR_VERSION)"

# Load NVM if available (try common MacOS locations)
export NVM_DIR="${NVM_DIR:-$HOME/AppiumBootstrap/.nvm}"
if [ -s "$NVM_DIR/nvm.sh" ]; then
    echo "Loading NVM from $NVM_DIR"
    . "$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1
    nvm use default > /dev/null 2>&1 || true
elif [ -s "$HOME/.nvm/nvm.sh" ]; then
    echo "Loading NVM from $HOME/.nvm"
    export NVM_DIR="$HOME/.nvm"
    . "$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1
    nvm use default > /dev/null 2>&1 || true
fi

# Add paths
export PATH="$APPIUM_HOME/bin:$APPIUM_HOME/node_modules/.bin:$PATH"
export PATH="$HOME/.local/bin:$PATH"

# Add Homebrew to PATH for MacOS
if [ -d "/opt/homebrew/bin" ]; then
    export PATH="/opt/homebrew/bin:$PATH"
elif [ -d "/usr/local/bin" ]; then
    export PATH="/usr/local/bin:$PATH"
fi

echo "Node version: $(node --version) | Appium version: $APPIUM_VERSION"

# Check for DeviceFarm plugin
echo "Checking for DeviceFarm plugin..."
DEVICE_FARM_INSTALLED=false
if [ -d "$APPIUM_HOME/node_modules/appium-device-farm" ]; then
    echo "✅ DeviceFarm plugin is installed"
    DEVICE_FARM_INSTALLED=true
else
    echo "ℹ️ DeviceFarm plugin is not installed"
fi

# Build plugin list dynamically
PLUGIN_LIST="inspector"
if [ "$DEVICE_FARM_INSTALLED" = true ]; then
    PLUGIN_LIST="device-farm,appium-dashboard,inspector"
    echo "DeviceFarm enabled - plugins: $PLUGIN_LIST"
else
    echo "DeviceFarm not installed - plugins: $PLUGIN_LIST"
fi

# Stop existing processes on ports
for port in $APPIUM_PORT $WDA_LOCAL_PORT $MPEG_LOCAL_PORT; do
    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
        echo "Stopping process on port $port..."
        lsof -Pi :$port -sTCP:LISTEN -t | xargs kill -9 2>/dev/null || true
    fi
done

echo "Starting Appium server on port $APPIUM_PORT..."

# Check if device-farm is installed
DEVICE_FARM_INSTALLED=false
if [ -d "$APPIUM_HOME/node_modules/appium-device-farm" ]; then
    DEVICE_FARM_INSTALLED=true
    echo "✅ DeviceFarm plugin is installed"
else
    echo "ℹ️ DeviceFarm plugin is not installed"
fi

# Build plugin list and options dynamically
PLUGIN_LIST="inspector"
PLUGIN_OPTIONS=""
if [ "$DEVICE_FARM_INSTALLED" = true ]; then
    PLUGIN_LIST="device-farm,appium-dashboard,inspector"
    
    # Detect installed drivers
    XCUITEST_INSTALLED=false
    UIAUTOMATOR_INSTALLED=false
    
    if [ -d "$APPIUM_HOME/node_modules/appium-xcuitest-driver" ]; then
        XCUITEST_INSTALLED=true
    fi
    
    if [ -d "$APPIUM_HOME/node_modules/appium-uiautomator2-driver" ]; then
        UIAUTOMATOR_INSTALLED=true
    fi
    
    # Determine platform based on installed drivers
    if [ "$XCUITEST_INSTALLED" = true ] && [ "$UIAUTOMATOR_INSTALLED" = true ]; then
        DEVICE_FARM_PLATFORM="both"
        echo "✅ Both iOS and Android drivers detected - platform set to 'both'"
    elif [ "$XCUITEST_INSTALLED" = true ]; then
        DEVICE_FARM_PLATFORM="ios"
        echo "✅ Only iOS driver detected - platform set to 'ios'"
    elif [ "$UIAUTOMATOR_INSTALLED" = true ]; then
        DEVICE_FARM_PLATFORM="android"
        echo "✅ Only Android driver detected - platform set to 'android'"
    else
        DEVICE_FARM_PLATFORM="both"
        echo "⚠️ No drivers detected - defaulting platform to 'both'"
    fi
    
    # Device Farm plugin configuration
    # Reference: https://github.com/AppiumTestDistribution/appium-device-farm/blob/main/server-config.json
    PLUGIN_OPTIONS="--plugin-device-farm-platform=$DEVICE_FARM_PLATFORM"
    PLUGIN_OPTIONS="$PLUGIN_OPTIONS --plugin-device-farm-skip-chrome-download"
    
    # Add iOS-specific options only if iOS is supported
    if [ "$DEVICE_FARM_PLATFORM" = "ios" ] || [ "$DEVICE_FARM_PLATFORM" = "both" ]; then
        PLUGIN_OPTIONS="$PLUGIN_OPTIONS --plugin-device-farm-ios-device-type=real"
    fi
    
    echo "DeviceFarm enabled - plugins: $PLUGIN_LIST"
    echo "DeviceFarm platform: $DEVICE_FARM_PLATFORM"
    echo "DeviceFarm options: $PLUGIN_OPTIONS"
else
    echo "DeviceFarm not installed - plugins: $PLUGIN_LIST"
fi

# Build Appium command
# Note: -pa /wd/hub is not required for Appium 2.x and 3.x (defaults to /)
if [[ "$APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
    # Appium 3.x
    APPIUM_CMD="$APPIUM_PATH/appium server -p $APPIUM_PORT --allow-cors --allow-insecure=xcuitest:get_server_logs --default-capabilities '{\"appium:wdaLocalPort\": $WDA_LOCAL_PORT,\"appium:mjpegServerPort\": $MPEG_LOCAL_PORT}' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PLUGIN_LIST $PLUGIN_OPTIONS"
else
    # Appium 2.x
    APPIUM_CMD="$APPIUM_PATH/appium -p $APPIUM_PORT --allow-cors --allow-insecure=get_server_logs --default-capabilities '{\"appium:wdaLocalPort\": $WDA_LOCAL_PORT,\"appium:mjpegServerPort\": $MPEG_LOCAL_PORT}' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PLUGIN_LIST $PLUGIN_OPTIONS"
fi

echo "Executing: $APPIUM_CMD"

# Execute Appium
exec $APPIUM_CMD
