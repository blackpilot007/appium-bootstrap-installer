using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppiumBootstrapInstaller.Plugins
{
    public interface IPlugin
    {
        string Id { get; }
        string Type { get; }
        PluginState State { get; }
        AppiumBootstrapInstaller.Models.PluginConfig Config { get; }

        Task<bool> StartAsync(PluginContext context, CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
        Task<bool> CheckHealthAsync();

        event EventHandler<PluginState> StateChanged;
    }
}
