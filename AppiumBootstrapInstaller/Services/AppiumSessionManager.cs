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

using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Manages Appium server instances for connected devices using NSSM (Windows) or Supervisord (Linux/macOS)
    /// </summary>
    public class AppiumSessionManager
    {
        private readonly ILogger<AppiumSessionManager> _logger;
        private readonly string _installFolder;
        private readonly PortRangeConfig _portConfig;
        private readonly HashSet<int> _usedPorts = new(); // Track all allocated ports
        private readonly SemaphoreSlim _portLock = new(1, 1);
        private readonly bool _isWindows;
        private readonly string? _nssmPath;
        private readonly string? _supervisorctlPath;
        private readonly DeviceMetrics _metrics;

        public AppiumSessionManager(
            ILogger<AppiumSessionManager> logger,
            string installFolder,
            PortRangeConfig portConfig,
            DeviceMetrics metrics)
        {
            _logger = logger;
            _installFolder = installFolder;
            _portConfig = portConfig;
            _metrics = metrics;
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // Detect process manager
            if (_isWindows)
            {
                _nssmPath = Path.Combine(installFolder, "nssm", "nssm.exe");
                if (!File.Exists(_nssmPath))
                {
                    _logger.LogWarning("NSSM not found at {Path}. Will use direct process execution.", _nssmPath);
                    _nssmPath = null;
                }
                else
                {
                    _logger.LogInformation("Using NSSM for process management: {Path}", _nssmPath);
                }
            }
            else
            {
                _supervisorctlPath = FindExecutable("supervisorctl");
                if (_supervisorctlPath == null)
                {
                    _logger.LogWarning("supervisorctl not found. Will use direct process execution.");
                }
                else
                {
                    _logger.LogInformation("Using Supervisord for process management: {Path}", _supervisorctlPath);
                }
            }
        }

        /// <summary>
        /// Starts an Appium session for a device using NSSM/Supervisord
        /// </summary>
        public async Task<AppiumSession?> StartSessionAsync(Device device)
        {
            try
            {
                _logger.LogInformation("Starting Appium session for device {DeviceId}", device.Id);

                // Allocate 3 consecutive ports dynamically for iOS, or 2 for Android
                int portsNeeded = device.Platform == DevicePlatform.iOS ? 3 : 2;
                var ports = await AllocateConsecutivePortsAsync(portsNeeded);
                
                if (ports == null || ports.Length == 0)
                {
                    _logger.LogError("No available consecutive ports for device {DeviceId}", device.Id);
                    return null;
                }

                var appiumPort = ports[0];
                int? wdaPort = null;
                int? mjpegPort = null;
                int? systemPort = null;

                if (device.Platform == DevicePlatform.iOS)
                {
                    wdaPort = ports[1];
                    mjpegPort = ports[2];
                    _logger.LogInformation(
                        "Allocated iOS ports - Appium: {Appium}, WDA: {Wda}, MJPEG: {Mjpeg}",
                        appiumPort, wdaPort, mjpegPort
                    );
                }
                else if (device.Platform == DevicePlatform.Android)
                {
                    systemPort = ports[1];
                    _logger.LogInformation(
                        "Allocated Android ports - Appium: {Appium}, SystemPort: {System}",
                        appiumPort, systemPort
                    );
                }

                // Create service name (sanitize device ID for service naming)
                var serviceName = $"AppiumBootstrap_{SanitizeServiceName(device.Id)}";

                // Start Appium using process manager
                var success = _isWindows
                    ? await StartWithNssmAsync(serviceName, device, appiumPort, wdaPort, mjpegPort, systemPort)
                    : await StartWithSupervisordAsync(serviceName, device, appiumPort, wdaPort, mjpegPort, systemPort);

                if (!success)
                {
                    await ReleasePortsAsync(ports);
                    return null;
                }

                // Get process ID
                var processId = await GetServiceProcessIdAsync(serviceName);

                var session = new AppiumSession
                {
                    SessionId = serviceName, // Use service name as session ID for easy lookup
                    AppiumPort = appiumPort,
                    WdaLocalPort = wdaPort,
                    MjpegServerPort = mjpegPort,
                    SystemPort = systemPort,
                    StartedAt = DateTime.UtcNow,
                    ProcessId = processId,
                    Status = SessionStatus.Running
                };

                _logger.LogInformation(
                    "Appium session started for {DeviceId} on port {Port} (Service: {ServiceName})",
                    device.Id, appiumPort, serviceName
                );

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Appium session for device {DeviceId}", device.Id);
                return null;
            }
        }

        /// <summary>
        /// Stops an Appium session using NSSM/Supervisord
        /// </summary>
        public async Task StopSessionAsync(AppiumSession session)
        {
            try
            {
                _logger.LogInformation("Stopping Appium session {SessionId}", session.SessionId);

                var serviceName = session.SessionId; // Session ID is the service name

                if (_isWindows)
                {
                    await StopWithNssmAsync(serviceName);
                }
                else
                {
                    await StopWithSupervisordAsync(serviceName);
                }

                // Release ports
                await ReleasePortsAsync(new[] { 
                    session.AppiumPort, 
                    session.WdaLocalPort ?? 0, 
                    session.MjpegServerPort ?? 0,
                    session.SystemPort ?? 0
                }.Where(p => p > 0).ToArray());

                session.Status = SessionStatus.Stopped;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop Appium session {SessionId}", session.SessionId);
            }
        }

        private async Task<bool> StartWithNssmAsync(
            string serviceName,
            Device device,
            int appiumPort,
            int? wdaPort,
            int? mjpegPort,
            int? systemPort)
        {
            if (_nssmPath == null)
            {
                _logger.LogWarning("NSSM not available, cannot start service");
                return false;
            }

            try
            {
                var scriptPath = Path.Combine(_installFolder, "Platform", "Windows", "Scripts", "StartAppiumServer.ps1");
                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("Appium startup script not found: {ScriptPath}", scriptPath);
                    return false;
                }

                var appiumHome = _installFolder;
                var appiumBin = Path.Combine(_installFolder, "bin");

                // Build NSSM service command
                var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                                $"-AppiumHomePath \"{appiumHome}\" " +
                                $"-AppiumBinPath \"{appiumBin}\" " +
                                $"-AppiumPort {appiumPort} " +
                                $"-WdaLocalPort {wdaPort ?? 0} " +
                                $"-MpegLocalPort {mjpegPort ?? 0}";

                // Install service
                _logger.LogDebug("Installing NSSM service: {ServiceName}", serviceName);
                var installResult = await RunCommandAsync(
                    _nssmPath,
                    $"install \"{serviceName}\" \"powershell.exe\" {arguments}"
                );

                if (installResult.ExitCode != 0)
                {
                    _logger.LogError("Failed to install NSSM service: {Error}", installResult.Error);
                    return false;
                }

                // Configure service
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" DisplayName \"Appium - {device.Name}\"");
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" Description \"Appium server for device {device.Id}\"");
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" AppDirectory \"{_installFolder}\"");
                
                var logDir = Path.Combine(_installFolder, "services", "logs");
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" AppStdout \"{logDir}\\{serviceName}_stdout.log\"");
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" AppStderr \"{logDir}\\{serviceName}_stderr.log\"");
                
                // Set restart on failure
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" AppExit Default Restart");
                await RunCommandAsync(_nssmPath, $"set \"{serviceName}\" AppRestartDelay 5000");

                // Start service
                _logger.LogDebug("Starting NSSM service: {ServiceName}", serviceName);
                var startResult = await RunCommandAsync(_nssmPath, $"start \"{serviceName}\"");

                if (startResult.ExitCode != 0)
                {
                    _logger.LogError("Failed to start NSSM service: {Error}", startResult.Error);
                    await RunCommandAsync(_nssmPath, $"remove \"{serviceName}\" confirm");
                    return false;
                }

                // Wait a bit for service to initialize
                await Task.Delay(2000);

                _logger.LogInformation("NSSM service {ServiceName} started successfully", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting NSSM service {ServiceName}", serviceName);
                return false;
            }
        }

        private async Task StopWithNssmAsync(string serviceName)
        {
            if (_nssmPath == null) return;

            try
            {
                _logger.LogDebug("Stopping NSSM service: {ServiceName}", serviceName);
                await RunCommandAsync(_nssmPath, $"stop \"{serviceName}\"");
                
                await Task.Delay(1000);
                
                _logger.LogDebug("Removing NSSM service: {ServiceName}", serviceName);
                await RunCommandAsync(_nssmPath, $"remove \"{serviceName}\" confirm");
                
                _logger.LogInformation("NSSM service {ServiceName} stopped and removed", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping NSSM service {ServiceName}", serviceName);
            }
        }

        private async Task<bool> StartWithSupervisordAsync(
            string serviceName,
            Device device,
            int appiumPort,
            int? wdaPort,
            int? mjpegPort,
            int? systemPort)
        {
            if (_supervisorctlPath == null)
            {
                _logger.LogWarning("supervisorctl not available, cannot start service");
                return false;
            }

            try
            {
                var configDir = Path.Combine(_installFolder, "services", "supervisor", "conf.d");
                Directory.CreateDirectory(configDir);

                var scriptPath = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? Path.Combine(_installFolder, "Platform", "MacOS", "Scripts", "StartAppiumServer.sh")
                    : Path.Combine(_installFolder, "Platform", "Linux", "Scripts", "StartAppiumServer.sh");

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("Appium startup script not found: {ScriptPath}", scriptPath);
                    return false;
                }

                var appiumHome = _installFolder;
                var appiumBin = Path.Combine(_installFolder, "bin");
                var logDir = Path.Combine(_installFolder, "services", "logs");
                Directory.CreateDirectory(logDir);

                // Create Supervisor config for this device
                var configContent = $@"[program:{serviceName}]
command=/bin/bash {scriptPath} {appiumHome} {appiumBin} {appiumPort} {wdaPort ?? 0} {mjpegPort ?? 0}
directory={_installFolder}
autostart=false
autorestart=true
startretries=3
stderr_logfile={logDir}/{serviceName}_stderr.log
stdout_logfile={logDir}/{serviceName}_stdout.log
user={Environment.UserName}
environment=HOME=""{Environment.GetEnvironmentVariable("HOME")}"",USER=""{Environment.UserName}""
";

                var configPath = Path.Combine(configDir, $"{serviceName}.conf");
                await File.WriteAllTextAsync(configPath, configContent);

                // Reload supervisor config
                await RunCommandAsync(_supervisorctlPath, "reread");
                await RunCommandAsync(_supervisorctlPath, "update");

                // Start the program
                _logger.LogDebug("Starting Supervisor program: {ServiceName}", serviceName);
                var result = await RunCommandAsync(_supervisorctlPath, $"start {serviceName}");

                if (result.ExitCode != 0)
                {
                    _logger.LogError("Failed to start Supervisor program: {Error}", result.Error);
                    return false;
                }

                await Task.Delay(2000);

                _logger.LogInformation("Supervisor program {ServiceName} started successfully", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Supervisor program {ServiceName}", serviceName);
                return false;
            }
        }

        private async Task StopWithSupervisordAsync(string serviceName)
        {
            if (_supervisorctlPath == null) return;

            try
            {
                _logger.LogDebug("Stopping Supervisor program: {ServiceName}", serviceName);
                await RunCommandAsync(_supervisorctlPath, $"stop {serviceName}");
                
                await Task.Delay(500);
                
                await RunCommandAsync(_supervisorctlPath, $"remove {serviceName}");
                
                // Remove config file
                var configPath = Path.Combine(_installFolder, "services", "supervisor", "conf.d", $"{serviceName}.conf");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
                
                await RunCommandAsync(_supervisorctlPath, "reread");
                await RunCommandAsync(_supervisorctlPath, "update");
                
                _logger.LogInformation("Supervisor program {ServiceName} stopped and removed", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Supervisor program {ServiceName}", serviceName);
            }
        }

        private async Task<int?> GetServiceProcessIdAsync(string serviceName)
        {
            try
            {
                if (_isWindows && _nssmPath != null)
                {
                    var result = await RunCommandAsync(_nssmPath, $"status \"{serviceName}\"");
                    // Parse PID from NSSM status output if available
                    // For now, return null as NSSM manages the process
                    return null;
                }
                else if (_supervisorctlPath != null)
                {
                    var result = await RunCommandAsync(_supervisorctlPath, $"pid {serviceName}");
                    if (int.TryParse(result.Output.Trim(), out var pid))
                    {
                        return pid;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private string SanitizeServiceName(string deviceId)
        {
            // Remove invalid characters for service names
            return deviceId.Replace(":", "_").Replace("-", "_").Replace(" ", "_");
        }

        private string? FindExecutable(string name)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(string command, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (1, "", "Failed to start process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output, error);
        }

        /// <summary>
        /// Allocates N consecutive ports starting from a 4-digit port
        /// </summary>
        private async Task<int[]?> AllocateConsecutivePortsAsync(int count)
        {
            await _portLock.WaitAsync();
            try
            {
                // Start from 4-digit range (1000-9999)
                const int minPort = 1000;
                const int maxPort = 65535 - 10; // Leave room for consecutive ports

                // Try to find consecutive available ports
                for (int startPort = minPort; startPort <= maxPort; startPort++)
                {
                    // Check if this port and the next (count-1) ports are all available
                    bool allAvailable = true;
                    var portsToCheck = new int[count];
                    
                    for (int i = 0; i < count; i++)
                    {
                        int port = startPort + i;
                        portsToCheck[i] = port;
                        
                        if (_usedPorts.Contains(port) || !IsPortAvailable(port))
                        {
                            allAvailable = false;
                            break;
                        }
                    }

                    if (allAvailable)
                    {
                        // Reserve all ports
                        foreach (var port in portsToCheck)
                        {
                            _usedPorts.Add(port);
                        }

                        _logger.LogInformation(
                            "Allocated {Count} consecutive ports starting at {StartPort}: {Ports}",
                            count, startPort, string.Join(", ", portsToCheck)
                        );

                        return portsToCheck;
                    }
                }
                
                _metrics.RecordPortAllocationFailure();
                _logger.LogError(
                    "No {Count} consecutive available ports found in range {Min}-{Max}",
                    count, minPort, maxPort
                );
                return null;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// Releases allocated ports back to the pool
        /// </summary>
        private async Task ReleasePortsAsync(int[] ports)
        {
            await _portLock.WaitAsync();
            try
            {
                foreach (var port in ports)
                {
                    _usedPorts.Remove(port);
                }
                
                if (ports.Length > 0)
                {
                    _logger.LogDebug(
                        "Released ports: {Ports}",
                        string.Join(", ", ports)
                    );
                }
            }
            finally
            {
                _portLock.Release();
            }
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
