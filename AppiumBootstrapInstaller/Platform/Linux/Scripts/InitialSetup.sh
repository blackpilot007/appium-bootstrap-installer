#!/bin/bash

# Start.sh - Linux entry point for AppiumBootstrapInstaller
# This script sets up the environment and starts the agent

echo "============================================="
echo "Appium Device Health Monitor - Linux Setup"
echo "============================================="

# Function to check the success of a command
check_success() {
    if [ $? -eq 0 ]; then
        echo "✅ $1 succeeded."
    else
        echo "❌ $1 failed."
        exit 1
    fi
}

# Detect Linux distribution
detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        echo "Detected Linux distribution: $NAME $VERSION"
        DISTRO=$ID
    else
        echo "Cannot detect Linux distribution. Assuming generic Linux."
        DISTRO="unknown"
    fi
}

# Install .NET 8.0 SDK
install_dotnet_sdk() {
    if command -v dotnet &> /dev/null; then
        echo ".NET is already installed: $(dotnet --version)"
        return 0
    fi
    
    echo "Installing .NET 8.0 SDK..."
    
    # Ubuntu/Debian
    if [[ "$DISTRO" == "ubuntu" ]] || [[ "$DISTRO" == "debian" ]]; then
        wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
        echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
    # Fedora/RHEL/CentOS
    elif [[ "$DISTRO" == "fedora" ]] || [[ "$DISTRO" == "rhel" ]] || [[ "$DISTRO" == "centos" ]]; then
        wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
        echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
    else
        # Generic installation
        wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
        chmod +x /tmp/dotnet-install.sh
        /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
        echo 'export PATH="$HOME/.dotnet:$PATH"' >> "$HOME/.bashrc"
    fi
    
    check_success ".NET 8.0 SDK installation"
}

# Verify .NET installation
verify_dotnet_installation() {
    echo "Verifying .NET installation..."
    if command -v dotnet &> /dev/null; then
        dotnet --version
        check_success ".NET verification"
    else
        echo "❌ .NET is not installed or not in PATH"
        exit 1
    fi
}

# Make scripts executable
make_executable() {
    local script_path="$1"
    local script_name="$2"

    echo "Making $script_name executable..."
    if [ -f "$script_path" ]; then
        chmod +x "$script_path"
        check_success "$script_name chmod +x"
    else
        echo "⚠️  Warning: $script_path not found."
    fi
}

# Install libimobiledevice for iOS device support (optional)
install_libimobiledevice() {
    if command -v idevice_id &> /dev/null; then
        echo "libimobiledevice is already installed"
        return 0
    fi
    
    echo "Installing libimobiledevice for iOS device support..."
    
    if [[ "$DISTRO" == "ubuntu" ]] || [[ "$DISTRO" == "debian" ]]; then
        sudo apt-get update
        sudo apt-get install -y libimobiledevice-utils usbmuxd
    elif [[ "$DISTRO" == "fedora" ]]; then
        sudo dnf install -y libimobiledevice-utils usbmuxd
    elif [[ "$DISTRO" == "arch" ]]; then
        sudo pacman -S --noconfirm libimobiledevice usbmuxd
    else
        echo "⚠️  Please install libimobiledevice manually for iOS support"
    fi
}

# Start AppiumBootstrapInstaller
start_worker() {
    echo "Starting AppiumBootstrapInstaller..."
    
    if [ ! -f "./AppiumBootstrapInstaller" ]; then
        echo "❌ Error: AppiumBootstrapInstaller not found."
        exit 1
    fi
    
    # Make agent executable
    chmod +x ./AppiumBootstrapInstaller
    
    # Determine terminal emulator
    if command -v gnome-terminal &> /dev/null; then
        gnome-terminal -- bash -c "cd $(pwd) && ./AppiumBootstrapInstaller; exec bash"
        check_success "AppiumBootstrapInstaller startup in gnome-terminal"
    elif command -v konsole &> /dev/null; then
        konsole -e "cd $(pwd) && ./AppiumBootstrapInstaller; exec bash"
        check_success "AppiumBootstrapInstaller startup in konsole"
    elif command -v xterm &> /dev/null; then
        xterm -e "cd $(pwd) && ./AppiumBootstrapInstaller; exec bash" &
        check_success "AppiumBootstrapInstaller startup in xterm"
    else
        # No terminal emulator, run in background with nohup
        echo "No terminal emulator found. Starting in background..."
        nohup ./AppiumBootstrapInstaller > appium_bootstrap.log 2>&1 &
        echo "AppiumBootstrapInstaller started in background. PID: $!"
        echo "Logs: $(pwd)/appium_bootstrap.log"
        echo "To view logs: tail -f $(pwd)/appium_bootstrap.log"
    fi
}

# Main script execution
main() {
    detect_distro
    install_dotnet_sdk
    verify_dotnet_installation
    
    # Make Linux platform scripts executable
    make_executable "./Platform/Linux/Scripts/SystemdSetup.sh" "SystemdSetup.sh"
    make_executable "./Platform/Linux/Scripts/InstallDependencies.sh" "InstallDependencies.sh"
    make_executable "./Platform/Linux/Scripts/StartAppiumServer.sh" "StartAppiumServer.sh"
    make_executable "./Platform/Linux/Scripts/portforward.sh" "portforward.sh"
    
    # Make agent executable
    make_executable "./AppiumBootstrapInstaller" "AppiumBootstrapInstaller"
    
    # Optional: Install libimobiledevice for iOS support
    read -p "Install libimobiledevice for iOS device support? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        install_libimobiledevice
    fi
    
    # Start the agent
    start_worker

    echo ""
    echo "============================================="
    echo "Setup completed."
    echo "============================================="
    echo ""
    echo "Next steps:"
    echo "1. Configure WorkerConfiguration.json"
    echo "2. Run ./Platform/Linux/Scripts/InstallDependencies.sh to install Appium"
    echo "3. Run ./Platform/Linux/Scripts/SystemdSetup.sh to set up systemd services"
    echo ""
}

# Run the main function
main
