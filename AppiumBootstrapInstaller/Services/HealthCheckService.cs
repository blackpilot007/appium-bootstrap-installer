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

using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Service for monitoring overall system health
    /// </summary>
    public class HealthCheckService : IHealthCheckService
    {
        private readonly IDeviceRegistry _registry;
        private readonly ILogger<HealthCheckService> _logger;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public HealthCheckService(
            IDeviceRegistry registry,
            ILogger<HealthCheckService> logger)
        {
            _registry = registry;
            _logger = logger;
        }

        public ServiceHealthStatus GetHealth()
        {
            var devices = _registry.GetConnectedDevices();
            var sessions = devices
                .Where(d => d.AppiumSession?.Status == SessionStatus.Running)
                .Select(d => d.AppiumSession!)
                .ToList();

            var componentStatus = new Dictionary<string, string>
            {
                ["DeviceRegistry"] = _registry.GetAllDevices().Count > 0 ? "Healthy" : "NoDevices",
                ["SessionManager"] = sessions.Count > 0 ? "Healthy" : "NoSessions",
                ["EventBus"] = "Healthy"
            };

            var isHealthy = componentStatus.Values.All(s => s == "Healthy" || s == "NoDevices" || s == "NoSessions");

            return new ServiceHealthStatus
            {
                IsHealthy = isHealthy,
                ConnectedDevices = devices.Count,
                ActiveSessions = sessions.Count,
                RunningPlugins = 0, // Will be updated by plugin orchestrator
                ComponentStatus = componentStatus,
                Uptime = GetUptime()
            };
        }

        public TimeSpan GetUptime() => DateTime.UtcNow - _startTime;
    }
}
