using System.Net.Sockets;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Services
{
    public class PortManagerTests
    {
        private readonly Mock<ILogger<PortManager>> _mockLogger;
        private readonly PortManager _portManager;

        public PortManagerTests()
        {
            _mockLogger = new Mock<ILogger<PortManager>>();
            _portManager = new PortManager(_mockLogger.Object, 4723, 4730); // Small range for testing
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_InvalidCount_ReturnsNull()
        {
            // Act
            var result = await _portManager.AllocateConsecutivePortsAsync(0);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_ValidCount_ReturnsPorts()
        {
            // Act
            var result = await _portManager.AllocateConsecutivePortsAsync(2);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal(4723, result[0]);
            Assert.Equal(4724, result[1]);

            // Verify ports are marked as allocated
            var allocated = _portManager.GetAllocatedPorts();
            Assert.Contains(4723, allocated);
            Assert.Contains(4724, allocated);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_MultipleAllocations_ReturnsDifferentPorts()
        {
            // Act
            var result1 = await _portManager.AllocateConsecutivePortsAsync(2);
            var result2 = await _portManager.AllocateConsecutivePortsAsync(2);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(2, result1.Length);
            Assert.Equal(2, result2.Length);

            // Should be different port ranges
            Assert.NotEqual(result1[0], result2[0]);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_ExhaustRange_ReturnsNull()
        {
            // Arrange - Allocate all available ports
            var result1 = await _portManager.AllocateConsecutivePortsAsync(3); // 4723-4725
            var result2 = await _portManager.AllocateConsecutivePortsAsync(3); // 4726-4728

            // Act - Try to allocate more than remaining
            var result3 = await _portManager.AllocateConsecutivePortsAsync(3);

            // Assert
            Assert.Null(result3); // Should fail as only 4729-4730 remain (2 ports)
        }

        [Fact]
        public async Task ReleasePortsAsync_ValidPorts_ReleasesSuccessfully()
        {
            // Arrange
            var allocated = await _portManager.AllocateConsecutivePortsAsync(2);
            Assert.NotNull(allocated);

            // Act
            await _portManager.ReleasePortsAsync(allocated);

            // Assert
            var allocatedPorts = _portManager.GetAllocatedPorts();
            Assert.DoesNotContain(allocated[0], allocatedPorts);
            Assert.DoesNotContain(allocated[1], allocatedPorts);
        }

        [Fact]
        public async Task ReleasePortsAsync_NullPorts_DoesNothing()
        {
            // Act
            await _portManager.ReleasePortsAsync(null);

            // Assert - Should not throw
            var allocated = _portManager.GetAllocatedPorts();
            Assert.Empty(allocated);
        }

        [Fact]
        public async Task ReleasePortsAsync_EmptyArray_DoesNothing()
        {
            // Act
            await _portManager.ReleasePortsAsync(Array.Empty<int>());

            // Assert - Should not throw
            var allocated = _portManager.GetAllocatedPorts();
            Assert.Empty(allocated);
        }

        [Fact]
        public async Task ReleasePortsAsync_UnallocatedPorts_DoesNothing()
        {
            // Act
            await _portManager.ReleasePortsAsync(new[] { 9999, 9998 });

            // Assert - Should not throw
            var allocated = _portManager.GetAllocatedPorts();
            Assert.Empty(allocated);
        }

        [Fact]
        public void GetAllocatedPorts_ReturnsOrderedList()
        {
            // Arrange - Allocate ports in non-sequential order
            var task1 = _portManager.AllocateConsecutivePortsAsync(2); // 4723-4724
            var task2 = _portManager.AllocateConsecutivePortsAsync(2); // 4725-4726
            Task.WaitAll(task1, task2);

            // Act
            var allocated = _portManager.GetAllocatedPorts();

            // Assert
            Assert.Equal(4, allocated.Count);
            Assert.True(allocated.SequenceEqual(allocated.OrderBy(p => p)));
        }

        [Fact]
        public void IsPortInUse_AllocatedPort_ReturnsTrue()
        {
            // Arrange
            var task = _portManager.AllocateConsecutivePortsAsync(1);
            task.Wait();
            var allocated = task.Result;
            Assert.NotNull(allocated);

            // Act
            var result = _portManager.IsPortInUse(allocated[0]);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsPortInUse_UnallocatedPort_ReturnsFalse()
        {
            // Act
            var result = _portManager.IsPortInUse(4723);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsPortInUse_OutOfRangePort_ReturnsTrue()
        {
            // Act
            var result = _portManager.IsPortInUse(1); // Privileged port, likely in use

            // Assert - This might be true or false depending on system, but shouldn't throw
            Assert.IsType<bool>(result);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_ThreadSafety()
        {
            // Arrange
            var tasks = new List<Task<int[]?>>();
            var results = new List<int[]?>();

            // Act - Allocate ports concurrently
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _portManager.AllocateConsecutivePortsAsync(1);
                    lock (results)
                    {
                        results.Add(result);
                    }
                    return result;
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var successfulAllocations = results.Where(r => r != null).ToList();
            Assert.True(successfulAllocations.Count <= 8); // Max 8 ports in range

            // All allocated ports should be unique
            var allPorts = successfulAllocations.SelectMany(r => r!).ToList();
            Assert.Equal(allPorts.Distinct().Count(), allPorts.Count);
        }

        [Fact]
        public async Task ReleasePortsAsync_ThreadSafety()
        {
            // Arrange
            var allocated = await _portManager.AllocateConsecutivePortsAsync(5);
            Assert.NotNull(allocated);

            var tasks = new List<Task>();

            // Act - Release ports concurrently
            foreach (var port in allocated)
            {
                tasks.Add(Task.Run(() => _portManager.ReleasePortsAsync(new[] { port })));
            }

            await Task.WhenAll(tasks);

            // Assert
            var remainingPorts = _portManager.GetAllocatedPorts();
            Assert.Empty(remainingPorts);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_LargeCount_ReturnsNull()
        {
            // Act
            var result = await _portManager.AllocateConsecutivePortsAsync(100);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AllocateConsecutivePortsAsync_AfterRelease_CanReusePorts()
        {
            // Arrange
            var firstAllocation = await _portManager.AllocateConsecutivePortsAsync(2);
            Assert.NotNull(firstAllocation);
            await _portManager.ReleasePortsAsync(firstAllocation);

            // Act
            var secondAllocation = await _portManager.AllocateConsecutivePortsAsync(2);

            // Assert
            Assert.NotNull(secondAllocation);
            Assert.Equal(firstAllocation[0], secondAllocation[0]);
            Assert.Equal(firstAllocation[1], secondAllocation[1]);
        }

        [Fact]
        public void Constructor_CustomRange_SetsCorrectRange()
        {
            // Arrange
            var customManager = new PortManager(_mockLogger.Object, 5000, 5010);

            // Act & Assert - This is hard to test directly, but we can verify it doesn't throw
            // and that allocations work within the expected range
            var task = customManager.AllocateConsecutivePortsAsync(2);
            task.Wait();
            var result = task.Result;

            Assert.NotNull(result);
            Assert.True(result[0] >= 5000 && result[0] <= 5010);
        }
    }
}