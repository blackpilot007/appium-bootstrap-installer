using System.Collections.Generic;
using System.Linq;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class ConfigurationValidatorTests
    {
        private readonly Mock<ILogger<ConfigurationValidator>> _mockLogger;
        private readonly ConfigurationValidator _validator;

        public ConfigurationValidatorTests()
        {
            _mockLogger = new Mock<ILogger<ConfigurationValidator>>();
            _validator = new ConfigurationValidator(_mockLogger.Object);
        }

        [Fact]
        public void Validate_WithValidConfig_ReturnsTrue()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test/install",
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
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_WithMissingInstallFolder_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2"
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("InstallFolder"));
        }

        [Fact]
        public void Validate_WithMissingNodeVersion_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2"
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("NodeVersion"));
        }

        [Fact]
        public void Validate_WithMissingAppiumVersion_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "",
                NvmVersion = "0.40.2"
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("AppiumVersion"));
        }

        [Fact]
        public void Validate_WithDriverMissingName_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "", Version = "1.0.0", Enabled = true }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Driver") && e.Contains("name"));
        }

        [Fact]
        public void Validate_WithDriverMissingVersion_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "uiautomator2", Version = "", Enabled = true }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("uiautomator2") && e.Contains("version"));
        }

        [Fact]
        public void Validate_WithPluginMissingId_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = null, Version = "1.0.0", Enabled = true }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("Plugin") && e.Contains("id"));
        }

        [Fact]
        public void Validate_WithPluginMissingVersion_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "test-plugin", Version = "", Enabled = true }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("test-plugin") && e.Contains("version"));
        }

        [Fact]
        public void Validate_WithInvalidHealthCheckTimeout_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig 
                    { 
                        Id = "test-plugin", 
                        Version = "1.0.0", 
                        Enabled = true,
                        HealthCheckTimeoutSeconds = 0
                    }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("test-plugin") && e.Contains("healthCheckTimeoutSeconds"));
        }

        [Fact]
        public void Validate_WithInvalidHealthCheckInterval_ReturnsError()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig 
                    { 
                        Id = "test-plugin", 
                        Version = "1.0.0", 
                        Enabled = true,
                        HealthCheckIntervalSeconds = 0
                    }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("test-plugin") && e.Contains("healthCheckIntervalSeconds"));
        }

        [Fact]
        public void Validate_WithDisabledPlugin_SkipsValidation()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "test-plugin", Version = "", Enabled = false }
                }
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.True(result);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WithInvalidDeviceListenerInterval_ReturnsError(int interval)
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "/test",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                DeviceListenerPollInterval = interval
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.Contains(errors, e => e.Contains("DeviceListenerPollInterval"));
        }

        [Fact]
        public void Validate_WithMultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var config = new InstallConfig
            {
                InstallFolder = "",
                NodeVersion = "",
                AppiumVersion = "",
                NvmVersion = ""
            };

            // Act
            var result = _validator.Validate(config, out var errors, out var warnings);

            // Assert
            Assert.False(result);
            Assert.True(errors.Count >= 3);
        }
    }
}
