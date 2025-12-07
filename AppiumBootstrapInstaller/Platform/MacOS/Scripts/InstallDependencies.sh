#!/bin/bash

# Exit on error - critical failures will stop execution
set -e

# Error handling function
handle_error() {
    local exit_code=$?
    local line_number=$1
    echo ""
    echo "================================================================"
    echo "                   CRITICAL FAILURE DETECTED                    "
    echo "================================================================"
    echo "Error occurred in InstallDependencies.sh at line ${line_number}"
    echo "Exit code: ${exit_code}"
    echo ""
    echo "EXECUTION STOPPED - Please review the error above and fix it"
    echo "before proceeding."
    echo "================================================================"
    exit ${exit_code}
}

# Trap errors and call error handler
trap 'handle_error ${LINENO}' ERR

# Architecture and Homebrew detection (inspired by SupervisorSetup.sh)
ARCH="$(uname -m)"
HOMEBREW_PATH=""
HOMEBREW_PREFIX=""

if [[ "$ARCH" == "arm64" ]]; then
    if [[ "$(sysctl -n sysctl.proc_translated 2>/dev/null)" == "1" ]]; then
        echo "Detected: Running under Rosetta translation on Apple Silicon"
        HOMEBREW_PATH="/usr/local/bin"
        HOMEBREW_PREFIX="/usr/local"
    else
        echo "Detect# Check if the driver is recognized by Appium
    echo "Checking if Appium recognizes the XCUITest driver..."
    
    # Ensure driver directory has correct permissions
    chmod -R 755 "$APPIUM_HOME/node_modules" 2>/dev/null || true
    
    # Create cache directory and ensure it has correct permissions
    mkdir -p "$APPIUM_HOME/node_modules/.cache/appium/extensions" 2>/dev/null || true
    chmod -R 755 "$APPIUM_HOME/node_modules/.cache" 2>/dev/null || true
    
    # For Appium 3.x, we need to handle the command differently
    if [[ "$APPIUM_VERSION" == 3* ]]; then
        echo "Using Appium 3.x command format"
        
        # Create explicit Appium 3.x config with home path
        echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$APPIUM_HOME/.appiumrc.json"
        echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$HOME/.appiumrc.json"
        
        echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list"
        DRIVER_LIST=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list)
        echo "$DRIVER_LIST"
        
        # Now check installed drivers
        echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list --installed"
        INSTALLED_DRIVERS=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed)
        echo "$INSTALLED_DRIVERS"
    else
        echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list"
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list || echo "Driver list command failed, but continuing verification"
        
        echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list --installed"
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed || echo "Driver list --installed command failed, but continuing verification"
    fiatively on Apple Silicon"
        HOMEBREW_PATH="/opt/homebrew/bin"
        HOMEBREW_PREFIX="/opt/homebrew"
    fi
else
    echo "Detected: Running on Intel Mac"
    HOMEBREW_PATH="/usr/local/bin"
    HOMEBREW_PREFIX="/usr/local"
fi

if [[ ! -d "$HOMEBREW_PATH" ]]; then
    if [[ -d "/opt/homebrew/bin" ]]; then
        HOMEBREW_PATH="/opt/homebrew/bin"
        HOMEBREW_PREFIX="/opt/homebrew"
    elif [[ -d "/usr/local/bin" ]]; then
        HOMEBREW_PATH="/usr/local/bin"
        HOMEBREW_PREFIX="/usr/local"
    fi
fi

if [[ -d "$HOMEBREW_PATH" && ":$PATH:" != *":$HOMEBREW_PATH:"* ]]; then
    export PATH="$HOMEBREW_PATH:$PATH"
fi

BREW_CMD="brew"
if [[ "$ARCH" == "arm64" && "$HOMEBREW_PREFIX" == "/opt/homebrew" ]]; then
    BREW_CMD="arch -arm64 brew"
fi

# Default parameter values
NODE_VERSION="22"
APPIUM_VERSION="2.17.1"
# Empty version means install latest available
XCUITEST_VERSION=""
UIAUTOMATOR2_VERSION=""
INSTALL_FOLDER="$HOME/.local"
NVM_VERSION="0.40.2"
LIBIMOBILEDEVICE_VERSION=""  # Empty means latest version
INSTALL_DEVICE_FARM="true"  # Default to true
DEVICEFARM_VERSION="8.3.5"  # Compatible with Appium 2.x
DEVICEFARM_DASHBOARD_VERSION="2.0.3"  # Compatible with Appium 2.x

# Installation toggles: control whether drivers are installed at all
INSTALL_XCUITEST="true"
INSTALL_UIAUTOMATOR="true"

# Parse key-value pair arguments
parse_arguments() {
    for arg in "$@"; do
        case $arg in
            --node_version=*) NODE_VERSION="${arg#*=}" ;;
            --appium_version=*) APPIUM_VERSION="${arg#*=}" ;;
            --xcuitest_version=*) XCUITEST_VERSION="${arg#*=}" ;;
            --uiautomator2_version=*) UIAUTOMATOR2_VERSION="${arg#*=}" ;;
            --install_folder=*) INSTALL_FOLDER="${arg#*=}" ;;
            --nvm_version=*) NVM_VERSION="${arg#*=}" ;;
            --libimobiledevice_version=*) LIBIMOBILEDEVICE_VERSION="${arg#*=}" ;;
            --install_device_farm=*) INSTALL_DEVICE_FARM="${arg#*=}" ;;
            --devicefarm_version=*) DEVICEFARM_VERSION="${arg#*=}" ;;
            --devicefarm_dashboard_version=*) DEVICEFARM_DASHBOARD_VERSION="${arg#*=}" ;;
            --install_xcuitest=*) INSTALL_XCUITEST="${arg#*=}" ;;
            --install_uiautomator=*) INSTALL_UIAUTOMATOR="${arg#*=}" ;;
            *)
                echo "Unknown argument: $arg"
                echo "Usage: $0 [--node_version=<value>] [--appium_version=<value>] [--xcuitest_version=<value>] [--uiautomator2_version=<value>] [--install_folder=<value>] [--nvm_version=<value>] [--libimobiledevice_version=<value>] [--install_device_farm=<true|false>] [--devicefarm_version=<value>] [--devicefarm_dashboard_version=<value>] [--install_xcuitest=<true|false>] [--install_uiautomator=<true|false>]"
                exit 1
                ;;
        esac
    done
    
    # Default to appiumagent folder for standard installations
    if [ "$INSTALL_FOLDER" = "$HOME/.local" ]; then
        # Check if we should be using the appiumagent path
        if [[ -d "/Users/Shared/tmp" ]]; then
            INSTALL_FOLDER="/Users/Shared/tmp/appiumagent"
            echo "Setting installation folder to standard location: $INSTALL_FOLDER"
        fi
    fi
    
    # Ensure the install folder exists
    mkdir -p "$INSTALL_FOLDER"
}

# Check the success of the last command and display formatted message
check_success() {
    local status=$?
    local message=${1:-"operation"}
    local message_upper=$(echo "$message" | tr '[:lower:]' '[:upper:]')
    
    if [ $status -ne 0 ]; then
        echo "================================================================"
        echo "                 ${message_upper} FAILED                            "
        echo "================================================================"
        exit 1
    else
        echo "================================================================"
        echo "             ${message_upper} COMPLETED SUCCESSFULLY                "
        echo "================================================================"
    fi
}

# A version of check_success that doesn't exit on failure
# Used for non-critical components that can fail without stopping the entire script
check_success_noexit() {
    local status=$?
    local message=${1:-"operation"}
    local message_upper=$(echo "$message" | tr '[:lower:]' '[:upper:]')
    
    if [ $status -ne 0 ]; then
        echo "================================================================"
        echo "                 ${message_upper} FAILED                            "
        echo "         Continuing with installation process                      "
        echo "================================================================"
        return 1
    else
        echo "================================================================"
        echo "             ${message_upper} COMPLETED SUCCESSFULLY                "
        echo "================================================================"
        return 0
    fi
}

# Ensure nvm is installed locally
setup_nvm() {
    echo "================================================================"
    echo "               STARTING NVM INSTALLATION                        "
    echo "================================================================"
    
    # Instead of deleting the entire folder, just ensure important subdirectories are available
    # and clear only specific directories that need to be recreated
    if [ -d "$INSTALL_FOLDER" ]; then
        echo "INSTALL_FOLDER $INSTALL_FOLDER exists, cleaning specific subdirectories..."
        
        # Just create directories if they don't exist, don't remove
        mkdir -p "$INSTALL_FOLDER"
    fi

    export NVM_DIR="$INSTALL_FOLDER/.nvm"
    if [ ! -d "$NVM_DIR" ] || [ ! -f "$NVM_DIR/nvm.sh" ]; then
        echo "Installing nvm version $NVM_VERSION locally in $NVM_DIR..."
        # If directory exists but is incomplete, remove it first
        if [ -d "$NVM_DIR" ]; then
            echo "Found incomplete nvm installation, removing it first..."
            rm -rf "$NVM_DIR" || { 
                echo "Warning: Could not remove $NVM_DIR. Will attempt to continue anyway."
                chmod -R u+w "$NVM_DIR" 2>/dev/null 
            }
        fi
        
        mkdir -p "$NVM_DIR" || { 
            echo "Error: Could not create NVM_DIR at $NVM_DIR"
            # Try an alternative location if the first one fails
            export NVM_DIR="$HOME/.nvm"
            mkdir -p "$NVM_DIR" || { echo "Error: Could not create NVM_DIR in home directory either."; exit 1; }
            echo "Using alternative NVM_DIR: $NVM_DIR"
        }
        
        echo "Downloading nvm installation script..."
        curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v$NVM_VERSION/install.sh | bash || { 
            echo "Error: Failed to download/install nvm installation script."; 
            exit 1; 
        }
        check_success "nvm installation"
    else
        echo "nvm is already installed in $NVM_DIR."
        local date_time=$(date '+%Y-%m-%d %H:%M:%S')
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"             NVM ALREADY INSTALLED SUCCESSFULLY                 \""
        echo "[${date_time} INF] \"================================================================\""
    fi

    if [ -s "$NVM_DIR/nvm.sh" ]; then
        echo "Loading nvm from $NVM_DIR/nvm.sh"
        \. "$NVM_DIR/nvm.sh"
    else
        echo "Error: nvm.sh not found in $NVM_DIR. Attempting to fix installation..."
        # Try to reinstall nvm
        rm -rf "$NVM_DIR" 2>/dev/null
        mkdir -p "$NVM_DIR"
        curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v$NVM_VERSION/install.sh | bash
        
        if [ -s "$NVM_DIR/nvm.sh" ]; then
            echo "Successfully reinstalled nvm."
            \. "$NVM_DIR/nvm.sh"
        else
            echo "Error: Could not install nvm correctly. Please install nvm manually."
            exit 1
        fi
    fi

    if [ -s "$NVM_DIR/bash_completion" ]; then
        \. "$NVM_DIR/bash_completion"
    fi

    # Remove incompatible settings from .npmrc
    if [ -f "$HOME/.npmrc" ]; then
        echo "Removing incompatible settings from $HOME/.npmrc..."
        sed -i.bak '/^prefix=/d' "$HOME/.npmrc"
        sed -i.bak '/^globalconfig=/d' "$HOME/.npmrc"
    fi

    # Check if a global Node.js version is available
    global_node_version=$(node --version 2>/dev/null | sed 's/v//')
    if [ -n "$global_node_version" ]; then
        echo "Global Node.js version $global_node_version detected. Adding it to nvm..."
        nvm alias system "$global_node_version"
        nvm use system || { echo "Error: Failed to use global Node.js version $global_node_version."; exit 1; }
    fi

    command -v nvm &> /dev/null
    check_success "nvm environment setup"
}

# Install and use the specified Node.js version
setup_node() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"               STARTING NODE.JS INSTALLATION                    \""
    echo "[${date_time} INF] \"================================================================\""
    
    echo "Checking existing Node.js version..."
    existing_node_version=$(node --version 2>/dev/null | sed 's/v//')
    
    if [ "$existing_node_version" == "$NODE_VERSION" ]; then
        echo "Node.js version $NODE_VERSION is already installed. Setting nvm to use it..."
        export NVM_DIR="$INSTALL_FOLDER/.nvm"
        \. "$NVM_DIR/nvm.sh"
        nvm use --delete-prefix "$NODE_VERSION" --silent || { echo "Error: Failed to use Node.js version $NODE_VERSION."; exit 1; }
        
        local date_time=$(date '+%Y-%m-%d %H:%M:%S')
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"             NODE.JS ALREADY INSTALLED SUCCESSFULLY             \""
        echo "[${date_time} INF] \"================================================================\""
    else
        echo "Installing and setting up Node.js version $NODE_VERSION using nvm in local folder..."
        export NVM_DIR="$INSTALL_FOLDER/.nvm"
        \. "$NVM_DIR/nvm.sh"

        # Ensure the specified Node.js version exists
        if ! nvm ls-remote | grep -q "v$NODE_VERSION"; then
            echo "Error: Node.js version $NODE_VERSION not found. Please check available versions using 'nvm ls-remote'."
            exit 1
        fi

        nvm install "$NODE_VERSION" || { echo "Error: Failed to install Node.js version $NODE_VERSION."; exit 1; }
        nvm use --delete-prefix "$NODE_VERSION" --silent || { echo "Error: Failed to use Node.js version $NODE_VERSION."; exit 1; }
        nvm alias default "$NODE_VERSION" || { echo "Error: Failed to set default Node.js version."; exit 1; }
        nvm use default || { echo "Error: Failed to use default Node.js version."; exit 1; }
        check_success "Node.js $NODE_VERSION setup"
    fi

    # Remove npm-global configuration
    npm config delete prefix || { echo "Error: Failed to remove npm prefix configuration."; exit 1; }
    check_success "npm configuration reset"

    # Get Node.js binary path
    NODE_BIN_PATH=$(nvm which "$NODE_VERSION")
    echo "Node.js binary path: $NODE_BIN_PATH"
}

# Install Appium freshly every time
install_appium() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"               STARTING APPIUM INSTALLATION                     \""
    echo "[${date_time} INF] \"================================================================\""
    
    # Clean up global Appium plugin and cache directories to ensure a fresh start
    echo "Cleaning up any global Appium plugin registrations..."
    rm -rf "$HOME/.appium" 2>/dev/null || true
    rm -rf "$HOME/.cache/appium" 2>/dev/null || true
    rm -rf "$HOME/.appiumrc.json" 2>/dev/null || true
    
    export NPM_CONFIG_PREFIX="$INSTALL_FOLDER/npm-global"
    export PATH="$NPM_CONFIG_PREFIX/bin:$PATH"
    mkdir -p "$NPM_CONFIG_PREFIX"

    # Set APPIUM_HOME explicitly before any npm operations
    export APPIUM_HOME="$INSTALL_FOLDER/appium-home"
    
    # Create directory if it doesn't exist, but don't fail if it does
    if [ ! -d "$APPIUM_HOME" ]; then
        mkdir -p "$APPIUM_HOME"
        echo "Created APPIUM_HOME directory at $APPIUM_HOME"
    else
        echo "APPIUM_HOME directory already exists at $APPIUM_HOME"
        # Make sure it's writable
        chmod -R u+w "$APPIUM_HOME" 2>/dev/null || echo "Warning: Could not change permissions on $APPIUM_HOME"
    fi
    
    # Create additional directories that might be needed
    mkdir -p "$INSTALL_FOLDER/bin"
    echo "Created bin directory at $INSTALL_FOLDER/bin"
    
    # Create appium-home in /Users/Shared/tmp/appiumagent if that's not our current path
    # This helps ensure compatibility with the SupervisorManagerMac.cs expectations
    if [[ "$INSTALL_FOLDER" != "/Users/Shared/tmp/appiumagent" ]] && [[ ! -d "/Users/Shared/tmp/appiumagent/appium-home" ]]; then
        mkdir -p "/Users/Shared/tmp/appiumagent/appium-home"
        echo "Created standard appium-home directory at /Users/Shared/tmp/appiumagent/appium-home"
        # Create a symlink from our actual APPIUM_HOME to the standard location
        ln -sf "$APPIUM_HOME/"* "/Users/Shared/tmp/appiumagent/appium-home/" 2>/dev/null || echo "Warning: Could not create symlinks to standard location"
    fi

    echo "Installing Appium $APPIUM_VERSION freshly in $APPIUM_HOME using npm from Node.js installation..."
    NODE_BIN_PATH=$(nvm which "$NODE_VERSION")
    NPM_BIN_PATH=$(dirname "$NODE_BIN_PATH")/npm  # Explicitly locate npm binary
    
    echo "Node.js binary path: $NODE_BIN_PATH"
    echo "NPM binary path: $NPM_BIN_PATH"
    echo "Node.js version: $(node --version)"
    echo "NPM version: $("$NPM_BIN_PATH" --version)"

    # Create a package.json file in APPIUM_HOME
    (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" init -y)
    
    # For Appium 2.x, modify package.json to ensure no devDependencies that might cause conflicts
    if [[ "$APPIUM_VERSION" != 3* ]]; then
        echo "Setting up package.json for Appium 2.x compatibility..."
        cd "$APPIUM_HOME"
        if command -v jq &>/dev/null; then
            # Use jq to remove any existing devDependencies
            jq 'del(.devDependencies)' package.json > package.json.tmp && mv package.json.tmp package.json
            echo "Successfully cleaned package.json for Appium 2.x compatibility"
        else
            # Backup first
            cp package.json package.json.bak
            # If jq is not available, create a clean package.json with minimal content
            cat > package.json << EOF
{
  "name": "appium-home",
  "version": "1.0.0",
  "description": "Appium Home Directory",
  "main": "index.js",
  "dependencies": {}
}
EOF
            echo "Created clean package.json for Appium 2.x compatibility"
        fi
    fi
    
    # Pre-install required dependencies to avoid missing module errors
    echo "Installing required dependencies for Appium..."
    "$NPM_BIN_PATH" install bluebird --prefix "$APPIUM_HOME" --legacy-peer-deps
    check_success "bluebird dependency installation"

    # Install Appium with explicit APPIUM_HOME set
    echo "Setting APPIUM_HOME to $APPIUM_HOME for global Appium reference"
    echo "Running: $NPM_BIN_PATH install appium@$APPIUM_VERSION --prefix $APPIUM_HOME --legacy-peer-deps"
    "$NPM_BIN_PATH" install appium@$APPIUM_VERSION --prefix "$APPIUM_HOME" --legacy-peer-deps
    install_status=$?
    if [ $install_status -ne 0 ]; then
        echo "Warning: npm install returned exit code $install_status"
        echo "Trying again with increased network timeout..."
        "$NPM_BIN_PATH" install appium@$APPIUM_VERSION --prefix "$APPIUM_HOME" --legacy-peer-deps --network-timeout 100000
        install_status=$?
    fi
    
    if [ $install_status -eq 0 ]; then
        echo "Appium installation completed successfully"
        check_success "Appium $APPIUM_VERSION"
    else
        echo "Appium installation failed with exit code $install_status"
        exit $install_status
    fi
    
    # Make sure default Appium home directory exists
    mkdir -p "$HOME/.appium"
    
    # Get Appium version to determine correct configuration format
    INSTALLED_APPIUM_VERSION=$("$APPIUM_HOME/node_modules/.bin/appium" --version 2>/dev/null || echo "unknown")
    echo "Installed Appium version: $INSTALLED_APPIUM_VERSION"
    
    # Determine major version
    APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
    echo "Detected Appium major version: $APPIUM_MAJOR_VERSION"
    
    # Configure Appium home based on version
    if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        echo "Configuring for Appium 3.x..."
        
        # Remove any existing .appiumrc.json that might conflict
        if [ -f "$HOME/.appiumrc.json" ]; then
            rm "$HOME/.appiumrc.json"
        fi
        
        # Create proper Appium 3.x config (uses extensionPaths instead of appium_home)
        echo "Creating Appium 3.x configuration files..."
        mkdir -p "$HOME/.appium" "$APPIUM_HOME/.appium/plugins" "$APPIUM_HOME/.appium/drivers"
        chmod -R 755 "$HOME/.appium" "$APPIUM_HOME/.appium" 2>/dev/null || true
        
        # For Appium 3.x: extensionPaths.base is the ONLY required field
        # Do NOT add server.use-plugins or server.plugin-cache as they are not valid in 3.x config
        echo "{\"extensionPaths\": {\"base\": \"$APPIUM_HOME\"}}" > "$HOME/.appium/config.json"
        echo "{\"extensionPaths\": {\"base\": \"$APPIUM_HOME\"}}" > "$APPIUM_HOME/.appiumrc.json"
        echo "Created Appium 3.x config files with extensionPaths.base: $APPIUM_HOME"
        
        # Remove any old 2.x format config that might conflict
        rm -f "$APPIUM_HOME/.appiumrc.json.old" 2>/dev/null || true
    else
        # Legacy Appium 2.x configuration
        echo "Configuring for Appium 2.x..."
        echo "appium_home=$APPIUM_HOME" > "$APPIUM_HOME/.npmrc"
        
        # Create symbolic link or copy the extensions.yaml file if needed
        if [ -f "$HOME/.appium/node_modules/.cache/appium/extensions.yaml" ]; then
            mkdir -p "$APPIUM_HOME/node_modules/.cache/appium"
            cp "$HOME/.appium/node_modules/.cache/appium/extensions.yaml" "$APPIUM_HOME/node_modules/.cache/appium/" 2>/dev/null || true
        fi
    fi
}

# Install XCUITest driver locally
install_xcuitest_driver() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    
    # Check if XCUITest installation is enabled
    if [ "$INSTALL_XCUITEST" != "true" ]; then
        echo "[${date_time} INF] \"Skipping XCUITest driver installation (disabled via INSTALL_XCUITEST toggle)\""
        return 0
    fi
    
    # Determine version string - empty means latest
    local version_spec=""
    if [ -n "$XCUITEST_VERSION" ]; then
        version_spec="@$XCUITEST_VERSION"
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"      STARTING XCUITEST DRIVER $XCUITEST_VERSION INSTALLATION      \""
        echo "[${date_time} INF] \"================================================================\""
    else
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"      STARTING XCUITEST DRIVER (LATEST) INSTALLATION            \""
        echo "[${date_time} INF] \"================================================================\""
    fi
    
    local appium_path="$APPIUM_HOME/node_modules/.bin/appium"
    if [ -x "$appium_path" ]; then
        export PATH="$(dirname "$appium_path"):$PATH"

        echo "Installing XCUITest driver version $XCUITEST_VERSION using Appium CLI from APPIUM_HOME..."
        
        # Create important environment files in multiple locations to ensure Appium finds the right location
        echo "appium_home=$APPIUM_HOME" > "$HOME/.npmrc"
        echo "appium_home=$APPIUM_HOME" > "$APPIUM_HOME/.npmrc"
        
        # Create configuration files compatible with both Appium 2.x and 3.x
        if [[ "$APPIUM_VERSION" == 3* ]]; then
            # Appium 3.x configuration format
            echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$HOME/.appiumrc.json"
            echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$APPIUM_HOME/.appiumrc.json"
        else
            # Appium 2.x should use environment variables and NOT use .appiumrc.json
            # The presence of this file with the wrong format causes failures
            rm -f "$HOME/.appiumrc.json" 2>/dev/null || true
            rm -f "$APPIUM_HOME/.appiumrc.json" 2>/dev/null || true
        fi
        
        # Make sure HOME/.appium exists and points to APPIUM_HOME
        mkdir -p "$HOME/.appium"
        if [ -d "$HOME/.appium" ] && [ ! -L "$HOME/.appium" ]; then
            # If .appium exists but is not a symlink, back it up and replace it
            mv "$HOME/.appium" "$HOME/.appium.bak.$(date +%s)" 2>/dev/null || true
        fi
        
        # Ensure permissions are set correctly
        chmod -R 755 "$APPIUM_HOME"
        
        # Clean up any existing extensions cache
        echo "Clearing Appium extensions cache to ensure fresh installation..."
        rm -f "$APPIUM_HOME/node_modules/.cache/appium/extensions.yaml" 2>/dev/null || true
        mkdir -p "$APPIUM_HOME/node_modules/.cache/appium" 2>/dev/null || true
        chmod -R 755 "$APPIUM_HOME/node_modules/.cache" 2>/dev/null || true
        
        # First uninstall any existing XCUITest driver to ensure clean installation
        echo "Uninstalling any existing XCUITest driver..."
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver uninstall xcuitest 2>/dev/null || true
        
        # Install the XCUITest driver with explicit home directory setting
        echo "Installing XCUITest driver in $APPIUM_HOME..."
        
        # For Appium 3.x, use npm to install the driver directly
        if [[ "$APPIUM_VERSION" == 3* ]]; then
            echo "Using npm to install xcuitest driver for Appium 3.x"
            if [ -n "$XCUITEST_VERSION" ]; then
                echo "Running: cd $APPIUM_HOME && $NPM_BIN_PATH install --legacy-peer-deps --save appium-xcuitest-driver@$XCUITEST_VERSION"
                (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver@$XCUITEST_VERSION)
            else
                echo "Running: cd $APPIUM_HOME && $NPM_BIN_PATH install --legacy-peer-deps --save appium-xcuitest-driver"
                (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver)
            fi
            local install_status=$?
            
            if [ $install_status -ne 0 ]; then
                echo "Warning: XCUITest driver npm install failed with exit code $install_status, retrying with increased timeout..."
                if [ -n "$XCUITEST_VERSION" ]; then
                    (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save --network-timeout 100000 appium-xcuitest-driver@$XCUITEST_VERSION)
                else
                    (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save --network-timeout 100000 appium-xcuitest-driver)
                fi
                local install_status=$?
            fi
        else
            # For Appium 2.x, use the driver install command
            # Important: The driver name should be "xcuitest" but the package is "appium-xcuitest-driver"
            echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver install xcuitest$version_spec"
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver install xcuitest$version_spec
            local install_status=$?
            
            if [ $install_status -ne 0 ]; then
                echo "Warning: XCUITest driver installation failed with exit code $install_status, trying with --source=npm..."
                env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver install --source=npm xcuitest$version_spec
                install_status=$?
                
                # If that still fails, try installing the npm package directly
                if [ $install_status -ne 0 ]; then
                    echo "Warning: Appium driver install command failed, trying npm install directly..."
                    if [ -n "$version_spec" ]; then
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver$version_spec)
                    else
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver)
                    fi
                    install_status=$?
                fi
            fi
        fi
        
        if [ $install_status -ne 0 ]; then
            echo "Error: XCUITest driver installation command failed with exit code $install_status"
            
            echo "Checking if driver was installed despite error..."
            local driver_dir="$APPIUM_HOME/node_modules/appium-xcuitest-driver"
            if [ -d "$driver_dir" ]; then
                echo "XCUITest driver directory exists despite error. Continuing..."
            else
                echo "Trying alternative installation method..."
                
                # Try using npm directly as a fallback
                if [[ "$APPIUM_VERSION" != 3* ]]; then
                    echo "Installing XCUITest driver via npm as a fallback for Appium 2.x..."
                    if [ -n "$version_spec" ]; then
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver$version_spec)
                    else
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-xcuitest-driver)
                    fi
                    install_status=$?
                    
                    if [ $install_status -ne 0 ]; then
                        echo "Error: All XCUITest driver installation methods failed"
                        exit 1
                    fi
                else
                    echo "Error: All XCUITest driver installation methods failed"
                    exit 1
                fi
            fi
        else
            echo "XCUITest driver installation command completed successfully with exit code 0"
        fi
        
        # Verify the installation
        echo "Verifying XCUITest driver installation..."
        local driver_list_output
        driver_list_output=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed 2>&1)
        echo "$driver_list_output"
        
        if echo "$driver_list_output" | grep -q "xcuitest"; then
            echo "Success: XCUITest driver appears in the list of installed drivers"
        else
            echo "Warning: XCUITest driver not found in driver list, checking file system..."
            # Even if the driver command fails, check if the directory exists
            local driver_dir="$APPIUM_HOME/node_modules/appium-xcuitest-driver"
            if [ -d "$driver_dir" ]; then
                echo "Success: XCUITest driver directory exists at $driver_dir"
            else
                echo "Error: XCUITest driver directory not found at $driver_dir"
                echo "Installation failed. Please check logs for errors."
                exit 1
            fi
        fi
        
        # Create an appium wrapper script in the bin directory
        local bin_dir="$INSTALL_FOLDER/bin"
        mkdir -p "$bin_dir"
        
        cat > "$bin_dir/appium" << EOL
#!/bin/bash
# Setup environment for Appium
export APPIUM_HOME="$APPIUM_HOME"

# Set XDG_CACHE_HOME to keep dashboard cache in install directory
export XDG_CACHE_HOME="$INSTALL_FOLDER/.cache"

# Set up NVM environment
export NVM_DIR="$INSTALL_FOLDER/.nvm"
if [ -s "\$NVM_DIR/nvm.sh" ]; then
    . "\$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1
    nvm use "$NODE_VERSION" > /dev/null 2>&1 || nvm use default > /dev/null 2>&1
fi

# Add Node.js and Appium to PATH
NODE_BIN=\$(nvm which current 2>/dev/null || echo "")
if [ -n "\$NODE_BIN" ]; then
    NODE_DIR=\$(dirname "\$NODE_BIN")
    export PATH="\$NODE_DIR:\$PATH"
fi

export PATH="$INSTALL_FOLDER/bin:$APPIUM_HOME/node_modules/.bin:\$PATH"

"$appium_path" "\$@"
EOL
        chmod +x "$bin_dir/appium"
        
        echo "Created appium wrapper script at $bin_dir/appium"
        
        # Final verification already done above, just confirm success
        local driver_dir="$APPIUM_HOME/node_modules/appium-xcuitest-driver"
        if [ -d "$driver_dir" ]; then
            echo "Success: XCUITest driver version $XCUITEST_VERSION is installed"
            return 0
        else
            echo "Error: XCUITest driver directory does not exist at $driver_dir"
            return 1
        fi
    else
        echo "Error: Appium binary not found at $appium_path. Ensure Appium is installed correctly in APPIUM_HOME."
        exit 1
    fi
}

# Install UiAutomator2 driver for Android
install_uiautomator2_driver() {
    # Check if UiAutomator2 installation is enabled
    if [ "$INSTALL_UIAUTOMATOR" != "true" ]; then
        echo "UiAutomator2 driver installation is disabled. Skipping..."
        return 0
    fi
    
    # Handle empty version string - install latest
    local version_spec=""
    if [ -n "$UIAUTOMATOR2_VERSION" ]; then
        version_spec="@$UIAUTOMATOR2_VERSION"
        echo "Installing UiAutomator2 driver version $UIAUTOMATOR2_VERSION..."
    else
        echo "Installing latest UiAutomator2 driver (no version specified)..."
    fi
    
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"      STARTING UIAUTOMATOR2 DRIVER INSTALLATION      \""
    echo "[${date_time} INF] \"================================================================\""
    
    local appium_path="$APPIUM_HOME/node_modules/.bin/appium"
    if [ -x "$appium_path" ]; then
        export PATH="$(dirname "$appium_path"):$PATH"

        echo "Installing UiAutomator2 driver using Appium CLI from APPIUM_HOME..."
        
        # Create important environment files in multiple locations to ensure Appium finds the right location
        echo "appium_home=$APPIUM_HOME" > "$HOME/.npmrc"
        echo "appium_home=$APPIUM_HOME" > "$APPIUM_HOME/.npmrc"
        
        # Create configuration files compatible with both Appium 2.x and 3.x
        if [[ "$APPIUM_VERSION" == 3* ]]; then
            # Appium 3.x configuration format
            echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$HOME/.appiumrc.json"
            echo "{\"appium_home\":\"$APPIUM_HOME\"}" > "$APPIUM_HOME/.appiumrc.json"
        else
            # Appium 2.x should use environment variables and NOT use .appiumrc.json
            # The presence of this file with the wrong format causes failures
            rm -f "$HOME/.appiumrc.json" 2>/dev/null || true
            rm -f "$APPIUM_HOME/.appiumrc.json" 2>/dev/null || true
        fi
        
        # Make sure HOME/.appium exists and points to APPIUM_HOME
        mkdir -p "$HOME/.appium"
        if [ -d "$HOME/.appium" ] && [ ! -L "$HOME/.appium" ]; then
            # If .appium exists but is not a symlink, back it up and replace it
            mv "$HOME/.appium" "$HOME/.appium.bak.$(date +%s)" 2>/dev/null || true
        fi
        
        # Ensure permissions are set correctly
        chmod -R 755 "$APPIUM_HOME"
        
        # Clean up any existing extensions cache
        echo "Clearing Appium extensions cache to ensure fresh installation..."
        rm -f "$APPIUM_HOME/node_modules/.cache/appium/extensions.yaml" 2>/dev/null || true
        mkdir -p "$APPIUM_HOME/node_modules/.cache/appium" 2>/dev/null || true
        chmod -R 755 "$APPIUM_HOME/node_modules/.cache" 2>/dev/null || true
        
        # First uninstall any existing UiAutomator2 driver to ensure clean installation
        echo "Uninstalling any existing UiAutomator2 driver..."
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver uninstall uiautomator2 2>/dev/null || true
        
        # Install the UiAutomator2 driver with explicit home directory setting
        echo "Installing UiAutomator2 driver in $APPIUM_HOME..."
        
        # For Appium 3.x, use npm to install the driver directly
        if [[ "$APPIUM_VERSION" == 3* ]]; then
            echo "Using npm to install uiautomator2 driver for Appium 3.x"
            if [ -n "$version_spec" ]; then
                echo "Running: cd $APPIUM_HOME && $NPM_BIN_PATH install --legacy-peer-deps --save appium-uiautomator2-driver$version_spec"
                (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver$version_spec)
            else
                echo "Running: cd $APPIUM_HOME && $NPM_BIN_PATH install --legacy-peer-deps --save appium-uiautomator2-driver"
                (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver)
            fi
            local install_status=$?
            
            if [ $install_status -ne 0 ]; then
                echo "Warning: UiAutomator2 driver npm install failed with exit code $install_status, retrying with increased timeout..."
                if [ -n "$version_spec" ]; then
                    (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save --network-timeout 100000 appium-uiautomator2-driver$version_spec)
                else
                    (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save --network-timeout 100000 appium-uiautomator2-driver)
                fi
                local install_status=$?
            fi
        else
            # For Appium 2.x, use the driver install command
            # Important: The driver name should be "uiautomator2" but the package is "appium-uiautomator2-driver"
            echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver install uiautomator2$version_spec"
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver install uiautomator2$version_spec
            local install_status=$?
            
            if [ $install_status -ne 0 ]; then
                echo "Warning: UiAutomator2 driver installation failed with exit code $install_status, trying with --source=npm..."
                env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver install --source=npm uiautomator2$version_spec
                install_status=$?
                
                # If that still fails, try installing the npm package directly
                if [ $install_status -ne 0 ]; then
                    echo "Warning: Appium driver install command failed, trying npm install directly..."
                    if [ -n "$version_spec" ]; then
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver$version_spec)
                    else
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver)
                    fi
                    install_status=$?
                fi
            fi
        fi
        
        if [ $install_status -ne 0 ]; then
            echo "Error: UiAutomator2 driver installation command failed with exit code $install_status"
            
            echo "Checking if driver was installed despite error..."
            local driver_dir="$APPIUM_HOME/node_modules/appium-uiautomator2-driver"
            if [ -d "$driver_dir" ]; then
                echo "UiAutomator2 driver directory exists despite error. Continuing..."
            else
                echo "Trying alternative installation method..."
                
                # Try using npm directly as a fallback
                if [[ "$APPIUM_VERSION" != 3* ]]; then
                    echo "Installing UiAutomator2 driver via npm as a fallback for Appium 2.x..."
                    if [ -n "$version_spec" ]; then
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver$version_spec)
                    else
                        (cd "$APPIUM_HOME" && "$NPM_BIN_PATH" install --legacy-peer-deps --save appium-uiautomator2-driver)
                    fi
                    install_status=$?
                    
                    if [ $install_status -ne 0 ]; then
                        echo "Error: All UiAutomator2 driver installation methods failed"
                        exit 1
                    fi
                else
                    echo "Error: All UiAutomator2 driver installation methods failed"
                    exit 1
                fi
            fi
        else
            echo "UiAutomator2 driver installation command completed successfully with exit code 0"
        fi
        
        # Verify the installation by explicitly checking both the driver command and directory
        echo "Verifying UiAutomator2 driver installation..."
        local driver_list_output
        driver_list_output=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed 2>&1)
        echo "$driver_list_output"
        
        if echo "$driver_list_output" | grep -q "uiautomator2"; then
            echo "Success: UiAutomator2 driver appears in the list of installed drivers"
        else
            echo "Warning: UiAutomator2 driver not found in driver list, checking file system..."
            # Even if the driver command fails, check if the directory exists
            local driver_dir="$APPIUM_HOME/node_modules/appium-uiautomator2-driver"
            if [ -d "$driver_dir" ]; then
                echo "Success: UiAutomator2 driver directory exists at $driver_dir"
            else
                echo "Error: UiAutomator2 driver directory not found at $driver_dir"
                echo "Installation failed. Please check logs for errors."
                exit 1
            fi
        fi
        
        echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list --installed"
        local driver_list_output
        driver_list_output=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed 2>&1)
        echo "$driver_list_output"
        
        # Check specifically for UiAutomator2 in the output
        if echo "$driver_list_output" | grep -q "uiautomator2"; then
            echo "Success: UiAutomator2 driver appears in the list of installed drivers"
        else
            echo "Warning: UiAutomator2 driver not found in the list of installed drivers, but may still be installed correctly"
            
            # Try running the general driver list command for more info
            echo "Running driver list command for more information..."
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list 2>&1 || echo "Driver list command failed, but continuing verification"
        fi
        
        # Check if the driver was successfully installed regardless of the list output
        local driver_dir="$APPIUM_HOME/node_modules/appium-uiautomator2-driver"
        if [ -d "$driver_dir" ]; then
            echo "Success: UiAutomator2 driver directory exists at $driver_dir"
            echo "Success: UiAutomator2 driver version $UIAUTOMATOR2_VERSION is installed"
            return 0
        else
            echo "Error: UiAutomator2 driver directory does not exist at $driver_dir"
            return 1
        fi
    else
        echo "Error: Appium binary not found at $appium_path. Ensure Appium is installed correctly in APPIUM_HOME."
        exit 1
    fi
}

# Install go-ios for iOS real device support (required by device-farm)
# Reference: https://github.com/danielpaulus/go-ios
install_go_ios() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           INSTALLING GO-IOS FOR DEVICE-FARM                    \""
    echo "[${date_time} INF] \"================================================================\""
    
    local goios_dir="$INSTALL_FOLDER/.cache/appium-device-farm/goIOS"
    mkdir -p "$goios_dir"
    
    # Determine OS and architecture
    local os_type=$(uname -s | tr '[:upper:]' '[:lower:]')
    local arch=$(uname -m)
    local goios_url=""
    local zip_file=""
    
    if [ "$os_type" = "darwin" ]; then
        # macOS
        goios_url="https://github.com/danielpaulus/go-ios/releases/download/v1.0.182/go-ios-mac.zip"
        zip_file="go-ios-mac.zip"
    elif [ "$os_type" = "linux" ]; then
        # Linux
        goios_url="https://github.com/danielpaulus/go-ios/releases/download/v1.0.182/go-ios-linux.zip"
        zip_file="go-ios-linux.zip"
    else
        echo "Warning: Unsupported OS type: $os_type. Skipping go-ios installation."
        echo "Device-farm will work for simulators/emulators only."
        return 0
    fi
    
    echo "Downloading go-ios from: $goios_url"
    if curl -L "$goios_url" -o "$goios_dir/$zip_file"; then
        echo "Extracting go-ios..."
        (cd "$goios_dir" && unzip -o "$zip_file" && chmod +x ios && rm "$zip_file")
        if [ -x "$goios_dir/ios" ]; then
            echo "✅ go-ios installed successfully at: $goios_dir/ios"
            # Verify installation
            "$goios_dir/ios" version 2>&1 | head -5 || true
            
            # Set GO_IOS environment variable in the installation folder
            echo "export GO_IOS=\"$goios_dir/ios\"" >> "$INSTALL_FOLDER/.go_ios_env"
            echo "GO_IOS environment variable reference saved to: $INSTALL_FOLDER/.go_ios_env"
            echo "Device-farm will use: GO_IOS=$goios_dir/ios"
        else
            echo "Warning: go-ios binary not found after extraction"
            return 1
        fi
    else
        echo "Warning: Failed to download go-ios. Device-farm will work for simulators/emulators only."
        return 1
    fi
    
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"         GO-IOS INSTALLATION COMPLETED SUCCESSFULLY             \""
    echo "[${date_time} INF] \"================================================================\""
    return 0
}

# Install DeviceFarm plugin for Appium
install_device_farm() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    
    # Check if DeviceFarm installation is enabled
    if [ "$INSTALL_DEVICE_FARM" != "true" ]; then
        echo "[${date_time} INF] \"Skipping DeviceFarm plugin installation (disabled in configuration)\""
        return 0
    fi
    
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"         STARTING DEVICEFARM PLUGIN INSTALLATION               \""
    echo "[${date_time} INF] \"================================================================\""
    
    local appium_path="$APPIUM_HOME/node_modules/.bin/appium"
    if [ -x "$appium_path" ]; then
        export PATH="$(dirname "$appium_path"):$PATH"
        
        # Detect Appium version for compatibility
        local installed_appium_version
        installed_appium_version=$("$appium_path" --version 2>/dev/null || echo "unknown")
        echo "Detected Appium version: $installed_appium_version"
        
        local appium_major_version
        appium_major_version=$(echo "$installed_appium_version" | cut -d'.' -f1)
        
        echo "Installing DeviceFarm plugin using Appium CLI..."
        
        # Uninstall any existing DeviceFarm plugin
        echo "Uninstalling any existing DeviceFarm and dashboard plugins..."
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin uninstall appium-device-farm 2>/dev/null || true
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin uninstall appium-dashboard 2>/dev/null || true
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin uninstall device-farm 2>/dev/null || true
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin uninstall dashboard 2>/dev/null || true
        
        # Install DeviceFarm plugin first with specific version
        local devicefarm_spec="appium-device-farm"
        if [ -n "$DEVICEFARM_VERSION" ]; then
            devicefarm_spec="appium-device-farm@$DEVICEFARM_VERSION"
        fi
        
        # For Appium 3.x, use 'plugin add' instead of 'plugin install'
        if [[ "$installed_appium_version" == 3* ]] || [ "$appium_major_version" = "3" ]; then
            echo "Installing DeviceFarm for Appium 3.x..."
            echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path plugin add --source=npm $devicefarm_spec"
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin add --source=npm "$devicefarm_spec"
        else
            echo "Installing DeviceFarm for Appium 2.x..."
            echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path plugin install --source=npm $devicefarm_spec"
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin install --source=npm "$devicefarm_spec"
        fi
        local install_status=$?
        
        # Install Dashboard plugin (required by DeviceFarm) with specific version
        if [ $install_status -eq 0 ]; then
            # Create cache directory for appium-dashboard plugin in the install directory
            # This keeps everything isolated in INSTALL_FOLDER and avoids permission issues
            local dashboard_cache_dir="$INSTALL_FOLDER/.cache/appium-dashboard-plugin"
            if [ ! -d "$dashboard_cache_dir" ]; then
                echo "Creating dashboard cache directory in install folder: $dashboard_cache_dir"
                mkdir -p "$dashboard_cache_dir"
                chmod 755 "$dashboard_cache_dir" 2>/dev/null || true
            fi
            
            local dashboard_spec="appium-dashboard"
            if [ -n "$DEVICEFARM_DASHBOARD_VERSION" ]; then
                dashboard_spec="appium-dashboard@$DEVICEFARM_DASHBOARD_VERSION"
            fi
            
            # Install dashboard plugin with HOME set to INSTALL_FOLDER
            # This forces Sequelize and all npm postinstall scripts to use INSTALL_FOLDER/.cache
            # instead of the user's home directory, avoiding permission issues entirely
            if [[ "$installed_appium_version" == 3* ]] || [ "$appium_major_version" = "3" ]; then
                echo "Installing Dashboard for Appium 3.x..."
                echo "Running: HOME=$INSTALL_FOLDER APPIUM_HOME=$APPIUM_HOME XDG_CACHE_HOME=$INSTALL_FOLDER/.cache $appium_path plugin add --source=npm $dashboard_spec"
                env HOME="$INSTALL_FOLDER" APPIUM_HOME="$APPIUM_HOME" XDG_CACHE_HOME="$INSTALL_FOLDER/.cache" "$appium_path" plugin add --source=npm "$dashboard_spec"
            else
                echo "Installing Dashboard for Appium 2.x..."
                echo "Running: HOME=$INSTALL_FOLDER APPIUM_HOME=$APPIUM_HOME XDG_CACHE_HOME=$INSTALL_FOLDER/.cache $appium_path plugin install --source=npm $dashboard_spec"
                env HOME="$INSTALL_FOLDER" APPIUM_HOME="$APPIUM_HOME" XDG_CACHE_HOME="$INSTALL_FOLDER/.cache" "$appium_path" plugin install --source=npm "$dashboard_spec"
            fi
            install_status=$?
        fi
        
        if [ $install_status -eq 0 ]; then
            echo "DeviceFarm plugin installation command completed successfully"
            
            # Install go-ios for real device support
            install_go_ios || echo "Warning: go-ios installation failed, but continuing..."
            
            # Verify installation
            echo "Verifying DeviceFarm and Dashboard plugin installation..."
            local plugin_list
            plugin_list=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin list --installed 2>&1)
            echo "$plugin_list"
            
            local device_farm_ok=false
            local dashboard_ok=false
            
            if echo "$plugin_list" | grep -q "device-farm"; then
                echo "Success: DeviceFarm plugin appears in the list of installed plugins"
                device_farm_ok=true
            else
                echo "Debug: device-farm not found in plugin list"
            fi
            
            if echo "$plugin_list" | grep -q "dashboard"; then
                echo "Success: Dashboard plugin appears in the list of installed plugins"
                dashboard_ok=true
            else
                echo "Debug: dashboard not found in plugin list"
            fi
            
            if [ "$device_farm_ok" = true ] && [ "$dashboard_ok" = true ]; then
                echo "[${date_time} INF] \"================================================================\""
                echo "[${date_time} INF] \"      DEVICEFARM PLUGIN INSTALLATION COMPLETED SUCCESSFULLY     \""
                echo "[${date_time} INF] \"================================================================\""
                return 0
            else
                echo "Warning: DeviceFarm or Dashboard plugin not found in installed plugins list"
                return 1
            fi
        else
            echo "Error: DeviceFarm plugin installation failed with exit code $install_status"
            return 1
        fi
    else
        echo "Error: Appium binary not found at $appium_path"
        return 1
    fi
}

# Install Appium Inspector plugin for both Appium 2.x and 3.x
install_appium_inspector() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"         STARTING APPIUM INSPECTOR PLUGIN INSTALLATION          \""
    echo "[${date_time} INF] \"================================================================\""
    
    local appium_path="$APPIUM_HOME/node_modules/.bin/appium"
    if [ -x "$appium_path" ]; then
        export PATH="$(dirname "$appium_path"):$PATH"

        echo "Installing Appium Inspector plugin..."
        
        # Get Appium version to determine correct installation method
        INSTALLED_APPIUM_VERSION=$("$appium_path" --version 2>/dev/null || echo "unknown")
        echo "Detected Appium version: $INSTALLED_APPIUM_VERSION"
        
        # Determine major version
        APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
        
        # Clean up any existing inspector plugins in various locations before installing
        echo "Checking for existing Inspector plugin installations..."
        
        # First check if the plugin is already installed
        local plugin_check_output
        plugin_check_output=$(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin list --installed 2>&1)
        
        if echo "$plugin_check_output" | grep -q "inspector"; then
            echo "Found existing Inspector plugin. Checking if it needs to be updated..."
            local plugin_version
            plugin_version=$(echo "$plugin_check_output" | grep "inspector" | awk '{print $2}')
            echo "Current Inspector plugin version: $plugin_version"
            
            # Try to update the plugin
            echo "Attempting to update Inspector plugin..."
            env APPIUM_HOME="$APPIUM_HOME" "$appium_path" plugin update inspector
            
            # If the update succeeded or failed, consider it done either way
            echo "Inspector plugin is already installed and update attempted. Skipping installation step."
            echo "[${date_time} INF] \"================================================================\""
            echo "[${date_time} INF] \"     APPIUM INSPECTOR PLUGIN UPDATE COMPLETED SUCCESSFULLY \""
            echo "[${date_time} INF] \"================================================================\""
            return 0
        fi
        
        # Clean up potential global plugin locations that might interfere
        echo "Cleaning up potential global plugin locations..."
        rm -rf "$HOME/.appium/node_modules/appium-inspector" 2>/dev/null || true
        rm -rf "$HOME/.appium/plugins/inspector" 2>/dev/null || true
        rm -rf "$HOME/.appium/plugin/inspector" 2>/dev/null || true
        
        # Install Inspector plugin based on Appium version
        if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
            echo "Installing Inspector plugin for Appium 3.x..."
            # Ensure we're using the local APPIUM_HOME and not any global settings
            export NODE_PATH="$APPIUM_HOME/node_modules"
            env APPIUM_HOME="$APPIUM_HOME" NODE_PATH="$NODE_PATH" "$appium_path" plugin install inspector
            local install_status=$?
            
            # Try with --legacy-peer-deps if initial installation fails
            if [ $install_status -ne 0 ]; then
                echo "Inspector plugin installation failed, trying with --legacy-peer-deps..."
                cd "$APPIUM_HOME"
                "$NPM_BIN_PATH" install appium-inspector-plugin --save-dev --legacy-peer-deps
                install_status=$?
            fi
        else
            echo "Installing Inspector plugin for Appium 2.x..."
            export NODE_PATH="$APPIUM_HOME/node_modules"
            
            # For Appium 2.x, try with an older version of the inspector plugin
            echo "Trying with appium-inspector-plugin compatible with Appium 2.x..."
            cd "$APPIUM_HOME"
            "$NPM_BIN_PATH" install appium-inspector-plugin@2025.3.1 --save-dev --legacy-peer-deps
            local install_status=$?
            
            # If that fails, try using the plugin command with --force
            if [ $install_status -ne 0 ]; then
                echo "Direct npm install failed, trying plugin install command with --force..."
                env APPIUM_HOME="$APPIUM_HOME" NODE_PATH="$NODE_PATH" "$appium_path" plugin install --source=npm appium-inspector-plugin@2025.3.1 --force
                install_status=$?
            fi
        fi
        
        # Check installation status but don't exit on failure - Inspector is not critical
        if [ $install_status -ne 0 ]; then
            echo "[${date_time} WARN] \"================================================================\""
            echo "[${date_time} WARN] \"           APPIUM INSPECTOR PLUGIN INSTALLATION FAILED           \""
            echo "[${date_time} WARN] \"     Continuing with installation process - Inspector is optional \""
            echo "[${date_time} WARN] \"================================================================\""
            echo "Warning: Appium Inspector plugin installation failed with exit code $install_status"
            # Continue execution - Inspector is not critical
        else
            echo "[${date_time} INF] \"================================================================\""
            echo "[${date_time} INF] \"     APPIUM INSPECTOR PLUGIN INSTALLATION COMPLETED SUCCESSFULLY \""
            echo "[${date_time} INF] \"================================================================\""
        fi
        
        # Verify installation
        echo "Verifying Appium Inspector plugin installation..."
        if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
            env APPIUM_HOME="$APPIUM_HOME" NODE_PATH="$NODE_PATH" "$appium_path" plugin list --installed
        else
            env APPIUM_HOME="$APPIUM_HOME" NODE_PATH="$NODE_PATH" "$appium_path" plugin list --installed
        fi
    else
        echo "[${date_time} ERR] \"================================================================\""
        echo "[${date_time} ERR] \"           APPIUM INSPECTOR PLUGIN INSTALLATION FAILED           \""
        echo "[${date_time} ERR] \"================================================================\""
        echo "Error: Appium binary not found at $appium_path. Ensure Appium is installed correctly in APPIUM_HOME."
        exit 1
    fi
}

# Install FFmpeg locally
install_ffmpeg() {
    echo "================================================================"
    echo "               STARTING FFMPEG INSTALLATION                     "
    echo "================================================================"
    
    # Try to clean up potentially broken dependencies (ignore errors if they don't exist)
    echo "Cleaning up potentially broken dependencies..."
    brew uninstall librist --ignore-dependencies 2>/dev/null || echo "librist not installed or already removed"
    brew uninstall mbedtls --ignore-dependencies 2>/dev/null || echo "mbedtls not installed or already removed"
    
    # Clean up any broken symlinks in Homebrew
    echo "Cleaning up broken symlinks..."
    brew cleanup 2>/dev/null || true
    
    if brew reinstall ffmpeg@8; then
        echo "================================================================"
        echo "             FFMPEG INSTALLATION COMPLETED SUCCESSFULLY         "
        echo "================================================================"
    else
        echo "================================================================"
        echo "         FFMPEG INSTALLATION FAILED - CONTINUING ANYWAY          "
        echo "     (FFmpeg is optional for core functionality)                 "
        echo "================================================================"
        # Don't exit - ffmpeg is not critical for core device management
        return 0
    fi
}



# Install libimobiledevice for iOS device management
install_libimobiledevice() {
    echo "================================================================"
    echo "          STARTING LIBIMOBILEDEVICE INSTALLATION                "
    echo "================================================================"
    
    # Default version - empty string means latest
    local LIBIMOBILEDEVICE_VERSION=${LIBIMOBILEDEVICE_VERSION:-""}
    
    echo "Installing libimobiledevice using Homebrew..."
    
    # Check if a specific version should be installed
    local install_command="install"
    local version_info=""
    if [ -n "$LIBIMOBILEDEVICE_VERSION" ]; then
        echo "Installing specific version: libimobiledevice $LIBIMOBILEDEVICE_VERSION"
        install_command="install"
        version_info="@$LIBIMOBILEDEVICE_VERSION"
    fi
    
    # Check if it's already installed
    if $BREW_CMD list --versions libimobiledevice &>/dev/null; then
        local installed_version=$($BREW_CMD list --versions libimobiledevice | awk '{print $2}')
        echo "libimobiledevice is already installed (version $installed_version)."
        
        # If specific version is requested and different from installed, reinstall
        if [ -n "$LIBIMOBILEDEVICE_VERSION" ] && [ "$installed_version" != "$LIBIMOBILEDEVICE_VERSION" ]; then
            echo "Requested version $LIBIMOBILEDEVICE_VERSION differs from installed $installed_version. Reinstalling..."
            $BREW_CMD uninstall libimobiledevice
        else
            # Already installed with correct version
            echo "Using existing libimobiledevice installation."
            if [[ ":$PATH:" != *":$HOMEBREW_PREFIX/bin:"* ]]; then
                export PATH="$HOMEBREW_PREFIX/bin:$PATH"
            fi
            return 0
        fi
    else
        echo "libimobiledevice is not installed. Installing version$version_info..."
    fi
    
    # Try normal install, but if it fails with Rosetta error, retry with arch -arm64
    if ! $BREW_CMD $install_command libimobiledevice$version_info 2>libimobiledevice_install_err.log; then
        if grep -q "Cannot install under Rosetta 2 in ARM default prefix" libimobiledevice_install_err.log; then
            echo "Homebrew refused to install libimobiledevice under Rosetta. Retrying with arch -arm64..."
            arch -arm64 brew $install_command libimobiledevice$version_info || {
                echo "Error: libimobiledevice installation failed even with arch -arm64."
                return 1
            }
        else
            echo "Error: libimobiledevice installation failed. See libimobiledevice_install_err.log for details."
            cat libimobiledevice_install_err.log
            return 1
        fi
    fi
    
    check_success "libimobiledevice installation"
    
    # Ensure Homebrew's bin is in PATH for idevicediagnostics
    if [[ ":$PATH:" != *":$HOMEBREW_PREFIX/bin:"* ]]; then
        export PATH="$HOMEBREW_PREFIX/bin:$PATH"
    fi
    
    # Verify the installed version
    local final_version=$($BREW_CMD list --versions libimobiledevice | awk '{print $2}')
    echo "Successfully installed libimobiledevice version $final_version"
}

# Verify idevicediagnostics tool is available
verify_idevicediagnostics() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           VERIFYING IDEVICEDIAGNOSTICS INSTALLATION            \""
    echo "[${date_time} INF] \"================================================================\""
    
    echo "Verifying if idevicediagnostics is working..."
    if ! command -v idevicediagnostics &>/dev/null; then
        echo "Error: idevicediagnostics is not available. Please check the libimobiledevice installation."
        echo "PATH: $PATH"
        echo "Trying to locate idevicediagnostics in Homebrew bin..."
        if [[ -x "$HOMEBREW_PREFIX/bin/idevicediagnostics" ]]; then
            echo "Found idevicediagnostics at $HOMEBREW_PREFIX/bin/idevicediagnostics. Adding to PATH."
            export PATH="$HOMEBREW_PREFIX/bin:$PATH"
            echo "[${date_time} INF] \"================================================================\""
            echo "[${date_time} INF] \"       IDEVICEDIAGNOSTICS VERIFICATION COMPLETED SUCCESSFULLY   \""
            echo "[${date_time} INF] \"================================================================\""
        else
            echo "[${date_time} ERR] \"================================================================\""
            echo "[${date_time} ERR] \"         IDEVICEDIAGNOSTICS VERIFICATION FAILED                 \""
            echo "[${date_time} ERR] \"================================================================\""
            echo "idevicediagnostics not found in $HOMEBREW_PREFIX/bin."
            exit 1
        fi
    else
        echo "idevicediagnostics is available: $(command -v idevicediagnostics)"
        idevicediagnostics diagnostics || echo "idevicediagnostics ran, but no device may be connected."
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"       IDEVICEDIAGNOSTICS VERIFICATION COMPLETED SUCCESSFULLY   \""
        echo "[${date_time} INF] \"================================================================\""
    fi
}

# Verify installations
verify_installations() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           STARTING INSTALLATION VERIFICATION                   \""
    echo "[${date_time} INF] \"================================================================\""
    
    echo "Verifying installations..."
    export APPIUM_HOME="$INSTALL_FOLDER/appium-home"
    export PATH="$NPM_CONFIG_PREFIX/bin:$PATH"
    local appium_path="$APPIUM_HOME/node_modules/.bin/appium"
    local bin_dir="$INSTALL_FOLDER/bin"
    
    echo "Environment variables:"
    echo "- APPIUM_HOME: $APPIUM_HOME"
    echo "- NVM_DIR: $NVM_DIR"
    echo "- PATH: $PATH"
    echo "- NPM_CONFIG_PREFIX: $NPM_CONFIG_PREFIX"
    
    # Check Node.js installation
    echo "Node.js installation:"
    if command -v node >/dev/null 2>&1; then
        echo "- Node.js version: $(node -v)"
        echo "- NPM version: $(npm -v)"
    else
        echo "- Warning: Node.js not found in PATH"
    fi
    
    # Check Appium installation
    echo "Appium installation:"
    if [ -x "$appium_path" ]; then
        echo "- Appium binary found at: $appium_path"
        echo "- Appium version: $("$appium_path" --version 2>/dev/null || echo "Could not determine")"
        
        # Check if bin directory contains the wrapper script
        if [ -x "$bin_dir/appium" ]; then
            echo "- Appium wrapper script: $bin_dir/appium (OK)"
        else
            echo "- Warning: Appium wrapper script not found or not executable"
        fi
    else
        echo "- Warning: Appium binary not found at expected path"
    fi

    # Add the custom Appium installation to PATH
    export PATH="$bin_dir:$(dirname "$appium_path"):$PATH"  
    export PATH="$(dirname "$(nvm which "$NODE_VERSION")"):$PATH" 

    # Check Node.js and npm
    echo "Checking Node.js and npm installations..."
    node --version && echo "Node.js version check successful"
    npm --version && echo "npm version check successful"
    
    # Check Appium with correct APPIUM_HOME
    echo "Checking XCUITest driver installation..."
    local xcuitest_driver_path="$APPIUM_HOME/node_modules/appium-xcuitest-driver"
    
    if [ -d "$xcuitest_driver_path" ]; then
        echo "- XCUITest driver directory exists at: $xcuitest_driver_path"
        
        # Try to get the version from package.json
        if [ -f "$xcuitest_driver_path/package.json" ]; then
            local driver_version=$(grep -o '"version": "[^"]*"' "$xcuitest_driver_path/package.json" | cut -d'"' -f4)
            echo "- XCUITest driver version: $driver_version (target: $XCUITEST_VERSION)"
        else
            echo "- XCUITest driver installed, but couldn't determine version"
        fi
        
        # Check if appium can see the driver
        echo "Checking if Appium can see the XCUITest driver..."
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed | grep xcuitest && echo "- XCUITest driver recognized by Appium" || echo "- Warning: XCUITest driver not recognized by Appium"
    else
        echo "- Warning: XCUITest driver directory not found at expected path"
        echo "- Attempted path: $xcuitest_driver_path"
    fi
    
    # Check UiAutomator2 driver installation
    echo "Checking UiAutomator2 driver installation..."
    local uiautomator2_driver_path="$APPIUM_HOME/node_modules/appium-uiautomator2-driver"
    
    if [ -d "$uiautomator2_driver_path" ]; then
        echo "- UiAutomator2 driver directory exists at: $uiautomator2_driver_path"
        
        # Try to get the version from package.json
        if [ -f "$uiautomator2_driver_path/package.json" ]; then
            local driver_version=$(grep -o '"version": "[^"]*"' "$uiautomator2_driver_path/package.json" | cut -d'"' -f4)
            echo "- UiAutomator2 driver version: $driver_version (target: $UIAUTOMATOR2_VERSION)"
        else
            echo "- UiAutomator2 driver installed, but couldn't determine version"
        fi
        
        # Check if appium can see the driver
        echo "Checking if Appium can see the UiAutomator2 driver..."
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list --installed | grep uiautomator2 && echo "- UiAutomator2 driver recognized by Appium" || echo "- Warning: UiAutomator2 driver not recognized by Appium"
    else
        echo "- Warning: UiAutomator2 driver directory not found at expected path"
        echo "- Attempted path: $uiautomator2_driver_path"
    fi
    
    echo "Checking Appium installation with APPIUM_HOME=$APPIUM_HOME..."
    env APPIUM_HOME="$APPIUM_HOME" "$appium_path" --version && echo "Appium version check successful"
    
    # Verify XCUITest driver installation by checking the directory structure
    echo "Verifying XCUITest driver installation..."
    local xcuitest_dir="$APPIUM_HOME/node_modules/appium-xcuitest-driver"
    
    if [ -d "$xcuitest_dir" ]; then
        echo "XCUITest driver directory exists at $xcuitest_dir"
        local package_json="$xcuitest_dir/package.json"
        if [ -f "$package_json" ]; then
            local pkg_version=$(grep -o '"version": "[^"]*"' "$package_json" | cut -d '"' -f 4)
            echo "XCUITest driver version $pkg_version is installed (from package.json)."
        else
            echo "Warning: Could not find package.json in XCUITest driver directory."
        fi
    else
        echo "Warning: XCUITest driver directory not found at $xcuitest_dir"
        echo "Checking npm list for appium-xcuitest-driver..."
        (cd "$APPIUM_HOME" && npm list appium-xcuitest-driver)
    fi
    
    # Verify UiAutomator2 driver installation by checking the directory structure
    echo "Verifying UiAutomator2 driver installation..."
    local uiautomator2_dir="$APPIUM_HOME/node_modules/appium-uiautomator2-driver"
    
    if [ -d "$uiautomator2_dir" ]; then
        echo "UiAutomator2 driver directory exists at $uiautomator2_dir"
        local package_json="$uiautomator2_dir/package.json"
        if [ -f "$package_json" ]; then
            local pkg_version=$(grep -o '"version": "[^"]*"' "$package_json" | cut -d '"' -f 4)
            echo "UiAutomator2 driver version $pkg_version is installed (from package.json)."
        else
            echo "Warning: Could not find package.json in UiAutomator2 driver directory."
        fi
    else
        echo "Warning: UiAutomator2 driver directory not found at $uiautomator2_dir"
        echo "Checking npm list for appium-uiautomator2-driver..."
        (cd "$APPIUM_HOME" && npm list appium-uiautomator2-driver)
    fi
    
    # Also check if the driver is recognized by Appium
    echo "Checking if Appium recognizes the XCUITest driver..."
    echo "Running: APPIUM_HOME=$APPIUM_HOME $appium_path driver list"
    
    # For Appium 3.x, we need to handle the command differently
    if [[ "$APPIUM_VERSION" == 3* ]]; then
        echo "Using Appium 3.x command format"
        # Try without any config files first
        rm -f "$HOME/.appiumrc.json" "$APPIUM_HOME/.appiumrc.json" 2>/dev/null || true
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list || echo "Driver list command failed, but continuing verification"
    else
        env APPIUM_HOME="$APPIUM_HOME" "$appium_path" driver list || echo "Driver list command failed, but continuing verification"
    fi
    
    # Check if the wrapper scripts were created
    if [ -x "$bin_dir/appium" ]; then
        echo "Appium wrapper script is available at $bin_dir/appium"
    else
        echo "Warning: Appium wrapper script not found at $bin_dir/appium"
    fi
    
    if [ -x "$bin_dir/run-appium" ]; then
        echo "Run-Appium wrapper script is available at $bin_dir/run-appium"
        
        # Test if the run-appium script works
        VERSION_OUTPUT=$("$bin_dir/run-appium" --version 2>&1)
        if [[ "$VERSION_OUTPUT" == "$APPIUM_VERSION" ]]; then
            echo "run-appium wrapper correctly returns version $VERSION_OUTPUT"
        else
            echo "Warning: run-appium wrapper version check returned: $VERSION_OUTPUT"
        fi
    else
        echo "Warning: Run-Appium wrapper script not found at $bin_dir/run-appium"
    fi
    
    # Check FFmpeg installation
    if [ -x "$INSTALL_FOLDER/bin/ffmpeg" ]; then
        "$INSTALL_FOLDER/bin/ffmpeg" -version && echo "FFmpeg installation verified"
    else
        echo "Warning: FFmpeg not found at $INSTALL_FOLDER/bin/ffmpeg"
    fi
    
    # Verify libimobiledevice tools are available in PATH
    local libimobiledevice_version=""
    if command -v ideviceinfo &>/dev/null; then
        echo "libimobiledevice tools are available"
        libimobiledevice_version=$(ideviceinfo --version 2>/dev/null | head -1 || echo "unknown")
        echo "libimobiledevice version: $libimobiledevice_version"
    else
        echo "Warning: libimobiledevice tools not found in PATH"
    fi
    
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"             INSTALLATION COMPLETED SUCCESSFULLY                 \""
    echo "[${date_time} INF] \"================================================================\""
    echo "Node.js: $(node --version)"
    echo "npm: $(npm --version)"
    echo "Appium: $(env APPIUM_HOME="$APPIUM_HOME" "$appium_path" --version 2>/dev/null || echo "Not found")"
    echo "XCUITest driver directory: $([ -d "$xcuitest_dir" ] && echo "Exists" || echo "Not found")"
    echo "UiAutomator2 driver directory: $([ -d "$uiautomator2_dir" ] && echo "Exists" || echo "Not found")"
    echo "libimobiledevice: $libimobiledevice_version"
    echo "Appium binary: $appium_path"
    echo "Appium wrapper: $bin_dir/appium"
    echo "APPIUM_HOME: $APPIUM_HOME"
    echo ""
    echo "To use this installation in your shell, add the following to your ~/.zshrc or ~/.bash_profile:"
    echo "export APPIUM_HOME=\"$APPIUM_HOME\""
    echo "export PATH=\"$bin_dir:\$APPIUM_HOME/node_modules/.bin:\$PATH\""
    echo "export NVM_DIR=\"$INSTALL_FOLDER/.nvm\""
    echo "[ -s \"\$NVM_DIR/nvm.sh\" ] && . \"\$NVM_DIR/nvm.sh\"  # This loads nvm"
    echo "[${date_time} INF] \"================================================================\""
}

# Set locale to avoid warnings
set_locale() {
    export LC_ALL=C
    export LANG=C
    echo "Locale set to avoid warnings."
}

# Install mjpeg-consumer globally
install_mjpeg_consumer() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           STARTING MJPEG-CONSUMER INSTALLATION                 \""
    echo "[${date_time} INF] \"================================================================\""
    
    echo "Installing mjpeg-consumer ..."
    NODE_BIN_PATH=$(nvm which "$NODE_VERSION")
    NPM_BIN_PATH=$(dirname "$NODE_BIN_PATH")/npm
    
    # Modify package.json to add mjpeg-consumer dependency directly
    cd "$INSTALL_FOLDER/appium-home"
    
    # First try with --legacy-peer-deps to avoid dependency conflicts
    echo "Attempting to install mjpeg-consumer with --legacy-peer-deps..."
    "$NPM_BIN_PATH" install mjpeg-consumer@2.0.0 --save --legacy-peer-deps
    local install_status=$?
    
    # If the first attempt fails, try with --force
    if [ $install_status -ne 0 ]; then
        echo "First attempt failed, trying with --force..."
        "$NPM_BIN_PATH" install mjpeg-consumer@2.0.0 --save --force
        install_status=$?
    fi
    
    # If that also fails, try adding it to package.json manually and then running npm install
    if [ $install_status -ne 0 ]; then
        echo "Second attempt failed, trying to modify package.json manually..."
        
        # Create a backup of package.json
        cp package.json package.json.bak
        
        # Add mjpeg-consumer to dependencies using jq if available, otherwise use sed
        if command -v jq &>/dev/null; then
            jq '.dependencies["mjpeg-consumer"] = "^2.0.0"' package.json > package.json.tmp && mv package.json.tmp package.json
        else
            # Use sed to add the dependency just before the last } in the dependencies section
            sed -i '' 's/"dependencies": {/"dependencies": {\n    "mjpeg-consumer": "^2.0.0",/g' package.json
        fi
        
        # Run npm install with --legacy-peer-deps to update the lock file and install dependencies
        "$NPM_BIN_PATH" install --legacy-peer-deps
        install_status=$?
    fi
    
    if [ $install_status -eq 0 ]; then
        echo "Successfully installed mjpeg-consumer"
        check_success_noexit "mjpeg-consumer installation"
    else
        echo "All attempts to install mjpeg-consumer failed. Continuing without it."
        echo "This might cause video streaming functionality to be unavailable."
        # Use check_success_noexit to log a warning but continue execution
        check_success_noexit "mjpeg-consumer installation"
    fi
}

# Install xcode-select if not already installed
install_xcode_select() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"            STARTING XCODE-SELECT INSTALLATION                  \""
    echo "[${date_time} INF] \"================================================================\""
    
    if ! xcode-select --print-path &> /dev/null; then
        echo "Installing xcode-select..."
        xcode-select --install
        check_success "xcode-select installation"
    else
        echo "xcode-select is already installed."
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"         XCODE-SELECT ALREADY INSTALLED SUCCESSFULLY            \""
        echo "[${date_time} INF] \"================================================================\""
    fi
}


enable_rosetta() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"               CHECKING ROSETTA REQUIREMENTS                    \""
    echo "[${date_time} INF] \"================================================================\""
    
    if [[ $(uname -m) == 'arm64' ]]; then
        echo "Enabling Rosetta 2..."
        if /usr/sbin/softwareupdate --install-rosetta --agree-to-license; then
            echo "[${date_time} INF] \"================================================================\""
            echo "[${date_time} INF] \"           ROSETTA 2 INSTALLATION COMPLETED SUCCESSFULLY        \""
            echo "[${date_time} INF] \"================================================================\""
        else
            echo "[${date_time} ERR] \"================================================================\""
            echo "[${date_time} ERR] \"              ROSETTA 2 INSTALLATION FAILED                     \""
            echo "[${date_time} ERR] \"================================================================\""
            echo "Error: Failed to install Rosetta 2, which is required for running some components on Apple Silicon."
            exit 1
        fi
    else
        echo "Rosetta 2 is not required on this machine."
        echo "[${date_time} INF] \"================================================================\""
        echo "[${date_time} INF] \"           ROSETTA CHECK COMPLETED SUCCESSFULLY                \""
        echo "[${date_time} INF] \"================================================================\""
    fi
}
# Ensure required scripts are executable
make_scripts_executable() {
    local supervisor_setup_path="./SupervisorSetup.sh"
    local appium_script_path="./supervisord/Active/appium.sh"

    echo "Making SupervisorSetup.sh executable..."
    if [ -f "$supervisor_setup_path" ]; then
        chmod +x "$supervisor_setup_path"
        check_success "SupervisorSetup.sh chmod +x"
    else
        echo "Error: $supervisor_setup_path not found."
    fi

    echo "Making appium.sh executable..."
    if [ -f "$appium_script_path" ]; then
        chmod +x "$appium_script_path"
        check_success "appium.sh chmod +x"
    else
        echo "Error: $appium_script_path not found."
    fi
}

# Symlink Homebrew ffmpeg to ~/.local/bin/ffmpeg if not already present
symlink_ffmpeg() {
    mkdir -p "$HOME/.local/bin"
    if ! [ -e "$HOME/.local/bin/ffmpeg" ]; then
        local ffmpeg_path
        ffmpeg_path="$(command -v ffmpeg)"
        if [ -n "$ffmpeg_path" ]; then
            ln -sf "$ffmpeg_path" "$HOME/.local/bin/ffmpeg"
            echo "Symlinked ffmpeg to $HOME/.local/bin/ffmpeg"
        else
            echo "Warning: ffmpeg not found in PATH, cannot symlink."
        fi
    fi
}

# Create environment setup scripts for different shells
create_environment_scripts() {
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           CREATING ENVIRONMENT SCRIPTS                         \""
    echo "[${date_time} INF] \"================================================================\""
    
    local bin_dir="$INSTALL_FOLDER/bin"
    mkdir -p "$bin_dir"
    
    echo "Creating environment scripts for shell setup and Appium execution..."
    echo "- These scripts will make it easy to use the Appium installation"
    echo "- Directory: $bin_dir"
    
    # Create appium-env.sh for sourcing in bash/zsh
    cat > "$INSTALL_FOLDER/appium-env.sh" << EOL
#!/bin/bash
# Environment setup for Appium
export APPIUM_HOME="$APPIUM_HOME"
export NVM_DIR="$INSTALL_FOLDER/.nvm"

# Load NVM and use the correct Node.js version
if [ -s "\$NVM_DIR/nvm.sh" ]; then
    echo "Loading NVM from \$NVM_DIR"
    . "\$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1  # Load NVM quietly
    
    # Use the Node.js version that was used to install Appium
    if nvm use "$NODE_VERSION" > /dev/null 2>&1; then
        echo "Using Node.js version $NODE_VERSION from NVM"
    elif nvm use default > /dev/null 2>&1; then
        echo "Using default Node.js version from NVM"
    else
        echo "Warning: Could not activate Node.js from NVM"
    fi
    
    # Get Node.js binary path and add to PATH
    NODE_BIN=\$(nvm which current 2>/dev/null)
    if [ -n "\$NODE_BIN" ]; then
        NODE_DIR=\$(dirname "\$NODE_BIN")
        export PATH="\$NODE_DIR:\$PATH"
    fi
else
    echo "NVM script not found at \$NVM_DIR/nvm.sh"
fi

# Add Appium paths to PATH
export PATH="$bin_dir:$APPIUM_HOME/node_modules/.bin:\$PATH"

# Print environment info
echo "Appium environment activated:"
echo "APPIUM_HOME=$APPIUM_HOME"
echo "NVM_DIR=\$NVM_DIR"
echo "NODE_VERSION=\$(node --version 2>/dev/null || echo 'not found')"
echo "NPM_VERSION=\$(npm --version 2>/dev/null || echo 'not found')"
echo "APPIUM_VERSION=\$($APPIUM_HOME/node_modules/.bin/appium --version 2>/dev/null || echo 'not found')"
EOL
    chmod +x "$INSTALL_FOLDER/appium-env.sh"
    
    # Create a convenience script to run appium with proper environment
    cat > "$bin_dir/run-appium" << EOL
#!/bin/bash
# Source environment setup with custom Node.js installation
source "$INSTALL_FOLDER/appium-env.sh"

# Set APPIUM_HOME for both Appium 2.x and 3.x
export APPIUM_HOME="$APPIUM_HOME"

# Ensure we're using the custom Node.js
export NVM_DIR="$INSTALL_FOLDER/.nvm"
if [ -s "\$NVM_DIR/nvm.sh" ]; then
    . "\$NVM_DIR/nvm.sh" --no-use > /dev/null 2>&1
    nvm use "$NODE_VERSION" > /dev/null 2>&1 || nvm use default > /dev/null 2>&1
    
    # Get Node.js binary path and add to PATH
    NODE_BIN=\$(nvm which current 2>/dev/null)
    if [ -n "\$NODE_BIN" ]; then
        NODE_DIR=\$(dirname "\$NODE_BIN")
        export PATH="\$NODE_DIR:\$PATH"
        echo "Using Node.js: \$(node --version)"
    fi
fi

# Detect Appium version
APPIUM_VERSION=\$("$APPIUM_HOME/node_modules/.bin/appium" --version 2>/dev/null)
APPIUM_MAJOR_VERSION=\$(echo \$APPIUM_VERSION | cut -d'.' -f1)
echo "Using Appium: \$APPIUM_VERSION"

# Run appium with all arguments passed to this script
# For Appium 3.x, 'server' is required as the first argument for starting a server
if [ "\$APPIUM_MAJOR_VERSION" = "3" ] && [ "\$1" != "server" ] && [ "\$1" != "driver" ] && [ "\$1" != "plugin" ] && [ "\$1" != "--version" ] && [ "\$1" != "-v" ]; then
    # Insert 'server' as the first argument for Appium 3.x server start
    "$APPIUM_HOME/node_modules/.bin/appium" server "\$@"
else
    # Pass arguments as-is for Appium 2.x or when using explicit commands in 3.x
    "$APPIUM_HOME/node_modules/.bin/appium" "\$@"
fi
EOL
    chmod +x "$bin_dir/run-appium"
    
    echo "Created environment setup scripts:"
    echo "- $INSTALL_FOLDER/appium-env.sh (source this in your shell)"
    echo "- $bin_dir/run-appium (use this to run appium with proper environment)"
}

# Main execution
main() {
    parse_arguments "$@"
    set_locale
    enable_rosetta
    install_xcode_select
    setup_nvm
    setup_node
    install_appium
    
    # Install drivers based on toggles
    if [ "$INSTALL_XCUITEST" = "true" ]; then
        install_xcuitest_driver
    else
        local date_time=$(date '+%Y-%m-%d %H:%M:%S')
        echo "[${date_time} INF] \"Skipping XCUITest driver installation (INSTALL_XCUITEST=false)\""
    fi
    
    if [ "$INSTALL_UIAUTOMATOR" = "true" ]; then
        install_uiautomator2_driver
    else
        local date_time=$(date '+%Y-%m-%d %H:%M:%S')
        echo "[${date_time} INF] \"Skipping UiAutomator2 driver installation (INSTALL_UIAUTOMATOR=false)\""
    fi
    
    install_appium_inspector
    install_device_farm
    install_mjpeg_consumer
    install_ffmpeg
    symlink_ffmpeg
    install_libimobiledevice
    verify_idevicediagnostics
    create_environment_scripts
    verify_installations
    
    local date_time=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[${date_time} INF] \"================================================================\""
    echo "[${date_time} INF] \"           PREREQUISITES SETUP COMPLETED SUCCESSFULLY           \""
    echo "[${date_time} INF] \"================================================================\""
    echo ""
    echo "To use Appium and XCUITest from this installation:"
    echo ""
    echo "1. For one-time use in current shell:"
    echo "   source \"$INSTALL_FOLDER/appium-env.sh\""
    echo ""
    echo "2. To run Appium with proper environment:"
    echo "   \"$INSTALL_FOLDER/bin/run-appium\" --address 127.0.0.1 --port 4723"
    echo ""
    echo "3. To make permanent for your user, add to ~/.zshrc:"
    echo "   source \"$INSTALL_FOLDER/appium-env.sh\""
    echo ""
    echo "4. Test your installation with:"
    echo "   \"$INSTALL_FOLDER/bin/run-appium\" driver list"
    echo "[${date_time} INF] \"================================================================\""
}

main "$@"
