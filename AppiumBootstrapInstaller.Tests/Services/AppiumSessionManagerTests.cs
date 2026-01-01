using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class AppiumSessionManagerTests
    {
        private readonly Mock<ILogger<AppiumSessionManager>> _mockLogger;
        private readonly Mock<IPortManager> _mockPortManager;
        private readonly Mock<IDeviceMetrics> _mockMetrics;
        private readonly string _installFolder;
        private readonly PortRangeConfig _portConfig;

        public AppiumSessionManagerTests()
        {
            _mockLogger = new Mock<ILogger<AppiumSessionManager>>();
            _mockPortManager = new Mock<IPortManager>();
            _mockMetrics = new Mock<IDeviceMetrics>();
            _installFolder = Path.Combine(Path.GetTempPath(), "AppiumTestInstall");
            _portConfig = new PortRangeConfig { StartPort = 4723, EndPort = 4823 };
            
            // Create test directory structure and mock scripts
            CreateTestDirectoryStructure();
        }

        [Fact]
        public async Task StartSessionAsync_WithValidDevice_ReturnsSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };
            var ports = new[] { 4723, 4724 };
            var manager = CreateManager();

            _mockPortManager.Setup(x => x.AllocateConsecutivePortsAsync(2)).ReturnsAsync(ports);

            // Act
            var session = await manager.StartSessionAsync(device);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(4723, session.AppiumPort);
            Assert.Equal(4724, session.SystemPort);
            _mockPortManager.Verify(x => x.AllocateConsecutivePortsAsync(2), Times.Once);
        }

        [Fact]
        public async Task StartSessionAsync_WithPortAllocationFailure_ReturnsNull()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };
            var manager = CreateManager();

            _mockPortManager.Setup(x => x.AllocateConsecutivePortsAsync(2)).ReturnsAsync((int[])null);

            // Act
            var session = await manager.StartSessionAsync(device);

            // Assert
            Assert.Null(session);
            _mockMetrics.Verify(x => x.RecordSessionFailed(DevicePlatform.Android, "NoPortsAvailable"), Times.Once);
        }

        [Fact]
        public async Task StopSessionAsync_WithValidDevice_ReturnsTrue()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device",
                AppiumSession = new AppiumSession { AppiumPort = 4723, SystemPort = 4724 }
            };
            var manager = CreateManager();

            // Act
            var result = await manager.StopSessionAsync(device);

            // Assert
            Assert.True(result);
            _mockPortManager.Verify(x => x.ReleasePortsAsync(new[] { 4723, 4724 }), Times.Once);
            _mockMetrics.Verify(x => x.RecordSessionStopped(DevicePlatform.Android), Times.Once);
        }

        [Fact]
        public async Task StopSessionAsync_WithNoSession_ReturnsTrue()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };
            var manager = CreateManager();

            // Act
            var result = await manager.StopSessionAsync(device);

            // Assert
            Assert.True(result);
            _mockPortManager.Verify(x => x.ReleasePortsAsync(It.IsAny<int[]>()), Times.Never);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_DelegatesToPortManager()
        {
            // Arrange
            var manager = CreateManager();
            var expectedPorts = new[] { 4723, 4724, 4725 };
            _mockPortManager.Setup(x => x.AllocateConsecutivePortsAsync(3)).ReturnsAsync(expectedPorts);

            // Act
            var ports = await manager.AllocateConsecutivePortsAsync(3);

            // Assert
            Assert.Equal(expectedPorts, ports);
            _mockPortManager.Verify(x => x.AllocateConsecutivePortsAsync(3), Times.Once);
        }

        [Fact]
        public async Task ReleasePortsAsync_DelegatesToPortManager()
        {
            // Arrange
            var manager = CreateManager();
            var ports = new[] { 4723, 4724 };

            // Act
            await manager.ReleasePortsAsync(ports);

            // Assert
            _mockPortManager.Verify(x => x.ReleasePortsAsync(ports), Times.Once);
        }

        [Fact]
        public void GetAllocatedPorts_DelegatesToPortManager()
        {
            // Arrange
            var manager = CreateManager();
            var expectedPorts = new List<int> { 4723, 4724 };
            _mockPortManager.Setup(x => x.GetAllocatedPorts()).Returns(expectedPorts);

            // Act
            var ports = manager.GetAllocatedPorts();

            // Assert
            Assert.Equal(expectedPorts, ports);
            _mockPortManager.Verify(x => x.GetAllocatedPorts(), Times.Once);
        }

        private AppiumSessionManager CreateManager()
        {
            return new AppiumSessionManager(
                _mockLogger.Object,
                _installFolder,
                _portConfig,
                _mockMetrics.Object,
                _mockPortManager.Object);
        }

        private void CreateTestDirectoryStructure()
        {
            // Create directories
            var scriptsDir = Path.Combine(_installFolder, "Platform", "Windows", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            
            // Create mock script files that exit successfully
            var startScriptPath = Path.Combine(scriptsDir, "StartAppiumServer.ps1");
            File.WriteAllText(startScriptPath, "exit 0");
            
            var stopScriptPath = Path.Combine(scriptsDir, "StopAppiumServer.ps1");
            File.WriteAllText(stopScriptPath, "exit 0");
        }
    }
}