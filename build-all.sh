#!/bin/bash
#
# Copyright 2025 Appium Bootstrap Installer Contributors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

# Bash script to build self-contained single-file executables for all platforms

echo "Building Appium Bootstrap Installer for all platforms..."
echo ""

# Create output directory
outputDir="./publish"
if [ -d "$outputDir" ]; then
    rm -rf "$outputDir"
fi
mkdir -p "$outputDir"

# Define target platforms
declare -a platforms=(
    "win-x64:Windows x64:.exe"
    "win-arm64:Windows ARM64:.exe"
    "linux-x64:Linux x64:"
    "linux-arm64:Linux ARM64:"
    "osx-x64:macOS x64:"
    "osx-arm64:macOS ARM64 (Apple Silicon):"
)

for platform in "${platforms[@]}"; do
    IFS=':' read -r rid name ext <<< "$platform"
    
    echo "Building for $name..."
    
    outputPath="$outputDir/$rid"
    
    dotnet publish "AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj" -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:PublishReadyToRun=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$outputPath"
    
    if [ $? -eq 0 ]; then
        echo "✓ Successfully built for $name"
        
        # Show the output file
        exeName="AppiumBootstrapInstaller$ext"
        exePath="$outputPath/$exeName"
        if [ -f "$exePath" ]; then
            fileSize=$(du -h "$exePath" | cut -f1)
            echo "  Output: $exePath ($fileSize)"
        fi
    else
        echo "✗ Failed to build for $name"
    fi
    echo ""
done

echo "Build complete! Check the '$outputDir' folder for executables."
