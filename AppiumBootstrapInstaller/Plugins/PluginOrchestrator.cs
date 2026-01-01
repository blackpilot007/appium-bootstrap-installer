using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AppiumBootstrapInstaller.Plugins
{
    using AppiumBootstrapInstaller.Models;
    using System.Collections.Concurrent;

    public class PluginOrchestrator : IPluginOrchestrator
    {
        private readonly PluginRegistry _registry;
        private readonly ILogger<PluginOrchestrator> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, DateTime> _lastRestart = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastHealthCheck = new();
        private Task? _monitorTask;
        private CancellationTokenSource? _monitorCts;

        public PluginOrchestrator(PluginRegistry registry, ILogger<PluginOrchestrator> logger, IServiceProvider serviceProvider)
        {
            _registry = registry;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Starts a background health monitor that will check plugin health and apply restart policies.
        /// This method returns immediately; monitoring runs in background until the provided token is cancelled.
        /// </summary>
        public void StartMonitoring(PluginContext context, int monitorIntervalSeconds = 10, int restartBackoffSeconds = 5, CancellationToken cancellationToken = default)
        {
            // Avoid starting multiple monitors
            if (_monitorTask != null && !_monitorTask.IsCompleted)
                return;

            _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ct = _monitorCts.Token;
            _monitorTask = Task.Run(async () =>
            {
                _logger.LogInformation("PluginOrchestrator: starting health monitor");
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var plugins = _registry.GetInstances().ToList();
                        foreach (var plugin in plugins)
                        {
                            try
                            {
                                if (plugin.State != PluginState.Running)
                                    continue;

                                // Throttle per-instance health-checks using configured interval
                                var instanceId = plugin.Id;
                                int instanceInterval = monitorIntervalSeconds;
                                try
                                {
                                    if (plugin.Config?.HealthCheckIntervalSeconds is int v && v > 0)
                                        instanceInterval = v;
                                }
                                catch { }

                                var now = DateTime.UtcNow;
                                if (_lastHealthCheck.TryGetValue(instanceId, out var lastCheck) && (now - lastCheck) < TimeSpan.FromSeconds(instanceInterval))
                                {
                                    continue; // skip this instance this cycle
                                }

                                _lastHealthCheck[instanceId] = now;

                                bool healthy = false;
                                try
                                {
                                    healthy = await plugin.CheckHealthAsync();
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Health check for plugin {PluginId} threw an exception", plugin.Id);
                                    healthy = false;
                                }

                                if (!healthy)
                                {
                                    var policy = plugin.Config?.RestartPolicy ?? RestartPolicy.OnFailure;
                                    _logger.LogWarning("Plugin {PluginId} reported unhealthy; applying restart policy {Policy}", plugin.Id, policy);

                                    // Emit metric for unhealthy
                                    try
                                    {
                                        var metrics = _serviceProvider.GetService<AppiumBootstrapInstaller.Services.Interfaces.IDeviceMetrics>();
                                        metrics?.RecordPluginUnhealthy(plugin.Id);
                                    }
                                    catch { }

                                    if (policy == RestartPolicy.Never)
                                    {
                                        _logger.LogInformation("Restart disabled for plugin {PluginId}", plugin.Id);
                                        continue;
                                    }

                                    // throttle restarts to avoid tight loops
                                    var key = plugin.Id;
                                    var now2 = DateTime.UtcNow;
                                    if (_lastRestart.TryGetValue(key, out var last) && (now2 - last) < TimeSpan.FromSeconds(restartBackoffSeconds))
                                    {
                                        _logger.LogInformation("Skipping restart for {PluginId} due to recent restart", plugin.Id);
                                        continue;
                                    }

                                    _lastRestart[key] = now2;

                                    try
                                    {
                                        await plugin.StopAsync(CancellationToken.None);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Error stopping plugin {PluginId} before restart", plugin.Id);
                                    }

                                    try
                                    {
                                        await plugin.StartAsync(context, CancellationToken.None);
                                        _logger.LogInformation("Plugin {PluginId} restarted successfully", plugin.Id);

                                        try
                                        {
                                            var metrics = _serviceProvider.GetService<AppiumBootstrapInstaller.Services.Interfaces.IDeviceMetrics>();
                                            metrics?.RecordPluginRestart(plugin.Id);
                                        }
                                        catch { }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to restart plugin {PluginId}", plugin.Id);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error while monitoring plugin {PluginId}", plugin.Id);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Plugin monitor loop encountered an error");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(monitorIntervalSeconds), ct);
                }

                _logger.LogInformation("PluginOrchestrator: health monitor stopped");
            }, ct);
        }

        public async Task StartEnabledPluginsAsync(PluginContext context, CancellationToken cancellationToken)
        {
            // Start enabled definitions (definitions are blueprints registered at startup)
            var defs = _registry.GetDefinitions().Where(kv => kv.Value.Enabled).ToList();
            foreach (var kv in defs)
            {
                var defId = kv.Key;
                try
                {
                    _logger.LogInformation("Starting plugin definition {PluginId}", defId);
                    await StartPluginAsync(defId, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while starting plugin definition {PluginId}", defId);
                }
            }
        }

        public virtual async Task<bool> StartPluginAsync(string pluginId, PluginContext context, CancellationToken cancellationToken)
        {
            // pluginId here refers to a definition id. Create a runtime instance id.
            var def = _registry.GetDefinition(pluginId);
            if (def == null)
            {
                _logger.LogWarning("Plugin definition {PluginId} not found", pluginId);
                return false;
            }

            // Determine device context if provided
            string? deviceId = null;
            if (context?.Variables != null && context.Variables.TryGetValue("deviceId", out var dv) && dv != null)
            {
                deviceId = dv.ToString();
            }

            string instanceId = string.IsNullOrEmpty(deviceId) ? pluginId : $"{pluginId}:{deviceId}";

            // Prevent double-start of same instance
            if (_registry.GetInstance(instanceId) != null)
            {
                _logger.LogInformation("Plugin instance {InstanceId} already running", instanceId);
                return true;
            }

            try
            {
                _logger.LogInformation("Creating plugin instance {InstanceId} (definition={PluginId})", instanceId, pluginId);
                // Determine effective timeout and place it into the PluginContext so the plugin instance can use it.
                var globalConfig = _serviceProvider.GetService<AppiumBootstrapInstaller.Models.InstallConfig>();
                int globalTimeout = globalConfig?.HealthCheckTimeoutSeconds ?? 5;
                context = context ?? new PluginContext();
                context.HealthCheckTimeoutSeconds = def.HealthCheckTimeoutSeconds ?? globalTimeout;

                var type = (def.Type ?? "process").ToLowerInvariant();
                IPlugin instance;
                if (type == "script")
                {
                    var logger = _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AppiumBootstrapInstaller.Plugins.BuiltIn.ScriptPlugin>>();
                    instance = new AppiumBootstrapInstaller.Plugins.BuiltIn.ScriptPlugin(instanceId, def, logger);
                }
                else
                {
                    var logger = _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AppiumBootstrapInstaller.Plugins.BuiltIn.ProcessPlugin>>();
                    instance = new AppiumBootstrapInstaller.Plugins.BuiltIn.ProcessPlugin(instanceId, def, logger);
                }

                // Register runtime instance
                _registry.RegisterInstance(instance);

                // Start instance
                var started = await instance.StartAsync(context, cancellationToken);
                if (!started)
                {
                    _logger.LogWarning("Plugin instance {InstanceId} failed to start", instanceId);
                }

                return started;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/start plugin instance {InstanceId}", instanceId);
                return false;
            }
        }

        public virtual async Task<bool> StopPluginAsync(string pluginId, CancellationToken cancellationToken)
        {
            var instance = _registry.GetInstance(pluginId);
            if (instance == null)
            {
                _logger.LogWarning("Plugin instance {PluginId} not found", pluginId);
                return false;
            }

            try
            {
                _logger.LogInformation("Stopping plugin instance {PluginId}", pluginId);
                await instance.StopAsync(cancellationToken);
                _registry.RemoveInstance(pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop plugin instance {PluginId}", pluginId);
                return false;
            }
        }

        public async Task StopAllAsync(CancellationToken cancellationToken)
        {
            var plugins = _registry.GetInstances().ToList();
            foreach (var plugin in plugins)
            {
                try
                {
                    _logger.LogInformation("Stopping plugin {PluginId}", plugin.Id);
                    await plugin.StopAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while stopping plugin {PluginId}", plugin.Id);
                }
            }
        }
    }
}
