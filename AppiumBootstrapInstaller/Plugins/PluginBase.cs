using System;
using System.Threading;

namespace AppiumBootstrapInstaller.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        public string Id { get; protected set; } = string.Empty;
        public string Type { get; protected set; } = string.Empty;

        private PluginState _state = PluginState.Disabled;
        public PluginState State
        {
            get => _state;
            protected set
            {
                _state = value;
                StateChanged?.Invoke(this, _state);
            }
        }

        public AppiumBootstrapInstaller.Models.PluginConfig Config { get; protected set; } = new AppiumBootstrapInstaller.Models.PluginConfig();

        public event EventHandler<PluginState>? StateChanged;

        public abstract Task<bool> StartAsync(PluginContext context, CancellationToken cancellationToken);
        public abstract Task StopAsync(CancellationToken cancellationToken);
        public abstract Task<bool> CheckHealthAsync();
    }
}
