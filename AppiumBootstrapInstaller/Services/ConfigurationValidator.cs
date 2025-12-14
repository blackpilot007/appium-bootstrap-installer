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
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Validates configuration before startup
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ILogger<ConfigurationValidator> _logger;

        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger;
        }

        public bool Validate(InstallConfig config, out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            // Required fields
            if (string.IsNullOrWhiteSpace(config.InstallFolder))
                errors.Add("InstallFolder cannot be empty");

            if (string.IsNullOrWhiteSpace(config.NodeVersion))
                errors.Add("NodeVersion cannot be empty");

            if (string.IsNullOrWhiteSpace(config.AppiumVersion))
                errors.Add("AppiumVersion cannot be empty");

            // Range validations
            if (config.DeviceListenerPollInterval < 1)
                errors.Add("DeviceListenerPollInterval must be >= 1 second");

            if (config.DeviceRegistry.SaveIntervalSeconds < 1)
                errors.Add("DeviceRegistry.SaveIntervalSeconds must be >= 1 second");

            if (config.PluginMonitorIntervalSeconds < 1)
                errors.Add("PluginMonitorIntervalSeconds must be >= 1 second");

            if (config.PluginRestartBackoffSeconds < 0)
                errors.Add("PluginRestartBackoffSeconds must be >= 0 seconds");

            if (config.HealthCheckTimeoutSeconds < 1)
                errors.Add("healthCheckTimeoutSeconds must be >= 1 second");

            // Driver validation
            foreach (var driver in config.Drivers.Where(d => d.Enabled))
            {
                if (string.IsNullOrWhiteSpace(driver.Name))
                    errors.Add($"Driver at index {config.Drivers.IndexOf(driver)} has no name");

                if (string.IsNullOrWhiteSpace(driver.Version))
                    errors.Add($"Driver '{driver.Name}' has no version specified");
            }

            // Plugin validation (require 'id' as canonical identifier)
            foreach (var plugin in config.Plugins.Where(p => p.Enabled))
            {
                if (string.IsNullOrWhiteSpace(plugin.Id))
                    errors.Add($"Plugin at index {config.Plugins.IndexOf(plugin)} has no 'id' field");

                if (string.IsNullOrWhiteSpace(plugin.Version))
                    errors.Add($"Plugin '{plugin.Id ?? "<unknown>"}' has no version specified");

                if (plugin.HealthCheckTimeoutSeconds.HasValue && plugin.HealthCheckTimeoutSeconds.Value < 1)
                    errors.Add($"Plugin '{plugin.Id ?? "<unknown>"}' has invalid healthCheckTimeoutSeconds (must be >= 1)");

                if (plugin.HealthCheckIntervalSeconds.HasValue && plugin.HealthCheckIntervalSeconds.Value < 1)
                    errors.Add($"Plugin '{plugin.Id ?? "<unknown>"}' has invalid healthCheckIntervalSeconds (must be >= 1)");

                // Warn if a health-check command is provided but no timeout is set (defensive recommendation)
                if (!string.IsNullOrWhiteSpace(plugin.HealthCheckCommand) && !plugin.HealthCheckTimeoutSeconds.HasValue)
                {
                    _logger.LogWarning("Plugin '{PluginId}' defines a healthCheckCommand but no healthCheckTimeoutSeconds. A global default ({GlobalTimeout}s) will be used if configured.", plugin.Id, config.HealthCheckTimeoutSeconds);
                }
            }

            if (errors.Any())
            {
                _logger.LogError("Configuration validation failed with {ErrorCount} error(s):", errors.Count);
                foreach (var error in errors)
                {
                    _logger.LogError("  - {Error}", error);
                }
                return false;
            }

            if (warnings.Any())
            {
                _logger.LogWarning("Configuration validation returned {WarningCount} warning(s):", warnings.Count);
                foreach (var w in warnings)
                {
                    _logger.LogWarning("  - {Warning}", w);
                }
            }

            _logger.LogInformation("Configuration validation passed");
            return true;
        }
    }
}
