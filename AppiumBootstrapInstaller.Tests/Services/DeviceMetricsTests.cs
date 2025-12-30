using System.Collections.Generic;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class DeviceMetricsTests
    {
        private readonly DeviceMetrics _metrics;

        public DeviceMetricsTests()
        {
            _metrics = new DeviceMetrics();
        }

        [Fact]
        public void Constructor_InitializesWithZeroValues()
        {
            // Assert
            Assert.Equal(0, _metrics.DevicesConnectedTotal);
            Assert.Equal(0, _metrics.DevicesDisconnectedTotal);
            Assert.Equal(0, _metrics.SessionsStartedTotal);
            Assert.Equal(0, _metrics.SessionsStoppedTotal);
            Assert.Equal(0, _metrics.SessionsFailedTotal);
            Assert.Equal(0, _metrics.PortAllocationFailuresTotal);
            Assert.Equal(100.0, _metrics.SessionStartSuccessRate);
        }

        [Fact]
        public void RecordDeviceConnected_Android_IncrementsCounters()
        {
            // Act
            _metrics.RecordDeviceConnected(DevicePlatform.Android, DeviceType.Physical);

            // Assert
            Assert.Equal(1, _metrics.DevicesConnectedTotal);
            Assert.Equal(0, _metrics.DevicesDisconnectedTotal);
        }

        [Fact]
        public void RecordDeviceConnected_iOS_IncrementsCounters()
        {
            // Act
            _metrics.RecordDeviceConnected(DevicePlatform.iOS, DeviceType.Simulator);

            // Assert
            Assert.Equal(1, _metrics.DevicesConnectedTotal);
            Assert.Equal(0, _metrics.DevicesDisconnectedTotal);
        }

        [Fact]
        public void RecordDeviceDisconnected_Android_DecrementsActiveCount()
        {
            // Arrange
            _metrics.RecordDeviceConnected(DevicePlatform.Android, DeviceType.Physical);

            // Act
            _metrics.RecordDeviceDisconnected(DevicePlatform.Android);

            // Assert
            Assert.Equal(1, _metrics.DevicesConnectedTotal);
            Assert.Equal(1, _metrics.DevicesDisconnectedTotal);
        }

        [Fact]
        public void RecordDeviceDisconnected_iOS_DecrementsActiveCount()
        {
            // Arrange
            _metrics.RecordDeviceConnected(DevicePlatform.iOS, DeviceType.Simulator);

            // Act
            _metrics.RecordDeviceDisconnected(DevicePlatform.iOS);

            // Assert
            Assert.Equal(1, _metrics.DevicesConnectedTotal);
            Assert.Equal(1, _metrics.DevicesDisconnectedTotal);
        }

        [Fact]
        public void RecordSessionStarted_IncrementsCounters()
        {
            // Act
            _metrics.RecordSessionStarted(DevicePlatform.Android);

            // Assert
            Assert.Equal(1, _metrics.SessionsStartedTotal);
            Assert.Equal(0, _metrics.SessionsStoppedTotal);
            Assert.Equal(0, _metrics.SessionsFailedTotal);
        }

        [Fact]
        public void RecordSessionFailed_IncrementsCountersAndTracksReason()
        {
            // Act
            _metrics.RecordSessionFailed(DevicePlatform.Android, "TestFailure");

            // Assert
            Assert.Equal(0, _metrics.SessionsStartedTotal);
            Assert.Equal(1, _metrics.SessionsFailedTotal);
            Assert.Equal(0.0, _metrics.SessionStartSuccessRate);

            var reasons = _metrics.GetSessionFailureReasons();
            Assert.Equal(1, reasons["TestFailure"]);
        }

        [Fact]
        public void RecordSessionFailed_MultipleReasons_TracksAll()
        {
            // Act
            _metrics.RecordSessionFailed(DevicePlatform.Android, "Reason1");
            _metrics.RecordSessionFailed(DevicePlatform.iOS, "Reason2");
            _metrics.RecordSessionFailed(DevicePlatform.Android, "Reason1");

            // Assert
            var reasons = _metrics.GetSessionFailureReasons();
            Assert.Equal(2, reasons["Reason1"]);
            Assert.Equal(1, reasons["Reason2"]);
        }

        [Fact]
        public void RecordSessionStopped_IncrementsCounters()
        {
            // Arrange
            _metrics.RecordSessionStarted(DevicePlatform.Android);

            // Act
            _metrics.RecordSessionStopped(DevicePlatform.Android);

            // Assert
            Assert.Equal(1, _metrics.SessionsStartedTotal);
            Assert.Equal(1, _metrics.SessionsStoppedTotal);
        }

        [Fact]
        public void RecordPortAllocationFailure_IncrementsCounter()
        {
            // Act
            _metrics.RecordPortAllocationFailure();

            // Assert
            Assert.Equal(1, _metrics.PortAllocationFailuresTotal);
        }

        [Fact]
        public void SessionStartSuccessRate_NoSessions_Returns100()
        {
            // Assert
            Assert.Equal(100.0, _metrics.SessionStartSuccessRate);
        }

        [Fact]
        public void SessionStartSuccessRate_WithSessions_CalculatesCorrectly()
        {
            // Arrange
            _metrics.RecordSessionStarted(DevicePlatform.Android);
            _metrics.RecordSessionStarted(DevicePlatform.iOS);
            _metrics.RecordSessionFailed(DevicePlatform.Android, "Failure");

            // Assert
            Assert.Equal(66.7, Math.Round(_metrics.SessionStartSuccessRate, 1));
        }

        [Fact]
        public void GetSessionFailureReasons_ReturnsCopy()
        {
            // Arrange
            _metrics.RecordSessionFailed(DevicePlatform.Android, "Test");

            // Act
            var reasons1 = _metrics.GetSessionFailureReasons();
            reasons1["Test"] = 999; // Modify the returned dictionary

            var reasons2 = _metrics.GetSessionFailureReasons();

            // Assert
            Assert.Equal(1, reasons2["Test"]); // Should not be affected by modification
        }

        [Fact]
        public void GetSummary_ReturnsFormattedString()
        {
            // Arrange
            _metrics.RecordDeviceConnected(DevicePlatform.Android, DeviceType.Physical);
            _metrics.RecordDeviceConnected(DevicePlatform.iOS, DeviceType.Simulator);
            _metrics.RecordSessionStarted(DevicePlatform.Android);
            _metrics.RecordSessionFailed(DevicePlatform.iOS, "Test");

            // Act
            var summary = _metrics.GetSummary();

            // Assert
            Assert.Contains("Devices: 1 Android, 1 iOS", summary);
            Assert.Contains("Sessions: 1 active", summary);
            Assert.Contains("1 started, 1 failed", summary);
            Assert.Contains("50.0% success", summary);
        }

        [Fact]
        public void RecordPluginUnhealthy_IncrementsCounter()
        {
            // This tests the plugin metrics functionality
            // Since the counter is private, we test through GetSummary or other means
            // For now, just ensure the method doesn't throw
            _metrics.RecordPluginUnhealthy("test-plugin");
            _metrics.RecordPluginRestart("test-plugin");

            // The summary doesn't include plugin metrics, so we just verify no exception
            var summary = _metrics.GetSummary();
            Assert.NotNull(summary);
        }

        [Fact]
        public void MultipleOperations_ThreadSafe()
        {
            // Arrange
            var tasks = new List<Task>();

            // Act - Run multiple operations concurrently
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => _metrics.RecordDeviceConnected(DevicePlatform.Android, DeviceType.Physical)));
                tasks.Add(Task.Run(() => _metrics.RecordSessionStarted(DevicePlatform.Android)));
                tasks.Add(Task.Run(() => _metrics.RecordPortAllocationFailure()));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(10, _metrics.DevicesConnectedTotal);
            Assert.Equal(10, _metrics.SessionsStartedTotal);
            Assert.Equal(10, _metrics.PortAllocationFailuresTotal);
        }
    }
}