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

using System.Text.Json;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Reads and validates configuration from JSON files
    /// Supports multiple locations with priority: CLI arg > current dir > home dir
    /// </summary>
    public class ConfigurationReader
    {
        private readonly ILogger<ConfigurationReader> _logger;

        public ConfigurationReader(ILogger<ConfigurationReader> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads configuration from file with priority:
        /// 1. Specified path (if provided)
        /// 2. config.json in current directory
        /// 3. config.json in user's home directory
        /// </summary>
        public InstallConfig LoadConfiguration(string? specifiedPath = null)
        {
            string? configPath = FindConfigurationFile(specifiedPath);

            if (configPath == null)
            {
                throw new FileNotFoundException(
                    "Configuration file not found. Searched locations:\n" +
                    $"  1. Specified path: {specifiedPath ?? "(none)"}\n" +
                    $"  2. Current directory: {Path.Combine(Directory.GetCurrentDirectory(), "config.json")}\n" +
                    $"  3. Home directory: {Path.Combine(GetHomeDirectory(), ".appium-bootstrap", "config.json")}"
                );
            }

            _logger.LogInformation("Loading configuration from: {ConfigPath}", configPath);

            try
            {
                string jsonContent = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize(jsonContent, AppJsonSerializerContext.Default.InstallConfig);

                if (config == null)
                {
                    throw new InvalidOperationException("Failed to deserialize configuration file");
                }

                // Expand environment variables in paths
                config.InstallFolder = ExpandEnvironmentVariables(config.InstallFolder);

                // Validate configuration
                var validationErrors = config.Validate();
                if (validationErrors.Any())
                {
                    foreach (var error in validationErrors)
                    {
                        _logger.LogError("Validation error: {Error}", error);
                    }
                    throw new InvalidOperationException(
                        $"Configuration validation failed:\n  - {string.Join("\n  - ", validationErrors)}"
                    );
                }

                return config;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse configuration file");
                throw new InvalidOperationException($"Failed to parse configuration file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Finds configuration file with priority order
        /// </summary>
        private string? FindConfigurationFile(string? specifiedPath)
        {
            // Priority 1: Specified path
            if (!string.IsNullOrWhiteSpace(specifiedPath))
            {
                if (File.Exists(specifiedPath))
                {
                    return Path.GetFullPath(specifiedPath);
                }
                // If specified but doesn't exist, don't fall back to other locations
                throw new FileNotFoundException($"Specified configuration file not found: {specifiedPath}");
            }

            // Priority 2: Current directory
            string currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            if (File.Exists(currentDirConfig))
            {
                return currentDirConfig;
            }

            // Priority 3: Home directory
            string homeDir = GetHomeDirectory();
            string homeDirConfig = Path.Combine(homeDir, ".appium-bootstrap", "config.json");
            if (File.Exists(homeDirConfig))
            {
                return homeDirConfig;
            }

            return null;
        }

        /// <summary>
        /// Gets the user's home directory in a cross-platform way
        /// </summary>
        private string GetHomeDirectory()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        /// <summary>
        /// Expands environment variables in a string
        /// Supports both ${VAR} and %VAR% syntax
        /// </summary>
        private string ExpandEnvironmentVariables(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            // Expand Windows-style %VAR%
            string result = Environment.ExpandEnvironmentVariables(input);

            // Expand Unix-style ${VAR}
            var regex = new System.Text.RegularExpressions.Regex(@"\$\{([^}]+)\}");
            result = regex.Replace(result, match =>
            {
                string varName = match.Groups[1].Value;
                string? value = Environment.GetEnvironmentVariable(varName);

                // Special handling for HOME on Windows if not set
                if (value == null && varName == "HOME" && OperatingSystem.IsWindows())
                {
                    value = Environment.GetEnvironmentVariable("USERPROFILE");
                }

                return value ?? match.Value;
            });

            return result;
        }

        /// <summary>
        /// Creates a sample configuration file at the specified location
        /// </summary>
        public void CreateSampleConfig(string outputPath)
        {
            var sampleConfig = new InstallConfig
            {
                InstallFolder = "${USERPROFILE}/AppiumBootstrap",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "xcuitest", Version = "7.24.3", Enabled = true },
                    new DriverConfig { Name = "uiautomator2", Version = "3.8.3", Enabled = true }
                },
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Name = "device-farm", Version = "8.3.5", Enabled = true },
                    new PluginConfig { Name = "appium-dashboard", Version = "2.0.3", Enabled = true }
                },
                PlatformSpecific = new PlatformSpecificConfig
                {
                    MacOS = new MacOSConfig
                    {
                        LibimobiledeviceVersion = "latest"
                    },
                    Windows = new WindowsConfig
                    {
                        InstallIOSSupport = false,
                        InstallAndroidSupport = true,
                        NvmVersion = "1.1.12"
                    },
                    Linux = new LinuxConfig
                    {
                        InstallIOSSupport = false,
                        InstallAndroidSupport = true
                    }
                }
            };

            string json = JsonSerializer.Serialize(sampleConfig, AppJsonSerializerContext.Default.InstallConfig);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(outputPath, json);
            _logger.LogInformation("Sample configuration created at: {OutputPath}", outputPath);
        }
    }
}
