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

namespace AppiumBootstrapInstaller.Models
{
    /// <summary>
    /// Event fired when a device connects
    /// </summary>
    public record DeviceConnectedEvent(Device Device);

    /// <summary>
    /// Event fired when a device disconnects
    /// </summary>
    public record DeviceDisconnectedEvent(Device Device);

    /// <summary>
    /// Event fired when an Appium session starts successfully
    /// </summary>
    public record SessionStartedEvent(Device Device, AppiumSession Session);

    /// <summary>
    /// Event fired when an Appium session stops
    /// </summary>
    public record SessionStoppedEvent(Device Device, AppiumSession Session);

    /// <summary>
    /// Event fired when an Appium session fails to start
    /// </summary>
    public record SessionFailedEvent(Device Device, string Reason);

    /// <summary>
    /// Health status for the overall service
    /// </summary>
    public class ServiceHealthStatus
    {
        public bool IsHealthy { get; set; }
        public int ConnectedDevices { get; set; }
        public int ActiveSessions { get; set; }
        public int RunningPlugins { get; set; }
        public Dictionary<string, string> ComponentStatus { get; set; } = new();
        public TimeSpan Uptime { get; set; }
    }
}
