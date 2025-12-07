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

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Collects and tracks metrics for device listener operations
    /// </summary>
    public class DeviceMetrics
    {
        private readonly object _lock = new();
        private int _devicesConnectedTotal = 0;
        private int _devicesDisconnectedTotal = 0;
        private int _sessionsStartedTotal = 0;
        private int _sessionsFailedTotal = 0;
        private int _portAllocationFailuresTotal = 0;
        private readonly Dictionary<string, int> _sessionFailureReasons = new();
        private readonly List<TimeSpan> _sessionStartDurations = new();

        // Current state
        public int AndroidDevicesConnected { get; private set; }
        public int IOSDevicesConnected { get; private set; }
        public int ActiveSessions { get; private set; }

        // Totals
        public int DevicesConnectedTotal => _devicesConnectedTotal;
        public int DevicesDisconnectedTotal => _devicesDisconnectedTotal;
        public int SessionsStartedTotal => _sessionsStartedTotal;
        public int SessionsFailedTotal => _sessionsFailedTotal;
        public int PortAllocationFailuresTotal => _portAllocationFailuresTotal;

        // Calculated
        public double SessionStartSuccessRate =>
            SessionsStartedTotal + SessionsFailedTotal > 0
                ? (double)SessionsStartedTotal / (SessionsStartedTotal + SessionsFailedTotal) * 100
                : 100.0;

        public TimeSpan AverageSessionStartDuration
        {
            get
            {
                lock (_lock)
                {
                    return _sessionStartDurations.Any()
                        ? TimeSpan.FromMilliseconds(_sessionStartDurations.Average(ts => ts.TotalMilliseconds))
                        : TimeSpan.Zero;
                }
            }
        }

        public void RecordDeviceConnected(string platform)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _devicesConnectedTotal);
                if (platform == "Android")
                    AndroidDevicesConnected++;
                else if (platform == "iOS")
                    IOSDevicesConnected++;
            }
        }

        public void RecordDeviceDisconnected(string platform)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _devicesDisconnectedTotal);
                if (platform == "Android")
                    AndroidDevicesConnected = Math.Max(0, AndroidDevicesConnected - 1);
                else if (platform == "iOS")
                    IOSDevicesConnected = Math.Max(0, IOSDevicesConnected - 1);
            }
        }

        public void RecordSessionStarted(TimeSpan duration)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _sessionsStartedTotal);
                ActiveSessions++;
                _sessionStartDurations.Add(duration);
                
                // Keep only last 100 durations for moving average
                if (_sessionStartDurations.Count > 100)
                    _sessionStartDurations.RemoveAt(0);
            }
        }

        public void RecordSessionFailed(string reason)
        {
            lock (_lock)
            {
                Interlocked.Increment(ref _sessionsFailedTotal);
                if (!_sessionFailureReasons.ContainsKey(reason))
                    _sessionFailureReasons[reason] = 0;
                _sessionFailureReasons[reason]++;
            }
        }

        public void RecordSessionStopped()
        {
            lock (_lock)
            {
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
                       $"Port Failures: {PortAllocationFailuresTotal} | " +
                       $"Avg Start Time: {AverageSessionStartDuration.TotalSeconds:F2}s";
            }
        }
    }
}
