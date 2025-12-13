#!/bin/bash

# SystemdSetup.sh - Sets up Python and systemd services for AppiumBootstrap on Linux
# 
# This script installs and configures:
# 1. Python environment
# 2. Systemd user services for process management
# 3. Required directories and configurations
#
# Author: Appium Bootstrap Team
# Last Updated: $(date +"%Y-%m-%d")

# Logging functions
script_log() {
    local timestamp
    timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    echo "[$timestamp INFO] $1"
}

warning() {
    local timestamp
    timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    echo "[$timestamp WARNING] $1" >&2
}

error_exit() {
    local timestamp
    timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    echo "[$timestamp ERROR] $1" >&2
    if [ "${FATAL_ERRORS:-0}" -eq 1 ]; then
        echo "Fatal error encountered. Exiting."
        exit 1
    fi
    return 1
}

# Exit on command errors
set -euo pipefail

# Variables
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_VERSION="3.12"

# Accept installation path as first argument, default to relative path from SCRIPT_DIR
# SCRIPT_DIR should be NewPublished/Platform/Linux/Scripts, so we go up to NewPublished
INSTALL_DIR="${1:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
SERVICE_CONF_DIR="$INSTALL_DIR/systemd"
SERVICE_INCLUDE_DIR="$INSTALL_DIR/systemd/services"
VENV_DIR="$INSTALL_DIR/venv"

# Enable debug output if DEBUG is set
if [[ "${DEBUG:-0}" == "1" ]]; then
    set -x
fi

# Portable-first: skip systemd setup unless explicitly enabled
if [[ "${ENABLE_SYSTEMD:-0}" != "1" ]]; then
    script_log "Skipping systemd setup (portable mode)."
    script_log "Set ENABLE_SYSTEMD=1 if you intentionally want per-user systemd units."
    script_log "Current install directory: $INSTALL_DIR"
    exit 0
fi

# Functions
create_directory() {
    local dir=$1
    local preserve=${2:-0}
    
    if [[ -d "$dir" ]]; then
        if [[ $preserve -eq 1 ]]; then
            script_log "Directory exists and will be preserved: $dir"
        else
            script_log "Directory exists. Deleting: $dir"
            rm -rf "$dir" || error_exit "Failed to delete directory: $dir"
        fi
    fi
    
    if [[ ! -d "$dir" ]]; then
        script_log "Creating directory: $dir"
        mkdir -p "$dir" || error_exit "Failed to create directory: $dir"
    fi
}

check_system_resources() {
    script_log "Checking system resources..."
    
    local parent_dir
    parent_dir=$(dirname "$INSTALL_DIR")
    
    local available_disk
    if [[ -d "$parent_dir" ]]; then
        available_disk=$(df -BM "$parent_dir" | awk 'NR==2 {print $4}' | sed 's/M//')
        script_log "Available disk space in $parent_dir: ${available_disk}MB"
    else
        available_disk=$(df -BM / | awk 'NR==2 {print $4}' | sed 's/M//')
        script_log "Available disk space on root filesystem: ${available_disk}MB"
    fi
    
    if ! [[ "$available_disk" =~ ^[0-9]+$ ]]; then
        warning "Could not determine available disk space"
        available_disk=0
    fi
    
    if [[ $available_disk -lt 1024 ]]; then
        warning "Low disk space. At least 1GB recommended, you have ${available_disk}MB available."
        echo -n "Continue anyway? (y/n) "
        read -r -n 1 REPLY
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            error_exit "Setup aborted due to insufficient disk space."
            return 1
        fi
    fi
    
    return 0
}

check_installed_versions() {
    script_log "Checking system Python version..."
    if command -v python3 &>/dev/null; then
        script_log "System Python: $(python3 --version 2>&1)"
        return 0
    else
        script_log "Python 3 not found in PATH"
        return 1
    fi
}

setup_systemd_directories() {
    script_log "Setting up systemd directories..."
    
    # Create user systemd directory
    create_directory "$SYSTEMD_USER_DIR" 1
    
    # Create service configuration directories
    create_directory "$SERVICE_CONF_DIR" 1
    create_directory "$SERVICE_INCLUDE_DIR" 1
    
    # Create placeholder service file
    if [[ ! -f "$SERVICE_INCLUDE_DIR/dummy.service" ]]; then
        script_log "Creating placeholder service file..."
        cat > "$SERVICE_INCLUDE_DIR/dummy.service" <<EOL
# Placeholder service - Generated on $(date)
[Unit]
Description=Placeholder Service
After=network.target

[Service]
Type=simple
ExecStart=/bin/echo "Placeholder - replace with actual command"
Restart=no

[Install]
WantedBy=default.target
EOL
    fi
    
    script_log "Systemd directories created successfully."
}

setup_python_environment() {
    script_log "Setting up Python environment..."
    
    # Check if Python 3 is available
    if ! command -v python3 &>/dev/null; then
        error_exit "Python 3 not found. Please install Python 3.$PYTHON_VERSION or higher"
        return 1
    fi
    
    local python_version
    python_version=$(python3 --version 2>&1 | awk '{print $2}')
    script_log "Found Python version: $python_version"
    
    # Check Python version
    local python_minor
    python_minor=$(echo "$python_version" | cut -d. -f2)
    if [[ "$python_minor" -lt 8 ]]; then
        error_exit "Python version too old: $python_version. Need at least 3.8"
        return 1
    fi
    
    PYTHON_BIN=$(command -v python3)
    script_log "Using Python: $PYTHON_BIN"
    
    if [[ ! -d "$INSTALL_DIR" ]]; then
        script_log "Creating installation directory at $INSTALL_DIR..."
        mkdir -p "$INSTALL_DIR"
    fi
    
    cd "$INSTALL_DIR" || error_exit "Failed to change to installation directory: $INSTALL_DIR"

    # Create virtualenv
    if [[ ! -d "$VENV_DIR" ]]; then
        script_log "Creating virtual environment using $PYTHON_BIN..."
        "$PYTHON_BIN" -m venv "$VENV_DIR" || error_exit "Failed to create virtualenv"
    else
        script_log "Virtual environment already exists at $VENV_DIR"
    fi

    if [[ ! -f "$VENV_DIR/bin/activate" ]]; then
        error_exit "Virtual environment activation script not found"
    fi

    script_log "Activating virtual environment..."
    source "$VENV_DIR/bin/activate"

    script_log "Upgrading pip..."
    "$VENV_DIR/bin/python" -m pip install --upgrade pip || {
        script_log "Failed to upgrade pip with standard method. Trying get-pip.py..."
        curl -sS https://bootstrap.pypa.io/get-pip.py | "$VENV_DIR/bin/python" || error_exit "Failed to install pip"
    }

    script_log "Installing Python packages..."
    "$VENV_DIR/bin/pip" install psutil requests || warning "Some Python packages failed to install"

    deactivate

    export PATH="$VENV_DIR/bin:$PATH"

    script_log "Python environment setup completed successfully."
}

reload_systemd() {
    script_log "Reloading systemd user daemon..."
    systemctl --user daemon-reload || warning "Failed to reload systemd daemon"
}

enable_systemd_linger() {
    script_log "Enabling user lingering (allows services to run without login)..."
    loginctl enable-linger "$USER" || warning "Failed to enable user lingering"
}

print_debug_info() {
    script_log "Printing configuration details..."
    echo "SCRIPT_DIR: $SCRIPT_DIR"
    echo "INSTALL_DIR: $INSTALL_DIR"
    echo "SYSTEMD_USER_DIR: $SYSTEMD_USER_DIR"
    echo "SERVICE_CONF_DIR: $SERVICE_CONF_DIR"
    echo "SERVICE_INCLUDE_DIR: $SERVICE_INCLUDE_DIR"
    echo "VENV_DIR: $VENV_DIR"
    
    if [[ -f "$VENV_DIR/bin/activate" ]]; then
        source "$VENV_DIR/bin/activate"
        echo "Python version: $(python --version 2>&1)"
        deactivate
    else
        echo "WARNING: Virtual environment not found!"
    fi
    
    echo "User: $(whoami)"
    echo "User services directory: $SYSTEMD_USER_DIR"
}

verify_systemd_setup() {
    script_log "Verifying systemd setup..."
    local has_issues=0
    
    if [[ ! -d "$SYSTEMD_USER_DIR" ]]; then
        error_exit "Systemd user directory not found: $SYSTEMD_USER_DIR"
        has_issues=1
    fi
    
    if [[ ! -d "$SERVICE_CONF_DIR" ]]; then
        error_exit "Service configuration directory not found: $SERVICE_CONF_DIR"
        has_issues=1
    fi
    
    if [[ ! -d "$VENV_DIR" ]]; then
        error_exit "Virtual environment not found: $VENV_DIR"
        has_issues=1
    fi
    
    echo ""
    echo "================================="
    if [[ $has_issues -eq 0 ]]; then
        echo "SYSTEMD SETUP COMPLETED SUCCESSFULLY!"
        echo "Installation directory: $INSTALL_DIR"
        echo "Service configuration: $SERVICE_CONF_DIR"
        echo "================================="
        return 0
    else
        echo "SYSTEMD SETUP COMPLETED WITH ISSUES"
        echo "Please check the logs above for more information."
        echo "================================="
        return 1
    fi
}

uninstall() {
    script_log "Starting uninstall process..."
    
    # Stop all user services
    script_log "Stopping all user services..."
    systemctl --user stop --all 2>/dev/null || true
    
    # Remove service files
    if [[ -d "$SERVICE_CONF_DIR" ]]; then
        script_log "Removing service configuration directory: $SERVICE_CONF_DIR"
        rm -rf "$SERVICE_CONF_DIR"
    fi
    
    # Remove virtual environment
    if [[ -d "$VENV_DIR" ]]; then
        script_log "Removing virtual environment: $VENV_DIR"
        rm -rf "$VENV_DIR"
    fi
    
    # Remove installation directory if empty
    if [[ -d "$INSTALL_DIR" ]] && [[ -z "$(ls -A "$INSTALL_DIR")" ]]; then
        script_log "Removing empty installation directory: $INSTALL_DIR"
        rmdir "$INSTALL_DIR" 2>/dev/null || true
    elif [[ -d "$INSTALL_DIR" ]]; then
        script_log "Installation directory is not empty, not removing: $INSTALL_DIR"
    fi
    
    reload_systemd
    
    script_log "Uninstall completed successfully."
    return 0
}

show_help() {
    cat << EOF
SystemdSetup.sh - Setup script for AppiumBootstrap on Linux with systemd

Usage: ./SystemdSetup.sh [INSTALL_DIR] [OPTIONS]

Arguments:
  INSTALL_DIR            Installation directory (default: \$HOME/.appium-bootstrap)

Options:
  -h, --help             Show this help message and exit
  -i, --install          Install systemd services (default action)
  -u, --uninstall        Uninstall everything previously installed
  -s, --status           Check status of installed components
  -d, --debug            Enable debug output
  -f, --force            Force reinstallation
  -p, --preserve         Preserve existing directories during installation

Examples:
  ./SystemdSetup.sh                          # Install to \$HOME/.appium-bootstrap
  ./SystemdSetup.sh /opt/appium-bootstrap            # Install to /opt/appium-bootstrap
  ./SystemdSetup.sh --uninstall              # Remove all components
  ./SystemdSetup.sh --debug --force          # Force reinstall with debug output

Environment variables:
  DEBUG=1                Enable debug output (same as --debug)
  FATAL_ERRORS=1         Exit on first error

EOF
}

main() {
    local errors=0
    local start_time
    start_time=$(date +%s)
    
    script_log "Starting systemd setup on $(date)"
    script_log "System: $(uname -m) | $(uname -s) $(uname -r)"
    script_log "User: $(whoami)"
    script_log "Installation directory: $INSTALL_DIR"
    
    check_system_resources || {
        error_exit "System resource check failed."
        return 1
    }
    
    if [[ $FORCE_INSTALL -eq 1 ]]; then
        script_log "Force reinstall requested. Cleaning up previous installation..."
        if [[ $PRESERVE_DIRS -ne 1 ]]; then
            rm -rf "$SERVICE_CONF_DIR" "$VENV_DIR" 2>/dev/null || true
        fi
    fi
    
    script_log "Setting up systemd directories..."
    setup_systemd_directories || {
        error_exit "Failed to setup systemd directories"
        errors=$((errors + 1))
    }
    
    script_log "Setting up Python environment..."
    check_installed_versions || true
    setup_python_environment || {
        error_exit "Failed to set up Python environment."
        errors=$((errors + 1))
    }
    
    enable_systemd_linger
    reload_systemd
    
    print_debug_info
    
    verify_systemd_setup || {
        script_log "ERROR: Final verification failed."
        errors=$((errors + 1))
    }
    
    local end_time
    end_time=$(date +%s)
    local elapsed=$((end_time - start_time))
    local minutes=$((elapsed / 60))
    local seconds=$((elapsed % 60))
    
    script_log "Setup completed in ${minutes}m ${seconds}s"
    
    if [[ $errors -eq 0 ]]; then
        echo ""
        echo "================================================================"
        echo "           SYSTEMD SETUP COMPLETED SUCCESSFULLY                 "
        echo "================================================================"
        echo "Summary of installation:"
        echo "- Python version: $($PYTHON_BIN --version 2>&1)"
        echo "- Installation directory: $INSTALL_DIR"
        echo "- Virtual environment: $VENV_DIR"
        echo "- Service directory: $SERVICE_CONF_DIR"
        echo "- User systemd directory: $SYSTEMD_USER_DIR"
        echo ""
        echo "To manage services, use:"
        echo "  systemctl --user start <service-name>"
        echo "  systemctl --user stop <service-name>"
        echo "  systemctl --user status <service-name>"
        echo "  systemctl --user list-units --type=service"
        echo ""
        echo "To uninstall, run:"
        echo "  $0 --uninstall"
        echo "================================================================"
        return 0
    else
        echo ""
        echo "================================================================"
        echo "           SYSTEMD SETUP COMPLETED WITH ERRORS                  "
        echo "================================================================"
        echo "Setup encountered $errors error(s)."
        echo "Please review the logs above for more information."
        echo ""
        echo "You can try again with debug output:"
        echo "  DEBUG=1 $0"
        echo "================================================================"
        return 1
    fi
}

# Parse arguments
ACTION="install"
FORCE_INSTALL=0
PRESERVE_DIRS=0

# Check if first argument is a path (doesn't start with -)
if [[ $# -gt 0 && ! "$1" =~ ^- ]]; then
    INSTALL_DIR="$1"
    shift
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)
            show_help
            exit 0
            ;;
        -u|--uninstall)
            ACTION="uninstall"
            shift
            ;;
        -s|--status)
            ACTION="status"
            shift
            ;;
        -d|--debug)
            set -x
            export DEBUG=1
            shift
            ;;
        -f|--force)
            FORCE_INSTALL=1
            shift
            ;;
        -p|--preserve)
            PRESERVE_DIRS=1
            shift
            ;;
        *)
            warning "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

case "$ACTION" in
    install)
        main
        ;;
    uninstall)
        uninstall
        ;;
    status)
        check_installed_versions
        verify_systemd_setup || true
        ;;
    *)
        error_exit "Invalid action: $ACTION"
        show_help
        exit 1
        ;;
esac
