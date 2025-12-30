using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class ScriptExecutorTests
    {
        private readonly Mock<ILogger<ScriptExecutor>> _mockLogger;
        private readonly string _testScriptsRoot;
        private readonly ScriptExecutor _executor;

        public ScriptExecutorTests()
        {
            _mockLogger = new Mock<ILogger<ScriptExecutor>>();
            _testScriptsRoot = Path.Combine(Path.GetTempPath(), $"ScriptExecutorTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testScriptsRoot);
            _executor = new ScriptExecutor(_testScriptsRoot, _mockLogger.Object);
        }

        [Fact]
        public void DetectOperatingSystem_ReturnsCorrectOS()
        {
            // Act
            var os = _executor.DetectOperatingSystem();

            // Assert
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Equal(ScriptExecutor.OperatingSystem.Windows, os);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Assert.Equal(ScriptExecutor.OperatingSystem.MacOS, os);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Assert.Equal(ScriptExecutor.OperatingSystem.Linux, os);
            else
                Assert.Equal(ScriptExecutor.OperatingSystem.Unknown, os);
        }

        [Theory]
        [InlineData(ScriptExecutor.OperatingSystem.Windows, "InstallDependencies.ps1")]
        [InlineData(ScriptExecutor.OperatingSystem.MacOS, "InstallDependencies.sh")]
        [InlineData(ScriptExecutor.OperatingSystem.Linux, "InstallDependencies.sh")]
        public void GetInstallationScriptPath_ReturnsCorrectPath(ScriptExecutor.OperatingSystem os, string expectedScript)
        {
            // Arrange
            var osFolder = os.ToString();
            var scriptsDir = Path.Combine(_testScriptsRoot, osFolder, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, expectedScript), "# Test script");

            // Act
            var path = _executor.GetInstallationScriptPath(os);

            // Assert
            Assert.Equal(Path.Combine(_testScriptsRoot, osFolder, "Scripts", expectedScript), path);
        }

        [Fact]
        public void GetInstallationScriptPath_WithMissingScript_ThrowsFileNotFoundException()
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => _executor.GetInstallationScriptPath(ScriptExecutor.OperatingSystem.Windows));
        }

        [Theory]
        [InlineData(ScriptExecutor.OperatingSystem.Windows, "ServiceSetup.ps1")]
        [InlineData(ScriptExecutor.OperatingSystem.MacOS, "SupervisorSetup.sh")]
        [InlineData(ScriptExecutor.OperatingSystem.Linux, "SystemdSetup.sh")]
        public void GetServiceSetupScriptPath_ReturnsCorrectPath(ScriptExecutor.OperatingSystem os, string expectedScript)
        {
            // Arrange
            var osFolder = os.ToString();
            var scriptsDir = Path.Combine(_testScriptsRoot, osFolder, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, expectedScript), "# Test script");

            // Act
            var path = _executor.GetServiceSetupScriptPath(os);

            // Assert
            Assert.Equal(Path.Combine(_testScriptsRoot, osFolder, "Scripts", expectedScript), path);
        }

        [Fact]
        public void GetServiceSetupScriptPath_WithUnsupportedOS_ThrowsPlatformNotSupportedException()
        {
            // Act & Assert
            Assert.Throws<PlatformNotSupportedException>(() => _executor.GetServiceSetupScriptPath(ScriptExecutor.OperatingSystem.Unknown));
        }

        [Fact]
        public void BuildArguments_Windows_BuildsCorrectArguments()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "C:\\test\\install",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "uiautomator2", Version = "3.8.3", Enabled = true }
                },
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "device-farm", Version = "8.3.5", Enabled = true }
                },
                PlatformSpecific = new PlatformSpecificConfig
                {
                    Windows = new WindowsConfig
                    {
                        GoIosVersion = "v1.0.200",
                        InstallIOSSupport = true,
                        InstallAndroidSupport = false
                    }
                }
            };

            // Act
            var args = _executor.BuildArguments(config, ScriptExecutor.OperatingSystem.Windows);

            // Assert
            Assert.Contains("-InstallFolder \"C:\\test\\install\"", args);
            Assert.Contains("-NodeVersion \"22\"", args);
            Assert.Contains("-AppiumVersion \"2.17.1\"", args);
            Assert.Contains("-GoIosVersion \"v1.0.200\"", args);
            Assert.Contains("-DriversJson", args);
            Assert.Contains("-PluginsJson", args);
            Assert.Contains("-InstallIOSSupport", args);
            Assert.Contains("-InstallAndroidSupport:$false", args);
        }

        [Fact]
        public void BuildArguments_MacOS_BuildsCorrectArguments()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/usr/local/appium",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "uiautomator2", Version = "3.8.3", Enabled = true }
                },
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "device-farm", Version = "8.3.5", Enabled = true }
                },
                PlatformSpecific = new PlatformSpecificConfig
                {
                    MacOS = new MacOSConfig
                    {
                        NvmVersion = "0.39.7",
                        LibimobiledeviceVersion = "1.3.0",
                        GoIosVersion = "v1.0.200"
                    }
                }
            };

            // Act
            var args = _executor.BuildArguments(config, ScriptExecutor.OperatingSystem.MacOS);

            // Assert
            Assert.Contains("--install_folder=\"/usr/local/appium\"", args);
            Assert.Contains("--node_version=\"22\"", args);
            Assert.Contains("--appium_version=\"2.17.1\"", args);
            Assert.Contains("--nvm_version=\"0.39.7\"", args);
            Assert.Contains("--drivers_json=", args);
            Assert.Contains("--plugins_json=", args);
            Assert.Contains("--libimobiledevice_version=\"1.3.0\"", args);
            Assert.Contains("--go_ios_version=\"v1.0.200\"", args);
        }

        [Fact]
        public void BuildArguments_Linux_BuildsCorrectArguments()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/opt/appium",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "uiautomator2", Version = "3.8.3", Enabled = true }
                },
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "device-farm", Version = "8.3.5", Enabled = true }
                },
                PlatformSpecific = new PlatformSpecificConfig
                {
                    Linux = new LinuxConfig
                    {
                        NvmVersion = "0.39.7",
                        InstallIOSSupport = true,
                        InstallAndroidSupport = false,
                        GoIosVersion = "v1.0.200"
                    }
                }
            };

            // Act
            var args = _executor.BuildArguments(config, ScriptExecutor.OperatingSystem.Linux);

            // Assert
            Assert.Contains("--install_folder=\"/opt/appium\"", args);
            Assert.Contains("--node_version=\"22\"", args);
            Assert.Contains("--appium_version=\"2.17.1\"", args);
            Assert.Contains("--nvm_version=\"0.39.7\"", args);
            Assert.Contains("--drivers_json=", args);
            Assert.Contains("--plugins_json=", args);
            Assert.Contains("--install_ios_support=true", args);
            Assert.Contains("--install_android_support=false", args);
            Assert.Contains("--go_ios_version=\"v1.0.200\"", args);
        }

        [Fact]
        public void ExecuteScript_DryRun_ReturnsZero()
        {
            // Arrange
            var scriptPath = Path.Combine(_testScriptsRoot, "test.ps1");
            File.WriteAllText(scriptPath, "Write-Host 'Test'");

            // Act
            var exitCode = _executor.ExecuteScript(scriptPath, "-TestParam value", true);

            // Assert
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void CleanInstallationFolder_WithExistingFolder_DeletesContents()
        {
            // Arrange
            var installFolder = Path.Combine(_testScriptsRoot, "install");
            Directory.CreateDirectory(installFolder);
            File.WriteAllText(Path.Combine(installFolder, "test.txt"), "content");

            // Act
            _executor.CleanInstallationFolder(installFolder);

            // Assert
            Assert.False(Directory.Exists(installFolder));
        }

        [Fact]
        public void CleanInstallationFolder_WithNonExistentFolder_DoesNothing()
        {
            // Arrange
            var installFolder = Path.Combine(_testScriptsRoot, "nonexistent");

            // Act
            _executor.CleanInstallationFolder(installFolder);

            // Assert - No exception thrown
        }

        [Fact]
        public void SetExecutePermissions_Windows_DoesNothing()
        {
            // Arrange
            var scriptPath = Path.Combine(_testScriptsRoot, "test.sh");

            // Act - Should not throw on Windows
            if (_executor.DetectOperatingSystem() == ScriptExecutor.OperatingSystem.Windows)
            {
                _executor.SetExecutePermissions(scriptPath);
                // Assert - No exception
            }
        }
    }
}