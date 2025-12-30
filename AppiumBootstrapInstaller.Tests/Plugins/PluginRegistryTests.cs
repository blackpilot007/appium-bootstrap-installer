using System.Linq;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins
{
    public class PluginRegistryTests
    {
        private readonly PluginRegistry _registry;

        public PluginRegistryTests()
        {
            _registry = new PluginRegistry();
        }

        [Fact]
        public void RegisterDefinition_ValidId_AddsDefinition()
        {
            // Arrange
            var config = new PluginConfig { Id = "test-plugin", Type = "test", Enabled = true };

            // Act
            _registry.RegisterDefinition("test-plugin", config);

            // Assert
            var definition = _registry.GetDefinition("test-plugin");
            Assert.NotNull(definition);
            Assert.Equal(config, definition);
        }

        [Fact]
        public void RegisterDefinition_NullOrEmptyId_DoesNothing()
        {
            // Arrange
            var config = new PluginConfig { Id = "test-plugin", Type = "test", Enabled = true };

            // Act
            _registry.RegisterDefinition(null, config);
            _registry.RegisterDefinition("", config);
            _registry.RegisterDefinition("   ", config);

            // Assert
            var definitions = _registry.GetDefinitions();
            Assert.Empty(definitions);
        }

        [Fact]
        public void RegisterDefinition_DuplicateId_ReplacesExisting()
        {
            // Arrange
            var config1 = new PluginConfig { Id = "test-plugin", Type = "test1", Enabled = true };
            var config2 = new PluginConfig { Id = "test-plugin", Type = "test2", Enabled = false };

            // Act
            _registry.RegisterDefinition("test-plugin", config1);
            _registry.RegisterDefinition("test-plugin", config2);

            // Assert
            var definition = _registry.GetDefinition("test-plugin");
            Assert.NotNull(definition);
            Assert.Equal(config2.Type, definition.Type);
            Assert.Equal(config2.Enabled, definition.Enabled);

            var definitions = _registry.GetDefinitions();
            Assert.Single(definitions); // Should still be one entry
        }

        [Fact]
        public void GetDefinitions_ReturnsInRegistrationOrder()
        {
            // Arrange
            var config1 = new PluginConfig { Id = "plugin1", Type = "type1" };
            var config2 = new PluginConfig { Id = "plugin2", Type = "type2" };
            var config3 = new PluginConfig { Id = "plugin3", Type = "type3" };

            _registry.RegisterDefinition("plugin1", config1);
            _registry.RegisterDefinition("plugin2", config2);
            _registry.RegisterDefinition("plugin3", config3);

            // Act
            var definitions = _registry.GetDefinitions().ToList();

            // Assert
            Assert.Equal(3, definitions.Count);
            Assert.Equal("plugin1", definitions[0].Key);
            Assert.Equal("plugin2", definitions[1].Key);
            Assert.Equal("plugin3", definitions[2].Key);
        }

        [Fact]
        public void GetDefinition_NonExistentId_ReturnsNull()
        {
            // Act
            var definition = _registry.GetDefinition("non-existent");

            // Assert
            Assert.Null(definition);
        }

        [Fact]
        public void RegisterInstance_ValidInstance_AddsInstance()
        {
            // Arrange
            var mockPlugin = new Mock<IPlugin>();
            mockPlugin.Setup(p => p.Id).Returns("test-instance");

            // Act
            _registry.RegisterInstance(mockPlugin.Object);

            // Assert
            var instance = _registry.GetInstance("test-instance");
            Assert.NotNull(instance);
            Assert.Equal(mockPlugin.Object, instance);
        }

        [Fact]
        public void GetInstances_ReturnsAllInstances()
        {
            // Arrange
            var mockPlugin1 = new Mock<IPlugin>();
            mockPlugin1.Setup(p => p.Id).Returns("instance1");

            var mockPlugin2 = new Mock<IPlugin>();
            mockPlugin2.Setup(p => p.Id).Returns("instance2");

            _registry.RegisterInstance(mockPlugin1.Object);
            _registry.RegisterInstance(mockPlugin2.Object);

            // Act
            var instances = _registry.GetInstances().ToList();

            // Assert
            Assert.Equal(2, instances.Count);
            Assert.Contains(mockPlugin1.Object, instances);
            Assert.Contains(mockPlugin2.Object, instances);
        }

        [Fact]
        public void GetInstance_NonExistentId_ReturnsNull()
        {
            // Act
            var instance = _registry.GetInstance("non-existent");

            // Assert
            Assert.Null(instance);
        }

        [Fact]
        public void GetInstancesByDefinitionId_ExactMatch_ReturnsInstance()
        {
            // Arrange
            var mockPlugin = new Mock<IPlugin>();
            mockPlugin.Setup(p => p.Id).Returns("definition1");

            _registry.RegisterInstance(mockPlugin.Object);

            // Act
            var instances = _registry.GetInstancesByDefinitionId("definition1").ToList();

            // Assert
            Assert.Single(instances);
            Assert.Equal(mockPlugin.Object, instances[0]);
        }

        [Fact]
        public void GetInstancesByDefinitionId_PrefixedMatch_ReturnsInstance()
        {
            // Arrange
            var mockPlugin = new Mock<IPlugin>();
            mockPlugin.Setup(p => p.Id).Returns("definition1:instance1");

            _registry.RegisterInstance(mockPlugin.Object);

            // Act
            var instances = _registry.GetInstancesByDefinitionId("definition1").ToList();

            // Assert
            Assert.Single(instances);
            Assert.Equal(mockPlugin.Object, instances[0]);
        }

        [Fact]
        public void GetInstancesByDefinitionId_NoMatch_ReturnsEmpty()
        {
            // Arrange
            var mockPlugin = new Mock<IPlugin>();
            mockPlugin.Setup(p => p.Id).Returns("definition1:instance1");

            _registry.RegisterInstance(mockPlugin.Object);

            // Act
            var instances = _registry.GetInstancesByDefinitionId("definition2").ToList();

            // Assert
            Assert.Empty(instances);
        }

        [Fact]
        public void RemoveInstance_ExistingInstance_ReturnsTrue()
        {
            // Arrange
            var mockPlugin = new Mock<IPlugin>();
            mockPlugin.Setup(p => p.Id).Returns("test-instance");

            _registry.RegisterInstance(mockPlugin.Object);

            // Act
            var result = _registry.RemoveInstance("test-instance");

            // Assert
            Assert.True(result);
            var instance = _registry.GetInstance("test-instance");
            Assert.Null(instance);
        }

        [Fact]
        public void RemoveInstance_NonExistentInstance_ReturnsFalse()
        {
            // Act
            var result = _registry.RemoveInstance("non-existent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RegisterInstance_OverwritesExisting()
        {
            // Arrange
            var mockPlugin1 = new Mock<IPlugin>();
            mockPlugin1.Setup(p => p.Id).Returns("test-instance");

            var mockPlugin2 = new Mock<IPlugin>();
            mockPlugin2.Setup(p => p.Id).Returns("test-instance");

            // Act
            _registry.RegisterInstance(mockPlugin1.Object);
            _registry.RegisterInstance(mockPlugin2.Object);

            // Assert
            var instance = _registry.GetInstance("test-instance");
            Assert.NotNull(instance);
            Assert.Equal(mockPlugin2.Object, instance);
        }

        [Fact]
        public void ThreadSafety_DefinitionsAndInstances()
        {
            // Arrange
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act - Concurrent operations
            for (int i = 0; i < 10; i++)
            {
                var index = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Register definitions
                        _registry.RegisterDefinition($"plugin{index}", new PluginConfig { Id = $"plugin{index}" });

                        // Register instances
                        var mockPlugin = new Mock<IPlugin>();
                        mockPlugin.Setup(p => p.Id).Returns($"instance{index}");
                        _registry.RegisterInstance(mockPlugin.Object);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Empty(exceptions);
            var definitions = _registry.GetDefinitions();
            var instances = _registry.GetInstances();
            Assert.Equal(10, definitions.Count());
            Assert.Equal(10, instances.Count());
        }
    }
}