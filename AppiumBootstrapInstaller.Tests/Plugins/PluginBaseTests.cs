using System.Threading;
using AppiumBootstrapInstaller.Plugins;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins
{
    public class PluginBaseTests
    {
        private readonly TestPlugin _plugin;

        public PluginBaseTests()
        {
            _plugin = new TestPlugin();
        }

        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            // Assert
            Assert.Equal(string.Empty, _plugin.Id);
            Assert.Equal(string.Empty, _plugin.Type);
            Assert.Equal(PluginState.Disabled, _plugin.State);
            Assert.NotNull(_plugin.Config);
        }

        [Fact]
        public void State_Set_RaisesStateChangedEvent()
        {
            // Arrange
            var eventRaised = false;
            PluginState newState = PluginState.Disabled;

            _plugin.StateChanged += (sender, state) =>
            {
                eventRaised = true;
                newState = state;
            };

            // Act
            _plugin.SetState(PluginState.Running);

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(PluginState.Running, newState);
            Assert.Equal(PluginState.Running, _plugin.State);
        }

        [Fact]
        public void StateChanged_EventHandlerCanBeNull()
        {
            // Act - Should not throw
            _plugin.SetState(PluginState.Starting);

            // Assert
            Assert.Equal(PluginState.Starting, _plugin.State);
        }

        [Fact]
        public async Task StartAsync_WhenImplemented_ReturnsTrue()
        {
            // Arrange
            var context = new PluginContext();
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await _plugin.StartAsync(context, cancellationToken);

            // Assert
            Assert.True(result);
            Assert.Equal(PluginState.Running, _plugin.State);
        }

        [Fact]
        public async Task StopAsync_WhenImplemented_CompletesSuccessfully()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;

            // Act
            await _plugin.StopAsync(cancellationToken);

            // Assert
            Assert.Equal(PluginState.Stopped, _plugin.State);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenImplemented_ReturnsTrue()
        {
            // Act
            var result = await _plugin.CheckHealthAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void Config_CanBeSet()
        {
            // Arrange
            var config = new AppiumBootstrapInstaller.Models.PluginConfig
            {
                Id = "test-plugin",
                Type = "test",
                Enabled = true
            };

            // Act
            _plugin.SetConfig(config);

            // Assert
            Assert.Equal(config, _plugin.Config);
        }

        [Fact]
        public void Id_CanBeSet()
        {
            // Act
            _plugin.SetId("test-id");

            // Assert
            Assert.Equal("test-id", _plugin.Id);
        }

        [Fact]
        public void Type_CanBeSet()
        {
            // Act
            _plugin.SetType("test-type");

            // Assert
            Assert.Equal("test-type", _plugin.Type);
        }

        // Test implementation of PluginBase
        private class TestPlugin : PluginBase
        {
            public void SetState(PluginState state)
            {
                State = state;
            }

            public void SetConfig(AppiumBootstrapInstaller.Models.PluginConfig config)
            {
                Config = config;
            }

            public void SetId(string id)
            {
                Id = id;
            }

            public void SetType(string type)
            {
                Type = type;
            }

            public override async Task<bool> StartAsync(PluginContext context, CancellationToken cancellationToken)
            {
                await Task.Delay(1, cancellationToken); // Simulate async work
                State = PluginState.Running;
                return true;
            }

            public override async Task StopAsync(CancellationToken cancellationToken)
            {
                await Task.Delay(1, cancellationToken); // Simulate async work
                State = PluginState.Stopped;
            }

            public override async Task<bool> CheckHealthAsync()
            {
                await Task.Delay(1); // Simulate health check
                return true;
            }
        }
    }
}