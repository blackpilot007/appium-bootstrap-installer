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

namespace AppiumBootstrapInstaller.Services.Interfaces
{
    /// <summary>
    /// Interface for tracking device and session metrics
    /// </summary>
    public interface IDeviceMetrics
    {
        int DevicesConnectedTotal { get; }
        int DevicesDisconnectedTotal { get; }
        int SessionsStartedTotal { get; }
        int SessionsStoppedTotal { get; }
        int SessionsFailedTotal { get; }
        int PortAllocationFailuresTotal { get; }

        void RecordDeviceConnected(DevicePlatform platform, DeviceType type);
        void RecordDeviceDisconnected(DevicePlatform platform);
        void RecordSessionStarted(DevicePlatform platform);
        void RecordSessionStopped(DevicePlatform platform);
        void RecordSessionFailed(DevicePlatform platform, string reason);
        void RecordPortAllocationFailure();
        
            // Plugin metrics
            void RecordPluginUnhealthy(string pluginId);
            void RecordPluginRestart(string pluginId);
        string GetSummary();
    }
}
