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
        private readonly DeviceMetrics _metrics;
        private Timer? _metricsTimer;
        private string? _goIosPath;
        private bool _useGoIosForDevices = false;

        public DeviceListenerService(
            ILogger<DeviceListenerService> logger,
            InstallConfig config,
            string installFolder,
            DeviceRegistry registry,
            AppiumSessionManager sessionManager,
            DeviceMetrics metrics)
        {
            _logger = logger;
            _config = config;
            _installFolder = installFolder;
            _registry = registry;
            _sessionManager = sessionManager;
            _metrics = metrics;
            
            // Check for go-ios installation
            var goIosBin = Path.Combine(_installFolder, ".cache", "appium-device-farm", "goIOS", "ios", "ios.exe");
            if (File.Exists(goIosBin))
            {
                _goIosPath = goIosBin;
                _logger.LogInformation("Found go-ios at: {Path}", _goIosPath);
            }
            
            // Log metrics every 5 minutes
            _metricsTimer = new Timer(
                _ => _logger.LogInformation("[METRICS] {Summary}", _metrics.GetSummary()),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );
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
            
            // If libimobiledevice not available, check for go-ios fallback
            if (!ideviceAvailable && !string.IsNullOrEmpty(_goIosPath))
            {
                _logger.LogInformation("libimobiledevice not available, using go-ios as fallback");
                _useGoIosForDevices = true;
                ideviceAvailable = true;
            }

            _logger.LogInformation("ADB available: {Available}", adbAvailable);
            _logger.LogInformation("iOS tools available: {Available} (using {Tool})", ideviceAvailable, _useGoIosForDevices ? "go-ios" : "libimobiledevice");

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
                if (_useGoIosForDevices && !string.IsNullOrEmpty(_goIosPath))
                {
                    // Use go-ios with graceful error handling for pairing issues
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = _goIosPath,
                            Arguments = "list --details",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var process = Process.Start(psi);
                        if (process == null) return devices;

                        var output = await process.StandardOutput.ReadToEndAsync();
                        var stderr = await process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();

                    // go-ios returns non-zero exit code when no devices found or pairing issues
                    // This is expected behavior for monitoring, not a fatal error
                    if (process.ExitCode != 0)
                    {
                        // Check for device trust/pairing issues
                        if (stderr.Contains("could not retrieve PairRecord") || stderr.Contains("ReadPair failed"))
                        {
                            _logger.LogWarning("═══════════════════════════════════════════════════════════════");
                            _logger.LogWarning("  iOS DEVICE DETECTED BUT NOT TRUSTED");
                            _logger.LogWarning("═══════════════════════════════════════════════════════════════");
                            _logger.LogWarning("Action Required:");
                            _logger.LogWarning("  1. Unlock your iPhone/iPad");
                            _logger.LogWarning("  2. Look for 'Trust This Computer?' dialog on device");
                            _logger.LogWarning("  3. Tap 'Trust' and enter device passcode");
                            _logger.LogWarning("  4. Device will appear automatically once trusted");
                            _logger.LogWarning("═══════════════════════════════════════════════════════════════");
                        }
                        // Only log unexpected errors (not pairing or "no devices")
                        else if (!stderr.Contains("no device found") && !stderr.Contains("go-ios agent is not running"))
                        {
                            _logger.LogDebug("go-ios list returned exit code {ExitCode}: {Error}", 
                                process.ExitCode, stderr);
                        }
                        return devices; // Empty list - no devices paired or connected
                    }

                    // go-ios returns JSON format: {"deviceList":[{"Udid":"...","ProductName":"...","ProductVersion":"...","DeviceName":"..."}]}
                    var jsonOutput = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(line => line.TrimStart().StartsWith("{"));
                    
                    if (!string.IsNullOrEmpty(jsonOutput))
                    {
                        try
                        {
                            using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonOutput);
                            if (jsonDoc.RootElement.TryGetProperty("deviceList", out var deviceList))
                            {
                                foreach (var deviceElement in deviceList.EnumerateArray())
                                {
                                    var udid = deviceElement.TryGetProperty("Udid", out var udidProp) ? udidProp.GetString() : null;
                                    var deviceName = deviceElement.TryGetProperty("DeviceName", out var nameProp) ? nameProp.GetString() : null;
                                    var productName = deviceElement.TryGetProperty("ProductName", out var prodProp) ? prodProp.GetString() : null;
                                    var productVersion = deviceElement.TryGetProperty("ProductVersion", out var verProp) ? verProp.GetString() : null;
                                    
                                    if (!string.IsNullOrEmpty(udid))
                                    {
                                        var displayName = !string.IsNullOrEmpty(deviceName) ? deviceName : 
                                                         (!string.IsNullOrEmpty(productName) ? productName : "Unknown iOS Device");
                                        
                                        devices.Add(new Device
                                        {
                                            Id = udid,
                                            Platform = DevicePlatform.iOS,
                                            Type = DeviceType.Physical,
                                            Name = displayName,
                                            State = DeviceState.Connected,
                                            ConnectedAt = DateTime.UtcNow,
                                            LastSeen = DateTime.UtcNow
                                        });
                                        
                                        // Log all device details
                                        _logger.LogInformation(
                                            "iOS Device Detected: {DeviceName} | UDID: {Udid} | Model: {ProductName} | iOS: {ProductVersion}",
                                            displayName, udid, productName ?? "N/A", productVersion ?? "N/A"
                                        );
                                    }
                                }
                            }
                        }
                        catch (System.Text.Json.JsonException ex)
                        {
                            _logger.LogDebug(ex, "Failed to parse go-ios JSON output: {Output}", jsonOutput);
                        }
                    }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "go-ios command execution failed, treating as no devices");
                        return devices; // Return empty list on any error
                    }
                }
                else
                {
                    // Use libimobiledevice
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
                if (_useGoIosForDevices && !string.IsNullOrEmpty(_goIosPath))
                {
                    // Use go-ios devicename command with error handling
                    var psi = new ProcessStartInfo
                    {
                        FileName = _goIosPath,
                        Arguments = $"devicename --udid={udid}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) return "Unknown iOS Device";

                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        return output.Trim();
                    }
                    return "Unknown iOS Device";
                }
                else
                {
                    // Use libimobiledevice
                    var output = await RunCommandAsync("ideviceinfo", $"-u {udid} -k DeviceName");
                    return output.Trim();
                }
            }
            catch
            {
                return "Unknown iOS Device";
            }
        }

        private async Task OnDeviceConnectedAsync(Device device)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["DeviceId"] = device.Id,
                ["Platform"] = device.Platform.ToString()
            }))
            {
                _logger.LogInformation(
                    "[{CorrelationId}] Device connected: {DeviceId} ({Platform}, {Type}) - {Name}",
                    correlationId, device.Id, device.Platform, device.Type, device.Name
                );

                _metrics.RecordDeviceConnected(device.Platform.ToString());
                _registry.AddOrUpdateDevice(device);

                if (_config.AutoStartAppium)
                {
                    var startTime = DateTime.UtcNow;
                    try
                    {
                        var session = await _sessionManager.StartSessionAsync(device);
                        if (session != null)
                        {
                            var duration = DateTime.UtcNow - startTime;
                            _metrics.RecordSessionStarted(duration);
                            
                            device.AppiumSession = session;
                            _registry.AddOrUpdateDevice(device);

                            // Log service log paths for easy troubleshooting
                            var executableDir = AppDomain.CurrentDomain.BaseDirectory;
                            var logDir = Path.Combine(executableDir, "logs");
                            var serviceName = $"Appium-{device.Id}";
                            var stdoutLog = Path.Combine(logDir, $"{serviceName}_stdout.log");
                            var stderrLog = Path.Combine(logDir, $"{serviceName}_stderr.log");

                            _logger.LogInformation(
                                "[{CorrelationId}] Appium session started for {DeviceId} on port {Port} (took {Duration}ms)",
                                correlationId, device.Id, session.AppiumPort, duration.TotalMilliseconds
                            );
                            _logger.LogInformation(
                                "[{CorrelationId}] Service logs: {StdoutLog} | {StderrLog}",
                                correlationId, stdoutLog, stderrLog
                            );
                        }
                        else
                        {
                            _metrics.RecordSessionFailed("StartSessionReturnedNull");
                            _logger.LogWarning(
                                "[{CorrelationId}] Failed to start Appium session for {DeviceId} - returned null",
                                correlationId, device.Id
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _metrics.RecordSessionFailed(ex.GetType().Name);
                        _logger.LogError(ex,
                            "[{CorrelationId}] Exception starting Appium session for {DeviceId}",
                            correlationId, device.Id
                        );
                    }
                }
            }
        }

        private async Task OnDeviceDisconnectedAsync(string deviceId)
        {
            var device = _registry.GetDevice(deviceId);
            if (device == null) return;

            var correlationId = Guid.NewGuid().ToString("N")[..8];
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["DeviceId"] = deviceId
            }))
            {
                _logger.LogInformation(
                    "[{CorrelationId}] Device disconnected: {DeviceId}",
                    correlationId, deviceId
                );

                _metrics.RecordDeviceDisconnected(device.Platform.ToString());

                if (device.AppiumSession != null)
                {
                    try
                    {
                        await _sessionManager.StopSessionAsync(device.AppiumSession);
                        _metrics.RecordSessionStopped();
                        _logger.LogInformation(
                            "[{CorrelationId}] Appium session stopped for {DeviceId}",
                            correlationId, deviceId
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "[{CorrelationId}] Error stopping Appium session for {DeviceId}",
                            correlationId, deviceId
                        );
                    }
                }

                _registry.RemoveDevice(deviceId);
            }
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

                using var process = Process.Start(psi);
                if (process == null) return false;
                
                process.WaitForExit();
                return process.ExitCode == 0;
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
