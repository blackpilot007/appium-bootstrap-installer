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

using System.Runtime.InteropServices;
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
                _logger.LogDebug("Configuration file size: {Size} bytes", jsonContent.Length);
                
                var config = JsonSerializer.Deserialize(jsonContent, AppJsonSerializerContext.Default.InstallConfig);

                if (config == null)
                {
                    _logger.LogError("Configuration deserialization returned null");
                    throw new InvalidOperationException("Failed to deserialize configuration file - result was null");
                }

                // Expand environment variables in paths
                config.InstallFolder = ExpandEnvironmentVariables(config.InstallFolder);
                _logger.LogDebug("Install folder expanded to: {InstallFolder}", config.InstallFolder);

                // Merge optional plugin definitions from plugins.d directory (preserve order)
                try
                {
                    MergePluginsFromPluginsD(configPath, config);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to merge plugins from plugins.d directory");
                }

                // Normalize plugin definitions: ensure Id and Name are present (derive one from the other when missing)
                try
                {
                    NormalizePluginDefinitions(config);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to normalize plugin definitions");
                }

                // Validate configuration (after merging plugins)
                var validationErrors = config.Validate();
                if (validationErrors.Any())
                {
                    _logger.LogError("Configuration validation failed with {Count} error(s)", validationErrors.Count);
                    foreach (var error in validationErrors)
                    {
                        _logger.LogError("  - {Error}", error);
                    }
                    throw new InvalidOperationException(
                        $"Configuration validation failed:\n  - {string.Join("\n  - ", validationErrors)}"
                    );
                }

                _logger.LogInformation("Configuration loaded and validated successfully");
                return config;
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError(ex, "Configuration file not found: {Path}", configPath);
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied reading configuration file: {Path}", configPath);
                throw new InvalidOperationException($"Access denied reading configuration file: {configPath}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse configuration JSON. Check file format at: {Path}", configPath);
                throw new InvalidOperationException(
                    $"Failed to parse configuration file. Invalid JSON format at line {ex.LineNumber}, position {ex.BytePositionInLine}: {ex.Message}",
                    ex
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading configuration from: {Path}", configPath);
                throw;
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
                
                // Check relative to executable directory if it's a relative path
                if (!Path.IsPathRooted(specifiedPath))
                {
                    string exeDir = AppContext.BaseDirectory;
                    string exeDirConfig = Path.Combine(exeDir, specifiedPath);
                    if (File.Exists(exeDirConfig))
                    {
                        return exeDirConfig;
                    }
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
                return value ?? match.Value;
            });

            // Normalize path separators for current platform
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = result.Replace('/', '\\');
            }

            return result;
        }

        /// <summary>
        /// Merge plugin definitions from a `plugins.d` directory adjacent to the config file
        /// If an environment variable `PLUGINS_DIR` is set, files from that directory are also loaded (appended after local plugins.d)
        /// Files are processed in sorted filename order and each file's `plugins` array is appended in-file order.
        /// </summary>
        private void MergePluginsFromPluginsD(string configPath, InstallConfig config)
        {
            if (string.IsNullOrWhiteSpace(configPath)) return;

            var configDir = Path.GetDirectoryName(configPath) ?? Directory.GetCurrentDirectory();
            var localPluginsDir = Path.Combine(configDir, "plugins.d");

            var pluginDirs = new List<string>();
            if (Directory.Exists(localPluginsDir)) pluginDirs.Add(localPluginsDir);

            var envDir = Environment.GetEnvironmentVariable("PLUGINS_DIR");
            if (!string.IsNullOrWhiteSpace(envDir) && Directory.Exists(envDir)) pluginDirs.Add(envDir);

            foreach (var dir in pluginDirs)
            {
                _logger.LogInformation("Loading plugin descriptors from: {Dir}", dir);
                var files = Directory.GetFiles(dir, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    try
                    {
                        _logger.LogDebug("Reading plugin file: {File}", file);
                        var content = File.ReadAllText(file);
                        var partial = JsonSerializer.Deserialize(content, AppJsonSerializerContext.Default.InstallConfig);
                        if (partial?.Plugins != null && partial.Plugins.Any())
                        {
                            foreach (var p in partial.Plugins)
                            {
                                // preserve order by simply appending
                                config.Plugins.Add(p);
                            }
                            _logger.LogInformation("Appended {Count} plugin(s) from {File}", partial.Plugins.Count, file);
                        }
                    }
                    catch (JsonException jex)
                    {
                        _logger.LogWarning(jex, "Failed to parse plugins file: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load plugins file: {File}", file);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a sample configuration file at the specified location
        /// </summary>
        public void CreateSampleConfig(string outputPath)
        {
            var sampleConfig = new InstallConfig
            {
                InstallFolder = "${HOME}/AppiumBootstrap",
                NodeVersion = "22",
                AppiumVersion = "2.17.1",
                NvmVersion = "0.40.2",
                CleanInstallFolder = false,
                Drivers = new List<DriverConfig>
                {
                    new DriverConfig { Name = "xcuitest", Version = "7.24.3", Enabled = true },
                    new DriverConfig { Name = "uiautomator2", Version = "3.8.3", Enabled = true }
                },
                // Example plugins show minimal form: specify `id` only (name will default to id)
                Plugins = new List<PluginConfig>
                {
                    new PluginConfig { Id = "device-farm", Version = "8.3.5", Enabled = true },
                    new PluginConfig { Id = "appium-dashboard", Version = "2.0.3", Enabled = true }
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

        /// <summary>
        /// Ensures each plugin has both an Id and a Name for internal use.
        /// Backwards compatibility: if only Name is provided, it will be used as Id; if only Id is provided, Name will be set to Id.
        /// If neither is set, a deterministic generated id will be assigned.
        /// </summary>
        private void NormalizePluginDefinitions(InstallConfig config)
        {
            if (config?.Plugins == null) return;

            for (int i = 0; i < config.Plugins.Count; i++)
            {
                var p = config.Plugins[i];
                // Trim whitespace from Id if present
                if (!string.IsNullOrWhiteSpace(p?.Id))
                {
                    p.Id = p.Id.Trim();
                }

                // If Id is missing, log a warning (validation will catch and fail)
                if (string.IsNullOrWhiteSpace(p?.Id))
                {
                    _logger.LogWarning("Plugin at index {Index} has no 'id' field; this configuration is invalid and will fail validation", i);
                }
            }
        }
    }
}
