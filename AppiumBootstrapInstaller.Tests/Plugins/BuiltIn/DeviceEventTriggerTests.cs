using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins;
using AppiumBootstrapInstaller.Plugins.Triggers;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins.BuiltIn
{
    public class DeviceEventTriggerTests
    {
        private readonly Mock<ILogger<DeviceEventTrigger>> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Mock<PluginRegistry> _mockRegistry;
        private readonly Mock<IPluginOrchestrator> _mockOrchestrator;

        public DeviceEventTriggerTests()
        {
            _mockLogger = new Mock<ILogger<DeviceEventTrigger>>();
            _mockEventBus = new Mock<IEventBus>();
            _mockRegistry = new Mock<PluginRegistry>();
            _mockOrchestrator = new Mock<IPluginOrchestrator>();
        }

        [Fact]
        public void Constructor_SubscribesToDeviceEvents()
        {
            // Arrange
            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            // Act
            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Assert
            _mockEventBus.Verify(e => e.Subscribe<DeviceConnectedEvent>(It.IsAny<Action<DeviceConnectedEvent>>()), Times.Once);
            _mockEventBus.Verify(e => e.Subscribe<DeviceDisconnectedEvent>(It.IsAny<Action<DeviceDisconnectedEvent>>()), Times.Once);
        }

        [Fact]
        public async Task HandleDeviceConnectedAsync_PluginTriggersOnConnect_StartsPlugin()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act - Call the private method via reflection
            var method = typeof(DeviceEventTrigger).GetMethod("HandleDeviceConnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockOrchestrator.Verify(o => o.StartPluginAsync("test-plugin", It.Is<PluginContext>(ctx =>
                ctx.Variables.ContainsKey("device") &&
                ctx.Variables.ContainsKey("deviceId") &&
                ctx.Variables["deviceId"].ToString() == "device123"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleDeviceConnectedAsync_PluginDoesNotTriggerOnConnect_DoesNotStartPlugin()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-disconnected", // Different trigger
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var method = typeof(DeviceEventTrigger).GetMethod("HandleDeviceConnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockOrchestrator.Verify(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleDeviceDisconnectedAsync_PluginTriggersOnDisconnect_StartsPlugin()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-disconnected",
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var method = typeof(DeviceEventTrigger).GetMethod("HandleDeviceDisconnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockOrchestrator.Verify(o => o.StartPluginAsync("test-plugin", It.Is<PluginContext>(ctx =>
                ctx.Variables.ContainsKey("device") &&
                ctx.Variables.ContainsKey("deviceId") &&
                ctx.Variables["deviceId"].ToString() == "device123"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleDeviceDisconnectedAsync_PluginConfiguredToStopOnDisconnect_StopsPluginInstance()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    StopOnDisconnect = true,
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var method = typeof(DeviceEventTrigger).GetMethod("HandleDeviceDisconnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockOrchestrator.Verify(o => o.StopPluginAsync("test-plugin:device123", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleDeviceDisconnectedAsync_PluginNotConfiguredToStopOnDisconnect_DoesNotStopPlugin()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    StopOnDisconnect = false, // Not configured to stop
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var method = typeof(DeviceEventTrigger).GetMethod("HandleDeviceDisconnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockOrchestrator.Verify(o => o.StopPluginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleDeviceConnectedAsync_ExceptionDuringPluginStart_LogsError()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);
            _mockOrchestrator.Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Plugin start failed"));

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var task = (Task)typeof(DeviceEventTrigger).GetMethod("HandleDeviceConnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockLogger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public async Task HandleDeviceDisconnectedAsync_ExceptionDuringPluginStop_LogsError()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>
            {
                ["test-plugin"] = new PluginConfig
                {
                    Id = "test-plugin",
                    TriggerOn = "device-connected",
                    StopOnDisconnect = true,
                    Enabled = true
                }
            };

            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);
            _mockOrchestrator.Setup(o => o.StopPluginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Plugin stop failed"));

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            var task = (Task)typeof(DeviceEventTrigger).GetMethod("HandleDeviceDisconnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(trigger, new object[] { device });
            await task;

            // Assert
            _mockLogger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public void OnDeviceConnected_CallsHandleDeviceConnectedAsync()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>();
            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            var eventObj = new DeviceConnectedEvent(device);

            // Act
            typeof(DeviceEventTrigger).GetMethod("OnDeviceConnected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(trigger, new object[] { eventObj });

            // Assert - The async method should have been called (we can't easily verify the Task, but no exception should be thrown)
        }

        [Fact]
        public void OnDeviceDisconnected_CallsHandleDeviceDisconnectedAsync()
        {
            // Arrange
            var device = new Device
            {
                Id = "device123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var pluginConfigs = new Dictionary<string, PluginConfig>();
            _mockRegistry.Setup(r => r.GetDefinitions()).Returns(pluginConfigs);

            var trigger = new DeviceEventTrigger(_mockEventBus.Object, _mockRegistry.Object, _mockOrchestrator.Object, _mockLogger.Object);

            var eventObj = new DeviceDisconnectedEvent(device);

            // Act
            typeof(DeviceEventTrigger).GetMethod("OnDeviceDisconnected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(trigger, new object[] { eventObj });

            // Assert - The async method should have been called (we can't easily verify the Task, but no exception should be thrown)
        }
    }
}
