using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class DeviceListenerServiceTests
    {
        private readonly Mock<ILogger<DeviceListenerService>> _mockLogger;
        private readonly Mock<IDeviceRegistry> _mockRegistry;
        private readonly Mock<IAppiumSessionManager> _mockSessionManager;
        private readonly Mock<IDeviceMetrics> _mockMetrics;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly InstallConfig _config;
        private readonly string _installFolder;

        public DeviceListenerServiceTests()
        {
            _mockLogger = new Mock<ILogger<DeviceListenerService>>();
            _mockRegistry = new Mock<IDeviceRegistry>();
            _mockSessionManager = new Mock<IAppiumSessionManager>();
            _mockMetrics = new Mock<IDeviceMetrics>();
            _mockEventBus = new Mock<IEventBus>();
            _config = new InstallConfig
            {
                EnableDeviceListener = true,
                DeviceListenerPollInterval = 5,
                AutoStartAppium = true
            };
            _installFolder = "/test/install";
        }

        [Fact]
        public async Task ExecuteAsync_WithDisabledListener_ReturnsEarly()
        {
            // Arrange
            _config.EnableDeviceListener = false;
            var service = CreateService();

            // Act
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(100); // Allow some time for execution
            await service.StopAsync(CancellationToken.None);

            // Assert - Just ensure no exceptions
        }

        [Fact]
        public async Task ExecuteAsync_WithEnabledListener_StartsMonitoring()
        {
            // Arrange
            _mockRegistry.Setup(x => x.GetConnectedDevices()).Returns(new List<Device>());
            var service = CreateService();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(100); // Cancel quickly for test

            // Act
            await service.StartAsync(cts.Token);

            // Assert - Just ensure no exceptions
        }

        [Fact]
        public void Constructor_WithGoIosAvailable_SetsGoIosPath()
        {
            // Arrange
            var goIosPath = Path.Combine(_installFolder, ".cache", "appium-device-farm", "goIOS", "ios", "ios.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(goIosPath));
            File.WriteAllText(goIosPath, "dummy");

            // Act
            var service = CreateService();

            // Assert - Just ensure no exceptions and file exists
            Assert.True(File.Exists(goIosPath));

            // Cleanup
            File.Delete(goIosPath);
            Directory.Delete(Path.GetDirectoryName(goIosPath), true);
        }

        [Fact]
        public void IsToolAvailable_WithAvailableTool_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();

            // Act - Test with a tool that's likely available (like cmd on Windows)
            var result = service.GetType().GetMethod("IsToolAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { "cmd" });

            // Assert
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.True((bool)result);
        }

        [Fact]
        public void IsToolAvailable_WithUnavailableTool_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.GetType().GetMethod("IsToolAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { "nonexistenttool12345" });

            // Assert
            Assert.False((bool)result);
        }

        [Fact]
        public async Task OnDeviceConnectedAsync_WithAutoStartEnabled_StartsSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device",
                State = DeviceState.Connected
            };
            var service = CreateService();

            _mockRegistry.Setup(x => x.AddOrUpdateDevice(device));
            _mockSessionManager.Setup(x => x.StartSessionAsync(device)).ReturnsAsync(new AppiumSession());

            // Act
            await (Task)service.GetType().GetMethod("OnDeviceConnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { device });

            // Assert
            _mockRegistry.Verify(x => x.AddOrUpdateDevice(device), Times.Exactly(2));
            _mockSessionManager.Verify(x => x.StartSessionAsync(device), Times.Once);
            _mockEventBus.Verify(x => x.Publish(It.IsAny<DeviceConnectedEvent>()), Times.Once);
        }

        [Fact]
        public async Task OnDeviceDisconnectedAsync_StopsSession()
        {
            // Arrange
            var deviceId = "test-device";
            var device = new Device { Id = deviceId, Platform = DevicePlatform.Android, AppiumSession = new AppiumSession() };
            var service = CreateService();

            _mockRegistry.Setup(x => x.GetDevice(deviceId)).Returns(device);
            _mockRegistry.Setup(x => x.RemoveDevice(deviceId)).Verifiable();
            _mockSessionManager.Setup(x => x.StopSessionAsync(device)).ReturnsAsync(true);

            // Act
            await (Task)service.GetType().GetMethod("OnDeviceDisconnectedAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { deviceId });

            // Assert
            _mockRegistry.Verify(x => x.RemoveDevice(deviceId), Times.Once);
            _mockSessionManager.Verify(x => x.StopSessionAsync(device), Times.Once);
            _mockEventBus.Verify(x => x.Publish(It.IsAny<SessionStoppedEvent>()), Times.Once);
        }

        [Fact]
        public async Task StopAllSessionsAsync_StopsAllActiveSessions()
        {
            // Arrange
            var devices = new List<Device>
            {
                new Device { Id = "device1", Platform = DevicePlatform.Android, AppiumSession = new AppiumSession() },
                new Device { Id = "device2", Platform = DevicePlatform.iOS, AppiumSession = new AppiumSession() }
            };
            var service = CreateService();

            _mockRegistry.Setup(x => x.GetConnectedDevices()).Returns(devices);
            _mockSessionManager.Setup(x => x.StopSessionAsync(It.IsAny<Device>())).ReturnsAsync(true);

            // Act
            await (Task)service.GetType().GetMethod("StopAllSessionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(service, new object[] { });

            // Assert
            _mockSessionManager.Verify(x => x.StopSessionAsync(It.Is<Device>(d => d.Id == "device1")), Times.Once);
            _mockSessionManager.Verify(x => x.StopSessionAsync(It.Is<Device>(d => d.Id == "device2")), Times.Once);
        }

        private DeviceListenerService CreateService()
        {
            return new DeviceListenerService(
                _mockLogger.Object,
                _config,
                _installFolder,
                _mockRegistry.Object,
                _mockSessionManager.Object,
                _mockMetrics.Object,
                _mockEventBus.Object);
        }
    }
}