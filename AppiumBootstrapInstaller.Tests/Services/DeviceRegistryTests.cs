using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class DeviceRegistryTests : IDisposable
    {
        private readonly Mock<ILogger<DeviceRegistry>> _mockLogger;
        private readonly DeviceRegistryConfig _config;
        private readonly string _testDir;
        private readonly DeviceRegistry _registry;

        public DeviceRegistryTests()
        {
            _mockLogger = new Mock<ILogger<DeviceRegistry>>();
            _testDir = Path.Combine(Path.GetTempPath(), $"DeviceRegistryTest_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDir);
            _config = new DeviceRegistryConfig
            {
                Enabled = true,
                FilePath = Path.Combine(_testDir, "devices.json"),
                AutoSave = false
            };
            _registry = new DeviceRegistry(_mockLogger.Object, _config);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public void AddOrUpdateDevice_WithNewDevice_AddsToRegistry()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device",
                State = DeviceState.Connected
            };

            // Act
            _registry.AddOrUpdateDevice(device);

            // Assert
            var retrieved = _registry.GetDevice("test-device");
            Assert.NotNull(retrieved);
            Assert.Equal("test-device", retrieved.Id);
            Assert.Equal(DevicePlatform.Android, retrieved.Platform);
        }

        [Fact]
        public void AddOrUpdateDevice_WithExistingDevice_UpdatesDevice()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device",
                State = DeviceState.Connected
            };
            _registry.AddOrUpdateDevice(device);

            var updatedDevice = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Updated Device",
                State = DeviceState.Disconnected
            };

            // Act
            _registry.AddOrUpdateDevice(updatedDevice);

            // Assert
            var retrieved = _registry.GetDevice("test-device");
            Assert.NotNull(retrieved);
            Assert.Equal("Updated Device", retrieved.Name);
            Assert.Equal(DeviceState.Disconnected, retrieved.State);
        }

        [Fact]
        public void GetDevice_WithExistingDevice_ReturnsDevice()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };
            _registry.AddOrUpdateDevice(device);

            // Act
            var retrieved = _registry.GetDevice("test-device");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("test-device", retrieved.Id);
        }

        [Fact]
        public void GetDevice_WithNonExistingDevice_ReturnsNull()
        {
            // Act
            var retrieved = _registry.GetDevice("non-existing");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void GetAllDevices_ReturnsAllDevices()
        {
            // Arrange
            var device1 = new Device { Id = "device1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "device2", Platform = DevicePlatform.iOS };
            _registry.AddOrUpdateDevice(device1);
            _registry.AddOrUpdateDevice(device2);

            // Act
            var devices = _registry.GetAllDevices();

            // Assert
            Assert.Equal(2, devices.Count);
            Assert.Contains(devices, d => d.Id == "device1");
            Assert.Contains(devices, d => d.Id == "device2");
        }

        [Fact]
        public void GetConnectedDevices_ReturnsOnlyConnectedDevices()
        {
            // Arrange
            var connectedDevice = new Device
            {
                Id = "connected",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected
            };
            var disconnectedDevice = new Device
            {
                Id = "disconnected",
                Platform = DevicePlatform.iOS,
                State = DeviceState.Disconnected
            };
            _registry.AddOrUpdateDevice(connectedDevice);
            _registry.AddOrUpdateDevice(disconnectedDevice);

            // Act
            var connectedDevices = _registry.GetConnectedDevices();

            // Assert
            Assert.Single(connectedDevices);
            Assert.Equal("connected", connectedDevices.First().Id);
        }

        [Fact]
        public void RemoveDevice_WithExistingDevice_RemovesFromRegistry()
        {
            // Arrange
            var device = new Device { Id = "test-device", Platform = DevicePlatform.Android };
            _registry.AddOrUpdateDevice(device);

            // Act
            _registry.RemoveDevice("test-device");

            // Assert
            var retrieved = _registry.GetDevice("test-device");
            Assert.NotNull(retrieved);
            Assert.Equal(DeviceState.Disconnected, retrieved.State);
            Assert.NotNull(retrieved.DisconnectedAt);
        }

        [Fact]
        public void RemoveDevice_WithNonExistingDevice_DoesNothing()
        {
            // Act
            _registry.RemoveDevice("non-existing");

            // Assert - No exception thrown
        }

        [Fact]
        public void GetAllDevices_WhenEmpty_ReturnsEmptyCollection()
        {
            // Act
            var devices = _registry.GetAllDevices();

            // Assert
            Assert.Empty(devices);
        }

        [Fact]
        public void GetConnectedDevices_WhenNoConnectedDevices_ReturnsEmptyCollection()
        {
            // Arrange
            var disconnectedDevice = new Device
            {
                Id = "disconnected",
                Platform = DevicePlatform.Android,
                State = DeviceState.Disconnected
            };
            _registry.AddOrUpdateDevice(disconnectedDevice);

            // Act
            var connectedDevices = _registry.GetConnectedDevices();

            // Assert
            Assert.Empty(connectedDevices);
        }
    }
}