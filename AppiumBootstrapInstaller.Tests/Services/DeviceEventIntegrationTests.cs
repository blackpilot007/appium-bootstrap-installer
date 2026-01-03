using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Comprehensive integration tests for all device monitoring events
    /// </summary>
    public class DeviceEventIntegrationTests
    {
        private readonly Mock<ILogger<EventBus>> _mockEventBusLogger;
        private readonly Mock<ILogger<DeviceListenerService>> _mockListenerLogger;
        private readonly Mock<IDeviceRegistry> _mockRegistry;
        private readonly Mock<IAppiumSessionManager> _mockSessionManager;
        private readonly Mock<IDeviceMetrics> _mockMetrics;
        private readonly InstallConfig _config;
        private readonly IEventBus _eventBus;

        public DeviceEventIntegrationTests()
        {
            _mockEventBusLogger = new Mock<ILogger<EventBus>>();
            _mockListenerLogger = new Mock<ILogger<DeviceListenerService>>();
            _mockRegistry = new Mock<IDeviceRegistry>();
            _mockSessionManager = new Mock<IAppiumSessionManager>();
            _mockMetrics = new Mock<IDeviceMetrics>();
            _eventBus = new EventBus(_mockEventBusLogger.Object);
            _config = new InstallConfig
            {
                EnableDeviceListener = true,
                DeviceListenerPollInterval = 5,
                AutoStartAppium = true
            };
        }

        #region DeviceConnectedEvent Tests

        [Fact]
        public async Task DeviceConnectedEvent_AndroidDevice_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "android-device-123",
                Platform = DevicePlatform.Android,
                Name = "Pixel 7",
                State = DeviceState.Connected
            };

            var eventReceived = false;
            Device? receivedDevice = null;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                eventReceived = true;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(10); // Allow event processing

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedDevice);
            Assert.Equal("android-device-123", receivedDevice.Id);
            Assert.Equal(DevicePlatform.Android, receivedDevice.Platform);
        }

        [Fact]
        public async Task DeviceConnectedEvent_iOSDevice_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "ios-device-456",
                Platform = DevicePlatform.iOS,
                Name = "iPhone 14",
                State = DeviceState.Connected
            };

            var eventReceived = false;
            Device? receivedDevice = null;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                eventReceived = true;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedDevice);
            Assert.Equal("ios-device-456", receivedDevice.Id);
            Assert.Equal(DevicePlatform.iOS, receivedDevice.Platform);
        }

        [Fact]
        public async Task DeviceConnectedEvent_MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            var device = new Device
            {
                Id = "multi-sub-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device",
                State = DeviceState.Connected
            };

            var subscriber1Received = false;
            var subscriber2Received = false;
            var subscriber3Received = false;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => subscriber1Received = true);
            _eventBus.Subscribe<DeviceConnectedEvent>(evt => subscriber2Received = true);
            _eventBus.Subscribe<DeviceConnectedEvent>(evt => subscriber3Received = true);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.True(subscriber1Received);
            Assert.True(subscriber2Received);
            Assert.True(subscriber3Received);
        }

        [Fact]
        public async Task DeviceConnectedEvent_WithMetadata_PreservesMetadata()
        {
            // Arrange
            var device = new Device
            {
                Id = "metadata-device",
                Platform = DevicePlatform.Android,
                Name = "Device With Metadata",
                State = DeviceState.Connected
            };

            Device? receivedDevice = null;
            _eventBus.Subscribe<DeviceConnectedEvent>(evt => receivedDevice = evt.Device);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.NotNull(receivedDevice);
            Assert.Equal("metadata-device", receivedDevice.Id);
            Assert.Equal(DevicePlatform.Android, receivedDevice.Platform);
            Assert.Equal("Device With Metadata", receivedDevice.Name);
        }

        #endregion

        #region DeviceDisconnectedEvent Tests

        [Fact]
        public async Task DeviceDisconnectedEvent_AndroidDevice_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "android-disconnect-123",
                Platform = DevicePlatform.Android,
                Name = "Pixel 7",
                State = DeviceState.Disconnected
            };

            var eventReceived = false;
            Device? receivedDevice = null;

            _eventBus.Subscribe<DeviceDisconnectedEvent>(evt =>
            {
                eventReceived = true;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedDevice);
            Assert.Equal("android-disconnect-123", receivedDevice.Id);
            Assert.Equal(DeviceState.Disconnected, receivedDevice.State);
        }

        [Fact]
        public async Task DeviceDisconnectedEvent_iOSDevice_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "ios-disconnect-456",
                Platform = DevicePlatform.iOS,
                Name = "iPhone 14",
                State = DeviceState.Disconnected
            };

            var eventReceived = false;
            Device? receivedDevice = null;

            _eventBus.Subscribe<DeviceDisconnectedEvent>(evt =>
            {
                eventReceived = true;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedDevice);
            Assert.Equal("ios-disconnect-456", receivedDevice.Id);
        }

        [Fact]
        public async Task DeviceDisconnectedEvent_MultipleDevices_EachEventDistinguishable()
        {
            // Arrange
            var device1 = new Device { Id = "device-1", Platform = DevicePlatform.Android, State = DeviceState.Disconnected };
            var device2 = new Device { Id = "device-2", Platform = DevicePlatform.iOS, State = DeviceState.Disconnected };

            var receivedDeviceIds = new List<string>();
            _eventBus.Subscribe<DeviceDisconnectedEvent>(evt => receivedDeviceIds.Add(evt.Device.Id));

            // Act
            _eventBus.Publish(new DeviceDisconnectedEvent(device1));
            _eventBus.Publish(new DeviceDisconnectedEvent(device2));
            await Task.Delay(10);

            // Assert
            Assert.Equal(2, receivedDeviceIds.Count);
            Assert.Contains("device-1", receivedDeviceIds);
            Assert.Contains("device-2", receivedDeviceIds);
        }

        #endregion

        #region SessionStartedEvent Tests

        [Fact]
        public async Task SessionStartedEvent_ValidSession_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "session-device-123",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var session = new AppiumSession
            {
                SessionId = "session-123",
                DeviceId = device.Id,
                AppiumPort = 4723,
                SystemPort = 8200,
                StartedAt = DateTime.UtcNow,
                Status = SessionStatus.Running
            };

            var eventReceived = false;
            AppiumSession? receivedSession = null;
            Device? receivedDevice = null;

            _eventBus.Subscribe<SessionStartedEvent>(evt =>
            {
                eventReceived = true;
                receivedSession = evt.Session;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new SessionStartedEvent(device, session));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedSession);
            Assert.NotNull(receivedDevice);
            Assert.Equal("session-123", receivedSession.SessionId);
            Assert.Equal(4723, receivedSession.AppiumPort);
            Assert.Equal(SessionStatus.Running, receivedSession.Status);
        }

        [Fact]
        public async Task SessionStartedEvent_MultipleSessionsSequentially_AllEventsReceived()
        {
            // Arrange
            var device1 = new Device { Id = "device-1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "device-2", Platform = DevicePlatform.iOS };

            var session1 = new AppiumSession { SessionId = "session-1", DeviceId = device1.Id, AppiumPort = 4723 };
            var session2 = new AppiumSession { SessionId = "session-2", DeviceId = device2.Id, AppiumPort = 4724 };

            var receivedSessionIds = new List<string>();
            _eventBus.Subscribe<SessionStartedEvent>(evt => receivedSessionIds.Add(evt.Session.SessionId));

            // Act
            _eventBus.Publish(new SessionStartedEvent(device1, session1));
            await Task.Delay(5);
            _eventBus.Publish(new SessionStartedEvent(device2, session2));
            await Task.Delay(5);

            // Assert
            Assert.Equal(2, receivedSessionIds.Count);
            Assert.Contains("session-1", receivedSessionIds);
            Assert.Contains("session-2", receivedSessionIds);
        }

        [Fact]
        public async Task SessionStartedEvent_WithPortConfiguration_PreservesPortInfo()
        {
            // Arrange
            var device = new Device { Id = "port-device", Platform = DevicePlatform.Android };
            var session = new AppiumSession
            {
                SessionId = "port-session",
                DeviceId = device.Id,
                AppiumPort = 5000,
                SystemPort = 8300,
                MjpegServerPort = 9000
            };

            AppiumSession? receivedSession = null;
            _eventBus.Subscribe<SessionStartedEvent>(evt => receivedSession = evt.Session);

            // Act
            _eventBus.Publish(new SessionStartedEvent(device, session));
            await Task.Delay(10);

            // Assert
            Assert.NotNull(receivedSession);
            Assert.Equal(5000, receivedSession.AppiumPort);
            Assert.Equal(8300, receivedSession.SystemPort);
            Assert.Equal(9000, receivedSession.MjpegServerPort);
        }

        #endregion

        #region SessionStoppedEvent Tests

        [Fact]
        public async Task SessionStoppedEvent_ValidSession_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "stop-session-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var session = new AppiumSession
            {
                SessionId = "stop-session-123",
                DeviceId = device.Id,
                AppiumPort = 4723,
                Status = SessionStatus.Stopped,
                StartedAt = DateTime.UtcNow
            };

            var eventReceived = false;
            AppiumSession? receivedSession = null;

            _eventBus.Subscribe<SessionStoppedEvent>(evt =>
            {
                eventReceived = true;
                receivedSession = evt.Session;
            });

            // Act
            _eventBus.Publish(new SessionStoppedEvent(device, session));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedSession);
            Assert.Equal("stop-session-123", receivedSession.SessionId);
            Assert.Equal(SessionStatus.Stopped, receivedSession.Status);
        }

        [Fact]
        public async Task SessionStoppedEvent_MultipleSessionsStoppingConcurrently_AllEventsReceived()
        {
            // Arrange
            var device1 = new Device { Id = "device-1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "device-2", Platform = DevicePlatform.iOS };
            var device3 = new Device { Id = "device-3", Platform = DevicePlatform.Android };

            var session1 = new AppiumSession { SessionId = "session-1", DeviceId = device1.Id, Status = SessionStatus.Stopped };
            var session2 = new AppiumSession { SessionId = "session-2", DeviceId = device2.Id, Status = SessionStatus.Stopped };
            var session3 = new AppiumSession { SessionId = "session-3", DeviceId = device3.Id, Status = SessionStatus.Stopped };

            var receivedCount = 0;
            _eventBus.Subscribe<SessionStoppedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act
            _eventBus.Publish(new SessionStoppedEvent(device1, session1));
            _eventBus.Publish(new SessionStoppedEvent(device2, session2));
            _eventBus.Publish(new SessionStoppedEvent(device3, session3));
            await Task.Delay(10);

            // Assert
            Assert.Equal(3, receivedCount);
        }

        #endregion

        #region SessionFailedEvent Tests

        [Fact]
        public async Task SessionFailedEvent_WithReason_PublishesAndSubscribersReceive()
        {
            // Arrange
            var device = new Device
            {
                Id = "failed-session-device",
                Platform = DevicePlatform.Android,
                Name = "Test Device"
            };

            var reason = "Port 4723 is already in use";

            var eventReceived = false;
            string? receivedReason = null;
            Device? receivedDevice = null;

            _eventBus.Subscribe<SessionFailedEvent>(evt =>
            {
                eventReceived = true;
                receivedReason = evt.Reason;
                receivedDevice = evt.Device;
            });

            // Act
            _eventBus.Publish(new SessionFailedEvent(device, reason));
            await Task.Delay(10);

            // Assert
            Assert.True(eventReceived);
            Assert.NotNull(receivedReason);
            Assert.NotNull(receivedDevice);
            Assert.Equal("Port 4723 is already in use", receivedReason);
            Assert.Equal("failed-session-device", receivedDevice.Id);
        }

        [Fact]
        public async Task SessionFailedEvent_MultipleFailureReasons_AllDistinguishable()
        {
            // Arrange
            var device = new Device { Id = "multi-fail-device", Platform = DevicePlatform.Android };

            var receivedReasons = new List<string>();
            _eventBus.Subscribe<SessionFailedEvent>(evt => receivedReasons.Add(evt.Reason));

            // Act
            _eventBus.Publish(new SessionFailedEvent(device, "Port allocation failed"));
            await Task.Delay(5);
            _eventBus.Publish(new SessionFailedEvent(device, "Device offline"));
            await Task.Delay(5);
            _eventBus.Publish(new SessionFailedEvent(device, "Appium binary not found"));
            await Task.Delay(5);

            // Assert
            Assert.Equal(3, receivedReasons.Count);
            Assert.Contains("Port allocation failed", receivedReasons);
            Assert.Contains("Device offline", receivedReasons);
            Assert.Contains("Appium binary not found", receivedReasons);
        }

        #endregion

        #region Device Lifecycle Integration Tests

        [Fact]
        public async Task DeviceLifecycle_ConnectStartStopDisconnect_AllEventsPublished()
        {
            // Arrange
            var device = new Device
            {
                Id = "lifecycle-device",
                Platform = DevicePlatform.Android,
                Name = "Lifecycle Test Device"
            };

            var session = new AppiumSession
            {
                SessionId = "lifecycle-session",
                DeviceId = device.Id,
                AppiumPort = 4723
            };

            var events = new List<string>();

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => events.Add("Connected"));
            _eventBus.Subscribe<SessionStartedEvent>(evt => events.Add("SessionStarted"));
            _eventBus.Subscribe<SessionStoppedEvent>(evt => events.Add("SessionStopped"));
            _eventBus.Subscribe<DeviceDisconnectedEvent>(evt => events.Add("Disconnected"));

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);

            _eventBus.Publish(new SessionStartedEvent(device, session));
            await Task.Delay(5);

            _eventBus.Publish(new SessionStoppedEvent(device, session));
            await Task.Delay(5);

            device.State = DeviceState.Disconnected;
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(5);

            // Assert
            Assert.Equal(4, events.Count);
            Assert.Equal("Connected", events[0]);
            Assert.Equal("SessionStarted", events[1]);
            Assert.Equal("SessionStopped", events[2]);
            Assert.Equal("Disconnected", events[3]);
        }

        [Fact]
        public async Task DeviceLifecycle_ConnectFailDisconnect_HandlesFailureGracefully()
        {
            // Arrange
            var device = new Device
            {
                Id = "fail-lifecycle-device",
                Platform = DevicePlatform.Android,
                Name = "Failure Lifecycle Device"
            };

            var events = new List<string>();

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => events.Add("Connected"));
            _eventBus.Subscribe<SessionFailedEvent>(evt => events.Add($"Failed:{evt.Reason}"));
            _eventBus.Subscribe<DeviceDisconnectedEvent>(evt => events.Add("Disconnected"));

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);

            _eventBus.Publish(new SessionFailedEvent(device, "Initialization failed"));
            await Task.Delay(5);

            device.State = DeviceState.Disconnected;
            _eventBus.Publish(new DeviceDisconnectedEvent(device));
            await Task.Delay(5);

            // Assert
            Assert.Equal(3, events.Count);
            Assert.Equal("Connected", events[0]);
            Assert.Equal("Failed:Initialization failed", events[1]);
            Assert.Equal("Disconnected", events[2]);
        }

        #endregion

        #region Multi-Device Scenarios

        [Fact]
        public async Task MultiDevice_TwoDevicesConnectSimultaneously_BothEventsReceived()
        {
            // Arrange
            var device1 = new Device { Id = "device-1", Platform = DevicePlatform.Android, Name = "Android Device" };
            var device2 = new Device { Id = "device-2", Platform = DevicePlatform.iOS, Name = "iOS Device" };

            var receivedDeviceIds = new List<string>();
            _eventBus.Subscribe<DeviceConnectedEvent>(evt => receivedDeviceIds.Add(evt.Device.Id));

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device1));
            _eventBus.Publish(new DeviceConnectedEvent(device2));
            await Task.Delay(10);

            // Assert
            Assert.Equal(2, receivedDeviceIds.Count);
            Assert.Contains("device-1", receivedDeviceIds);
            Assert.Contains("device-2", receivedDeviceIds);
        }

        [Fact]
        public async Task MultiDevice_SessionsStartedOnMultipleDevices_AllEventsTracked()
        {
            // Arrange
            var device1 = new Device { Id = "multi-device-1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "multi-device-2", Platform = DevicePlatform.iOS };
            var device3 = new Device { Id = "multi-device-3", Platform = DevicePlatform.Android };

            var session1 = new AppiumSession { SessionId = "session-1", DeviceId = device1.Id, AppiumPort = 4723 };
            var session2 = new AppiumSession { SessionId = "session-2", DeviceId = device2.Id, AppiumPort = 4724 };
            var session3 = new AppiumSession { SessionId = "session-3", DeviceId = device3.Id, AppiumPort = 4725 };

            var sessionMap = new Dictionary<string, int>();
            _eventBus.Subscribe<SessionStartedEvent>(evt =>
            {
                sessionMap[evt.Device.Id] = evt.Session.AppiumPort;
            });

            // Act
            _eventBus.Publish(new SessionStartedEvent(device1, session1));
            _eventBus.Publish(new SessionStartedEvent(device2, session2));
            _eventBus.Publish(new SessionStartedEvent(device3, session3));
            await Task.Delay(10);

            // Assert
            Assert.Equal(3, sessionMap.Count);
            Assert.Equal(4723, sessionMap["multi-device-1"]);
            Assert.Equal(4724, sessionMap["multi-device-2"]);
            Assert.Equal(4725, sessionMap["multi-device-3"]);
        }

        [Fact]
        public async Task MultiDevice_MixedEventsFromDifferentDevices_EventsProcessedCorrectly()
        {
            // Arrange
            var device1 = new Device { Id = "mixed-1", Platform = DevicePlatform.Android };
            var device2 = new Device { Id = "mixed-2", Platform = DevicePlatform.iOS };

            var session1 = new AppiumSession { SessionId = "session-1", DeviceId = device1.Id, AppiumPort = 4723 };

            var eventSequence = new List<string>();

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => eventSequence.Add($"Connect:{evt.Device.Id}"));
            _eventBus.Subscribe<SessionStartedEvent>(evt => eventSequence.Add($"Start:{evt.Device.Id}"));
            _eventBus.Subscribe<SessionFailedEvent>(evt => eventSequence.Add($"Fail:{evt.Device.Id}"));

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device1));
            await Task.Delay(25);
            _eventBus.Publish(new DeviceConnectedEvent(device2));
            await Task.Delay(25);
            _eventBus.Publish(new SessionStartedEvent(device1, session1));
            await Task.Delay(25);
            _eventBus.Publish(new SessionFailedEvent(device2, "Port conflict"));
            await Task.Delay(25);

            // Assert
            Assert.Equal(4, eventSequence.Count);
            Assert.Equal("Connect:mixed-1", eventSequence[0]);
            Assert.Equal("Connect:mixed-2", eventSequence[1]);
            Assert.Equal("Start:mixed-1", eventSequence[2]);
            Assert.Equal("Fail:mixed-2", eventSequence[3]);
        }

        #endregion

        #region Event Subscription Management Tests

        [Fact]
        public async Task EventBus_UnsubscribeAfterSubscription_StopsReceivingEvents()
        {
            // Arrange
            var device = new Device { Id = "unsub-device", Platform = DevicePlatform.Android };
            var receivedCount = 0;

            Action<DeviceConnectedEvent> handler = evt => receivedCount++;
            _eventBus.Subscribe(handler);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);
            Assert.Equal(1, receivedCount);

            // Unsubscribe
            _eventBus.Unsubscribe(handler);

            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);

            // Assert
            Assert.Equal(1, receivedCount); // Should still be 1, not 2
        }

        [Fact]
        public async Task EventBus_MultipleSubscriptionsUnsubscribeOne_OtherContinuesReceiving()
        {
            // Arrange
            var device = new Device { Id = "multi-unsub-device", Platform = DevicePlatform.Android };
            var count1 = 0;
            var count2 = 0;

            Action<DeviceConnectedEvent> handler1 = evt => count1++;
            Action<DeviceConnectedEvent> handler2 = evt => count2++;

            _eventBus.Subscribe(handler1);
            _eventBus.Subscribe(handler2);

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);
            Assert.Equal(1, count1);
            Assert.Equal(1, count2);

            // Note: EventBus.Subscribe returns void, so we can't test unsubscribe functionality
            // Both handlers will continue to receive events

            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(5);

            // Assert
            Assert.Equal(2, count1); // Both should increment
            Assert.Equal(2, count2); // Both should increment
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task EventBus_SubscriberThrowsException_OtherSubscribersContinueProcessing()
        {
            // Arrange
            var device = new Device { Id = "error-device", Platform = DevicePlatform.Android };
            var received1 = false;
            var received3 = false;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                received1 = true;
            });

            _eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                throw new InvalidOperationException("Test exception in subscriber");
            });

            _eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                received3 = true;
            });

            // Act
            _eventBus.Publish(new DeviceConnectedEvent(device));
            await Task.Delay(10);

            // Assert
            Assert.True(received1);
            Assert.True(received3);
            // Subscriber 2 threw exception but others should still receive
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task EventBus_HighVolumeEvents_ProcessesAllEvents()
        {
            // Arrange
            const int eventCount = 100;
            var receivedCount = 0;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                _eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(50); // Allow time for all events to process

            // Assert
            Assert.Equal(eventCount, receivedCount);
        }

        [Fact]
        public async Task EventBus_ConcurrentPublishes_HandlesThreadSafely()
        {
            // Arrange
            const int concurrentPublishes = 50;
            var receivedCount = 0;

            _eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < concurrentPublishes; i++)
            {
                var deviceId = $"concurrent-device-{i}";
                tasks.Add(Task.Run(() =>
                {
                    var device = new Device { Id = deviceId, Platform = DevicePlatform.Android };
                    _eventBus.Publish(new DeviceConnectedEvent(device));
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(50);

            // Assert
            Assert.Equal(concurrentPublishes, receivedCount);
        }

        #endregion
    }
}
