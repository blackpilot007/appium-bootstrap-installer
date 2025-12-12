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
    /// Device registry data model for serialization
    /// </summary>
    public class DeviceRegistryData
    {
        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonPropertyName("devices")]
        public List<Device> Devices { get; set; } = new();
    }

    /// <summary>
    /// Represents a connected device (Android or iOS)
    /// </summary>
    public class Device
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // Serial (Android) or UDID (iOS)

        [JsonPropertyName("platform")]
        public DevicePlatform Platform { get; set; }

        [JsonPropertyName("type")]
        public DeviceType Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Unknown";

        [JsonPropertyName("state")]
        public DeviceState State { get; set; }

        [JsonPropertyName("connectedAt")]
        public DateTime ConnectedAt { get; set; }

        [JsonPropertyName("disconnectedAt")]
        public DateTime? DisconnectedAt { get; set; }

        [JsonPropertyName("appiumSession")]
        public AppiumSession? AppiumSession { get; set; }

        [JsonPropertyName("lastSeen")]
        public DateTime LastSeen { get; set; }
    }

    /// <summary>
    /// Represents an active Appium session for a device
    /// </summary>
    public class AppiumSession
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("appiumPort")]
        public int AppiumPort { get; set; }

        [JsonPropertyName("wdaLocalPort")]
        public int? WdaLocalPort { get; set; } // iOS only

        [JsonPropertyName("mjpegServerPort")]
        public int? MjpegServerPort { get; set; } // iOS only

        [JsonPropertyName("systemPort")]
        public int? SystemPort { get; set; } // Android only

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("processId")]
        public int? ProcessId { get; set; }

        [JsonPropertyName("status")]
        public SessionStatus Status { get; set; }
    }

    public enum DevicePlatform
    {
        Android,
        iOS
    }

    public enum DeviceType
    {
        Physical,
        Emulator,
        Simulator
    }

    public enum DeviceState
    {
        Connected,
        Disconnected,
        Offline,
        Unauthorized
    }

    public enum SessionStatus
    {
        Starting,
        Running,
        Failed,
        Stopped
    }

    /// <summary>
    /// Port allocation configuration
    /// </summary>
    public class PortRangeConfig
    {
        [JsonPropertyName("appiumStart")]
        public int AppiumStart { get; set; } = 4723;

        [JsonPropertyName("appiumEnd")]
        public int AppiumEnd { get; set; } = 4823;

        [JsonPropertyName("wdaStart")]
        public int WdaStart { get; set; } = 8100;

        [JsonPropertyName("wdaEnd")]
        public int WdaEnd { get; set; } = 8200;

        [JsonPropertyName("mjpegStart")]
        public int MjpegStart { get; set; } = 9100;

        [JsonPropertyName("mjpegEnd")]
        public int MjpegEnd { get; set; } = 9200;

        [JsonPropertyName("systemPortStart")]
        public int SystemPortStart { get; set; } = 8200;

        [JsonPropertyName("systemPortEnd")]
        public int SystemPortEnd { get; set; } = 8300;
    }

    /// <summary>
    /// Device registry storage configuration
    /// </summary>
    public class DeviceRegistryConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } = "device-registry.json";

        [JsonPropertyName("autoSave")]
        public bool AutoSave { get; set; } = true;

        [JsonPropertyName("saveIntervalSeconds")]
        public int SaveIntervalSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Device event notification
    /// </summary>
    public class DeviceEvent
    {
        [JsonPropertyName("eventType")]
        public DeviceEventType EventType { get; set; }

        [JsonPropertyName("device")]
        public Device Device { get; set; } = new();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum DeviceEventType
    {
        Connected,
        Disconnected,
        SessionStarted,
        SessionEnded,
        SessionFailed
    }
}
