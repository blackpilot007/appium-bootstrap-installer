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

        public bool Validate(InstallConfig config, out List<string> errors)
        {
            errors = new List<string>();

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

            // Driver validation
            foreach (var driver in config.Drivers.Where(d => d.Enabled))
            {
                if (string.IsNullOrWhiteSpace(driver.Name))
                    errors.Add($"Driver at index {config.Drivers.IndexOf(driver)} has no name");

                if (string.IsNullOrWhiteSpace(driver.Version))
                    errors.Add($"Driver '{driver.Name}' has no version specified");
            }

            // Plugin validation
            foreach (var plugin in config.Plugins.Where(p => p.Enabled))
            {
                if (string.IsNullOrWhiteSpace(plugin.Name))
                    errors.Add($"Plugin at index {config.Plugins.IndexOf(plugin)} has no name");

                if (string.IsNullOrWhiteSpace(plugin.Version))
                    errors.Add($"Plugin '{plugin.Name}' has no version specified");
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

            _logger.LogInformation("Configuration validation passed");
            return true;
        }
    }
}
