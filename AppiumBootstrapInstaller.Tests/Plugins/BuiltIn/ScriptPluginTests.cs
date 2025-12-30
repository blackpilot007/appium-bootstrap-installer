using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins.BuiltIn
{
    public class ScriptPluginTests
    {
        private readonly Mock<ILogger<ScriptPlugin>> _mockLogger;
        private readonly PluginConfig _config;

        public ScriptPluginTests()
        {
            _mockLogger = new Mock<ILogger<ScriptPlugin>>();
            _config = new PluginConfig
            {
                Id = "test-script",
                Name = "Test Script Plugin",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", "ping", "-t", "127.0.0.1" },
                Enabled = true,
                HealthCheckCommand = "cmd.exe",
                HealthCheckArguments = new List<string> { "/c", "echo", "healthy" },
                HealthCheckTimeoutSeconds = 2
            };
        }

        [Fact]
        public async Task StartAsync_ValidConfig_StartsScript()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal("script", plugin.Type);
            Assert.Equal("test-id", plugin.Id);
        }

        [Fact]
        public async Task StopAsync_ScriptRunning_StopsScript()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CheckHealthAsync_NoHealthCheckCommand_ReturnsTrue()
        {
            // Arrange
            var configWithoutHealthCheck = new PluginConfig
            {
                Id = "test-script",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", "ping", "-t", "127.0.0.1" },
                Enabled = true
            };
            var plugin = new ScriptPlugin("test-id", configWithoutHealthCheck, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            // Start the plugin first
            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckHealthAsync_WithHealthCheckCommand_ReturnsHealthStatus()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.IsType<bool>(result);
        }

        [Fact]
        public void GetStatus_ScriptNotStarted_ReturnsStopped()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);

            // Act
            var status = plugin.State;

            // Assert
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Disabled, status);
        }

        [Fact]
        public async Task GetStatus_ScriptStarted_ReturnsRunning()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var status = plugin.State;

            // Assert
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Running, status);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public void Constructor_ValidParameters_SetsProperties()
        {
            // Arrange & Act
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);

            // Assert
            Assert.Equal("test-id", plugin.Id);
            Assert.Equal("script", plugin.Type);
            Assert.Equal(_config, plugin.Config);
        }

        [Fact]
        public async Task StartAsync_CancellationRequested_CompletesWithoutException()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert - Should complete without throwing
            var result = await plugin.StartAsync(context, cts.Token);
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task CheckHealthAsync_ScriptExited_ReturnsFalse()
        {
            // Arrange
            var plugin = new ScriptPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);
            await plugin.StopAsync(CancellationToken.None); // Stop the script
            await Task.Delay(100); // Give time for script to exit

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StartAsync_WithBashRuntime_UsesBashExecution()
        {
            // Arrange
            var bashConfig = new PluginConfig
            {
                Id = "bash-script",
                Executable = "Write-Host 'hello world'",
                Runtime = "powershell",
                Enabled = true
            };
            var plugin = new ScriptPlugin("test-id", bashConfig, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CheckHealthAsync_WithBashRuntime_UsesBashForHealthCheck()
        {
            // Arrange
            var bashConfig = new PluginConfig
            {
                Id = "bash-health",
                Executable = "echo 'test'",
                HealthCheckCommand = "echo 'healthy'",
                HealthCheckRuntime = "bash",
                Enabled = true
            };
            var plugin = new ScriptPlugin("test-id", bashConfig, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.IsType<bool>(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }
    }
}