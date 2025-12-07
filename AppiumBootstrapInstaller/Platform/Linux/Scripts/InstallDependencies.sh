#!/bin/bash

# InstallDependencies.sh - Linux version
# Installs Node.js, NVM, Appium, and optionally iOS/Android drivers

set -e  # Exit on any error

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

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
INSTALL_FOLDER="$HOME/.local"
NODE_VERSION="22"
APPIUM_VERSION="2.17.1"
NVM_VERSION="0.40.2"
XCUITEST_VERSION=""
UIAUTOMATOR2_VERSION=""
INSTALL_IOS_SUPPORT=false
INSTALL_ANDROID_SUPPORT=true
INSTALL_XCUITEST="true"
INSTALL_UIAUTOMATOR="true"
INSTALL_DEVICE_FARM="true"
DEVICEFARM_VERSION="8.3.5"
DEVICEFARM_DASHBOARD_VERSION="2.0.3"

# Logging functions
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Parse command line arguments
parse_arguments() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --install_folder=*)
                INSTALL_FOLDER="${1#*=}"
                shift
                ;;
            --node_version=*)
                NODE_VERSION="${1#*=}"
                shift
                ;;
            --appium_version=*)
                APPIUM_VERSION="${1#*=}"
                shift
                ;;
            --nvm_version=*)
                NVM_VERSION="${1#*=}"
                shift
                ;;
            --xcuitest_version=*)
                XCUITEST_VERSION="${1#*=}"
                shift
                ;;
            --uiautomator2_version=*)
                UIAUTOMATOR2_VERSION="${1#*=}"
                shift
                ;;
            --install_device_farm=*)
                INSTALL_DEVICE_FARM="${1#*=}"
                shift
                ;;
            --devicefarm_version=*)
                DEVICEFARM_VERSION="${1#*=}"
                shift
                ;;
            --devicefarm_dashboard_version=*)
                DEVICEFARM_DASHBOARD_VERSION="${1#*=}"
                shift
                ;;
            --install_ios_support|--install_ios_support=true)
                INSTALL_IOS_SUPPORT=true
                shift
                ;;
            --install_ios_support=false)
                INSTALL_IOS_SUPPORT=false
                shift
                ;;
            --install_android_support|--install_android_support=true)
                INSTALL_ANDROID_SUPPORT=true
                shift
                ;;
            --install_android_support=false)
                INSTALL_ANDROID_SUPPORT=false
                shift
                ;;
            *)
                log_error "Unknown argument: $1"
                exit 1
                ;;
        esac
    done
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Detect system architecture
detect_architecture() {
    ARCH="$(uname -m)"
    log_info "Detected architecture: $ARCH"
    
    case "$ARCH" in
        x86_64|amd64)
            log_info "Running on x86_64 architecture"
            ;;
        aarch64|arm64)
            log_info "Running on ARM64 architecture"
            ;;
        *)
            log_warn "Unknown architecture: $ARCH"
            ;;
    esac
}

# Install NVM
install_nvm() {
    log_info "Installing NVM ${NVM_VERSION}..."
    
    if [ -d "$HOME/.nvm" ]; then
        log_warn "NVM directory already exists. Skipping installation."
        return 0
    fi
    
    curl -o- "https://raw.githubusercontent.com/nvm-sh/nvm/v${NVM_VERSION}/install.sh" | bash
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    log_info "NVM ${NVM_VERSION} installed successfully"
}

# Install Node.js via NVM
install_node() {
    log_info "Installing Node.js ${NODE_VERSION}..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    if ! command_exists nvm; then
        log_error "NVM not found. Please install NVM first."
        exit 1
    fi
    
    nvm install "$NODE_VERSION"
    nvm use "$NODE_VERSION"
    nvm alias default "$NODE_VERSION"
    
    log_info "Node.js ${NODE_VERSION} installed successfully"
    node --version
    npm --version
}

# Install Appium
install_appium() {
    log_info "Installing Appium ${APPIUM_VERSION}..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    npm install -g "appium@${APPIUM_VERSION}"
    
    log_info "Appium ${APPIUM_VERSION} installed successfully"
    appium --version
    
    # Detect major version for configuration
    INSTALLED_APPIUM_VERSION=$(appium --version 2>/dev/null || echo "unknown")
    APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
    
    # Create proper configuration based on Appium version
    if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        log_info "Creating Appium 3.x configuration..."
        mkdir -p "$HOME/.appium"
        chmod -R 755 "$HOME/.appium" 2>/dev/null || true
        # For Appium 3.x: use extensionPaths.base (not appium_home)
        echo '{"extensionPaths": {"base": "'$HOME'/.appium"}}' > "$HOME/.appium/config.json"
        log_info "Created Appium 3.x config with extensionPaths.base"
    else
        log_info "Appium 2.x detected - using default global configuration"
    fi
}

# Install go-ios for iOS real device support (required by device-farm)
# Reference: https://github.com/danielpaulus/go-ios
install_go_ios() {
    log_info "Installing go-ios for device-farm iOS real device support..."
    
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
        log_warn "Unsupported OS type: $os_type. Skipping go-ios installation."
        log_warn "Device-farm will work for simulators/emulators only."
        return 0
    fi
    
    log_info "Downloading go-ios from: $goios_url"
    if curl -L "$goios_url" -o "$goios_dir/$zip_file"; then
        log_info "Extracting go-ios..."
        (cd "$goios_dir" && unzip -o "$zip_file" && chmod +x ios && rm "$zip_file")
        if [ -x "$goios_dir/ios" ]; then
            log_info "✅ go-ios installed successfully at: $goios_dir/ios"
            # Verify installation
            "$goios_dir/ios" version 2>&1 | head -5 || true
            
            # Set GO_IOS environment variable in the installation folder
            echo "export GO_IOS=\"$goios_dir/ios\"" >> "$INSTALL_FOLDER/.go_ios_env"
            log_info "GO_IOS environment variable reference saved to: $INSTALL_FOLDER/.go_ios_env"
            log_info "Device-farm will use: GO_IOS=$goios_dir/ios"
        else
            log_warn "go-ios binary not found after extraction"
            return 1
        fi
    else
        log_warn "Failed to download go-ios. Device-farm will work for simulators/emulators only."
        return 1
    fi
    
    log_info "go-ios installation completed successfully"
}

# Install DeviceFarm plugin for Appium
install_device_farm() {
    if [ "$INSTALL_DEVICE_FARM" != "true" ]; then
        log_info "Skipping DeviceFarm plugin installation (disabled in configuration)"
        return 0
    fi
    
    log_info "Installing Appium DeviceFarm plugin..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    nvm use "$NODE_VERSION" || true
    
    # Detect Appium version
    INSTALLED_APPIUM_VERSION=$(appium --version 2>/dev/null || echo "unknown")
    APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
    
    # Install device-farm plugin
    log_info "Installing device-farm plugin..."
    if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        appium plugin add --source=npm appium-device-farm
    else
        appium plugin install --source=npm appium-device-farm
    fi
    
    # Install dashboard plugin
    log_info "Installing appium-dashboard plugin..."
    if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
        appium plugin add --source=npm appium-dashboard || log_warn "Failed to install dashboard plugin"
    else
        appium plugin install --source=npm appium-dashboard || log_warn "Failed to install dashboard plugin"
    fi
    
    # Install go-ios for real device support
    install_go_ios || log_warn "go-ios installation failed, but continuing..."
    
    # Verify installation
    log_info "Verifying DeviceFarm plugin installation..."
    appium plugin list --installed | grep -i "device-farm" && log_info "✅ DeviceFarm plugin installed" || log_warn "DeviceFarm plugin not found"
    
    log_info "DeviceFarm plugin installation completed"
}

# Install XCUITest driver (iOS)
install_xcuitest_driver() {
    if [ "$INSTALL_IOS_SUPPORT" = false ]; then
        log_info "Skipping XCUITest driver installation (iOS support not requested)"
        return 0
    fi
    
    log_warn "iOS support on Linux is limited and requires manual setup of libimobiledevice"
    log_info "Installing XCUITest driver..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    # Detect Appium version
    INSTALLED_APPIUM_VERSION=$(appium --version 2>/dev/null || echo "unknown")
    APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
    
    # Use appropriate command based on version with retry logic
    local max_retries=3
    local retry_count=0
    local install_success=false
    
    while [ $retry_count -lt $max_retries ] && [ "$install_success" = false ]; do
        if [ $retry_count -gt 0 ]; then
            log_warn "Retry attempt $retry_count of $((max_retries-1))..."
            sleep 5
        fi
        
        if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
            if [ -n "$XCUITEST_VERSION" ]; then
                if appium driver install "xcuitest@${XCUITEST_VERSION}"; then
                    install_success=true
                fi
            else
                if appium driver install xcuitest; then
                    install_success=true
                fi
            fi
        else
            if [ -n "$XCUITEST_VERSION" ]; then
                if appium driver install "xcuitest@${XCUITEST_VERSION}"; then
                    install_success=true
                fi
            else
                if appium driver install xcuitest; then
                    install_success=true
                fi
            fi
        fi
        
        retry_count=$((retry_count + 1))
    done
    
    if [ "$install_success" = false ]; then
        log_error "Failed to install XCUITest driver after $max_retries attempts"
        return 1
    fi
    
    log_info "XCUITest driver installed successfully"
}

# Install UiAutomator2 driver (Android)
install_uiautomator2_driver() {
    if [ "$INSTALL_ANDROID_SUPPORT" = false ]; then
        log_info "Skipping UiAutomator2 driver installation (Android support not requested)"
        return 0
    fi
    
    log_info "Installing UiAutomator2 driver..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    # Detect Appium version
    INSTALLED_APPIUM_VERSION=$(appium --version 2>/dev/null || echo "unknown")
    APPIUM_MAJOR_VERSION=$(echo "$INSTALLED_APPIUM_VERSION" | cut -d'.' -f1)
    
    # Use appropriate command based on version with retry logic
    local max_retries=3
    local retry_count=0
    local install_success=false
    
    while [ $retry_count -lt $max_retries ] && [ "$install_success" = false ]; do
        if [ $retry_count -gt 0 ]; then
            log_warn "Retry attempt $retry_count of $((max_retries-1))..."
            sleep 5
        fi
        
        if [[ "$INSTALLED_APPIUM_VERSION" == 3* ]] || [ "$APPIUM_MAJOR_VERSION" = "3" ]; then
            if [ -n "$UIAUTOMATOR2_VERSION" ]; then
                if appium driver install "uiautomator2@${UIAUTOMATOR2_VERSION}"; then
                    install_success=true
                fi
            else
                if appium driver install uiautomator2; then
                    install_success=true
                fi
            fi
        else
            if [ -n "$UIAUTOMATOR2_VERSION" ]; then
                if appium driver install "uiautomator2@${UIAUTOMATOR2_VERSION}"; then
                    install_success=true
                fi
            else
                if appium driver install uiautomator2; then
                    install_success=true
                fi
            fi
        fi
        
        retry_count=$((retry_count + 1))
    done
    
    if [ "$install_success" = false ]; then
        log_error "Failed to install UiAutomator2 driver after $max_retries attempts"
        return 1
    fi
    
    log_info "UiAutomator2 driver installed successfully"
}

# Install libimobiledevice (for iOS support)
install_libimobiledevice() {
    if [ "$INSTALL_IOS_SUPPORT" = false ]; then
        log_info "Skipping libimobiledevice installation (iOS support not requested)"
        return 0
    fi
    
    log_info "Installing libimobiledevice for iOS device support..."
    
    # Detect package manager
    if command_exists apt-get; then
        sudo apt-get update
        sudo apt-get install -y libimobiledevice-utils usbmuxd
    elif command_exists dnf; then
        sudo dnf install -y libimobiledevice-utils
    elif command_exists yum; then
        sudo yum install -y libimobiledevice-utils
    elif command_exists pacman; then
        sudo pacman -S --noconfirm libimobiledevice usbmuxd
    else
        log_warn "Could not detect package manager. Please install libimobiledevice manually."
        return 1
    fi
    
    log_info "libimobiledevice installed successfully"
}

# Install Android SDK tools
install_android_tools() {
    if [ "$INSTALL_ANDROID_SUPPORT" = false ]; then
        log_info "Skipping Android tools installation (Android support not requested)"
        return 0
    fi
    
    log_info "Checking for Android SDK tools..."
    
    if command_exists adb; then
        log_info "ADB already installed: $(adb version | head -n 1)"
    else
        log_warn "ADB not found. Installing platform-tools..."
        
        # Detect package manager
        if command_exists apt-get; then
            sudo apt-get update
            sudo apt-get install -y android-tools-adb android-tools-fastboot
        elif command_exists dnf; then
            sudo dnf install -y android-tools
        elif command_exists yum; then
            sudo yum install -y android-tools
        elif command_exists pacman; then
            sudo pacman -S --noconfirm android-tools
        else
            log_warn "Could not detect package manager. Please install ADB manually."
            return 1
        fi
    fi
    
    log_info "Android tools check completed"
}

# Verify installations
verify_installations() {
    log_info "Verifying installations..."
    
    # Load NVM
    export NVM_DIR="$HOME/.nvm"
    [ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"
    
    # Check Node.js
    if command_exists node; then
        log_info "Node.js version: $(node --version)"
    else
        log_error "Node.js not found!"
        exit 1
    fi
    
    # Check NPM
    if command_exists npm; then
        log_info "NPM version: $(npm --version)"
    else
        log_error "NPM not found!"
        exit 1
    fi
    
    # Check Appium
    if command_exists appium; then
        log_info "Appium version: $(appium --version)"
    else
        log_error "Appium not found!"
        exit 1
    fi
    
    # Check drivers
    log_info "Installed Appium drivers:"
    appium driver list --installed
    
    # Check iOS tools
    if [ "$INSTALL_IOS_SUPPORT" = true ]; then
        if command_exists ideviceinfo; then
            log_info "libimobiledevice installed: $(ideviceinfo --version 2>&1 | head -n 1)"
        else
            log_warn "libimobiledevice not found. iOS device management may not work."
        fi
    fi
    
    # Check Android tools
    if [ "$INSTALL_ANDROID_SUPPORT" = true ]; then
        if command_exists adb; then
            log_info "ADB version: $(adb version | head -n 1)"
        else
            log_warn "ADB not found. Android device management may not work."
        fi
    fi
    
    log_info "Verification completed successfully"
}

# Main installation process
main() {
    log_info "========================================"
    log_info "AppiumBootstrap Prerequisites Installation (Linux)"
    log_info "========================================"
    log_info "Install folder: $INSTALL_FOLDER"
    log_info "Node version: $NODE_VERSION"
    log_info "Appium version: $APPIUM_VERSION"
    log_info "NVM version: $NVM_VERSION"
    log_info "iOS support: $INSTALL_IOS_SUPPORT"
    log_info "Android support: $INSTALL_ANDROID_SUPPORT"
    log_info "Device Farm: $INSTALL_DEVICE_FARM"
    log_info "========================================"
    
    # Detect architecture
    detect_architecture
    
    # Create install folder
    mkdir -p "$INSTALL_FOLDER"
    
    # Install components
    install_nvm
    install_node
    install_appium
    
    if [ "$INSTALL_IOS_SUPPORT" = true ]; then
        install_libimobiledevice
        install_xcuitest_driver
    fi
    
    if [ "$INSTALL_ANDROID_SUPPORT" = true ]; then
        install_android_tools
        install_uiautomator2_driver
    fi
    
    # Install device-farm if enabled
    if [ "$INSTALL_DEVICE_FARM" = true ]; then
        install_device_farm
    fi
    
    # Verify installations
    verify_installations
    
    log_info "========================================"
    log_info "Installation completed successfully!"
    log_info "========================================"
    log_info "Please restart your shell or run:"
    log_info "  source ~/.nvm/nvm.sh"
    log_info "  nvm use ${NODE_VERSION}"
}

# Parse arguments and run main
parse_arguments "$@"
main
