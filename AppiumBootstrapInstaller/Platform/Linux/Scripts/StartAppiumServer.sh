#!/bin/bash

# appium.sh - Linux version
# Starts Appium server with specified ports
# Uses explicit fully qualified paths for complete isolation

# Require at least 9 parameters; 8th optional PrebuiltWdaPath, 9-10 DeviceUdid and Platform
if [ "$#" -lt 7 ] || [ "$#" -gt 10 ]; then
    echo "Usage: $0 <appium_home_path> <appium_bin_path> <node_path> <install_folder> <appium_port> <wda_local_port> <mpeg_local_port> [prebuilt_wda_path] [device_udid] [platform]"
    exit 1
fi

# Parse arguments - required for isolation
APPIUM_HOME="$1"
APPIUM_BIN="$2"
NODE_PATH="$3"
INSTALL_FOLDER="$4"
APPIUM_PORT="$5"
WDA_LOCAL_PORT="$6"
MPEG_LOCAL_PORT="$7"
# Optional prebuilt WDA path (local path or URL)
PREBUILT_WDA_PATH=""
if [ "$#" -ge 8 ]; then
    PREBUILT_WDA_PATH="$8"
fi
# Optional device UDID and platform
DEVICE_UDID=""
PLATFORM=""
if [ "$#" -ge 9 ]; then
    DEVICE_UDID="$9"
fi
if [ "$#" -eq 10 ]; then
    PLATFORM="${10}"
fi

echo "========================================="
echo "Starting Appium Server (Linux)"
echo "========================================="
echo "Appium Home: $APPIUM_HOME"
echo "Node.js Path: $NODE_PATH"
echo "Install Folder: $INSTALL_FOLDER"
echo "Appium Port: $APPIUM_PORT"
echo "WDA Local Port: $WDA_LOCAL_PORT"
echo "MPEG Local Port: $MPEG_LOCAL_PORT"
if [ -n "$PREBUILT_WDA_PATH" ]; then
    echo "Prebuilt WDA Path: $PREBUILT_WDA_PATH"
fi
if [ -n "$DEVICE_UDID" ]; then
    echo "Device UDID: $DEVICE_UDID"
fi
if [ -n "$PLATFORM" ]; then
    echo "Platform: $PLATFORM"
fi
echo "========================================="

# Setup iOS port forwarding if it's an iOS device
if [ "$PLATFORM" = "iOS" ] && [ -n "$DEVICE_UDID" ]; then
    echo "Setting up iOS port forwarding for device $DEVICE_UDID..."
    
    GOIOS_PATH="$INSTALL_FOLDER/.cache/appium-device-farm/goIOS/ios"
    
    if [ -x "$GOIOS_PATH" ]; then
        # Forward WDA port (8100 on device -> WdaLocalPort on host)
        echo "Forwarding WDA port: $WDA_LOCAL_PORT -> 8100 on device"
        "$GOIOS_PATH" forward --udid "$DEVICE_UDID" "$WDA_LOCAL_PORT" 8100 &
        
        # Forward MJPEG port if specified (9100 on device -> MpegLocalPort on host)
        if [ "$MPEG_LOCAL_PORT" -gt 0 ]; then
            echo "Forwarding MJPEG port: $MPEG_LOCAL_PORT -> 9100 on device"
            "$GOIOS_PATH" forward --udid "$DEVICE_UDID" "$MPEG_LOCAL_PORT" 9100 &
        fi
        
        sleep 2
        echo "✅ iOS port forwarding setup completed"
    else
        echo "⚠️ go-ios not found at $GOIOS_PATH, skipping port forwarding"
    fi
fi

# Use explicit fully qualified node path
NODE_EXE="$NODE_PATH/bin/node"

if [ ! -f "$NODE_EXE" ]; then
    echo "ERROR: Node.js executable not found at $NODE_EXE"
    exit 1
fi

echo "Using local Node.js: $NODE_EXE"

# Set APPIUM_HOME explicitly for this process only (not system-wide)
export APPIUM_HOME="$APPIUM_HOME"

# If provided, export prebuilt WDA path for Appium process
if [ -n "$PREBUILT_WDA_PATH" ]; then
    export APPIUM_PREBUILT_WDA="$PREBUILT_WDA_PATH"
    echo "Using prebuilt WDA: $APPIUM_PREBUILT_WDA"
fi

# Prepare Appium script path - use explicit node execution
APPIUM_SCRIPT="$APPIUM_HOME/node_modules/appium/build/lib/main.js"

if [ ! -f "$APPIUM_SCRIPT" ]; then
    echo "ERROR: Appium script not found at $APPIUM_SCRIPT"
    exit 1
fi

# Detect Appium version using explicit node path
APPIUM_VERSION=$($NODE_EXE "$APPIUM_SCRIPT" --version 2>/dev/null)
APPIUM_MAJOR_VERSION=$(echo $APPIUM_VERSION | cut -d'.' -f1)
echo "Detected Appium version: $APPIUM_VERSION (Major: $APPIUM_MAJOR_VERSION)"

# No NVM loading or PATH manipulation needed - using fully qualified paths
echo "Node version: $($NODE_EXE --version)"

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

# Build Appium command using explicit node and script paths
# Note: -pa /wd/hub is not required for Appium 2.x and 3.x (defaults to /)
if [[ "$APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
    # Appium 3.x
    APPIUM_CMD="$NODE_EXE \"$APPIUM_SCRIPT\" server -p $APPIUM_PORT --allow-cors --allow-insecure=xcuitest:get_server_logs --default-capabilities '{\"appium:wdaLocalPort\": $WDA_LOCAL_PORT,\"appium:mjpegServerPort\": $MPEG_LOCAL_PORT}' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PLUGIN_LIST $PLUGIN_OPTIONS"
else
    # Appium 2.x
    APPIUM_CMD="$NODE_EXE \"$APPIUM_SCRIPT\" -p $APPIUM_PORT --allow-cors --allow-insecure=get_server_logs --default-capabilities '{\"appium:wdaLocalPort\": $WDA_LOCAL_PORT,\"appium:mjpegServerPort\": $MPEG_LOCAL_PORT}' --log-level info --log-timestamp --local-timezone --log-no-colors --use-plugins=$PLUGIN_LIST $PLUGIN_OPTIONS"
fi

echo "Executing: $APPIUM_CMD"

# Execute Appium
exec $APPIUM_CMD
