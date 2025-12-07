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

using System.Collections.Concurrent;
using System.Text.Json;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Manages device registry and persistence
    /// </summary>
    public class DeviceRegistry
    {
        private readonly ILogger<DeviceRegistry> _logger;
        private readonly DeviceRegistryConfig _config;
        private readonly ConcurrentDictionary<string, Device> _devices = new();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private Timer? _autoSaveTimer;

        public DeviceRegistry(ILogger<DeviceRegistry> logger, DeviceRegistryConfig config)
        {
            _logger = logger;
            _config = config;

            if (_config.Enabled)
            {
                LoadFromDisk();

                if (_config.AutoSave)
                {
                    _autoSaveTimer = new Timer(
                        _ => SaveToDisk(),
                        null,
                        TimeSpan.FromSeconds(_config.SaveIntervalSeconds),
                        TimeSpan.FromSeconds(_config.SaveIntervalSeconds)
                    );
                }
            }
        }

        public IReadOnlyCollection<Device> GetAllDevices() => _devices.Values.ToList();

        public Device? GetDevice(string deviceId) => _devices.TryGetValue(deviceId, out var device) ? device : null;

        public IReadOnlyCollection<Device> GetConnectedDevices() =>
            _devices.Values.Where(d => d.State == DeviceState.Connected).ToList();

        public void AddOrUpdateDevice(Device device)
        {
            device.LastSeen = DateTime.UtcNow;
            _devices.AddOrUpdate(device.Id, device, (_, _) => device);
            _logger.LogInformation("Device {DeviceId} ({Platform}) updated in registry", device.Id, device.Platform);
        }

        public void RemoveDevice(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out var device))
            {
                device.State = DeviceState.Disconnected;
                device.DisconnectedAt = DateTime.UtcNow;
                _devices.TryAdd(deviceId, device); // Re-add with updated state
                _logger.LogInformation("Device {DeviceId} marked as disconnected", deviceId);
            }
        }

        public async Task SaveToDiskAsync()
        {
            if (!_config.Enabled) return;

            await _saveLock.WaitAsync();
            try
            {
                var data = new
                {
                    LastUpdated = DateTime.UtcNow,
                    Devices = _devices.Values.ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_config.FilePath, json);
                _logger.LogDebug("Device registry saved to {FilePath}", _config.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save device registry");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private void SaveToDisk() => SaveToDiskAsync().GetAwaiter().GetResult();

        private void LoadFromDisk()
        {
            if (!File.Exists(_config.FilePath))
            {
                _logger.LogInformation("No existing device registry found");
                return;
            }

            try
            {
                var json = File.ReadAllText(_config.FilePath);
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.TryGetProperty("devices", out var devicesArray))
                {
                    var devices = JsonSerializer.Deserialize<List<Device>>(devicesArray.GetRawText());
                    if (devices != null)
                    {
                        foreach (var device in devices)
                        {
                            _devices.TryAdd(device.Id, device);
                        }
                        _logger.LogInformation("Loaded {Count} devices from registry", devices.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load device registry");
            }
        }

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            SaveToDisk();
        }
    }
}
