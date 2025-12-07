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

using System.Text.Json.Serialization;

namespace AppiumBootstrapInstaller.Models
{
    /// <summary>
    /// Root configuration model for Appium installation
    /// </summary>
    public class InstallConfig
    {
        [JsonPropertyName("installFolder")]
        public string InstallFolder { get; set; } = string.Empty;

        [JsonPropertyName("nodeVersion")]
        public string NodeVersion { get; set; } = string.Empty;

        [JsonPropertyName("appiumVersion")]
        public string AppiumVersion { get; set; } = string.Empty;

        [JsonPropertyName("nvmVersion")]
        public string NvmVersion { get; set; } = string.Empty;

        [JsonPropertyName("drivers")]
        public List<DriverConfig> Drivers { get; set; } = new();

        [JsonPropertyName("plugins")]
        public List<PluginConfig> Plugins { get; set; } = new();

        [JsonPropertyName("enableDeviceListener")]
        public bool EnableDeviceListener { get; set; } = false;

        [JsonPropertyName("deviceListenerPollInterval")]
        public int DeviceListenerPollInterval { get; set; } = 5;

        [JsonPropertyName("autoStartAppium")]
        public bool AutoStartAppium { get; set; } = true;

        [JsonPropertyName("portRanges")]
        public PortRangeConfig PortRanges { get; set; } = new();

        [JsonPropertyName("webhooks")]
        public WebhookConfig Webhooks { get; set; } = new();

        [JsonPropertyName("deviceRegistry")]
        public DeviceRegistryConfig DeviceRegistry { get; set; } = new();

        [JsonPropertyName("platformSpecific")]
        public PlatformSpecificConfig? PlatformSpecific { get; set; }

        /// <summary>
        /// Validates the configuration and returns any validation errors
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(InstallFolder))
            {
                errors.Add("InstallFolder is required");
            }

            if (string.IsNullOrWhiteSpace(NodeVersion))
            {
                errors.Add("NodeVersion is required");
            }

            if (string.IsNullOrWhiteSpace(AppiumVersion))
            {
                errors.Add("AppiumVersion is required");
            }

            if (string.IsNullOrWhiteSpace(NvmVersion))
            {
                errors.Add("NvmVersion is required");
            }

            return errors;
        }
    }

    /// <summary>
    /// Configuration for an Appium driver
    /// </summary>
    public class DriverConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Configuration for an Appium plugin
    /// </summary>
    public class PluginConfig
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Platform-specific configuration overrides
    /// </summary>
    public class PlatformSpecificConfig
    {
        [JsonPropertyName("macOS")]
        public MacOSConfig? MacOS { get; set; }

        [JsonPropertyName("windows")]
        public WindowsConfig? Windows { get; set; }

        [JsonPropertyName("linux")]
        public LinuxConfig? Linux { get; set; }
    }

    /// <summary>
    /// macOS-specific configuration
    /// </summary>
    public class MacOSConfig
    {
        [JsonPropertyName("libimobiledeviceVersion")]
        public string LibimobiledeviceVersion { get; set; } = string.Empty;

        [JsonPropertyName("nvmVersion")]
        public string? NvmVersion { get; set; }
    }

    /// <summary>
    /// Windows-specific configuration
    /// </summary>
    public class WindowsConfig
    {
        [JsonPropertyName("installIOSSupport")]
        public bool InstallIOSSupport { get; set; } = false;

        [JsonPropertyName("installAndroidSupport")]
        public bool InstallAndroidSupport { get; set; } = true;

        [JsonPropertyName("nvmVersion")]
        public string? NvmVersion { get; set; }
    }

    /// <summary>
    /// Linux-specific configuration
    /// </summary>
    public class LinuxConfig
    {
        [JsonPropertyName("installIOSSupport")]
        public bool InstallIOSSupport { get; set; } = false;

        [JsonPropertyName("installAndroidSupport")]
        public bool InstallAndroidSupport { get; set; } = true;

        [JsonPropertyName("nvmVersion")]
        public string? NvmVersion { get; set; }
    }

    [JsonSerializable(typeof(InstallConfig))]
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }
}
