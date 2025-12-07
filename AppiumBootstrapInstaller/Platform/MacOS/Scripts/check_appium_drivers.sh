#!/bin/bash

# check_appium_drivers.sh - A diagnostic tool to check Appium driver installation
# This script helps diagnose why Appium may not be finding its drivers

# Set default APPIUM_HOME and INSTALL_FOLDER if not provided
INSTALL_FOLDER="$HOME/.local"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --appium_home=*)
        APPIUM_HOME="${1#*=}"
        shift
        ;;
        --install_folder=*)
        INSTALL_FOLDER="${1#*=}"
        shift
        ;;
        *)
        # Default case: if $1 doesn't match a pattern, assume it's APPIUM_HOME
        if [ -z "$APPIUM_HOME" ]; then
            APPIUM_HOME="$1"
        else
            echo "Unknown parameter: $1"
            echo "Usage: $0 [--appium_home=<path>] [--install_folder=<path>]"
            exit 1
        fi
        shift
        ;;
    esac
done

# Try to find appium-home in common locations if not provided
if [ -z "$APPIUM_HOME" ]; then
    if [ -d "$INSTALL_FOLDER/appium-home" ]; then
        APPIUM_HOME="$INSTALL_FOLDER/appium-home"
    elif [ -d "$HOME/.local/appium-home" ]; then
        APPIUM_HOME="$HOME/.local/appium-home"
    elif [ -d "/Users/Shared/tmp/appiumagent/appium-home" ]; then
        APPIUM_HOME="/Users/Shared/tmp/appiumagent/appium-home"
    elif [ -d "$HOME/.appium" ]; then
        APPIUM_HOME="$HOME/.appium"
    else
        echo "Error: APPIUM_HOME not found in common locations"
        echo "Usage: $0 [--appium_home=<path>] [--install_folder=<path>]"
        exit 1
    fi
fi

# Print header
echo "===== Appium Driver Diagnostic Tool ====="
echo "Date: $(date)"
echo "APPIUM_HOME: $APPIUM_HOME"

# Check if APPIUM_HOME exists and is accessible
if [ ! -d "$APPIUM_HOME" ]; then
    echo "ERROR: APPIUM_HOME directory ($APPIUM_HOME) does not exist!"
    exit 1
fi

# Find appium executable
APPIUM_PATH=""
if [ -f "$APPIUM_HOME/node_modules/.bin/appium" ]; then
    APPIUM_PATH="$APPIUM_HOME/node_modules/.bin/appium"
    echo "Found Appium at: $APPIUM_PATH"
elif [ -f "$HOME/.local/bin/run-appium" ]; then
    APPIUM_PATH="$HOME/.local/bin/run-appium"
    echo "Found run-appium wrapper at: $APPIUM_PATH"
elif [ -f "/Users/Shared/tmp/appiuminstaller/bin/run-appium" ]; then
    APPIUM_PATH="/Users/Shared/tmp/appiuminstaller/bin/run-appium"
    echo "Found run-appium wrapper at: $APPIUM_PATH"
elif command -v appium &>/dev/null; then
    APPIUM_PATH=$(command -v appium)
    echo "Found global Appium at: $APPIUM_PATH"
else
    echo "ERROR: Could not find Appium executable!"
    exit 1
fi

# Get Appium version
echo ""
echo "Checking Appium version:"
export APPIUM_HOME="$APPIUM_HOME"
APPIUM_VERSION=$("$APPIUM_PATH" --version 2>/dev/null)
echo "Appium version: $APPIUM_VERSION"
APPIUM_MAJOR_VERSION=$(echo $APPIUM_VERSION | cut -d'.' -f1)
echo "Major version: $APPIUM_MAJOR_VERSION"

# Check node and npm version from the custom installation
echo ""
echo "Node.js environment:"

# Try to find NVM installation
if [ -d "$APPIUM_HOME/../.nvm" ]; then
    export NVM_DIR="$APPIUM_HOME/../.nvm"
    echo "Found NVM at: $NVM_DIR"
    # Source nvm without printing messages
    if [ -s "$NVM_DIR/nvm.sh" ]; then
        . "$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1
        echo "Sourced NVM script"
        
        # Try to use the default Node.js version
        nvm use default > /dev/null 2>&1
        CUSTOM_NODE_PATH=$(nvm which current 2>/dev/null)
        if [ -n "$CUSTOM_NODE_PATH" ]; then
            echo "Using Node.js from NVM: $CUSTOM_NODE_PATH"
            echo "Node version: $($CUSTOM_NODE_PATH --version 2>/dev/null || echo 'Not found')"
            
            # Get npm from same location
            CUSTOM_NPM_PATH="$(dirname "$CUSTOM_NODE_PATH")/npm"
            if [ -x "$CUSTOM_NPM_PATH" ]; then
                echo "Using npm from NVM: $CUSTOM_NPM_PATH"
                echo "npm version: $($CUSTOM_NPM_PATH --version 2>/dev/null || echo 'Not found')"
            else
                echo "npm not found in expected location: $CUSTOM_NPM_PATH"
                echo "Falling back to: $(npm --version 2>/dev/null || echo 'Not found')"
            fi
        else
            echo "Failed to get Node.js path from NVM"
            echo "Falling back to: $(node --version 2>/dev/null || echo 'Not found')"
            echo "npm version: $(npm --version 2>/dev/null || echo 'Not found')"
        fi
    else
        echo "NVM script not found at $NVM_DIR/nvm.sh"
        echo "Node version: $(node --version 2>/dev/null || echo 'Not found')"
        echo "npm version: $(npm --version 2>/dev/null || echo 'Not found')"
    fi
else
    echo "NVM directory not found"
    echo "Node version: $(node --version 2>/dev/null || echo 'Not found')"
    echo "npm version: $(npm --version 2>/dev/null || echo 'Not found')"
fi

# Check file structure of APPIUM_HOME
echo ""
echo "Appium directory structure:"
if [ -d "$APPIUM_HOME/node_modules/appium" ]; then
    echo "✓ $APPIUM_HOME/node_modules/appium exists"
else
    echo "✗ $APPIUM_HOME/node_modules/appium does NOT exist"
fi

if [ -d "$APPIUM_HOME/node_modules/appium-xcuitest-driver" ]; then
    echo "✓ $APPIUM_HOME/node_modules/appium-xcuitest-driver exists"
    ls -la "$APPIUM_HOME/node_modules/appium-xcuitest-driver" | head -5
else
    echo "✗ $APPIUM_HOME/node_modules/appium-xcuitest-driver does NOT exist"
fi

if [ -d "$APPIUM_HOME/node_modules/.cache/appium/extensions" ]; then
    echo "✓ $APPIUM_HOME/node_modules/.cache/appium/extensions exists"
    ls -la "$APPIUM_HOME/node_modules/.cache/appium/extensions" 2>/dev/null
else
    echo "✗ $APPIUM_HOME/node_modules/.cache/appium/extensions does NOT exist"
    echo "  Creating directory..."
    mkdir -p "$APPIUM_HOME/node_modules/.cache/appium/extensions" 2>/dev/null
    chmod -R 755 "$APPIUM_HOME/node_modules/.cache" 2>/dev/null
fi

# Check appium configuration files
echo ""
echo "Appium configuration files:"
if [ -f "$HOME/.appiumrc.json" ]; then
    echo "✓ $HOME/.appiumrc.json exists:"
    cat "$HOME/.appiumrc.json"
else
    echo "✗ $HOME/.appiumrc.json does NOT exist"
    
    # Create appropriate config file
    if [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        echo "  Creating Appium 3.x style config..."
        echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$HOME/.appiumrc.json"
    else
        echo "  Creating Appium 2.x style config..."
        echo "{\"appium\":{\"home\":\"$APPIUM_HOME\"}}" > "$HOME/.appiumrc.json"
    fi
    echo "  Created: $(cat "$HOME/.appiumrc.json")"
fi

if [ -f "$APPIUM_HOME/.appiumrc.json" ]; then
    echo "✓ $APPIUM_HOME/.appiumrc.json exists:"
    cat "$APPIUM_HOME/.appiumrc.json"
else
    echo "✗ $APPIUM_HOME/.appiumrc.json does NOT exist"
    
    # Create appropriate config file
    if [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        echo "  Creating Appium 3.x style config..."
        echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$APPIUM_HOME/.appiumrc.json"
    else
        echo "  Creating Appium 2.x style config..."
        echo "{\"appium\":{\"home\":\"$APPIUM_HOME\"}}" > "$APPIUM_HOME/.appiumrc.json"
    fi
    echo "  Created: $(cat "$APPIUM_HOME/.appiumrc.json")"
fi

# Fix permissions
echo ""
echo "Fixing permissions on Appium directories:"
chmod -R 755 "$APPIUM_HOME/node_modules/.bin" 2>/dev/null && echo "✓ Fixed permissions on bin directory" || echo "✗ Failed to fix permissions on bin directory"
chmod -R 755 "$APPIUM_HOME/node_modules/.cache" 2>/dev/null && echo "✓ Fixed permissions on cache directory" || echo "✗ Failed to fix permissions on cache directory"
chmod -R 755 "$APPIUM_HOME/node_modules/appium-xcuitest-driver" 2>/dev/null && echo "✓ Fixed permissions on xcuitest driver directory" || echo "✗ Failed to fix permissions on xcuitest driver directory"

# Try to run driver list command with explicit environment
echo ""
echo "Attempting to list drivers with explicit environment settings:"
echo "Command: APPIUM_HOME=\"$APPIUM_HOME\" \"$APPIUM_PATH\" driver list"

# For Appium 3.x, use different command format
if [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
    DRIVER_LIST=$(APPIUM_HOME="$APPIUM_HOME" "$APPIUM_PATH" driver list 2>&1)
else
    DRIVER_LIST=$(APPIUM_HOME="$APPIUM_HOME" "$APPIUM_PATH" driver list 2>&1)
fi

echo "$DRIVER_LIST"

# Try with --installed flag
echo ""
echo "Attempting to list installed drivers:"
echo "Command: APPIUM_HOME=\"$APPIUM_HOME\" \"$APPIUM_PATH\" driver list --installed"

# For Appium 3.x, use different command format
if [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
    INSTALLED_DRIVERS=$(APPIUM_HOME="$APPIUM_HOME" "$APPIUM_PATH" driver list --installed 2>&1)
else
    INSTALLED_DRIVERS=$(APPIUM_HOME="$APPIUM_HOME" "$APPIUM_PATH" driver list --installed 2>&1)
fi

echo "$INSTALLED_DRIVERS"

# Print suggestions
echo ""
echo "===== Diagnostic Summary ====="
if echo "$INSTALLED_DRIVERS" | grep -q "xcuitest"; then
    echo "✅ XCUITest driver is properly installed and detected"
else
    echo "⚠️ XCUITest driver is NOT being detected. Try these solutions:"
    echo "  1. Run: npm install appium-xcuitest-driver --prefix \"$APPIUM_HOME\" --legacy-peer-deps"
    echo "  2. Create explicit config files:"
    echo "     echo '{\"appium_home\":\"$APPIUM_HOME\"}' > \"$HOME/.appiumrc.json\""
    echo "     echo '{\"appium_home\":\"$APPIUM_HOME\"}' > \"$APPIUM_HOME/.appiumrc.json\""
    echo "  3. Reinstall Appium using the InstallDependencies.sh script"
    echo "     ./InstallDependencies.sh --install_folder=\"$APPIUM_HOME\" --appium_version=3.0.2 --xcuitest_version=10.1.0"
fi

echo ""
echo "For Appium 3.x, remember to use the 'server' command:"
echo "  $APPIUM_PATH server -p 4723"
echo ""
