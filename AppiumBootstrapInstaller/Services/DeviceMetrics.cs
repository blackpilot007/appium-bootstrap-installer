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

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Collects and tracks metrics for device listener operations
    /// </summary>
    public class DeviceMetrics : IDeviceMetrics
    {
        private readonly object _lock = new();
        private int _devicesConnectedTotal = 0;
        private int _devicesDisconnectedTotal = 0;
        private int _sessionsStartedTotal = 0;
        private int _sessionsStoppedTotal = 0;
        private int _sessionsFailedTotal = 0;
        private int _portAllocationFailuresTotal = 0;
        private readonly Dictionary<string, int> _sessionFailureReasons = new();

        // Current state
        public int AndroidDevicesConnected { get; private set; }
        public int IOSDevicesConnected { get; private set; }
        public int ActiveSessions { get; private set; }

        // Totals
        public int DevicesConnectedTotal => _devicesConnectedTotal;
        public int DevicesDisconnectedTotal => _devicesDisconnectedTotal;
        public int SessionsStartedTotal => _sessionsStartedTotal;
        public int SessionsStoppedTotal => _sessionsStoppedTotal;
        public int SessionsFailedTotal => _sessionsFailedTotal;
        public int PortAllocationFailuresTotal => _portAllocationFailuresTotal;

        // Calculated
        public double SessionStartSuccessRate =>
            SessionsStartedTotal + SessionsFailedTotal > 0
                ? (double)SessionsStartedTotal / (SessionsStartedTotal + SessionsFailedTotal) * 100
                : 100.0;

        public void RecordDeviceConnected(DevicePlatform platform, DeviceType type)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _devicesConnectedTotal);
                if (platform == DevicePlatform.Android)
                    AndroidDevicesConnected++;
                else if (platform == DevicePlatform.iOS)
                    IOSDevicesConnected++;
            }
        }

        public void RecordDeviceDisconnected(DevicePlatform platform)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _devicesDisconnectedTotal);
                if (platform == DevicePlatform.Android)
                    AndroidDevicesConnected = Math.Max(0, AndroidDevicesConnected - 1);
                else if (platform == DevicePlatform.iOS)
                    IOSDevicesConnected = Math.Max(0, IOSDevicesConnected - 1);
            }
        }

        public void RecordSessionStarted(DevicePlatform platform)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _sessionsStartedTotal);
                ActiveSessions++;
            }
        }

        public void RecordSessionFailed(DevicePlatform platform, string reason)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _sessionsFailedTotal);
                if (!_sessionFailureReasons.ContainsKey(reason))
                    _sessionFailureReasons[reason] = 0;
                _sessionFailureReasons[reason]++;
            }
        }

        public void RecordSessionStopped(DevicePlatform platform)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _sessionsStoppedTotal);
                ActiveSessions = Math.Max(0, ActiveSessions - 1);
            }
        }

        public void RecordPortAllocationFailure()
        {
            Interlocked.Increment(ref _portAllocationFailuresTotal);
        }

        public Dictionary<string, int> GetSessionFailureReasons()
        {
            lock (_lock)
            {
                return new Dictionary<string, int>(_sessionFailureReasons);
            }
        }

        public string GetSummary()
        {
            lock (_lock)
            {
                return $"Devices: {AndroidDevicesConnected} Android, {IOSDevicesConnected} iOS | " +
                       $"Sessions: {ActiveSessions} active, {SessionsStartedTotal} started, {SessionsFailedTotal} failed ({SessionStartSuccessRate:F1}% success) | " +
                       $"Port Failures: {PortAllocationFailuresTotal}";
            }
        }

        // Plugin metrics
        private int _pluginUnhealthyTotal = 0;
        private int _pluginRestartsTotal = 0;

        public void RecordPluginUnhealthy(string pluginId)
        {
            Interlocked.Increment(ref _pluginUnhealthyTotal);
        }

        public void RecordPluginRestart(string pluginId)
        {
            Interlocked.Increment(ref _pluginRestartsTotal);
        }
    }
}
