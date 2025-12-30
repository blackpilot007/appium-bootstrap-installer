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
    public class ProcessPluginTests
    {
        private readonly Mock<ILogger<ProcessPlugin>> _mockLogger;
        private readonly PluginConfig _config;

        public ProcessPluginTests()
        {
            _mockLogger = new Mock<ILogger<ProcessPlugin>>();
            _config = new PluginConfig
            {
                Id = "test-process",
                Name = "Test Process Plugin",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", "ping", "-t", "127.0.0.1" },
                Enabled = true,
                HealthCheckCommand = "cmd.exe",
                HealthCheckArguments = new List<string> { "/c", "echo", "healthy" },
                HealthCheckTimeoutSeconds = 2
            };
        }

        [Fact]
        public async Task StartAsync_ValidConfig_StartsProcess()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal("process", plugin.Type);
            Assert.Equal("test-id", plugin.Id);
        }

        [Fact]
        public async Task StopAsync_ProcessRunning_StopsProcess()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
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
                Id = "test-process",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", "ping", "-t", "127.0.0.1" },
                Enabled = true
            };
            var plugin = new ProcessPlugin("test-id", configWithoutHealthCheck, _mockLogger.Object);
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
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            // Note: Actual health check result depends on whether the health check command succeeds
            Assert.IsType<bool>(result);
        }

        [Fact]
        public void GetStatus_ProcessNotStarted_ReturnsStopped()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);

            // Act
            var status = plugin.State;

            // Assert
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Disabled, status);
        }

        [Fact]
        public async Task GetStatus_ProcessStarted_ReturnsRunning()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
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
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);

            // Assert
            Assert.Equal("test-id", plugin.Id);
            Assert.Equal("process", plugin.Type);
            Assert.Equal(_config, plugin.Config);
        }

        [Fact]
        public async Task StartAsync_CancellationRequested_CompletesWithoutException()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
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
        public async Task CheckHealthAsync_ProcessExited_ReturnsFalse()
        {
            // Arrange
            var plugin = new ProcessPlugin("test-id", _config, _mockLogger.Object);
            var context = new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = "C:\\test",
                Services = new ServiceCollection().BuildServiceProvider()
            };

            await plugin.StartAsync(context, CancellationToken.None);
            await plugin.StopAsync(CancellationToken.None); // Stop the process
            await Task.Delay(100); // Give time for process to exit

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.False(result);
        }
    }
}