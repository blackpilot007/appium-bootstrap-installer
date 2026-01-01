using System;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Plugins.Triggers
{
    public class DeviceEventTrigger
    {
        private readonly IEventBus _eventBus;
        private readonly Plugins.PluginRegistry _registry;
        private readonly IPluginOrchestrator _orchestrator;
        private readonly ILogger<DeviceEventTrigger> _logger;

        public DeviceEventTrigger(IEventBus eventBus, Plugins.PluginRegistry registry, IPluginOrchestrator orchestrator, ILogger<DeviceEventTrigger> logger)
        {
            _eventBus = eventBus;
            _registry = registry;
            _orchestrator = orchestrator;
            _logger = logger;

            // Subscribe to device events
            _eventBus.Subscribe<DeviceConnectedEvent>(OnDeviceConnected);
            _eventBus.Subscribe<DeviceDisconnectedEvent>(OnDeviceDisconnected);
        }

        private void OnDeviceConnected(DeviceConnectedEvent ev)
        {
            _ = HandleDeviceConnectedAsync(ev.Device);
        }

        private void OnDeviceDisconnected(DeviceDisconnectedEvent ev)
        {
            _ = HandleDeviceDisconnectedAsync(ev.Device);
        }

        private async Task HandleDeviceConnectedAsync(Device device)
        {
            try
            {
                _logger.LogInformation("DeviceEventTrigger handling connected device {DeviceId}", device.Id);

                foreach (var def in _registry.GetDefinitions())
                {
                    var defId = def.Key;
                    var cfg = def.Value;
                    var trigger = cfg?.TriggerOn?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(trigger)) continue;

                    if (trigger == "device-connected" && cfg.Enabled)
                    {
                        var ctx = new Plugins.PluginContext
                        {
                            InstallFolder = string.Empty,
                            Services = null!,
                            Logger = _logger
                        };
                        ctx.Config = cfg;
                        ctx.Variables = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["device"] = device,
                            ["deviceId"] = device.Id
                        };

                        try
                        {
                            // Start an instance for this device (or a single instance if plugin is not per-device)
                            await _orchestrator.StartPluginAsync(defId, ctx, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to start plugin {PluginId} for device {DeviceId}", defId, device.Id);
                            // Continue processing other plugins
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceEventTrigger failed to handle device connected");
            }
        }

        private async Task HandleDeviceDisconnectedAsync(Device device)
        {
            try
            {
                _logger.LogInformation("DeviceEventTrigger handling disconnected device {DeviceId}", device.Id);

                // For definitions that trigger on disconnect, start them
                foreach (var def in _registry.GetDefinitions())
                {
                    var defId = def.Key;
                    var cfg = def.Value;
                    var trigger = cfg?.TriggerOn?.ToLowerInvariant();
                    if (string.IsNullOrEmpty(trigger)) continue;

                    if (trigger == "device-disconnected" && cfg.Enabled)
                    {
                        var ctx = new Plugins.PluginContext
                        {
                            InstallFolder = string.Empty,
                            Services = null!,
                            Logger = _logger,
                            Config = cfg
                        };

                        ctx.Variables = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["device"] = device,
                            ["deviceId"] = device.Id
                        };

                        try
                        {
                            await _orchestrator.StartPluginAsync(defId, ctx, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to start plugin {PluginId} for device disconnect {DeviceId}", defId, device.Id);
                            // Continue processing other plugins
                        }
                    }

                    // If this definition was configured to stop instances on disconnect, stop any running instances for this device
                    if (cfg?.TriggerOn?.ToLowerInvariant() == "device-connected" && cfg?.StopOnDisconnect == true)
                    {
                        var instanceId = $"{defId}:{device.Id}";
                        try
                        {
                            _logger.LogInformation("Stopping plugin instance {InstanceId} due to device disconnect", instanceId);
                            await _orchestrator.StopPluginAsync(instanceId, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to stop plugin instance {InstanceId} for device disconnect {DeviceId}", instanceId, device.Id);
                            // Continue processing other plugins
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceEventTrigger failed to handle device disconnected");
            }
        }
    }
}
