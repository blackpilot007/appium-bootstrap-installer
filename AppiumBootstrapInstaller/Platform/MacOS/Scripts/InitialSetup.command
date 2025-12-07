#!/bin/bash

# Display a message about installing XCode and Simulators
echo "            ========================================="
echo "Please make sure to install the latest XCode and Simulators of targeted iOS versions"
echo "For more details, refer to the XCode/iOS version support documentation:"
echo "https://github.com/appium/appium-xcuitest-driver/blob/master/docs/installation/requirements.md#xcodeios-version-support"
echo "Use below command to install the required simulator runtimes"
echo "xcodebuild -downloadPlatform iOS -exportPath ~/Downloads -buildVersion <iOS verson no>"
echo "            ========================================="


# Function to check the success of a command
check_success() {
    if [ $? -eq 0 ]; then
        echo "$1 succeeded."
    else
        echo "$1 failed."
        exit 1
    fi
}




# Install .NET 8.0 SDK
install_dotnet_sdk() {
    echo "Installing .NET 8.0 SDK..."
    /bin/bash -c "bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0"
    check_success ".NET 8.0 SDK installation"
}

# Verify .NET installation
verify_dotnet_installation() {
    echo "Verifying .NET installation..."
    dotnet --version
    check_success ".NET verification"
}

# Make a script executable
make_executable() {
    local script_path="$1"
    local script_name="$2"

    echo "Making $script_name executable..."
    if [ -f "$script_path" ]; then
        chmod +x "$script_path"
        check_success "$script_name chmod +x"
    else
        echo "Error: $script_path not found."
    fi
}

# Remove quarantine attribute
remove_quarantine() {
    local target_path="$1"
    echo "Removing quarantine attribute from $target_path..."
    xattr -r -d com.apple.quarantine "$target_path"
    check_success "Quarantine removal for $target_path"
}

# Start AppiumBootstrapInstaller
start_worker() {
    echo "Starting AppiumBootstrapInstaller in a new terminal..."
    if [ -f "./AppiumBootstrapInstaller" ]; then
        current_dir=$(pwd)
        osascript -e "tell application \"Terminal\" to do script \"cd $current_dir && ./AppiumBootstrapInstaller\"" &
        check_success "AppiumBootstrapInstaller startup in new terminal"
    else
        echo "Error: AppiumBootstrapInstaller not found."
        exit 1
    fi
}

# Install xcode-select if not already installed
install_xcode_select() {
    if ! xcode-select --print-path &> /dev/null; then
        echo "Installing xcode-select..."
        xcode-select --install
        check_success "xcode-select installation"
    else
        echo "xcode-select is already installed."
    fi
}
install_brew()
{
    # Install Homebrew:
     if ! brew --version &>/dev/null; then
        echo "Homebrew not found. Installing Homebrew..."
        echo "Installing Homebrew in the user's home directory..."
        /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)" \
                </dev/null 2>/dev/null
        export PATH="$HOME/.homebrew/bin:$PATH"

    else
        echo "Homebrew is already installed."
    fi

# Install pyenv, pipx with Homebrew:
brew install pyenv@2.6.8

# Make sure pyenv is on the path:
echo 'export PATH="$HOME/.pyenv/shims:$PATH"' >> ~/.bash_profile

# Restart your shell (so we have the updated path):
#exec $SHELL
#exec zsh -l

}

# Main script execution
main() {
    install_brew
    #install_dotnet_sdk
    #verify_dotnet_installation
    install_xcode_select
    make_executable "./Platform/MacOS/Scripts/SupervisorSetup.sh" "SupervisorSetup.sh"
    make_executable "./Platform/MacOS/Scripts/InstallDependencies.sh" "InstallDependencies.sh"
    # make_executable "./Platform/MacOS/Scripts/ResignWebDriverAgent.sh" "ResignWebDriverAgent.sh"
    # make_executable "./Platform/MacOS/Scripts/ResignIPA.sh" "ResignIPA.sh"
    make_executable "./Platform/MacOS/Scripts/check_appium_drivers.sh" "check_appium_drivers.sh"
    make_executable "./Platform/MacOS/Scripts/clean-appium-install.sh" "clean-appium-install.sh"
    make_executable "./AppiumBootstrapInstaller"
    remove_quarantine "AppiumBootstrapInstaller"
    start_worker

    echo "Script execution completed."

}

# Run the main function
main