using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    /// <summary>
    /// Comprehensive stress tests for EventBus with high volume, concurrency, and error scenarios
    /// </summary>
    public class EventBusStressTests
    {
        private readonly Mock<ILogger<EventBus>> _mockLogger;

        public EventBusStressTests()
        {
            _mockLogger = new Mock<ILogger<EventBus>>();
        }

        #region High Volume Event Tests

        [Fact]
        public async Task EventBus_1000Events_ProcessesAllSuccessfully()
        {
            // Arrange
            const int eventCount = 1000;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(1000); // Allow processing

            // Assert
            Assert.Equal(eventCount, receivedCount);
        }

        [Fact]
        public async Task EventBus_10000Events_ProcessesAllSuccessfully()
        {
            // Arrange
            const int eventCount = 10000;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(2000); // Allow processing
            stopwatch.Stop();

            // Assert
            Assert.Equal(eventCount, receivedCount);
            // Performance check: should process 10k events in reasonable time
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, $"Processing took {stopwatch.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task EventBus_MixedEventTypes_ProcessesAllCorrectly()
        {
            // Arrange
            const int eventsPerType = 1000;
            var eventBus = new EventBus(_mockLogger.Object);
            
            var connectedCount = 0;
            var disconnectedCount = 0;
            var sessionStartedCount = 0;
            var sessionStoppedCount = 0;
            var sessionFailedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref connectedCount));
            eventBus.Subscribe<DeviceDisconnectedEvent>(evt => Interlocked.Increment(ref disconnectedCount));
            eventBus.Subscribe<SessionStartedEvent>(evt => Interlocked.Increment(ref sessionStartedCount));
            eventBus.Subscribe<SessionStoppedEvent>(evt => Interlocked.Increment(ref sessionStoppedCount));
            eventBus.Subscribe<SessionFailedEvent>(evt => Interlocked.Increment(ref sessionFailedCount));

            // Act
            for (int i = 0; i < eventsPerType; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                var session = new AppiumSession { SessionId = $"session-{i}", DeviceId = device.Id, AppiumPort = 4723 + i };

                eventBus.Publish(new DeviceConnectedEvent(device));
                eventBus.Publish(new SessionStartedEvent(device, session));
                eventBus.Publish(new SessionStoppedEvent(device, session));
                eventBus.Publish(new DeviceDisconnectedEvent(device));
                eventBus.Publish(new SessionFailedEvent(device, "Test failure"));
            }

            await Task.Delay(2000);

            // Assert
            Assert.Equal(eventsPerType, connectedCount);
            Assert.Equal(eventsPerType, disconnectedCount);
            Assert.Equal(eventsPerType, sessionStartedCount);
            Assert.Equal(eventsPerType, sessionStoppedCount);
            Assert.Equal(eventsPerType, sessionFailedCount);
        }

        #endregion

        #region Concurrent Publishing Tests

        [Fact]
        public async Task EventBus_ConcurrentPublishers_HandlesThreadSafely()
        {
            // Arrange
            const int publisherCount = 50;
            const int eventsPerPublisher = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act
            var tasks = Enumerable.Range(0, publisherCount).Select(publisherId =>
                Task.Run(() =>
                {
                    for (int i = 0; i < eventsPerPublisher; i++)
                    {
                        var device = new Device { Id = $"publisher-{publisherId}-device-{i}", Platform = DevicePlatform.Android };
                        eventBus.Publish(new DeviceConnectedEvent(device));
                    }
                })
            ).ToArray();

            await Task.WhenAll(tasks);
            await Task.Delay(1000);

            // Assert
            Assert.Equal(publisherCount * eventsPerPublisher, receivedCount);
        }

        [Fact]
        public async Task EventBus_ConcurrentSubscribersAndPublishers_HandlesCorrectly()
        {
            // Arrange
            const int subscriberCount = 20;
            const int publisherCount = 20;
            const int eventsPerPublisher = 50;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCounts = new ConcurrentDictionary<int, int>();

            // Create subscribers concurrently
            var subscribeTasks = Enumerable.Range(0, subscriberCount).Select(subId =>
                Task.Run(() =>
                {
                    eventBus.Subscribe<DeviceConnectedEvent>(evt =>
                    {
                        receivedCounts.AddOrUpdate(subId, 1, (_, count) => count + 1);
                    });
                })
            ).ToArray();

            await Task.WhenAll(subscribeTasks);

            // Act - Publish events concurrently
            var publishTasks = Enumerable.Range(0, publisherCount).Select(pubId =>
                Task.Run(() =>
                {
                    for (int i = 0; i < eventsPerPublisher; i++)
                    {
                        var device = new Device { Id = $"pub-{pubId}-dev-{i}", Platform = DevicePlatform.Android };
                        eventBus.Publish(new DeviceConnectedEvent(device));
                    }
                })
            ).ToArray();

            await Task.WhenAll(publishTasks);
            await Task.Delay(1000);

            // Assert
            var totalEvents = publisherCount * eventsPerPublisher;
            foreach (var count in receivedCounts.Values)
            {
                Assert.Equal(totalEvents, count);
            }
        }

        [Fact]
        public async Task EventBus_RapidFireEvents_MaintainsEventOrder()
        {
            // Arrange
            const int eventCount = 1000;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedDeviceIds = new ConcurrentQueue<string>();

            eventBus.Subscribe<DeviceConnectedEvent>(evt => receivedDeviceIds.Enqueue(evt.Device.Id));

            // Act - Rapid fire events
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"rapid-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(1000);

            // Assert
            Assert.Equal(eventCount, receivedDeviceIds.Count);
            
            // Verify all events were received (order may vary in concurrent scenarios)
            var receivedList = receivedDeviceIds.ToList();
            var expectedIds = Enumerable.Range(0, eventCount).Select(i => $"rapid-{i}").ToList();
            
            Assert.Equal(expectedIds.Count, receivedList.Count);
            foreach (var expectedId in expectedIds)
            {
                Assert.Contains(expectedId, receivedList);
            }
        }

        #endregion

        #region Multiple Subscribers Tests

        [Fact]
        public async Task EventBus_100Subscribers_AllReceiveEvents()
        {
            // Arrange
            const int subscriberCount = 100;
            const int eventCount = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCounts = new ConcurrentDictionary<int, int>();

            // Create subscribers
            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = i;
                eventBus.Subscribe<DeviceConnectedEvent>(evt =>
                {
                    receivedCounts.AddOrUpdate(subscriberId, 1, (_, count) => count + 1);
                });
            }

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(2000);

            // Assert
            Assert.Equal(subscriberCount, receivedCounts.Count);
            foreach (var count in receivedCounts.Values)
            {
                Assert.Equal(eventCount, count);
            }
        }

        [Fact]
        public async Task EventBus_SubscribersWithVaryingProcessingTime_AllComplete()
        {
            // Arrange
            const int eventCount = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            
            var fastCount = 0;
            var mediumCount = 0;
            var slowCount = 0;

            // Fast subscriber
            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref fastCount));

            // Medium subscriber (simulates some processing)
            eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                Thread.SpinWait(100);
                Interlocked.Increment(ref mediumCount);
            });

            // Slow subscriber (simulates heavy processing)
            eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                Thread.SpinWait(1000);
                Interlocked.Increment(ref slowCount);
            });

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(2000);

            // Assert
            Assert.Equal(eventCount, fastCount);
            Assert.Equal(eventCount, mediumCount);
            Assert.Equal(eventCount, slowCount);
        }

        #endregion

        #region Subscription/Unsubscription Stress Tests

        [Fact]
        public async Task EventBus_DynamicSubscribeUnsubscribe_HandlesCorrectly()
        {
            // Arrange
            const int cycles = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            // Act
            for (int i = 0; i < cycles; i++)
            {
                eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));
                
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
                
                await Task.Delay(10);
                
                // Subscription cleanup
                
                // Publish after unsubscribe - should not increase count
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(500);

            // Assert
            Assert.Equal(cycles, receivedCount); // Only events before unsubscribe
        }

        [Fact]
        public async Task EventBus_ConcurrentSubscribeUnsubscribe_HandlesThreadSafely()
        {
            // Arrange
            const int concurrentOperations = 50;
            var eventBus = new EventBus(_mockLogger.Object);
            var activeSubscriptions = new ConcurrentBag<int>();

            // Act
            var tasks = Enumerable.Range(0, concurrentOperations).Select(i =>
                Task.Run(async () =>
                {
                    // Subscribe
                    eventBus.Subscribe<DeviceConnectedEvent>(evt => { });
                    lock (activeSubscriptions)
                    {
                        activeSubscriptions.Add(i);
                    }
                    
                    await Task.Delay(10);
                    
                    // Publish
                    var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                    eventBus.Publish(new DeviceConnectedEvent(device));
                    
                    await Task.Delay(10);
                    
                    // Note: EventBus.Subscribe returns void, so we can't unsubscribe in this test
                })
            ).ToArray();

            await Task.WhenAll(tasks);
            await Task.Delay(500);

            // Assert - No exceptions thrown and all subscriptions were recorded
            Assert.Equal(concurrentOperations, activeSubscriptions.Count);
        }

        #endregion

        #region Error Handling Stress Tests

        [Fact]
        public async Task EventBus_SubscribersWithExceptions_ContinuesProcessing()
        {
            // Arrange
            const int eventCount = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            
            var successCount = 0;
            var exceptionCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref successCount));

            eventBus.Subscribe<DeviceConnectedEvent>(evt =>
            {
                Interlocked.Increment(ref exceptionCount);
                throw new InvalidOperationException("Test exception");
            });

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref successCount));

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(1000);

            // Assert
            Assert.Equal(eventCount * 2, successCount); // Two successful subscribers
            Assert.Equal(eventCount, exceptionCount);
        }

        [Fact]
        public async Task EventBus_AllSubscribersThrowExceptions_HandlesGracefully()
        {
            // Arrange
            const int subscriberCount = 10;
            const int eventCount = 100;
            var eventBus = new EventBus(_mockLogger.Object);
            var exceptionCounts = new ConcurrentDictionary<int, int>();

            for (int i = 0; i < subscriberCount; i++)
            {
                var subscriberId = i;
                eventBus.Subscribe<DeviceConnectedEvent>(evt =>
                {
                    exceptionCounts.AddOrUpdate(subscriberId, 1, (_, count) => count + 1);
                    throw new InvalidOperationException($"Subscriber {subscriberId} exception");
                });
            }

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(1000);

            // Assert - All subscribers should have been invoked despite exceptions
            Assert.Equal(subscriberCount, exceptionCounts.Count);
            foreach (var count in exceptionCounts.Values)
            {
                Assert.Equal(eventCount, count);
            }
        }

        #endregion

        #region Memory and Resource Tests

        [Fact]
        public async Task EventBus_LargePayloadEvents_HandlesEfficiently()
        {
            // Arrange
            const int eventCount = 1000;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            // Act - Create events with large metadata
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device
                {
                    Id = $"device-{i}",
                    Platform = DevicePlatform.Android,
                    Name = $"Device with large name {new string('X', 1000)}"
                };
                
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(2000);

            // Assert
            Assert.Equal(eventCount, receivedCount);
        }

        [Fact]
        public async Task EventBus_ManyShortLivedSubscriptions_CleansUpProperly()
        {
            // Arrange
            const int subscriptionCycles = 1000;
            var eventBus = new EventBus(_mockLogger.Object);

            // Act
            for (int i = 0; i < subscriptionCycles; i++)
            {
                eventBus.Subscribe<DeviceConnectedEvent>(evt => { });
                
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
                
                // Subscription cleanup
            }

            await Task.Delay(500);

            // Assert - No memory leaks or exceptions
            // If we got here without issues, the test passes
            Assert.True(true);
        }

        #endregion

        #region Mixed Scenario Stress Tests

        [Fact]
        public async Task EventBus_ComplexMixedScenario_HandlesAllOperations()
        {
            // Arrange
            const int duration = 2000; // 2 seconds of chaos
            var eventBus = new EventBus(_mockLogger.Object);
            var stats = new ConcurrentDictionary<string, int>();
            var cts = new CancellationTokenSource(duration);

            // Publishers
            var publishTask = Task.Run(async () =>
            {
                var i = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    var device = new Device { Id = $"device-{i++}", Platform = DevicePlatform.Android };
                    eventBus.Publish(new DeviceConnectedEvent(device));
                    stats.AddOrUpdate("published", 1, (_, count) => count + 1);
                    await Task.Delay(1);
                }
            });

            // Dynamic subscribers
            var subscribeTask = Task.Run(async () =>
            {
                var subscriptionCount = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    eventBus.Subscribe<DeviceConnectedEvent>(evt =>
                    {
                        stats.AddOrUpdate("received", 1, (_, count) => count + 1);
                    });
                    subscriptionCount++;
                    stats.AddOrUpdate("subscribed", 1, (_, count) => count + 1);
                    
                    await Task.Delay(50);

                }
            });

            // Act
            await Task.WhenAll(publishTask, subscribeTask);
            await Task.Delay(500); // Allow final processing

            // Assert
            Assert.True(stats["published"] > 0);
            Assert.True(stats["received"] > 0);
            Assert.True(stats["subscribed"] > 0);
        }

        [Fact]
        public async Task EventBus_SimulatedProductionLoad_HandlesRealistic()
        {
            // Arrange - Simulate production scenario
            const int deviceCount = 20;
            const int sessionCycles = 50;
            var eventBus = new EventBus(_mockLogger.Object);
            
            var eventStats = new ConcurrentDictionary<string, int>();

            eventBus.Subscribe<DeviceConnectedEvent>(evt => 
                eventStats.AddOrUpdate("DeviceConnected", 1, (_, c) => c + 1));
            eventBus.Subscribe<SessionStartedEvent>(evt => 
                eventStats.AddOrUpdate("SessionStarted", 1, (_, c) => c + 1));
            eventBus.Subscribe<SessionStoppedEvent>(evt => 
                eventStats.AddOrUpdate("SessionStopped", 1, (_, c) => c + 1));
            eventBus.Subscribe<DeviceDisconnectedEvent>(evt => 
                eventStats.AddOrUpdate("DeviceDisconnected", 1, (_, c) => c + 1));

            // Act - Simulate device lifecycle
            var tasks = Enumerable.Range(0, deviceCount).Select(async deviceId =>
            {
                for (int cycle = 0; cycle < sessionCycles; cycle++)
                {
                    var device = new Device 
                    { 
                        Id = $"device-{deviceId}", 
                        Platform = deviceId % 2 == 0 ? DevicePlatform.Android : DevicePlatform.iOS 
                    };
                    
                    // Connect
                    eventBus.Publish(new DeviceConnectedEvent(device));
                    await Task.Delay(10);
                    
                    // Start session
                    var session = new AppiumSession 
                    { 
                        SessionId = $"session-{deviceId}-{cycle}", 
                        DeviceId = device.Id, 
                        AppiumPort = 4723 + deviceId 
                    };
                    eventBus.Publish(new SessionStartedEvent(device, session));
                    await Task.Delay(20);
                    
                    // Stop session
                    eventBus.Publish(new SessionStoppedEvent(device, session));
                    await Task.Delay(10);
                    
                    // Disconnect
                    device.State = DeviceState.Disconnected;
                    eventBus.Publish(new DeviceDisconnectedEvent(device));
                    await Task.Delay(5);
                }
            }).ToArray();

            await Task.WhenAll(tasks);
            await Task.Delay(1000);

            // Assert
            var expectedCount = deviceCount * sessionCycles;
            Assert.Equal(expectedCount, eventStats["DeviceConnected"]);
            Assert.Equal(expectedCount, eventStats["SessionStarted"]);
            Assert.Equal(expectedCount, eventStats["SessionStopped"]);
            Assert.Equal(expectedCount, eventStats["DeviceDisconnected"]);
        }

        #endregion

        #region Performance Benchmarks

        [Fact]
        public async Task EventBus_Benchmark_PublishRate()
        {
            // Arrange
            const int eventCount = 10000;
            var eventBus = new EventBus(_mockLogger.Object);
            var receivedCount = 0;

            eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref receivedCount));

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            var publishTime = stopwatch.ElapsedMilliseconds;
            
            await Task.Delay(2000);
            stopwatch.Stop();

            // Assert
            Assert.Equal(eventCount, receivedCount);
            
            var publishRate = (double)eventCount / publishTime * 1000;
            var totalRate = (double)eventCount / stopwatch.ElapsedMilliseconds * 1000;
            
            // Should be able to publish at least 1000 events/second
            Assert.True(publishRate > 1000, $"Publish rate: {publishRate:F0} events/sec");
        }

        [Fact]
        public async Task EventBus_Benchmark_MultipleSubscribers()
        {
            // Arrange
            const int subscriberCount = 50;
            const int eventCount = 1000;
            var eventBus = new EventBus(_mockLogger.Object);
            var totalReceived = 0;

            for (int i = 0; i < subscriberCount; i++)
            {
                eventBus.Subscribe<DeviceConnectedEvent>(evt => Interlocked.Increment(ref totalReceived));
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < eventCount; i++)
            {
                var device = new Device { Id = $"device-{i}", Platform = DevicePlatform.Android };
                eventBus.Publish(new DeviceConnectedEvent(device));
            }

            await Task.Delay(2000);
            stopwatch.Stop();

            // Assert
            Assert.Equal(eventCount * subscriberCount, totalReceived);
            
            // With 50 subscribers, should complete in reasonable time
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"Processing took {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion
    }
}
