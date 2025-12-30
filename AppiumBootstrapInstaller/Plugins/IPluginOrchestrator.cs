using System.Threading;
using System.Threading.Tasks;

namespace AppiumBootstrapInstaller.Plugins
{
    public interface IPluginOrchestrator
    {
        Task<bool> StartPluginAsync(string pluginId, PluginContext context, CancellationToken cancellationToken);
        Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken);
    }
}