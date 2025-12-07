#!/bin/bash

# portforward.sh - Linux version
# Forward ports for iOS devices using iproxy (libimobiledevice)

if [ "$#" -lt 3 ]; then
    echo "Usage: $0 <udid> <local_port> <device_port>"
    exit 1
fi

UDID=$1
LOCAL_PORT=$2
DEVICE_PORT=$3

echo "Starting port forward for device $UDID: localhost:$LOCAL_PORT -> device:$DEVICE_PORT"

# Check if libimobiledevice is installed
if ! command -v iproxy &>/dev/null; then
    echo "Error: iproxy (libimobiledevice) not found"
    echo "Please install libimobiledevice-utils:"
    echo "  Ubuntu/Debian: sudo apt-get install libimobiledevice-utils"
    echo "  Fedora: sudo dnf install libimobiledevice-utils"
    echo "  Arch: sudo pacman -S libimobiledevice"
    exit 1
fi

# Check if device is connected
if ! idevice_id -l | grep -q "$UDID"; then
    echo "Error: Device $UDID not found"
    echo "Connected devices:"
    idevice_id -l
    exit 1
fi

# Stop existing port forward if running
if lsof -Pi :$LOCAL_PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo "Stopping existing port forward on port $LOCAL_PORT..."
    lsof -Pi :$LOCAL_PORT -sTCP:LISTEN -t | xargs kill -9 2>/dev/null || true
    sleep 1
fi

# Start port forwarding
echo "Starting iproxy..."
exec iproxy -u $UDID $LOCAL_PORT $DEVICE_PORT
