<#
Copyright 2025 Appium Bootstrap Installer Contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
#>

# PowerShell script to build self-contained single-file executables for all platforms

Write-Host "Building Appium Bootstrap Installer for all platforms..." -ForegroundColor Cyan
Write-Host ""

# Create output directory
$outputDir = ".\publish"
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir | Out-Null

# Define target platforms
$platforms = @(
    @{Name = "Windows x64"; RID = "win-x64"; Ext = ".exe" },
    @{Name = "Windows ARM64"; RID = "win-arm64"; Ext = ".exe" },
    @{Name = "Linux x64"; RID = "linux-x64"; Ext = "" },
    @{Name = "Linux ARM64"; RID = "linux-arm64"; Ext = "" },
    @{Name = "macOS x64"; RID = "osx-x64"; Ext = "" },
    @{Name = "macOS ARM64 (Apple Silicon)"; RID = "osx-arm64"; Ext = "" }
)

foreach ($platform in $platforms) {
    Write-Host "Building for $($platform.Name)..." -ForegroundColor Yellow
    
    $outputPath = Join-Path $outputDir $platform.RID
    
    dotnet publish "AppiumBootstrapInstaller\AppiumBootstrapInstaller.csproj" -c Release `
        -r $platform.RID `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $outputPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Successfully built for $($platform.Name)" -ForegroundColor Green
        
        # Show the output file
        $exeName = "AppiumBootstrapInstaller$($platform.Ext)"
        $exePath = Join-Path $outputPath $exeName
        if (Test-Path $exePath) {
            $fileSize = (Get-Item $exePath).Length / 1MB
            Write-Host "  Output: $exePath ($([math]::Round($fileSize, 2)) MB)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "✗ Failed to build for $($platform.Name)" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "Build complete! Check the '$outputDir' folder for executables." -ForegroundColor Cyan
