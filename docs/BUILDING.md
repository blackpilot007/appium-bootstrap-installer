# Building from Source

Developer guide for building Appium Bootstrap Installer from source.

## Prerequisites

### Required
- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Git

### Optional (for testing)
- Android SDK Platform Tools (for Android testing)
- libimobiledevice (for iOS testing on macOS/Linux)

## Clone Repository

```bash
git clone https://github.com/blackpilot007/appium-bootstrap-installer.git
cd appium-bootstrap-installer
```

## Development Build

### Build for Current Platform

```bash
cd AppiumBootstrapInstaller
dotnet build -c Release
```

### Run from Source

```bash
dotnet run --project AppiumBootstrapInstaller -- --help
```

### Run with Arguments

```bash
# Generate config
dotnet run --project AppiumBootstrapInstaller -- --generate-config

# Run with config
dotnet run --project AppiumBootstrapInstaller -- --config config.json

# Device listener mode
dotnet run --project AppiumBootstrapInstaller -- --listen
```

## Release Build (Self-Contained)

### Windows x64

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-x64
```

### Windows ARM64

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r win-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/win-arm64
```

### Linux x64

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/linux-x64
```

### Linux ARM64

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r linux-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/linux-arm64
```

### macOS x64 (Intel)

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r osx-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/osx-x64
```

### macOS ARM64 (Apple Silicon)

```bash
dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=true \
  -o ./publish/osx-arm64
```

## Build All Platforms

### Windows (PowerShell)

```powershell
# Set execution policy if needed
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Run build script
.\build-all.ps1
```

### Linux/macOS (Bash)

```bash
# Make executable
chmod +x build-all.sh

# Run build script
./build-all.sh
```

This creates executables in `./publish/` for all platforms:
- `win-x64/` - Windows x64
- `win-arm64/` - Windows ARM64
- `linux-x64/` - Linux x64
- `linux-arm64/` - Linux ARM64
- `osx-x64/` - macOS Intel
- `osx-arm64/` - macOS Apple Silicon

## Build Output

After building, the `publish/` directory structure:

```
publish/
├── win-x64/
│   └── AppiumBootstrapInstaller.exe
├── linux-x64/
│   └── AppiumBootstrapInstaller
├── osx-x64/
│   └── AppiumBootstrapInstaller
└── osx-arm64/
    └── AppiumBootstrapInstaller
```

## Testing Builds

### Test on Current Platform

```bash
# Windows
.\publish\win-x64\AppiumBootstrapInstaller.exe --help

# Linux/macOS
chmod +x publish/linux-x64/AppiumBootstrapInstaller
./publish/linux-x64/AppiumBootstrapInstaller --help
```

### Test with Configuration

```bash
# Copy config
cp config.sample.json publish/win-x64/config.json

# Run
cd publish/win-x64
.\AppiumBootstrapInstaller.exe
```

## Troubleshooting Builds

### Build Warnings

**Trim Analysis Warnings (IL2026):**
```
warning IL2026: Using member 'System.Text.Json.JsonSerializer...'
```

These are expected for trimmed builds and can be safely ignored. The application handles these correctly.

### Build Fails

**Missing .NET SDK:**
```
The command 'dotnet' is not found
```

**Solution:** Install .NET 8 SDK from https://dotnet.microsoft.com/download

**Restore Issues:**
```
error NU1100: Unable to resolve...
```

**Solution:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore
```

### Platform-Specific Issues

**macOS Code Signing (Optional):**
```bash
# Sign executable
codesign --force --deep --sign - publish/osx-x64/AppiumBootstrapInstaller

# Verify
codesign --verify --verbose publish/osx-x64/AppiumBootstrapInstaller
```

**Linux Permissions:**
```bash
# Ensure executable bit
chmod +x publish/linux-x64/AppiumBootstrapInstaller

# Verify
ls -la publish/linux-x64/AppiumBootstrapInstaller
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Build

on: [push, pull_request]

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
        
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore
      run: dotnet restore
    
    - name: Build
      run: dotnet build -c Release
    
    - name: Test
      run: dotnet test -c Release
    
    - name: Publish
      run: |
        dotnet publish AppiumBootstrapInstaller/AppiumBootstrapInstaller.csproj \
          -c Release \
          -r ${{ matrix.rid }} \
          --self-contained true \
          -p:PublishSingleFile=true \
          -p:PublishTrimmed=true \
          -o ./publish
```

### Azure DevOps Example

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'

- script: dotnet restore
  displayName: 'Restore packages'

- script: dotnet build -c Release
  displayName: 'Build'

- script: ./build-all.sh
  displayName: 'Build all platforms'

- task: PublishBuildArtifacts@1
  inputs:
    pathToPublish: 'publish'
    artifactName: 'releases'
```

## Development Workflow

### 1. Create Feature Branch

```bash
git checkout -b feature/my-feature
```

### 2. Make Changes

Edit code in your preferred IDE:
- Visual Studio 2022
- Visual Studio Code
- JetBrains Rider

### 3. Build and Test

```bash
dotnet build
dotnet test
dotnet run --project AppiumBootstrapInstaller -- --help
```

### 4. Commit Changes

```bash
git add .
git commit -m "Add feature: description"
```

### 5. Push and Create PR

```bash
git push origin feature/my-feature
```

Then create a Pull Request on GitHub.

## Project Structure

```
appium-bootstrap-installer/
├── AppiumBootstrapInstaller/
│   ├── Models/              # Data models
│   ├── Services/            # Business logic
│   ├── Platform/            # Platform scripts
│   ├── Program.cs           # Entry point
│   └── *.csproj             # Project file
├── docs/                    # Documentation
├── examples/                # Example configurations
├── publish/                 # Build output
├── build-all.ps1           # Windows build script
├── build-all.sh            # Linux/macOS build script
└── *.sln                   # Solution file
```

## NuGet Packages Used

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
```

## IDE Setup

### Visual Studio 2022

1. Open `AppiumBootstrapInstaller.sln`
2. Set startup project: `AppiumBootstrapInstaller`
3. Configure launch settings in `Properties/launchSettings.json`

### Visual Studio Code

1. Install C# extension
2. Open workspace folder
3. Use integrated terminal for dotnet commands
4. Configure `.vscode/launch.json` for debugging

### JetBrains Rider

1. Open `AppiumBootstrapInstaller.sln`
2. Configure run configuration
3. Set command-line arguments in run configuration

## Release Checklist

Before creating a release:

- [ ] Update version in `VERSION` file
- [ ] Update CHANGELOG.md
- [ ] Run all builds: `./build-all.sh` or `.\build-all.ps1`
- [ ] Test executables on all platforms
- [ ] Update documentation
- [ ] Create GitHub release
- [ ] Upload binaries
- [ ] Tag release: `git tag v1.x.x`
