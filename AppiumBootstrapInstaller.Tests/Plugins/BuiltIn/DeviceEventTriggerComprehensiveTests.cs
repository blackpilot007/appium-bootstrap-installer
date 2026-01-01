using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins;
using AppiumBootstrapInstaller.Plugins.Triggers;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins.BuiltIn
{
    /// <summary>
    /// Comprehensive tests for DeviceEventTrigger covering all trigger scenarios
    /// </summary>
    public class DeviceEventTriggerComprehensiveTests
    {
        private readonly Mock<ILogger<DeviceEventTrigger>> _mockLogger;
        private readonly Mock<ILogger<EventBus>> _mockEventBusLogger;
        private readonly Mock<IPluginOrchestrator> _mockOrchestrator;
        private readonly IEventBus _eventBus;
        private readonly PluginRegistry _registry;

        public DeviceEventTriggerComprehensiveTests()
        {
            _mockLogger = new Mock<ILogger<DeviceEventTrigger>>();
            _mockEventBusLogger = new Mock<ILogger<EventBus>>();
            _mockOrchestrator = new Mock<IPluginOrchestrator>();
            _eventBus = new EventBus(_mockEventBusLogger.Object);
            _registry = new PluginRegistry();
        }

        #region Device Connected Trigger Tests

        [Fact]
        public async Task DeviceConnectedTrigger_SinglePlugin_ActivatesPlugin()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "connect-plugin",
                Name = "Connect Trigger Plugin",
                Type = "script",
                Executable = "echo",
                Arguments = new List<string> { "Device connected" },
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("connect-plugin", pluginConfig);

            var device = new Device
            {
                Id = "test-device-123",
                Platform = DevicePlatform.Android,
                Name = "Test Android Device"
            };

            string? activatedPluginId = null;
            PluginContext? capturedContext = null;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) =>
                {
                    activatedPluginId = id;
                    capturedContext = ctx;
                })
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200); // Allow async processing

            // Assert
            Assert.Equal("connect-plugin", activatedPluginId);
            Assert.NotNull(capturedContext);
            Assert.NotNull(capturedContext.Variables);
            Assert.True(capturedContext.Variables.ContainsKey("deviceId"));
            Assert.Equal("test-device-123", capturedContext.Variables["deviceId"]);
        }

        [Fact]
        public async Task DeviceConnectedTrigger_MultiplePlugins_ActivatesAllPlugins()
        {
            // Arrange
            var plugin1 = new PluginConfig
            {
                Id = "connect-plugin-1",
                Name = "Plugin 1",
                Type = "script",
                TriggerOn = "device-connected",
                Enabled = true
            };

            var plugin2 = new PluginConfig
            {
                Id = "connect-plugin-2",
                Name = "Plugin 2",
                Type = "script",
                TriggerOn = "device-connected",
                Enabled = true
            };

            var plugin3 = new PluginConfig
            {
                Id = "connect-plugin-3",
                Name = "Plugin 3",
                Type = "process",
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("connect-plugin-1", plugin1);
            _registry.RegisterDefinition("connect-plugin-2", plugin2);
            _registry.RegisterDefinition("connect-plugin-3", plugin3);

            var device = new Device { Id = "multi-device", Platform = DevicePlatform.Android };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Equal(3, activatedPlugins.Count);
            Assert.Contains("connect-plugin-1", activatedPlugins);
            Assert.Contains("connect-plugin-2", activatedPlugins);
            Assert.Contains("connect-plugin-3", activatedPlugins);
        }

        [Fact]
        public async Task DeviceConnectedTrigger_CaseInsensitiveTrigger_ActivatesPlugin()
        {
            // Arrange
            var pluginConfigs = new[]
            {
                new PluginConfig { Id = "upper-case", TriggerOn = "DEVICE-CONNECTED", Enabled = true },
                new PluginConfig { Id = "mixed-case", TriggerOn = "Device-Connected", Enabled = true },
                new PluginConfig { Id = "lower-case", TriggerOn = "device-connected", Enabled = true }
            };

            foreach (var config in pluginConfigs)
            {
                _registry.RegisterDefinition(config.Id!, config);
            }

            var device = new Device { Id = "case-test-device", Platform = DevicePlatform.Android };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Equal(3, activatedPlugins.Count);
        }

        [Fact]
        public async Task DeviceConnectedTrigger_AndroidDevice_PassesDeviceInfoToPlugin()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "android-plugin",
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("android-plugin", pluginConfig);

            var device = new Device
            {
                Id = "android-123",
                Platform = DevicePlatform.Android,
                Name = "Pixel 7"
            };

            Device? capturedDevice = null;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) =>
                {
                    if (ctx.Variables?.ContainsKey("device") == true)
                    {
                        capturedDevice = ctx.Variables["device"] as Device;
                    }
                })
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.NotNull(capturedDevice);
            Assert.Equal("android-123", capturedDevice.Id);
            Assert.Equal(DevicePlatform.Android, capturedDevice.Platform);
            Assert.Equal("Pixel 7", capturedDevice.Name);
        }

        [Fact]
        public async Task DeviceConnectedTrigger_iOSDevice_PassesDeviceInfoToPlugin()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "ios-plugin",
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("ios-plugin", pluginConfig);

            var device = new Device
            {
                Id = "ios-456",
                Platform = DevicePlatform.iOS,
                Name = "iPhone 14"
            };

            Device? capturedDevice = null;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) =>
                {
                    if (ctx.Variables?.ContainsKey("device") == true)
                    {
                        capturedDevice = ctx.Variables["device"] as Device;
                    }
                })
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.NotNull(capturedDevice);
            Assert.Equal("ios-456", capturedDevice.Id);
            Assert.Equal(DevicePlatform.iOS, capturedDevice.Platform);
        }

        #endregion

        #region Device Disconnected Trigger Tests

        [Fact]
        public async Task DeviceDisconnectedTrigger_SinglePlugin_ActivatesPlugin()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "disconnect-plugin",
                Name = "Disconnect Trigger Plugin",
                TriggerOn = "device-disconnected",
                Enabled = true
            };

            _registry.RegisterDefinition("disconnect-plugin", pluginConfig);

            var device = new Device
            {
                Id = "disconnect-device-789",
                Platform = DevicePlatform.Android,
                State = DeviceState.Disconnected
            };

            string? activatedPluginId = null;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPluginId = id)
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Equal("disconnect-plugin", activatedPluginId);
        }

        [Fact]
        public async Task DeviceDisconnectedTrigger_MultiplePlugins_ActivatesAllPlugins()
        {
            // Arrange
            var plugin1 = new PluginConfig { Id = "disconnect-1", TriggerOn = "device-disconnected", Enabled = true };
            var plugin2 = new PluginConfig { Id = "disconnect-2", TriggerOn = "device-disconnected", Enabled = true };

            _registry.RegisterDefinition("disconnect-1", plugin1);
            _registry.RegisterDefinition("disconnect-2", plugin2);

            var device = new Device { Id = "multi-disconnect", Platform = DevicePlatform.iOS, State = DeviceState.Disconnected };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Equal(2, activatedPlugins.Count);
            Assert.Contains("disconnect-1", activatedPlugins);
            Assert.Contains("disconnect-2", activatedPlugins);
        }

        #endregion

        #region StopOnDisconnect Tests

        [Fact]
        public async Task DeviceDisconnectedTrigger_PluginWithStopOnDisconnect_StopsPlugin()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "stop-on-disconnect-plugin",
                TriggerOn = "device-connected",
                StopOnDisconnect = true,
                Enabled = true
            };

            _registry.RegisterDefinition("stop-on-disconnect-plugin", pluginConfig);

            var device = new Device { Id = "stop-test-device", Platform = DevicePlatform.Android };

            var stopCalled = false;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockOrchestrator
                .Setup(o => o.StopPluginAsync(It.Is<string>(id => id == "stop-on-disconnect-plugin:stop-test-device"), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((id, ct) => stopCalled = true)
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act - Connect device
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Act - Disconnect device
            device.State = DeviceState.Disconnected;
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.True(stopCalled);
        }

        #endregion

        #region Trigger Filter Tests

        [Fact]
        public async Task MixedTriggers_ConnectEvent_OnlyConnectPluginsActivated()
        {
            // Arrange
            var connectPlugin = new PluginConfig { Id = "connect", TriggerOn = "device-connected", Enabled = true };
            var disconnectPlugin = new PluginConfig { Id = "disconnect", TriggerOn = "device-disconnected", Enabled = true };
            var noTriggerPlugin = new PluginConfig { Id = "no-trigger", TriggerOn = null, Enabled = true };

            _registry.RegisterDefinition("connect", connectPlugin);
            _registry.RegisterDefinition("disconnect", disconnectPlugin);
            _registry.RegisterDefinition("no-trigger", noTriggerPlugin);

            var device = new Device { Id = "filter-device", Platform = DevicePlatform.Android };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Single(activatedPlugins);
            Assert.Contains("connect", activatedPlugins);
            Assert.DoesNotContain("disconnect", activatedPlugins);
            Assert.DoesNotContain("no-trigger", activatedPlugins);
        }

        [Fact]
        public async Task MixedTriggers_DisconnectEvent_OnlyDisconnectPluginsActivated()
        {
            // Arrange
            var connectPlugin = new PluginConfig { Id = "connect", TriggerOn = "device-connected", Enabled = true };
            var disconnectPlugin = new PluginConfig { Id = "disconnect", TriggerOn = "device-disconnected", Enabled = true };

            _registry.RegisterDefinition("connect", connectPlugin);
            _registry.RegisterDefinition("disconnect", disconnectPlugin);

            var device = new Device { Id = "filter-device-2", Platform = DevicePlatform.iOS, State = DeviceState.Disconnected };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Single(activatedPlugins);
            Assert.Contains("disconnect", activatedPlugins);
            Assert.DoesNotContain("connect", activatedPlugins);
        }

        #endregion

        #region Multi-Device Scenarios

        [Fact]
        public async Task MultiDevice_TwoDevicesConnect_PluginActivatedForEach()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "multi-device-plugin",
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("multi-device-plugin", pluginConfig);

            var device1 = new Device { Id = "device-1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "device-2", Platform = DevicePlatform.iOS };

            var activationCount = 0;
            var capturedDeviceIds = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) =>
                {
                    activationCount++;
                    if (ctx.Variables?.ContainsKey("deviceId") == true)
                    {
                        capturedDeviceIds.Add(ctx.Variables["deviceId"]?.ToString() ?? string.Empty);
                    }
                })
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device1));
            await Task.Delay(100);
            _eventBus.Publish(new DeviceConnectedEvent(device2));
            await Task.Delay(100);

            // Assert
            Assert.Equal(2, activationCount);
            Assert.Contains("device-1", capturedDeviceIds);
            Assert.Contains("device-2", capturedDeviceIds);
        }

        [Fact]
        public async Task MultiDevice_SimultaneousConnections_HandlesCorrectly()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "concurrent-plugin",
                TriggerOn = "device-connected",
                Enabled = true
            };

            _registry.RegisterDefinition("concurrent-plugin", pluginConfig);

            var devices = Enumerable.Range(1, 10).Select(i => new Device
            {
                Id = $"concurrent-device-{i}",
                Platform = i % 2 == 0 ? DevicePlatform.Android : DevicePlatform.iOS
            }).ToList();

            var activationCount = 0;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => Interlocked.Increment(ref activationCount))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            foreach (var device in devices)
            {
                _eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(500);

            // Assert
            Assert.Equal(10, activationCount);
        }

        #endregion

        #region Plugin Lifecycle Tests

        [Fact]
        public async Task PluginLifecycle_ConnectDisconnectCycle_ProperlyManagesPluginState()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "lifecycle-plugin",
                TriggerOn = "device-connected",
                StopOnDisconnect = true,
                Enabled = true
            };

            _registry.RegisterDefinition("lifecycle-plugin", pluginConfig);

            var device = new Device { Id = "lifecycle-device", Platform = DevicePlatform.Android };

            var startCount = 0;
            var stopCount = 0;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => startCount++)
                .ReturnsAsync(true);

            _mockOrchestrator
                .Setup(o => o.StopPluginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => stopCount++)
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(100);

            device.State = DeviceState.Disconnected;
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(100);

            // Re-connect
            device.State = DeviceState.Connected;
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(100);

            // Assert
            Assert.Equal(2, startCount); // Started twice
            Assert.Equal(1, stopCount); // Stopped once
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task DeviceConnectedTrigger_OrchestratorFails_ContinuesProcessing()
        {
            // Arrange
            var plugin1 = new PluginConfig { Id = "plugin-1", TriggerOn = "device-connected", Enabled = true };
            var plugin2 = new PluginConfig { Id = "plugin-2", TriggerOn = "device-connected", Enabled = true };

            _registry.RegisterDefinition("plugin-1", plugin1);
            _registry.RegisterDefinition("plugin-2", plugin2);

            var device = new Device { Id = "error-device", Platform = DevicePlatform.Android };

            var activatedPlugins = new List<string>();

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync("plugin-1", It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Plugin 1 failed"));

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync("plugin-2", It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback<string, PluginContext, CancellationToken>((id, ctx, ct) => activatedPlugins.Add(id))
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            // Even though plugin-1 failed, plugin-2 should still be activated
            Assert.Contains("plugin-2", activatedPlugins);
        }

        [Fact]
        public async Task DeviceDisconnectedTrigger_StopPluginFails_LogsButContinues()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "stop-fail-plugin",
                TriggerOn = "device-connected",
                StopOnDisconnect = true,
                Enabled = true
            };

            _registry.RegisterDefinition("stop-fail-plugin", pluginConfig);

            var device = new Device { Id = "stop-fail-device", Platform = DevicePlatform.Android };

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockOrchestrator
                .Setup(o => o.StopPluginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Stop failed"));

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act - Should not throw
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(100);

            device.State = DeviceState.Disconnected;
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(100);

            // Assert - No exception thrown
        }

        #endregion

        #region Disabled Plugin Tests

        [Fact]
        public async Task DeviceConnectedTrigger_DisabledPlugin_DoesNotActivate()
        {
            // Arrange
            var pluginConfig = new PluginConfig
            {
                Id = "disabled-plugin",
                TriggerOn = "device-connected",
                Enabled = false // Disabled
            };

            _registry.RegisterDefinition("disabled-plugin", pluginConfig);

            var device = new Device { Id = "disabled-test", Platform = DevicePlatform.Android };

            var activationCount = 0;

            _mockOrchestrator
                .Setup(o => o.StartPluginAsync(It.IsAny<string>(), It.IsAny<PluginContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => activationCount++)
                .ReturnsAsync(true);

            var trigger = new DeviceEventTrigger(_eventBus, _registry, _mockOrchestrator.Object, _mockLogger.Object);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(200);

            // Assert
            Assert.Equal(0, activationCount);
        }

        #endregion
    }
}
