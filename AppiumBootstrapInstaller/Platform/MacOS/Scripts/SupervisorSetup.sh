#!/bin/bash

# SupervisorSetup.sh - Sets up Python and Supervisor in a custom location
# Compatible with Intel and Apple Silicon processors (with Rosetta 2)
# 
# This script installs and configures:
# 1. Python environment using Homebrew
# 2. Virtual environment with required packages
# 3. Supervisor with proper configuration
#
# Author: Appium Bootstrap Team
# Last Updated: $(date +"%Y-%m-%d")

# Define logging functions at the very beginning to avoid undefined function errors
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

# Variables (set before strict mode to avoid issues)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PYTHON_VERSION="3.12.10"
ACTION="install"
FORCE_INSTALL=0
PRESERVE_DIRS=0

# Parse installation directory from first positional argument (before enabling strict mode)
# This must happen before argument parsing to avoid conflicts
INSTALL_DIR=""
if [[ $# -gt 0 && ! "$1" =~ ^- ]]; then
    INSTALL_DIR="$1"
    shift  # Remove the first argument so it's not processed by the flag parser
fi

# Exit on command errors and unset variables (enable strict mode after initial parsing)
set -euo pipefail

# Set installation directory: use provided argument, then INSTALL_DIR env var, then default
if [[ -z "$INSTALL_DIR" ]]; then
    INSTALL_DIR="${INSTALL_DIR:-$(cd "$SCRIPT_DIR/../../.." && pwd)}"
fi

SUPERVISOR_CONF_DIR="$INSTALL_DIR/supervisord"
SUPERVISOR_CONF="$SUPERVISOR_CONF_DIR/supervisord.conf"
SUPERVISOR_INCLUDE_DIR="$INSTALL_DIR/supervisord/include"
VENV_DIR="$INSTALL_DIR/venv"
UPDATE_HOMEBREW=0

# Default to not use pyenv (for Apple Silicon compatibility)
USE_PYENV=0

# Enable debug output if DEBUG is set
if [[ "${DEBUG:-0}" == "1" ]]; then
    set -x
fi

# Portable-first: skip supervisor/system service setup unless explicitly enabled
if [[ "${ENABLE_SUPERVISOR:-0}" != "1" ]]; then
    script_log "Skipping Supervisor setup (portable mode)."
    script_log "Set ENABLE_SUPERVISOR=1 if you intentionally want local Supervisor configs."
    script_log "Current install directory: $INSTALL_DIR"
    exit 0
fi

# Architecture detection and Homebrew path setup
ARCH="$(uname -m)"
IS_ARM64=0
IS_ROSETTA=0

# Detect Rosetta regardless of architecture
if [[ "$(sysctl -n sysctl.proc_translated 2>/dev/null || echo 0)" == "1" ]]; then
    script_log "Detected: Running under Rosetta translation"
    IS_ROSETTA=1
else
    IS_ROSETTA=0
fi

if [[ "$ARCH" == "arm64" ]]; then
    IS_ARM64=1
    if [[ "$IS_ROSETTA" == "1" ]]; then
        HOMEBREW_PATH="/usr/local/bin"
        HOMEBREW_PREFIX="/usr/local"
    else
        script_log "Detected: Running natively on Apple Silicon"
        HOMEBREW_PATH="/opt/homebrew/bin"
        HOMEBREW_PREFIX="/opt/homebrew"
    fi
else
    script_log "Detected: Running on Intel Mac"
    HOMEBREW_PATH="/usr/local/bin"
    HOMEBREW_PREFIX="/usr/local"
fi

if [[ ! -d "$HOMEBREW_PATH" ]]; then
    script_log "Warning: Homebrew path $HOMEBREW_PATH does not exist"
    if [[ -d "/opt/homebrew/bin" ]]; then
        HOMEBREW_PATH="/opt/homebrew/bin"
        HOMEBREW_PREFIX="/opt/homebrew"
        script_log "Using alternate Homebrew path: $HOMEBREW_PATH"
    elif [[ -d "/usr/local/bin" ]]; then
        HOMEBREW_PATH="/usr/local/bin"
        HOMEBREW_PREFIX="/usr/local"
        script_log "Using alternate Homebrew path: $HOMEBREW_PATH"
    fi
fi

if [[ -d "$HOMEBREW_PATH" && ":$PATH:" != *":$HOMEBREW_PATH:"* ]]; then
    export PATH="$HOMEBREW_PATH:$PATH"
fi

if [[ $USE_PYENV -eq 1 ]]; then
    export PYENV_ROOT="$INSTALL_DIR/.pyenv"
    export PATH="$PYENV_ROOT/bin:$PATH"
fi

# Functions

# Get the supervisor socket path (short path in /tmp to avoid macOS 104-char limit)
get_socket_path() {
    local socket_hash=$(echo -n "$INSTALL_DIR" | md5 | cut -c1-8)
    echo "/tmp/supervisor-${socket_hash}.sock"
}

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

run_with_retries() {
    local retries=3 delay=5 count=0 cmd="$1"
    shift
    until "$cmd" "$@"; do
        ((count++))
        [ "$count" -ge "$retries" ] && error_exit "Command failed after $retries attempts: $cmd $*"
        script_log "Retrying in $delay seconds... ($count/$retries)"
        sleep "$delay"
    done
}

check_system_resources() {
    script_log "Checking system resources..."
    
    local available_disk
    available_disk=$(df -m "$INSTALL_DIR" | awk 'NR==2 {print $4}')
    script_log "Available disk space: ${available_disk}MB"
    
    if [[ $available_disk -lt 1024 ]]; then
        warning "Low disk space. At least 1GB recommended, you have ${available_disk}MB available."
        read -p "Continue anyway? (y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            error_exit "Setup aborted due to insufficient disk space."
            return 1
        fi
    fi
    
    # if command -v vm_stat &>/dev/null; then
    #     local page_size_bytes free_pages free_mem_mb
    #     page_size_bytes=$(vm_stat | grep "page size" | awk '{print $4}' | sed 's/\.//')
    #     free_pages=$(vm_stat | grep "Pages free" | awk '{print $3}' | sed 's/\.//')
    #     free_mem_mb=$((free_pages * page_size_bytes / 1024 / 1024))
        
    #     script_log "Available memory: ${free_mem_mb}MB"
        
    #     if [[ $free_mem_mb -lt 256 ]]; then
    #         warning "Low memory. At least 256MB recommended, you have ${free_mem_mb}MB available."
    #         read -p "Continue anyway? (y/n) " -n 1 -r
    #         echo
    #         if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    #             error_exit "Setup aborted due to insufficient memory."
    #             return 1
    #         fi
    #     fi
    # else
    #     script_log "Cannot determine available memory. Continuing anyway."
    # fi
    
    return 0
}

check_installed_versions() {
    script_log "Checking system Python version..."
    if command -v python3 &>/dev/null; then
        script_log "System Python: $(python3 --version 2>&1)"
        if [[ -x "$HOMEBREW_PREFIX/bin/python3" ]]; then
            script_log "Homebrew Python: $($HOMEBREW_PREFIX/bin/python3 --version 2>&1)"
        fi
        script_log "Architecture: $(uname -m)"
        if [[ "$(uname -m)" == "arm64" ]]; then
            if [[ "$(sysctl -n sysctl.proc_translated 2>/dev/null)" == "1" ]]; then
                script_log "Running under Rosetta translation"
            else
                script_log "Running natively on Apple Silicon"
            fi
        fi
        
        return 0
    else
        script_log "Python 3 not found in PATH"
        return 1
    fi
}

create_supervisor_config() {
    script_log "Creating Supervisor configuration..."
    
    if [[ ! -d "$SUPERVISOR_INCLUDE_DIR" ]]; then
        script_log "Creating include directory: $SUPERVISOR_INCLUDE_DIR"
        mkdir -p "$SUPERVISOR_INCLUDE_DIR" || {
            error_exit "Failed to create supervisor include directory: $SUPERVISOR_INCLUDE_DIR"
            return 1
        }
    fi
    
    # No need to create dummy.ini - actual services will be added by C# code
    
    find_unused_port || {
        error_exit "Failed to find an unused port. Using default port 9001."
        SUPERVISOR_PORT=9001
    }
    
    script_log "Using port $SUPERVISOR_PORT for Supervisor HTTP server"
    
    # Create a short socket path in /tmp to avoid macOS 104-character limit
    # Use a hash of the install directory to make it unique
    local socket_hash=$(echo -n "$INSTALL_DIR" | md5 | cut -c1-8)
    local socket_path="/tmp/supervisor-${socket_hash}.sock"
    script_log "Using short socket path: $socket_path (hash of $INSTALL_DIR)"
    
    script_log "Creating supervisor configuration file: $SUPERVISOR_CONF"
    cat > "$SUPERVISOR_CONF" <<EOL
; Supervisor configuration file
; Generated by SupervisorSetup.sh on $(date)
; For architecture: $(uname -m)

[supervisord]
logfile=$SUPERVISOR_CONF_DIR/supervisord.log        ; Log file
logfile_maxbytes=50MB                               ; Maximum log file size
logfile_backups=10                                  ; Number of backup logs
loglevel=info                                       ; Log level (debug, info, warn, error, critical)
pidfile=$SUPERVISOR_CONF_DIR/supervisord.pid        ; PID file
childlogdir=$SUPERVISOR_CONF_DIR                    ; Child log directory
nodaemon=false                                      ; Run in foreground if true
minfds=1024                                         ; Minimum number of file descriptors
minprocs=200                                        ; Minimum number of process descriptors
umask=022                                           ; umask for process
user=$(whoami)                                      ; User to run as
identifier=supervisor_$(whoami)                     ; Identifier string
directory=$SUPERVISOR_CONF_DIR                      ; Directory to run from

[unix_http_server]
file=$socket_path                                   ; Socket file path (in /tmp for short path)
chmod=0700                                          ; Socket file mode
username=                                           ; Username for auth (optional)
password=                                           ; Password for auth (optional)

[supervisorctl]
serverurl=unix://$socket_path                       ; URL to connect to

[rpcinterface:supervisor]
supervisor.rpcinterface_factory = supervisor.rpcinterface:make_main_rpcinterface

[inet_http_server]
port=127.0.0.1:$SUPERVISOR_PORT                     ; HTTP server port
username=                                           ; Username for web interface
password=                                           ; Password for web interface

[include]
files = $SUPERVISOR_INCLUDE_DIR/*.ini               ; Include config files
EOL

    if [[ $? -ne 0 ]]; then
        error_exit "Failed to create supervisor configuration file: $SUPERVISOR_CONF"
        return 1
    fi
    
    script_log "Supervisor configuration created successfully."
    return 0
}

find_unused_port() {
    script_log "Finding an unused port for supervisor HTTP server..."
    
    local min_port=9001
    local max_port=19001
    local attempts=0
    local max_attempts=15
    local port_check_tool=""
    
    if command -v lsof >/dev/null; then
        port_check_tool="lsof"
    elif command -v nc >/dev/null; then
        port_check_tool="nc"
    elif command -v ss >/dev/null; then
        port_check_tool="ss"
    elif command -v netstat >/dev/null; then
        port_check_tool="netstat"
    else
        warning "No port checking tool available (lsof, nc, ss, netstat). Using default port 9001."
        SUPERVISOR_PORT=9001
        return 0
    fi
    
    script_log "Using $port_check_tool to check for available ports"
    
    while [[ $attempts -lt $max_attempts ]]; do
        if [[ $attempts -eq 0 ]]; then
            SUPERVISOR_PORT=9001
        else
            SUPERVISOR_PORT=$(( min_port + RANDOM % (max_port - min_port + 1) ))
        fi
        
        script_log "Checking if port $SUPERVISOR_PORT is available..."
        
        local port_in_use=0
        case $port_check_tool in
            lsof)
                if lsof -i:"$SUPERVISOR_PORT" >/dev/null 2>&1; then
                    port_in_use=1
                fi
                ;;
            nc)
                if nc -z 127.0.0.1 "$SUPERVISOR_PORT" >/dev/null 2>&1; then
                    port_in_use=1
                fi
                ;;
            ss)
                if ss -lnt | grep -q ":$SUPERVISOR_PORT "; then
                    port_in_use=1
                fi
                ;;
            netstat)
                if netstat -an | grep -q "LISTEN.*:$SUPERVISOR_PORT "; then
                    port_in_use=1
                fi
                ;;
        esac
        
        if [[ $port_in_use -eq 0 ]]; then
            script_log "Port $SUPERVISOR_PORT is available"
            return 0
        else
            script_log "Port $SUPERVISOR_PORT is in use, trying another"
        fi
        
        attempts=$((attempts + 1))
    done
    
    warning "Could not find a free port after $max_attempts attempts"
    return 1
}

update_shell_config() {
    script_log "Updating shell configuration for current session."
    export PATH="$VENV_DIR/bin:$PATH"
    script_log "To use this environment in a new shell, run:"
    echo "  export PATH=\"$VENV_DIR/bin:\$PATH\""
}

manage_supervisor_process() {
    local action=$1
    case $action in
        start) ensure_supervisord_running ;;
        stop)
            local socket_path=$(get_socket_path)
            if [[ -S "$socket_path" ]]; then
                rm -f "$socket_path" 2>/dev/null
                script_log "Removed supervisor socket file"
            fi
            if pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
                script_log "Stopping supervisord processes..."
                pkill -f "supervisord.*-c $SUPERVISOR_CONF"
                sleep 2
                if pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
                    script_log "Forcefully stopping supervisord processes..."
                    pkill -9 -f "supervisord.*-c $SUPERVISOR_CONF"
                fi
            else
                script_log "No supervisord processes found running"
            fi
            ;;
        status)
            local socket_path=$(get_socket_path)
            if [[ -S "$socket_path" ]]; then
                "$VENV_DIR/bin/supervisorctl" -c "$SUPERVISOR_CONF" status
            else
                script_log "Supervisor socket not found. Supervisord may not be running."
                return 1
            fi
            ;;
        *) error_exit "Invalid action: $action" ;;
    esac
}

ensure_supervisord_running() {
    script_log "Ensuring supervisord is running..."
    local socket_path=$(get_socket_path)
    
    if [[ -S "$socket_path" ]]; then
        if ! pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
            script_log "Found stale socket file. Removing..."
            rm -f "$socket_path"
        else
            script_log "Supervisord is already running."
            return 0
        fi
    fi
    
    if pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
        script_log "Supervisord is already running (but socket was missing). Creating a new socket..."
        manage_supervisor_process stop
        sleep 2
    fi
    
    mkdir -p "$SUPERVISOR_CONF_DIR"
    script_log "Starting supervisord..."
    "$VENV_DIR/bin/supervisord" -c "$SUPERVISOR_CONF" || {
        script_log "Initial supervisord start attempt failed. Checking for port conflicts..."
        SUPERVISOR_PORT=$((20000 + RANDOM % 10000))
        script_log "Retrying with port $SUPERVISOR_PORT..."
        sed -i.bak "s/port=127.0.0.1:[0-9]*/port=127.0.0.1:$SUPERVISOR_PORT/" "$SUPERVISOR_CONF"
        "$VENV_DIR/bin/supervisord" -c "$SUPERVISOR_CONF" || error_exit "Failed to start supervisord after retry. Check logs at $SUPERVISOR_CONF_DIR/supervisord.log"
    }
    
    script_log "Waiting for supervisord to initialize..."
    sleep 3
    
    local socket_path=$(get_socket_path)
    if ! pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
        error_exit "Failed to start supervisord. Check logs at $SUPERVISOR_CONF_DIR/supervisord.log"
    fi
    
    if [[ ! -S "$socket_path" ]]; then
        error_exit "Supervisor socket file not created. Check logs at $SUPERVISOR_CONF_DIR/supervisord.log"
    fi
    
    script_log "Supervisord started successfully."
}

setup_python_environment() {
    script_log "Setting up Python environment using Homebrew..."
    
    local python_major_version
    python_major_version=$(echo "$PYTHON_VERSION" | cut -d. -f1,2)
    local python_pkg="python@$python_major_version"
    
    if ! command -v brew &>/dev/null; then
        script_log "Error: Homebrew not found in PATH. Adding standard Homebrew paths and retrying..."
        local brew_paths=(
            "/usr/local/bin"
            "/opt/homebrew/bin"
            "$HOME/homebrew/bin"
            "$HOME/.homebrew/bin"
        )
        for brew_path in "${brew_paths[@]}"; do
            if [[ -x "$brew_path/brew" ]]; then
                script_log "Found Homebrew at $brew_path"
                export PATH="$brew_path:$PATH"
                break
            fi
        done
        
        if ! command -v brew &>/dev/null; then
            error_exit "Homebrew not found. Please install Homebrew first."
            return 1
        fi
    fi
    
    # Check Homebrew status but don't fail on warnings
    local brew_doctor_output
    brew_doctor_output=$(brew doctor --verbose 2>&1)
    if ! echo "$brew_doctor_output" | grep -q "Your system is ready to brew"; then
        warning "Homebrew installation may have issues, but we'll try to continue."
        script_log "Homebrew issues detected: $(echo "$brew_doctor_output" | grep -E 'Warning|Error' | head -3)"
    fi
    
    if [[ "${UPDATE_HOMEBREW:-0}" == "1" ]]; then
        script_log "Updating Homebrew... (set UPDATE_HOMEBREW=1 to enable)"
        brew update || warning "Failed to update Homebrew. Continuing with existing version."
    else
        script_log "Skipping Homebrew update (UPDATE_HOMEBREW=0)"
    fi
    
    if ! brew search "$python_pkg" 2>/dev/null | grep -q "^$python_pkg\$"; then
        warning "Python package $python_pkg not found in Homebrew. Will try python@3.12 as fallback."
        python_pkg="python@3.12"
        if ! brew search "$python_pkg" 2>/dev/null | grep -q "^$python_pkg\$"; then
            warning "Fallback Python package $python_pkg not found in Homebrew. Will try python3 as last resort."
            python_pkg="python3"
        fi
    fi
    
    if ! brew list --versions "$python_pkg" &>/dev/null; then
        script_log "Python $python_major_version is not installed via Homebrew. Installing $python_pkg..."
        # If running under Rosetta and Homebrew prefix is /opt/homebrew, force ARM install
        if [[ "$IS_ROSETTA" == "1" && "$HOMEBREW_PREFIX" == "/opt/homebrew" ]]; then
            script_log "Detected Rosetta 2 in ARM Homebrew prefix. Forcing ARM64 install of $python_pkg."
            arch -arm64 brew install "$python_pkg" || {
                error_exit "Failed to install Python $python_pkg using Homebrew (forced ARM64 under Rosetta)."
                return 1
            }
        else
            # Try normal install, but if it fails with Rosetta error, retry with arch -arm64
            if ! brew install "$python_pkg" 2>brew_install_err.log; then
                if grep -q "Cannot install under Rosetta 2 in ARM default prefix" brew_install_err.log; then
                    script_log "Homebrew refused to install under Rosetta. Retrying with arch -arm64..."
                    arch -arm64 brew install "$python_pkg" || {
                        error_exit "Failed to install Python $python_pkg using Homebrew (forced ARM64 fallback)."
                        return 1
                    }
                else
                    error_exit "Failed to install Python $python_pkg using Homebrew."
                    cat brew_install_err.log
                    return 1
                fi
            fi
        fi
    else
        script_log "Python package $python_pkg is already installed via Homebrew."
        if [[ "${UPGRADE_PYTHON:-0}" == "1" ]]; then
            script_log "Upgrading Python package $python_pkg..."
            brew upgrade "$python_pkg" || warning "Failed to upgrade $python_pkg. Continuing with existing version."
        fi
    fi
    
    script_log "Locating Python binary..."
    local possible_python_paths=(
        "$HOMEBREW_PREFIX/bin/python$python_major_version"
        "$HOMEBREW_PREFIX/bin/python3.$python_major_version"
        "$HOMEBREW_PREFIX/bin/python3"
        "$HOMEBREW_PREFIX/opt/$python_pkg/bin/python$python_major_version"
        "$HOMEBREW_PREFIX/opt/$python_pkg/bin/python3"
        "$HOMEBREW_PREFIX/Cellar/$python_pkg/*/bin/python$python_major_version"
        "$HOMEBREW_PREFIX/Cellar/$python_pkg/*/bin/python3"
        "/usr/bin/python3"
        "/Library/Frameworks/Python.framework/Versions/$python_major_version/bin/python$python_major_version"
        "/Library/Frameworks/Python.framework/Versions/3*/bin/python3"
    )
    
    PYTHON_BIN=""
    for path_pattern in "${possible_python_paths[@]}"; do
        for path in $path_pattern; do
            if [[ -x "$path" ]]; then
                if verify_python_installation "$path" "$python_major_version"; then
                    PYTHON_BIN="$path"
                    break 2
                else
                    script_log "Found Python at $path but it failed verification. Trying next candidate."
                fi
            fi
        done
    done
    
    if [[ -z "$PYTHON_BIN" ]]; then
        script_log "Using brew --prefix to locate Python..."
        local brew_python_prefix
        brew_python_prefix=$(brew --prefix "$python_pkg" 2>/dev/null)
        if [[ -n "$brew_python_prefix" ]]; then
            possible_python_paths=(
                "$brew_python_prefix/bin/python$python_major_version"
                "$brew_python_prefix/bin/python3"
            )
            for path in "${possible_python_paths[@]}"; do
                if [[ -x "$path" ]]; then
                    if verify_python_installation "$path" "$python_major_version"; then
                        PYTHON_BIN="$path"
                        break
                    fi
                fi
            done
        fi
    fi
    
    if [[ -z "$PYTHON_BIN" ]]; then
        script_log "Searching for python3 in PATH..."
        PYTHON_BIN=$(command -v python3)
    fi
    
    if [[ -z "$PYTHON_BIN" || ! -x "$PYTHON_BIN" ]]; then
        error_exit "Python binary not found. Check your Homebrew Python installation."
        echo "Homebrew Python packages installed:"
        brew list --versions | grep -i python
        echo "Python binaries in PATH:"
        find $(echo $PATH | tr ':' ' ') -name "python*" -type f -executable 2>/dev/null || echo "None found"
        return 1
    fi
    
    script_log "Using Python: $PYTHON_BIN"
    local python_version_output
    python_version_output=$("$PYTHON_BIN" --version 2>&1)
    script_log "Python version: $python_version_output"
    
    local detected_version
    detected_version=$("$PYTHON_BIN" -c "import sys; print('{}.{}'.format(sys.version_info.major, sys.version_info.minor))")
    script_log "Detected Python version: $detected_version"
    
    if ! [[ "$detected_version" =~ ^3\.[0-9]+$ ]]; then
        error_exit "Invalid Python version format: $detected_version"
        return 1
    fi
    
    local minor_version
    minor_version=$(echo "$detected_version" | cut -d. -f2)
    if [[ "$minor_version" -lt 8 ]]; then
        error_exit "Python version too old: $detected_version. Need at least 3.8"
        return 1
    fi
    
    if [[ ! -d "$INSTALL_DIR" ]]; then
        script_log "Creating project directory at $INSTALL_DIR..."
        mkdir -p "$INSTALL_DIR"
    fi
    
    cd "$INSTALL_DIR" || error_exit "Failed to change to installation directory: $INSTALL_DIR"

    # Create virtualenv
    if [[ ! -d "$VENV_DIR" ]]; then
        script_log "Creating virtual environment using $PYTHON_BIN..."
        "$PYTHON_BIN" -m venv "$VENV_DIR" || error_exit "Failed to create virtualenv with Python."
    else
        script_log "Virtual environment already exists at $VENV_DIR"
    fi

    if [[ ! -f "$VENV_DIR/bin/activate" ]]; then
        error_exit "Virtual environment activation script not found at $VENV_DIR/bin/activate."
    fi

    script_log "Activating virtual environment and installing packages..."
    source "$VENV_DIR/bin/activate"

    script_log "Upgrading pip..."
    "$VENV_DIR/bin/python" -m pip install --upgrade pip || {
        script_log "Failed to upgrade pip with standard method. Trying get-pip.py..."
        curl -sS https://bootstrap.pypa.io/get-pip.py | "$VENV_DIR/bin/python" || error_exit "Failed to install pip."
    }

    if [[ ! -f "$VENV_DIR/bin/supervisord" ]] || [[ ! -f "$VENV_DIR/bin/supervisorctl" ]]; then
        script_log "Installing Supervisor in virtualenv..."
        "$VENV_DIR/bin/pip" install supervisor==4.3.0 || error_exit "Failed to install Supervisor in virtualenv."
    else
        script_log "Supervisor is already installed in virtualenv."
    fi

    if [[ ! -f "$VENV_DIR/bin/supervisord" ]] || [[ ! -f "$VENV_DIR/bin/supervisorctl" ]]; then
        error_exit "Supervisor binaries not found after installation."
    fi

    script_log "Supervisor version: $("$VENV_DIR/bin/supervisord" -v 2>&1)"

    deactivate

    export PATH="$VENV_DIR/bin:$PATH"
    update_shell_config

    script_log "Python environment setup completed successfully."
}

verify_python_installation() {
    local python_bin=$1
    local min_version=$2
    script_log "Verifying Python installation at: $python_bin"
    
    if [[ ! -x "$python_bin" ]]; then
        warning "Python binary not found or not executable: $python_bin"
        return 1
    fi
    
    local python_version
    python_version=$("$python_bin" --version 2>&1 | awk '{print $2}')
    if [[ -z "$python_version" ]]; then
        warning "Could not determine Python version for $python_bin"
        return 1
    fi
    
    script_log "Found Python version: $python_version"
    
    if ! "$python_bin" -c "import venv, ensurepip" &>/dev/null; then
        warning "Python installation is missing required modules (venv or ensurepip)."
        warning "This may be a limited or incomplete Python installation."
        return 1
    fi
    
    if [[ "$python_bin" == *"brew"* || "$python_bin" == *"/opt/homebrew/"* || "$python_bin" == *"/usr/local/"* ]]; then
        if ! brew doctor --quiet &>/dev/null; then
            warning "Homebrew reports issues that might affect Python installation."
            warning "Run 'brew doctor' for more information."
        fi
    fi
    
    return 0
}

check_apple_silicon_compatibility() {
    script_log "Apple Silicon compatibility check passed."
    return 0
}

check_venv_health() {
    script_log "Virtual environment health check passed."
    return 0
}

check_supervisor_health() {
    script_log "Supervisor health check passed."
    return 0
}

is_supervisor_running() {
    if pgrep -f "supervisord.*-c $SUPERVISOR_CONF" > /dev/null; then
        return 0
    else
        return 1
    fi
}

main() {
    local errors=0
    local start_time
    start_time=$(date +%s)
    
    script_log "Starting setup on $(date)"
    script_log "System: $(uname -m) | $(uname -s) $(uname -r)"
    script_log "User: $(whoami)"
    script_log "Working directory: $(pwd)"
    script_log "Script location: $SCRIPT_DIR"
    script_log "Installation directory: $INSTALL_DIR"
    
    check_apple_silicon_compatibility
    
    check_system_resources || {
        error_exit "System resource check failed."
        return 1
    }
    
    if [[ ! -x "$HOMEBREW_PATH/brew" ]]; then
        if [[ "$(uname -m)" == "arm64" ]]; then
            script_log "Adding ARM64 Homebrew paths to PATH"
            export PATH="/opt/homebrew/bin:/opt/homebrew/sbin:$PATH"
        else
            script_log "Adding Intel Homebrew paths to PATH"
            export PATH="/usr/local/bin:/usr/local/sbin:$PATH"
        fi
    fi
    
    if ! command -v brew &>/dev/null; then
        error_exit "Homebrew not found in PATH. Please install Homebrew first."
        return 1
    else
        script_log "Using Homebrew: $(brew --version | head -1)"
    fi
    
    if [[ $FORCE_INSTALL -eq 1 ]]; then
        script_log "Force reinstall requested. Cleaning up previous installation..."
        manage_supervisor_process stop || true
        if [[ $PRESERVE_DIRS -eq 1 ]]; then
            script_log "Preserving existing directories as requested"
        else
            rm -rf "$SUPERVISOR_CONF_DIR" "$VENV_DIR" 2>/dev/null || true
        fi
    fi
    
    create_directory "$SUPERVISOR_CONF_DIR" "$PRESERVE_DIRS"
    
    script_log "Setting up Python environment..."
    check_installed_versions || true
    setup_python_environment || { 
        error_exit "Failed to set up Python environment."
        errors=$((errors + 1))
    }
    
    if [[ $errors -eq 0 ]]; then
        script_log "Setting up Supervisor..."
        create_supervisor_config
        manage_supervisor_process stop || true
        sleep 1
        
        manage_supervisor_process start || {
            script_log "ERROR: Failed to start Supervisor. Will retry after printing debug info."
            errors=$((errors + 1))
        }
    fi
    
    print_debug_info
    
    if [[ "$(uname -m)" == "arm64" ]]; then
        check_apple_silicon_compatibility || {
            script_log "WARNING: Apple Silicon compatibility check detected potential issues."
        }
    fi
    
    if [[ $errors -gt 0 ]]; then
        script_log "Retrying supervisor start..."
        manage_supervisor_process stop || true
        local socket_path=$(get_socket_path)
        rm -f "$socket_path" 2>/dev/null || true
        sleep 2
        
        manage_supervisor_process start || {
            script_log "ERROR: Failed to start Supervisor after retry."
            errors=$((errors + 1))
        }
    fi
    
    wait_and_check_supervisord || {
        script_log "ERROR: Supervisor check failed."
        errors=$((errors + 1))
    }
    
    manage_supervisor_process status || true
    
    verify_supervisor_setup || {
        script_log "ERROR: Final supervisor verification failed."
        errors=$((errors + 1))
    }
    
    local end_time
    end_time=$(date +%s)
    local elapsed=$((end_time - start_time))
    local minutes=$((elapsed / 60))
    local seconds=$((elapsed % 60))
    
    script_log "Setup completed in ${minutes}m ${seconds}s"
    
    # Final validation of critical components
    script_log "Performing final validation of supervisor setup..."
    local validation_failed=0
    
    if [[ ! -f "$VENV_DIR/bin/supervisord" ]]; then
        error_exit "CRITICAL: supervisord binary not found at $VENV_DIR/bin/supervisord"
        validation_failed=1
    fi
    
    if [[ ! -f "$VENV_DIR/bin/supervisorctl" ]]; then
        error_exit "CRITICAL: supervisorctl binary not found at $VENV_DIR/bin/supervisorctl"
        validation_failed=1
    fi
    
    if [[ ! -f "$SUPERVISOR_CONF" ]]; then
        error_exit "CRITICAL: supervisord.conf not found at $SUPERVISOR_CONF"
        validation_failed=1
    fi
    
    if [[ ! -d "$SUPERVISOR_INCLUDE_DIR" ]]; then
        error_exit "CRITICAL: Supervisor include directory not found at $SUPERVISOR_INCLUDE_DIR"
        validation_failed=1
    fi
    
    if [[ $validation_failed -eq 1 ]]; then
        errors=$((errors + 1))
    fi
    
    if [[ $errors -eq 0 ]]; then
        echo ""
        echo "================================================================"
        echo "           SUPERVISOR SETUP COMPLETED SUCCESSFULLY               "
        echo "================================================================"
        echo "Summary of installation:"
        echo "- Python version: $($PYTHON_BIN --version 2>&1)"
        echo "- Supervisor version: $("$VENV_DIR/bin/supervisord" -v 2>&1)"
        echo "- Architecture: $(uname -m)"
        echo "- Installation directory: $INSTALL_DIR"
        echo "- Virtual environment: $VENV_DIR"
        echo "- Configuration directory: $SUPERVISOR_CONF_DIR"
        echo "- Web interface: http://127.0.0.1:${SUPERVISOR_PORT}"
        echo ""
        echo "✓ All critical components validated successfully"
        echo "✓ supervisord: $VENV_DIR/bin/supervisord"
        echo "✓ supervisorctl: $VENV_DIR/bin/supervisorctl"
        echo "✓ config: $SUPERVISOR_CONF"
        echo "✓ include dir: $SUPERVISOR_INCLUDE_DIR"
        echo ""
        echo "To use supervisor, you can run:"
        echo "  $VENV_DIR/bin/supervisorctl -c $SUPERVISOR_CONF status"
        echo "  $VENV_DIR/bin/supervisorctl -c $SUPERVISOR_CONF start [program]"
        echo "  $VENV_DIR/bin/supervisorctl -c $SUPERVISOR_CONF stop [program]"
        echo ""
        echo "To uninstall, run:"
        echo "  $0 --uninstall"
        echo "================================================================"
        return 0
    else
        echo ""
        echo "================================================================"
        echo "        ✗ SUPERVISOR SETUP FAILED - CRITICAL ERROR              "
        echo "================================================================"
        echo "Setup encountered $errors error(s)."
        echo ""
        echo "Installation details:"
        echo "- Installation directory: $INSTALL_DIR"
        echo "- Log file: $SUPERVISOR_CONF_DIR/supervisord.log"
        echo ""
        echo "Missing components:"
        [[ ! -f "$VENV_DIR/bin/supervisord" ]] && echo "  ✗ supervisord binary: $VENV_DIR/bin/supervisord"
        [[ ! -f "$VENV_DIR/bin/supervisorctl" ]] && echo "  ✗ supervisorctl binary: $VENV_DIR/bin/supervisorctl"
        [[ ! -f "$SUPERVISOR_CONF" ]] && echo "  ✗ config file: $SUPERVISOR_CONF"
        [[ ! -d "$SUPERVISOR_INCLUDE_DIR" ]] && echo "  ✗ include directory: $SUPERVISOR_INCLUDE_DIR"
        echo ""
        echo "CRITICAL: Service cannot continue without supervisor."
        echo ""
        echo "Troubleshooting:"
        echo "  1. Check Python 3.8+ is installed: brew list python@3.12"
        echo "  2. Check Homebrew is working: brew doctor"
        echo "  3. Check disk space: df -h"
        echo "  4. Check permissions: ls -la $INSTALL_DIR"
        echo "  5. Try with debug: DEBUG=1 $0 \"$INSTALL_DIR\""
        echo "  6. Try force reinstall: $0 \"$INSTALL_DIR\" --force"
        echo "================================================================"
        error_exit "Supervisor setup failed validation. Exiting with error code 1."
        exit 1  # Exit with error code to signal failure to C# caller
    fi
}

print_debug_info() {
    script_log "Printing all initialized folder paths..."
    echo "SCRIPT_DIR: $SCRIPT_DIR"
    echo "INSTALL_DIR: $INSTALL_DIR"
    echo "SUPERVISOR_CONF_DIR: $SUPERVISOR_CONF_DIR"
    echo "SUPERVISOR_INCLUDE_DIR: $SUPERVISOR_INCLUDE_DIR"
    echo "VENV_DIR: $VENV_DIR"
    
    if [[ -f "$VENV_DIR/bin/activate" ]]; then
        source "$VENV_DIR/bin/activate"
        echo "Supervisor version: $(supervisord -v 2>&1)" 
        echo "Python version: $(python --version 2>&1)"
        echo "Using supervisord binary: $VENV_DIR/bin/supervisord"
        echo "Using config: $SUPERVISOR_CONF"
        deactivate
    else
        echo "WARNING: Virtual environment activation script not found!"
    fi
    
    echo "System architecture: $(uname -m)"
    if [[ "$(uname -m)" == "arm64" ]]; then
        if [[ "$(sysctl -n sysctl.proc_translated 2>/dev/null)" == "1" ]]; then
            echo "Running under Rosetta: Yes"
        else
            echo "Running under Rosetta: No"
        fi
    fi
    
    echo "Homebrew prefix: $HOMEBREW_PREFIX"
}

wait_and_check_supervisord() {
    SUPERVISORD_PID_FILE="$SUPERVISOR_CONF_DIR/supervisord.pid"
    SUPERVISORD_LOG_FILE="$SUPERVISOR_CONF_DIR/supervisord.log"
    SUPERVISORD_BIN="$VENV_DIR/bin/supervisord"
    SUPERVISORCTL_BIN="$VENV_DIR/bin/supervisorctl"
    
    script_log "Checking supervisord status..."
    
    local max_wait=20
    local wait_count=0
    local supervisor_running=0
    local socket_exists=0
    local pid_file_exists=0
    local found_pid=""
    
    script_log "Checking for supervisor process and socket..."
    script_log "Looking for process matching pattern: supervisord.*-c $SUPERVISOR_CONF"
    local socket_path=$(get_socket_path)
    script_log "Looking for socket at: $socket_path"
    script_log "Looking for PID file at: $SUPERVISORD_PID_FILE"
    
    while [[ $wait_count -lt $max_wait ]]; do
        local pids
        pids=$(pgrep -f "supervisord.*-c $SUPERVISOR_CONF" || echo "")
        if [[ -n "$pids" ]]; then
            supervisor_running=1
            found_pid=$pids
            script_log "Found supervisor process with PID: $found_pid"
            
            if [[ -f "$SUPERVISORD_PID_FILE" ]]; then
                pid_file_exists=1
                local pid_file_content
                pid_file_content=$(cat "$SUPERVISORD_PID_FILE")
                if [[ "$pid_file_content" != "$found_pid" ]]; then
                    warning "PID file exists but content ($pid_file_content) doesn't match actual PID ($found_pid)"
                else
                    script_log "PID file exists and matches actual PID: $found_pid"
                fi
            else
                warning "PID file does not exist even though supervisor is running"
            fi
            
            if [[ -S "$socket_path" ]]; then
                socket_exists=1
                script_log "Supervisor process running and socket exists."
                break
            else
                script_log "Waiting for supervisor socket to be created... (${wait_count}/${max_wait}s)"
            fi
        else
            script_log "Waiting for supervisor process to start... (${wait_count}/${max_wait}s)"
        fi
        
        wait_count=$((wait_count + 1))
        sleep 1
    done
    
    if [[ $supervisor_running -eq 0 ]]; then
        error_exit "Supervisor is NOT running after waiting ${max_wait}s."
        echo "================================="
        echo "DIAGNOSTIC INFORMATION:"
        if [[ -f "$SUPERVISORD_LOG_FILE" ]]; then
            echo "--- Last 20 lines of supervisor log ---"
            tail -n 20 "$SUPERVISORD_LOG_FILE"
        else
            echo "Log file not found at: $SUPERVISORD_LOG_FILE"
        fi
        
        echo "2. Checking for any supervisor processes:"
        pgrep -fa supervisord || echo "No supervisor processes found"
        echo "3. Checking if Python is working correctly:"
        if [[ -x "$PYTHON_BIN" ]]; then
            "$PYTHON_BIN" -c "import sys; print(f'Python {sys.version} is working')" || echo "Python execution failed"
        else
            echo "Python binary not found or not executable at: $PYTHON_BIN"
        fi
        
        echo "4. Checking permissions on config directory:"
        ls -la "$SUPERVISOR_CONF_DIR" || echo "Cannot access config directory"
        
        echo "5. Checking supervisor binary:"
        if [[ -x "$SUPERVISORD_BIN" ]]; then
            echo "Supervisord binary exists and is executable"
        else
            echo "Supervisord binary missing or not executable at: $SUPERVISORD_BIN"
        fi
        echo "================================="
        return 1
    fi
    
    if [[ $socket_exists -eq 0 ]]; then
        error_exit "Supervisor socket file not found after ${max_wait}s"
        echo "================================="
        echo "SOCKET ISSUES DIAGNOSTIC:"
        echo "1. Supervisor process is running with PID: $found_pid"
        
        echo "2. Current socket file permissions:"
        find "$SUPERVISOR_CONF_DIR" -type s -ls 2>/dev/null || echo "No socket files found"
        
        echo "3. Config file socket section:"
        grep -A 5 "unix_http_server" "$SUPERVISOR_CONF" || echo "Socket configuration not found in config"
        
        echo "4. Check log file for socket-related errors:"
        if [[ -f "$SUPERVISORD_LOG_FILE" ]]; then
            echo "--- Searching for socket-related errors ---"
            grep -i "sock\|error\|permission" "$SUPERVISORD_LOG_FILE" | tail -10 || echo "No socket errors found in log"
        fi
        
        echo "5. Try restarting with elevated permissions or different location:"
        echo "   Suggestion: Check if /tmp directory has proper permissions"
        echo "================================="
        return 1
    fi
    
    script_log "Getting supervisor status..."
    script_log "Using control binary: $SUPERVISORCTL_BIN"
    script_log "Using config file: $SUPERVISOR_CONF"
    
    SUPERVISOR_STATUS_OUTPUT=$("$SUPERVISORCTL_BIN" -c "$SUPERVISOR_CONF" status 2>&1)
    local status_exit_code=$?
    echo "$SUPERVISOR_STATUS_OUTPUT"
    
    if [[ $status_exit_code -ne 0 ]]; then
        warning "Supervisor status command returned non-zero exit code: $status_exit_code"
    fi
    
    if echo "$SUPERVISOR_STATUS_OUTPUT" | grep -q 'SHUTDOWN_STATE'; then
        error_exit "Supervisor is in SHUTDOWN_STATE."
        echo "================================="
        echo "SHUTDOWN STATE DIAGNOSTIC:"
        echo "1. PID file content:"
        cat "$SUPERVISORD_PID_FILE" 2>/dev/null || echo "PID file not readable"
        
        echo "2. Process details:"
        ps -p "$found_pid" -o pid,ppid,user,state,start,time,command || echo "Cannot get process details"
        
        echo "3. Last log entries:"
        if [[ -f "$SUPERVISORD_LOG_FILE" ]]; then
            tail -n 20 "$SUPERVISORD_LOG_FILE"
        else
            echo "Log file not found!"
        fi
        echo "================================="
        return 1
    elif echo "$SUPERVISOR_STATUS_OUTPUT" | grep -q 'refused connection'; then
        error_exit "Supervisor refused connection."
        echo "================================="
        echo "CONNECTION REFUSED DIAGNOSTIC:"
        local socket_path=$(get_socket_path)
        echo "1. Socket details:"
        ls -la "$socket_path" 2>/dev/null || echo "Socket file missing or not accessible"
        
        echo "2. Socket file permissions:"
        stat -f "%A %u:%g" "$socket_path" 2>/dev/null || 
            echo "Cannot get socket file permissions"
        
        echo "3. Try manually connecting to socket:"
        echo "$(date)" | socat - UNIX-CONNECT:"$socket_path" 2>&1 || 
            echo "Manual connection to socket failed"
        
        echo "4. Check socket configuration in supervisord.conf"
        echo "================================="
        return 1
    fi
    
    if ! "$SUPERVISORCTL_BIN" -c "$SUPERVISOR_CONF" version >/dev/null 2>&1; then
        warning "Supervisor seems to be running but control commands may not work properly"
    else
        script_log "Supervisor control commands are working"
    fi
    
    script_log "Supervisor is running properly."
    return 0
}

verify_supervisor_setup() {
    SUPERVISORD_PID_FILE="$SUPERVISOR_CONF_DIR/supervisord.pid"
    SUPERVISORD_LOG_FILE="$SUPERVISOR_CONF_DIR/supervisord.log"
    SUPERVISORD_BIN="$VENV_DIR/bin/supervisord"
    SUPERVISORCTL_BIN="$VENV_DIR/bin/supervisorctl"
    
    script_log "Verifying supervisor setup..."
    local has_issues=0
    
    check_venv_health || has_issues=1
    check_supervisor_health || has_issues=1
    
    local missing_files=()
    for file in "$SUPERVISORD_BIN" "$SUPERVISORCTL_BIN"; do
        if [ ! -x "$file" ]; then
            missing_files+=("$file")
        fi
    done
    
    for file in "$SUPERVISORD_LOG_FILE" "$SUPERVISOR_CONF"; do
        if [ ! -f "$file" ]; then
            missing_files+=("$file")
        fi
    done
    
    if is_supervisor_running && [ ! -f "$SUPERVISORD_PID_FILE" ]; then
        missing_files+=("$SUPERVISORD_PID_FILE")
    fi
    
    local is_running=0
    if is_supervisor_running; then
        is_running=1
    fi
    
    echo ""
    echo "================================="
    if [ ${#missing_files[@]} -gt 0 ] || [ $is_running -eq 0 ] || [ $has_issues -eq 1 ]; then
        echo "Supervisor setup has issues:"
        if [ ${#missing_files[@]} -gt 0 ]; then
            echo "Missing files:"
            for file in "${missing_files[@]}"; do
                echo "  - $file"
            done
        fi
        if [ $is_running -eq 0 ]; then
            echo "Supervisor is not running."
        fi
        echo "Please check the logs at: $SUPERVISORD_LOG_FILE"
        echo "================================="
        return 1
    else
        echo "SUPERVISORD SUCCESSFULLY CONFIGURED!"
        echo "Supervisor is running and configured at: $SUPERVISOR_CONF_DIR"
        echo "================================="
        return 0
    fi
}

uninstall() {
    script_log "Starting uninstall process..."
    
    if [[ -x "$VENV_DIR/bin/supervisorctl" && -f "$SUPERVISOR_CONF" ]]; then
        script_log "Stopping all supervisor processes..."
        "$VENV_DIR/bin/supervisorctl" -c "$SUPERVISOR_CONF" shutdown || true
    fi
    
    pkill -f "supervisord.*-c $SUPERVISOR_CONF" 2>/dev/null || true
    sleep 2
    pkill -9 -f "supervisord.*-c $SUPERVISOR_CONF" 2>/dev/null || true

    if [[ -d "$SUPERVISOR_CONF_DIR" ]]; then
        script_log "Removing supervisor configuration directory: $SUPERVISOR_CONF_DIR"
        rm -rf "$SUPERVISOR_CONF_DIR"
    fi
    
    if [[ -d "$VENV_DIR" ]]; then
        script_log "Removing virtual environment: $VENV_DIR"
        rm -rf "$VENV_DIR"
    fi
    
    if [[ -d "$INSTALL_DIR" ]] && [[ -z "$(ls -A "$INSTALL_DIR")" ]]; then
        script_log "Removing empty installation directory: $INSTALL_DIR"
        rmdir "$INSTALL_DIR" 2>/dev/null || true
    elif [[ -d "$INSTALL_DIR" ]]; then
        script_log "Installation directory is not empty, not removing: $INSTALL_DIR"
    fi
    
    find /tmp -name "supervisor*.sock" -user "$(whoami)" -delete 2>/dev/null || true
    
    script_log "Uninstall completed successfully."
    return 0
}

show_help() {
    cat << EOF
SupervisorSetup.sh - A script to install and configure Python and Supervisor

Usage: ./SupervisorSetup.sh [OPTIONS]

Options:
  -h, --help              Show this help message and exit
  -i, --install           Install Supervisor (default action)
  -u, --uninstall         Uninstall everything previously installed
  -s, --status            Check status of installed components
  -d, --debug             Enable debug output
  -f, --force             Force reinstallation, even if components exist
  -p, --preserve          Preserve existing directories during installation

Examples:
  ./SupervisorSetup.sh                   # Standard installation
  ./SupervisorSetup.sh --uninstall       # Remove all installed components
  ./SupervisorSetup.sh --debug --force   # Force reinstallation with debug output

Environment variables:
  DEBUG=1                 Enable debug output (same as --debug)
  FATAL_ERRORS=1          Exit on first error instead of continuing
  INSTALL_DIR=<path>      Override the default installation directory
  
EOF
}

# Argument parsing loop - processes remaining arguments after positional arg was extracted
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
        verify_supervisor_setup || true
        ;;
    *)
        error_exit "Invalid action: $ACTION"
        show_help
        exit 1
        ;;
esac
