using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class EventBusTests
    {
        private readonly Mock<ILogger<EventBus>> _mockLogger;
        private readonly EventBus _eventBus;

        public EventBusTests()
        {
            _mockLogger = new Mock<ILogger<EventBus>>();
            _eventBus = new EventBus(_mockLogger.Object);
        }

        [Fact]
        public void Subscribe_AddsHandler()
        {
            // Arrange
            var handlerCalled = false;
            Action<TestEvent> handler = e => handlerCalled = true;

            // Act
            _eventBus.Subscribe(handler);
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public void Subscribe_MultipleHandlers_AllCalled()
        {
            // Arrange
            var callCount = 0;
            Action<TestEvent> handler1 = e => callCount++;
            Action<TestEvent> handler2 = e => callCount++;

            // Act
            _eventBus.Subscribe(handler1);
            _eventBus.Subscribe(handler2);
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void Unsubscribe_RemovesHandler()
        {
            // Arrange
            var handlerCalled = false;
            Action<TestEvent> handler = e => handlerCalled = true;

            _eventBus.Subscribe(handler);

            // Act
            _eventBus.Unsubscribe(handler);
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public void Publish_NoSubscribers_DoesNothing()
        {
            // Act - Should not throw
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert - No exception thrown
        }

        [Fact]
        public void Publish_HandlerThrowsException_ContinuesToOtherHandlers()
        {
            // Arrange
            var handler1Called = false;
            var handler2Called = false;

            Action<TestEvent> handler1 = e => { handler1Called = true; throw new Exception("Test exception"); };
            Action<TestEvent> handler2 = e => handler2Called = true;

            _eventBus.Subscribe(handler1);
            _eventBus.Subscribe(handler2);

            // Act
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.True(handler1Called);
            Assert.True(handler2Called); // Should still be called even though handler1 threw
        }

        [Fact]
        public void Subscribe_DifferentEventTypes_Isolated()
        {
            // Arrange
            var testEventCalled = false;
            var otherEventCalled = false;

            Action<TestEvent> testHandler = e => testEventCalled = true;
            Action<OtherTestEvent> otherHandler = e => otherEventCalled = true;

            _eventBus.Subscribe(testHandler);
            _eventBus.Subscribe(otherHandler);

            // Act
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.True(testEventCalled);
            Assert.False(otherEventCalled);
        }

        [Fact]
        public void Unsubscribe_NonExistentHandler_DoesNothing()
        {
            // Arrange
            Action<TestEvent> handler = e => { };

            // Act - Should not throw
            _eventBus.Unsubscribe(handler);

            // Assert - No exception thrown
        }

        [Fact]
        public void Publish_NullEventData_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _eventBus.Publish<TestEvent>(null!));
        }

        [Fact]
        public void Subscribe_SameHandlerMultipleTimes_CalledMultipleTimes()
        {
            // Arrange
            var callCount = 0;
            Action<TestEvent> handler = e => callCount++;

            // Act
            _eventBus.Subscribe(handler);
            _eventBus.Subscribe(handler); // Subscribe same handler twice
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void Unsubscribe_SameHandlerMultipleTimes_RemovesAll()
        {
            // Arrange
            var callCount = 0;
            Action<TestEvent> handler = e => callCount++;

            _eventBus.Subscribe(handler);
            _eventBus.Subscribe(handler);

            // Act
            _eventBus.Unsubscribe(handler); // Should remove first occurrence
            _eventBus.Publish(new TestEvent { Message = "test" });

            // Assert
            Assert.Equal(1, callCount); // One occurrence should still exist
        }

        [Fact]
        public void Publish_ThreadSafety()
        {
            // Arrange
            var callCount = 0;
            Action<TestEvent> handler = e => Interlocked.Increment(ref callCount);

            _eventBus.Subscribe(handler);

            // Act - Publish from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() => _eventBus.Publish(new TestEvent { Message = "test" })));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(10, callCount);
        }

        [Fact]
        public void Subscribe_ThreadSafety()
        {
            // Arrange
            var handlers = new List<Action<TestEvent>>();
            var callCount = 0;

            // Act - Subscribe from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    Action<TestEvent> handler = e => Interlocked.Increment(ref callCount);
                    _eventBus.Subscribe(handler);
                    lock (handlers)
                    {
                        handlers.Add(handler);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(10, handlers.Count);

            // Publish and verify all handlers are called
            _eventBus.Publish(new TestEvent { Message = "test" });
            Assert.Equal(10, callCount);
        }

        // Test event classes
        private class TestEvent
        {
            public string Message { get; set; } = string.Empty;
        }

        private class OtherTestEvent
        {
            public string Data { get; set; } = string.Empty;
        }
    }
}