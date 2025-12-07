#!/bin/bash

# clean-appium-install.sh
# This script performs a complete cleanup of Appium installations and caches
# to ensure a truly fresh install is possible

# Default locations to check
LOCATIONS=(
  "$HOME/.local/appium-home"
  "/Users/Shared/tmp/appiumagent/appium-home"
  "$HOME/.appium"
)

# Parse command-line arguments
CUSTOM_LOCATION=""
FORCE=0

usage() {
  echo "Usage: $0 [-f] [-p path/to/appium/home]"
  echo "  -f              Force cleanup without confirmation"
  echo "  -p <path>       Specify custom Appium home path to clean"
  exit 1
}

while getopts "fp:" opt; do
  case $opt in
    f) FORCE=1 ;;
    p) CUSTOM_LOCATION="$OPTARG" ;;
    \?) usage ;;
  esac
done

# Add custom location if provided
if [ -n "$CUSTOM_LOCATION" ]; then
  LOCATIONS=("$CUSTOM_LOCATION" "${LOCATIONS[@]}")
fi

# Print locations that will be cleaned
echo "The following Appium installation locations will be cleaned:"
for loc in "${LOCATIONS[@]}"; do
  if [ -d "$loc" ]; then
    echo "  - $loc (exists)"
  else
    echo "  - $loc (not found)"
  fi
done

# Configuration files that will be removed
CONFIG_FILES=(
  "$HOME/.appiumrc.json"
  "$HOME/.npmrc"
)

echo -e "\nThe following configuration files will be removed:"
for file in "${CONFIG_FILES[@]}"; do
  if [ -f "$file" ]; then
    echo "  - $file (exists)"
  else
    echo "  - $file (not found)"
  fi
done

# Cache directories that will be cleaned
CACHE_DIRS=(
  "$HOME/.cache/appium"
  "$HOME/.npm/_cacache"
)

echo -e "\nThe following cache directories will be cleaned:"
for dir in "${CACHE_DIRS[@]}"; do
  if [ -d "$dir" ]; then
    echo "  - $dir (exists)"
  else
    echo "  - $dir (not found)"
  fi
done

# Ask for confirmation unless force flag is set
if [ $FORCE -eq 0 ]; then
  echo -e "\nWARNING: This will remove all Appium installations, configurations, and caches."
  read -p "Do you want to continue? (y/N) " -n 1 -r
  echo
  if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Operation cancelled."
    exit 1
  fi
fi

# Perform the cleanup
echo "Starting cleanup..."

# Kill any running Appium processes
echo "Stopping any running Appium processes..."
pkill -f appium || true

# Clean up Appium installations
for loc in "${LOCATIONS[@]}"; do
  if [ -d "$loc" ]; then
    echo "Removing $loc..."
    rm -rf "$loc"
    echo "✓ Removed $loc"
  fi
done

# Clean up configuration files
for file in "${CONFIG_FILES[@]}"; do
  if [ -f "$file" ]; then
    echo "Removing $file..."
    rm -f "$file"
    echo "✓ Removed $file"
  fi
done

# Clean up cache directories
for dir in "${CACHE_DIRS[@]}"; do
  if [ -d "$dir" ]; then
    echo "Cleaning $dir..."
    rm -rf "$dir"
    echo "✓ Cleaned $dir"
  fi
done

# Clean up npm's global appium packages
echo "Checking for global Appium installations..."
if command -v npm &>/dev/null; then
  echo "Removing global Appium packages..."
  npm uninstall -g appium appium-xcuitest-driver appium-inspector-plugin &>/dev/null || true
  echo "✓ Removed global Appium packages"
fi

echo -e "\nCleanup complete. You can now perform a fresh installation of Appium."
echo "To install Appium, run the InstallDependencies.sh script."
