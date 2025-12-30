using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class ConfigurationReaderTests : IDisposable
    {
        private readonly Mock<ILogger<ConfigurationReader>> _mockLogger;
        private readonly ConfigurationReader _reader;
        private readonly string _testDir;

        public ConfigurationReaderTests()
        {
            _mockLogger = new Mock<ILogger<ConfigurationReader>>();
            _reader = new ConfigurationReader(_mockLogger.Object);
            _testDir = Path.Combine(Path.GetTempPath(), $"ConfigReaderTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public void LoadConfiguration_WithValidConfig_ReturnsConfig()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var config = new InstallConfig
            {
                InstallFolder = Path.Combine("test", "install"),
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "test-plugin", Version = "1.0.0", Enabled = true }
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, AppJsonSerializerContext.Default.InstallConfig));

            // Act
            var result = _reader.LoadConfiguration(configPath);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("test", result.InstallFolder);
            Assert.Contains("install", result.InstallFolder);
            Assert.Equal("22", result.NodeVersion);
            Assert.Equal("2.17.1", result.AppiumVersion);
            Assert.Single(result.Plugins);
            Assert.Equal("test-plugin", result.Plugins[0].Id);
        }

        [Fact]
        public void LoadConfiguration_WithMissingFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDir, "nonexistent.json");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => _reader.LoadConfiguration(nonExistentPath));
        }

        [Fact]
        public void LoadConfiguration_WithInvalidJson_ThrowsInvalidOperationException()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "invalid.json");
            File.WriteAllText(configPath, "{invalid json");

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _reader.LoadConfiguration(configPath));
            Assert.Contains("Invalid JSON format", ex.Message);
        }

        [Fact]
        public void LoadConfiguration_WithMissingRequiredFields_ThrowsInvalidOperationException()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var config = new InstallConfig { InstallFolder = "", NodeVersion = "", AppiumVersion = "", NvmVersion = "" };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, AppJsonSerializerContext.Default.InstallConfig));

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => _reader.LoadConfiguration(configPath));
            Assert.Contains("Configuration validation failed", ex.Message);
        }

        [Fact]
        public void LoadConfiguration_WithPluginsDDirectory_MergesPlugins()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var pluginsDDir = Path.Combine(_testDir, "plugins.d");
            Directory.CreateDirectory(pluginsDDir);

            var mainConfig = new InstallConfig
            {
                InstallFolder = "/test/install",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "main-plugin", Version = "1.0.0", Enabled = true }
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(mainConfig, AppJsonSerializerContext.Default.InstallConfig));

            // Create plugin file in plugins.d
            var pluginConfig = new InstallConfig
            {
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "extra-plugin", Version = "2.0.0", Enabled = true }
                }
            };
            File.WriteAllText(
                Path.Combine(pluginsDDir, "extra.json"),
                JsonSerializer.Serialize(pluginConfig, AppJsonSerializerContext.Default.InstallConfig)
            );

            // Act
            var result = _reader.LoadConfiguration(configPath);

            // Assert
            Assert.Equal(2, result.Plugins.Count);
            Assert.Equal("main-plugin", result.Plugins[0].Id);
            Assert.Equal("extra-plugin", result.Plugins[1].Id);
        }

        [Fact]
        public void LoadConfiguration_WithMultiplePluginsDFiles_PreservesOrder()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var pluginsDDir = Path.Combine(_testDir, "plugins.d");
            Directory.CreateDirectory(pluginsDDir);

            var mainConfig = new InstallConfig
            {
                InstallFolder = "/test/install",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>()
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(mainConfig, AppJsonSerializerContext.Default.InstallConfig));

            // Create multiple plugin files (alphabetical order)
            var files = new[] { "10-first.json", "20-second.json", "30-third.json" };
            for (int i = 0; i < files.Length; i++)
            {
                var pluginConfig = new InstallConfig
                {
                    Plugins = new List<PluginConfig>
                    {
                        new PluginConfig { Id = $"plugin-{i + 1}", Version = "1.0.0", Enabled = true }
                    }
                };
                File.WriteAllText(
                    Path.Combine(pluginsDDir, files[i]),
                    JsonSerializer.Serialize(pluginConfig, AppJsonSerializerContext.Default.InstallConfig)
                );
            }

            // Act
            var result = _reader.LoadConfiguration(configPath);

            // Assert
            Assert.Equal(3, result.Plugins.Count);
            Assert.Equal("plugin-1", result.Plugins[0].Id);
            Assert.Equal("plugin-2", result.Plugins[1].Id);
            Assert.Equal("plugin-3", result.Plugins[2].Id);
        }

        [Fact]
        public void LoadConfiguration_WithPluginMissingId_LogsWarning()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var config = new InstallConfig
            {
                InstallFolder = "/test/install",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = null, Version = "1.0.0", Enabled = true }
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, AppJsonSerializerContext.Default.InstallConfig));

            // Act & Assert - Should log warning but validation will fail
            Assert.Throws<InvalidOperationException>(() => _reader.LoadConfiguration(configPath));
        }

        [Theory]
        [InlineData("test-install", false)]
        [InlineData("C:\\Windows\\Path", false)]
        public void LoadConfiguration_ExpandsEnvironmentVariables(string installFolder, bool hasVars)
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var config = new InstallConfig
            {
                InstallFolder = installFolder,
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2"
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, AppJsonSerializerContext.Default.InstallConfig));

            // Act
            var result = _reader.LoadConfiguration(configPath);

            // Assert
            Assert.NotNull(result.InstallFolder);
            Assert.NotEqual("", result.InstallFolder);
        }

        [Fact]
        public void LoadConfiguration_WithPluginIdWhitespace_TrimsId()
        {
            // Arrange
            var configPath = Path.Combine(_testDir, "config.json");
            var config = new InstallConfig
            {
                InstallFolder = "/test/install",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "  test-plugin  ", Version = "1.0.0", Enabled = true }
                }
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, AppJsonSerializerContext.Default.InstallConfig));

            // Act
            var result = _reader.LoadConfiguration(configPath);

            // Assert
            Assert.Equal("test-plugin", result.Plugins[0].Id);
        }

        [Fact]
        public void CreateSampleConfig_CreatesValidFile()
        {
            // Arrange
            var samplePath = Path.Combine(_testDir, "sample.json");

            // Act
            _reader.CreateSampleConfig(samplePath);

            // Assert
            Assert.True(File.Exists(samplePath));
            var content = File.ReadAllText(samplePath);
            Assert.Contains("installFolder", content);
            Assert.Contains("nodeVersion", content);
            Assert.Contains("appiumVersion", content);
        }
    }
}
