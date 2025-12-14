using System;
using Microsoft.Extensions.Logging;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins
{
    public class PluginContext
    {
        public PluginConfig Config { get; set; } = new PluginConfig();
        public IServiceProvider Services { get; set; } = null!;
        public ILogger? Logger { get; set; }
        public string InstallFolder { get; set; } = string.Empty;
        public System.Collections.Generic.IDictionary<string, object> Variables { get; set; } = new System.Collections.Generic.Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public int? HealthCheckTimeoutSeconds { get; set; }
    }
}
