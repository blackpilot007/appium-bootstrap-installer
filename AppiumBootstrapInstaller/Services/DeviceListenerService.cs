/*
 * Copyright 2025 Appium Bootstrap Installer Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Diagnostics;
using System.Runtime.InteropServices;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Background service that monitors device connections and manages Appium sessions
    /// </summary>
    public class DeviceListenerService : BackgroundService
    {
        private readonly ILogger<DeviceListenerService> _logger;
        private readonly InstallConfig _config;
        private readonly string _installFolder;
        private readonly DeviceRegistry _registry;
        private readonly AppiumSessionManager _sessionManager;
        private readonly WebhookNotifier _webhookNotifier;

        public DeviceListenerService(
            ILogger<DeviceListenerService> logger,
            InstallConfig config,
            string installFolder,
            DeviceRegistry registry,
            AppiumSessionManager sessionManager,
            WebhookNotifier webhookNotifier)
        {
            _logger = logger;
            _config = config;
            _installFolder = installFolder;
            _registry = registry;
            _sessionManager = sessionManager;
            _webhookNotifier = webhookNotifier;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.EnableDeviceListener)
            {
                _logger.LogInformation("Device listener is disabled");
                return;
            }

            _logger.LogInformation("Device listener service starting...");
            _logger.LogInformation("Poll interval: {Interval} seconds", _config.DeviceListenerPollInterval);
            _logger.LogInformation("Auto-start Appium: {AutoStart}", _config.AutoStartAppium);

            // Check tool availability
            var adbAvailable = IsToolAvailable("adb");
            var ideviceAvailable = IsToolAvailable("idevice_id");

            _logger.LogInformation("ADB available: {Available}", adbAvailable);
            _logger.LogInformation("idevice_id available: {Available}", ideviceAvailable);

            if (!adbAvailable && !ideviceAvailable)
            {
                _logger.LogWarning("No device monitoring tools available. Service will not monitor devices.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (adbAvailable)
                    {
                        await MonitorAndroidDevicesAsync();
                    }

                    if (ideviceAvailable)
                    {
                        await MonitoriOSDevicesAsync();
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_config.DeviceListenerPollInterval), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in device monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(_config.DeviceListenerPollInterval), stoppingToken);
                }
            }

            _logger.LogInformation("Device listener service stopping...");
            await StopAllSessionsAsync();
        }

        private async Task MonitorAndroidDevicesAsync()
        {
            try
            {
                var devices = await GetAndroidDevicesAsync();
                var currentIds = devices.Select(d => d.Id).ToHashSet();
                var previousIds = _registry.GetAllDevices()
                    .Where(d => d.Platform == DevicePlatform.Android && d.State == DeviceState.Connected)
                    .Select(d => d.Id)
                    .ToHashSet();

                // Handle new devices
                foreach (var device in devices)
                {
                    if (!previousIds.Contains(device.Id))
                    {
                        await OnDeviceConnectedAsync(device);
                    }
                }

                // Handle disconnected devices
                foreach (var id in previousIds)
                {
                    if (!currentIds.Contains(id))
                    {
                        await OnDeviceDisconnectedAsync(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring Android devices");
            }
        }

        private async Task MonitoriOSDevicesAsync()
        {
            try
            {
                var devices = await GetiOSDevicesAsync();
                var currentIds = devices.Select(d => d.Id).ToHashSet();
                var previousIds = _registry.GetAllDevices()
                    .Where(d => d.Platform == DevicePlatform.iOS && d.State == DeviceState.Connected)
                    .Select(d => d.Id)
                    .ToHashSet();

                // Handle new devices
                foreach (var device in devices)
                {
                    if (!previousIds.Contains(device.Id))
                    {
                        await OnDeviceConnectedAsync(device);
                    }
                }

                // Handle disconnected devices
                foreach (var id in previousIds)
                {
                    if (!currentIds.Contains(id))
                    {
                        await OnDeviceDisconnectedAsync(id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring iOS devices");
            }
        }

        private async Task<List<Device>> GetAndroidDevicesAsync()
        {
            var devices = new List<Device>();

            try
            {
                var output = await RunCommandAsync("adb", "devices");
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Skip(1); // Skip header

                foreach (var line in lines)
                {
                    var parts = line.Trim().Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var serial = parts[0];
                        var state = parts[1];

                        if (state == "device" || state == "emulator")
                        {
                            devices.Add(new Device
                            {
                                Id = serial,
                                Platform = DevicePlatform.Android,
                                Type = serial.Contains("emulator") ? DeviceType.Emulator : DeviceType.Physical,
                                Name = await GetAndroidDeviceNameAsync(serial),
                                State = DeviceState.Connected,
                                ConnectedAt = DateTime.UtcNow,
                                LastSeen = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Android devices");
            }

            return devices;
        }

        private async Task<List<Device>> GetiOSDevicesAsync()
        {
            var devices = new List<Device>();

            try
            {
                var output = await RunCommandAsync("idevice_id", "-l");
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var udid = line.Trim();
                    if (!string.IsNullOrWhiteSpace(udid))
                    {
                        var name = await GetiOSDeviceNameAsync(udid);
                        devices.Add(new Device
                        {
                            Id = udid,
                            Platform = DevicePlatform.iOS,
                            Type = DeviceType.Physical,
                            Name = name,
                            State = DeviceState.Connected,
                            ConnectedAt = DateTime.UtcNow,
                            LastSeen = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get iOS devices");
            }

            return devices;
        }

        private async Task<string> GetAndroidDeviceNameAsync(string serial)
        {
            try
            {
                var output = await RunCommandAsync("adb", $"-s {serial} shell getprop ro.product.model");
                return output.Trim();
            }
            catch
            {
                return "Unknown Android Device";
            }
        }

        private async Task<string> GetiOSDeviceNameAsync(string udid)
        {
            try
            {
                var output = await RunCommandAsync("ideviceinfo", $"-u {udid} -k DeviceName");
                return output.Trim();
            }
            catch
            {
                return "Unknown iOS Device";
            }
        }

        private async Task OnDeviceConnectedAsync(Device device)
        {
            _logger.LogInformation(
                "Device connected: {DeviceId} ({Platform}, {Type}) - {Name}",
                device.Id, device.Platform, device.Type, device.Name
            );

            _registry.AddOrUpdateDevice(device);
            await _webhookNotifier.NotifyDeviceConnectedAsync(device);

            if (_config.AutoStartAppium)
            {
                var session = await _sessionManager.StartSessionAsync(device);
                if (session != null)
                {
                    device.AppiumSession = session;
                    _registry.AddOrUpdateDevice(device);
                    await _webhookNotifier.NotifySessionStartedAsync(device);

                    _logger.LogInformation(
                        "Appium session started for {DeviceId} on port {Port}",
                        device.Id, session.AppiumPort
                    );
                }
            }
        }

        private async Task OnDeviceDisconnectedAsync(string deviceId)
        {
            var device = _registry.GetDevice(deviceId);
            if (device == null) return;

            _logger.LogInformation("Device disconnected: {DeviceId}", deviceId);

            if (device.AppiumSession != null)
            {
                await _sessionManager.StopSessionAsync(device.AppiumSession);
                await _webhookNotifier.NotifySessionEndedAsync(device);
            }

            _registry.RemoveDevice(deviceId);
            await _webhookNotifier.NotifyDeviceDisconnectedAsync(device);
        }

        private async Task StopAllSessionsAsync()
        {
            var devices = _registry.GetConnectedDevices();
            foreach (var device in devices)
            {
                if (device.AppiumSession != null)
                {
                    await _sessionManager.StopSessionAsync(device.AppiumSession);
                }
            }
        }

        private bool IsToolAvailable(string toolName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                    Arguments = toolName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(psi);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> RunCommandAsync(string command, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException($"Failed to start process: {command}");

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Command failed: {command} {arguments}\n{error}");
            }

            return output;
        }
    }
}
