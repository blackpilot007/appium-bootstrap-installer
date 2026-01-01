using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class HealthCheckServiceTests
    {
        private readonly Mock<IDeviceRegistry> _mockRegistry;
        private readonly Mock<ILogger<HealthCheckService>> _mockLogger;
        private readonly HealthCheckService _healthCheckService;

        public HealthCheckServiceTests()
        {
            _mockRegistry = new Mock<IDeviceRegistry>();
            _mockLogger = new Mock<ILogger<HealthCheckService>>();
            _healthCheckService = new HealthCheckService(_mockRegistry.Object, _mockLogger.Object);
        }

        [Fact]
        public void GetHealth_NoDevices_ReturnsHealthyStatus()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device>());
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device>());

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // No devices is considered healthy
            Assert.Equal(0, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions);
            Assert.Equal(0, health.RunningPlugins);
            Assert.Contains("DeviceRegistry", health.ComponentStatus);
            Assert.Contains("SessionManager", health.ComponentStatus);
            Assert.Contains("EventBus", health.ComponentStatus);
            Assert.Equal("NoDevices", health.ComponentStatus["DeviceRegistry"]);
            Assert.Equal("NoSessions", health.ComponentStatus["SessionManager"]);
            Assert.Equal("Healthy", health.ComponentStatus["EventBus"]);
        }

        [Fact]
        public void GetHealth_WithConnectedDevicesAndSessions_ReturnsHealthyStatus()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Running
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device> { device });
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy);
            Assert.Equal(1, health.ConnectedDevices);
            Assert.Equal(1, health.ActiveSessions);
            Assert.Equal("Healthy", health.ComponentStatus["DeviceRegistry"]);
            Assert.Equal("Healthy", health.ComponentStatus["SessionManager"]);
        }

        [Fact]
        public void GetHealth_WithDisconnectedDevice_IgnoresSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Disconnected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Running
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device>()); // No connected devices
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // No connected devices/sessions is healthy
            Assert.Equal(0, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions); // Should not count sessions from disconnected devices
        }

        [Fact]
        public void GetHealth_WithStoppedSession_IgnoresSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Stopped
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device> { device });
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // Stopped sessions don't count as active, but service is still healthy
            Assert.Equal(1, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions); // Stopped sessions don't count as active
            Assert.Equal("NoSessions", health.ComponentStatus["SessionManager"]);
        }

        [Fact]
        public void GetHealth_WithFailedSession_IgnoresSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Failed
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device> { device });
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // Failed sessions don't count as active, but service is still healthy
            Assert.Equal(1, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions); // Failed sessions don't count as active
        }

        [Fact]
        public void GetHealth_WithStartingSession_IgnoresSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Starting
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device> { device });
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // Starting sessions don't count as active, but service is still healthy
            Assert.Equal(1, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions); // Starting sessions don't count as active
        }

        [Fact]
        public void GetHealth_WithStoppingSession_IgnoresSession()
        {
            // Arrange
            var device = new Device
            {
                Id = "test-device",
                Platform = DevicePlatform.Android,
                State = DeviceState.Connected,
                AppiumSession = new AppiumSession
                {
                    SessionId = "test-session",
                    Status = SessionStatus.Stopping
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device> { device });
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device> { device });

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy); // Stopping sessions don't count as active, but service is still healthy
            Assert.Equal(1, health.ConnectedDevices);
            Assert.Equal(0, health.ActiveSessions); // Stopping sessions don't count as active
        }

        [Fact]
        public void GetUptime_ReturnsPositiveTimeSpan()
        {
            // Act
            var uptime = _healthCheckService.GetUptime();

            // Assert
            Assert.True(uptime.TotalMilliseconds >= 0);
        }

        [Fact]
        public void GetHealth_IncludesUptime()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device>());
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device>());

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.Uptime.TotalMilliseconds >= 0);
        }

        [Fact]
        public void GetHealth_MultipleDevicesAndSessions()
        {
            // Arrange
            var devices = new List<Device>
            {
                new Device
                {
                    Id = "device1",
                    Platform = DevicePlatform.Android,
                    State = DeviceState.Connected,
                    AppiumSession = new AppiumSession { SessionId = "session1", Status = SessionStatus.Running }
                },
                new Device
                {
                    Id = "device2",
                    Platform = DevicePlatform.iOS,
                    State = DeviceState.Connected,
                    AppiumSession = new AppiumSession { SessionId = "session2", Status = SessionStatus.Running }
                },
                new Device
                {
                    Id = "device3",
                    Platform = DevicePlatform.Android,
                    State = DeviceState.Connected,
                    AppiumSession = new AppiumSession { SessionId = "session3", Status = SessionStatus.Stopped }
                }
            };

            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(devices);
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(devices);

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.True(health.IsHealthy);
            Assert.Equal(3, health.ConnectedDevices);
            Assert.Equal(2, health.ActiveSessions); // Only 2 running sessions
        }

        [Fact]
        public void GetHealth_ComponentStatusDictionary_IsNotNull()
        {
            // Arrange
            _mockRegistry.Setup(r => r.GetConnectedDevices()).Returns(new List<Device>());
            _mockRegistry.Setup(r => r.GetAllDevices()).Returns(new List<Device>());

            // Act
            var health = _healthCheckService.GetHealth();

            // Assert
            Assert.NotNull(health.ComponentStatus);
            Assert.NotEmpty(health.ComponentStatus);
        }
    }
}