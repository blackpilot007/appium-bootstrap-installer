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
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Manages Appium server instances for connected devices in portable process mode (child processes).
    /// </summary>
    public class AppiumSessionManager : IAppiumSessionManager
    {
        private readonly ILogger<AppiumSessionManager> _logger;
        private readonly string _installFolder;
        private readonly PortRangeConfig _portConfig;
        private readonly IPortManager _portManager;
        private readonly bool _isWindows;
        private readonly IDeviceMetrics _metrics;
        private readonly string? _prebuiltWdaPath;
        
        // Track running processes by SessionId
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Process> _runningProcesses = new();

        public AppiumSessionManager(
            ILogger<AppiumSessionManager> logger,
            string installFolder,
            PortRangeConfig portConfig,
            IDeviceMetrics metrics,
            IPortManager portManager,
            string? prebuiltWdaPath = null)
        {
            _logger = logger;
            _installFolder = installFolder;
            _portConfig = portConfig;
            _metrics = metrics;
            _portManager = portManager;
            _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            _prebuiltWdaPath = prebuiltWdaPath;
            
            _logger.LogInformation("AppiumSessionManager initialized in Process Mode (Non-Admin) with PortManager");
        }

        /// <summary>
        /// Starts an Appium session for a device using NSSM/Supervisord
        /// </summary>
        public async Task<AppiumSession?> StartSessionAsync(Device device)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 1000;
            int[]? allocatedPorts = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "Retry attempt {Attempt}/{MaxRetries} for device {DeviceId}",
                            attempt, maxRetries, device.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Starting Appium session for device {DeviceId}", device.Id);
                    }

                    // Allocate 3 consecutive ports dynamically for iOS, or 2 for Android
                    int portsNeeded = device.Platform == DevicePlatform.iOS ? 3 : 2;
                    allocatedPorts = await AllocateConsecutivePortsAsync(portsNeeded);
                    
                    if (allocatedPorts == null || allocatedPorts.Length == 0)
                    {
                        _logger.LogError("No available consecutive ports for device {DeviceId}", device.Id);
                        _metrics.RecordSessionFailed(device.Platform, "NoPortsAvailable");
                        
                        // Port exhaustion is not transient, don't retry
                        return null;
                    }

                var appiumPort = allocatedPorts[0];
                int? wdaPort = null;
                int? mjpegPort = null;
                int? systemPort = null;

                if (device.Platform == DevicePlatform.iOS)
                {
                    wdaPort = allocatedPorts[1];
                    mjpegPort = allocatedPorts[2];
                    _logger.LogInformation(
                        "Allocated iOS ports - Appium: {Appium}, WDA: {Wda}, MJPEG: {Mjpeg}",
                        appiumPort, wdaPort, mjpegPort
                    );
                }
                else if (device.Platform == DevicePlatform.Android)
                {
                    systemPort = allocatedPorts[1];
                    _logger.LogInformation(
                        "Allocated Android ports - Appium: {Appium}, SystemPort: {System}",
                        appiumPort, systemPort
                    );
                }

                // Create service name (sanitize device ID for service naming)
                var serviceName = $"AppiumBootstrap_{SanitizeServiceName(device.Id)}";

                // Start Appium using process manager
                var success = await StartLocalProcessAsync(serviceName, device, appiumPort, wdaPort, mjpegPort, systemPort);

                if (!success)
                {
                    await ReleasePortsAsync(allocatedPorts);
                    allocatedPorts = null;
                    
                    // Service start failure might be transient
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning(
                            "Failed to start service for {DeviceId}, will retry after delay",
                            device.Id);
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1)); // Exponential backoff
                        continue;
                    }
                    
                    return null;
                }

                // Get process ID
                var processId = await GetServiceProcessIdAsync(serviceName);

                // Get log directory for display
                var executableDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = Path.Combine(executableDir, "logs");

                var session = new AppiumSession
                {
                    SessionId = serviceName, // Use service name as session ID for easy lookup
                    DeviceId = device.Id,
                    AppiumPort = appiumPort,
                    WdaLocalPort = wdaPort,
                    MjpegServerPort = mjpegPort,
                    SystemPort = systemPort,
                    StartedAt = DateTime.UtcNow,
                    ProcessId = processId,
                    Status = SessionStatus.Running
                };

                // Log comprehensive device and session information
                _logger.LogInformation(
                    "═══════════════════════════════════════════════════════════════"
                );
                _logger.LogInformation(
                    "Appium Session Started for Device: {DeviceName}",
                    device.Name
                );
                _logger.LogInformation(
                    "  UDID: {DeviceId}",
                    device.Id
                );
                _logger.LogInformation(
                    "  Platform: {Platform} | Type: {Type}",
                    device.Platform, device.Type
                );
                _logger.LogInformation(
                    "  Appium Port: {AppiumPort}",
                    appiumPort
                );
                if (device.Platform == DevicePlatform.iOS)
                {
                    _logger.LogInformation(
                        "  WDA Port: {WdaPort} | MJPEG Port: {MjpegPort}",
                        wdaPort, mjpegPort
                    );
                }
                else if (device.Platform == DevicePlatform.Android)
                {
                    _logger.LogInformation(
                        "  System Port: {SystemPort}",
                        systemPort
                    );
                }
                _logger.LogInformation(
                    "  Service Name: {ServiceName}",
                    serviceName
                );
                _logger.LogInformation(
                    "  Service Logs: {LogDir}\\{ServiceName}_stdout.log | {LogDir}\\{ServiceName}_stderr.log",
                    logDir, serviceName, logDir, serviceName
                );
                _logger.LogInformation(
                    "═══════════════════════════════════════════════════════════════"
                );

                return session;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Session start cancelled for device {DeviceId}", device.Id);
                    if (allocatedPorts != null)
                    {
                        await ReleasePortsAsync(allocatedPorts);
                    }
                    return null;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex, 
                        "Timeout starting session for device {DeviceId}, attempt {Attempt}/{MaxRetries}",
                        device.Id, attempt, maxRetries);
                    
                    if (allocatedPorts != null)
                    {
                        await ReleasePortsAsync(allocatedPorts);
                        allocatedPorts = null;
                    }
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                        continue;
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, 
                        "I/O error starting session for device {DeviceId}, attempt {Attempt}/{MaxRetries}",
                        device.Id, attempt, maxRetries);
                    
                    if (allocatedPorts != null)
                    {
                        await ReleasePortsAsync(allocatedPorts);
                        allocatedPorts = null;
                    }
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                        continue;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Permission errors are not transient
                    _logger.LogError(ex, 
                        "Permission denied starting session for device {DeviceId}",
                        device.Id);
                    
                    if (allocatedPorts != null)
                    {
                        await ReleasePortsAsync(allocatedPorts);
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to start Appium session for device {DeviceId}, attempt {Attempt}/{MaxRetries}",
                        device.Id, attempt, maxRetries);
                    
                    if (allocatedPorts != null)
                    {
                        await ReleasePortsAsync(allocatedPorts);
                        allocatedPorts = null;
                    }
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelayMs * (int)Math.Pow(2, attempt - 1));
                        continue;
                    }
                }
            }
            
            // All retries exhausted
            _logger.LogError(
                "Failed to start Appium session for device {DeviceId} after {MaxRetries} attempts",
                device.Id, maxRetries);
            return null;
        }

        /// <summary>
        /// Stops an Appium session using NSSM/Supervisord
        /// </summary>
        /// <summary>
        /// Stops an Appium session for a device
        /// </summary>
        public async Task<bool> StopSessionAsync(Device device)
        {
            if (device.AppiumSession == null)
            {
                _logger.LogInformation("No active session for device {DeviceId} - nothing to stop", device.Id);
                return true;
            }

            var session = device.AppiumSession;
            const int maxRetries = 3;
            const int timeoutMs = 10000; // 10 seconds
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 1)
                    {
                        _logger.LogInformation(
                            "Retry attempt {Attempt}/{MaxRetries} to stop session {SessionId}",
                            attempt, maxRetries, session.SessionId);
                    }
                    else
                    {
                        _logger.LogInformation("Stopping Appium session {SessionId}", session.SessionId);
                    }

                    var serviceName = session.SessionId; // Session ID is the service name

                    using var cts = new CancellationTokenSource(timeoutMs);
                    
                    if (_isWindows)
                    {
                        await StopLocalProcessAsync(serviceName);
                    }
                    else
                    {
                        await StopLocalProcessAsync(serviceName);
                    }

                    // Release ports
                    await ReleasePortsAsync(new[] { 
                        session.AppiumPort, 
                        session.WdaLocalPort ?? 0, 
                        session.MjpegServerPort ?? 0,
                        session.SystemPort ?? 0
                    }.Where(p => p > 0).ToArray());

                    session.Status = SessionStatus.Stopped;
                    _logger.LogInformation("Successfully stopped session {SessionId}", session.SessionId);
                    _metrics.RecordSessionStopped(device.Platform);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Stop operation cancelled for session {SessionId}",
                        session.SessionId);
                    return false;
                }
                catch (TimeoutException ex)
                {
                    _logger.LogWarning(ex,
                        "Timeout stopping session {SessionId}, attempt {Attempt}/{MaxRetries}",
                        session.SessionId, attempt, maxRetries);
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(2000 * attempt);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to stop Appium session {SessionId}, attempt {Attempt}/{MaxRetries}",
                        session.SessionId, attempt, maxRetries);
                    
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(2000 * attempt);
                        continue;
                    }
                }
            }
            
            // Mark as stopped even if cleanup failed to prevent resource leaks
            session.Status = SessionStatus.Stopped;
            _logger.LogWarning(
                "Session {SessionId} marked as stopped after failed cleanup attempts",
                session.SessionId);
            return false;
        }

        private async Task<bool> StartLocalProcessAsync(
            string serviceName,
            Device device,
            int appiumPort,
            int? wdaPort,
            int? mjpegPort,
            int? systemPort)
        {
            try
            {
                string scriptPath;
                string arguments;
                string executable;

                // Use explicit fully qualified paths, not environment variables
                var appiumHome = Path.Combine(_installFolder, "appium-home");
                var nodejsPath = Path.Combine(_installFolder, "nodejs");
                var appiumBin = appiumHome; // Appium binaries are in appium-home
                
                // Logs directory next to the running binary
                var executableDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = Path.Combine(executableDir, "logs");
                Directory.CreateDirectory(logDir);

                    if (_isWindows)
                {
                    scriptPath = Path.Combine(_installFolder, "Platform", "Windows", "Scripts", "StartAppiumServer.ps1");
                    executable = "powershell.exe";
                    arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                                $"-AppiumHomePath \"{appiumHome}\" " +
                                $"-AppiumBinPath \"{appiumBin}\" " +
                                $"-NodePath \"{nodejsPath}\" " +
                                $"-InstallFolder \"{_installFolder}\" " +
                                $"-AppiumPort {appiumPort} " +
                                $"-WdaLocalPort {wdaPort ?? 0} " +
                                $"-MpegLocalPort {mjpegPort ?? 0} " +
                                $"-PrebuiltWdaPath \"{_prebuiltWdaPath ?? string.Empty}\" " +
                                $"-DeviceUdid \"{device.Id}\" " +
                                $"-Platform \"{device.Platform}\"";
                }
                else
                {
                    scriptPath = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                        ? Path.Combine(_installFolder, "Platform", "MacOS", "Scripts", "StartAppiumServer.sh")
                        : Path.Combine(_installFolder, "Platform", "Linux", "Scripts", "StartAppiumServer.sh");
                    
                    executable = "/bin/bash";
                    // Pass explicit paths instead of relying on environment
                    arguments = $"\"{scriptPath}\" \"{appiumHome}\" \"{appiumBin}\" \"{nodejsPath}\" \"{_installFolder}\" {appiumPort} {wdaPort ?? 0} {mjpegPort ?? 0} \"{_prebuiltWdaPath ?? string.Empty}\" \"{device.Id}\" \"{device.Platform}\"";
                    
                    // Ensure script is executable (Unix only)
                    if (!_isWindows && File.Exists(scriptPath))
                    {
                        try
                        {
#pragma warning disable CA1416 // Platform-specific API; guarded by !_isWindows
                            File.SetUnixFileMode(
                                scriptPath,
                                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
                        }
                        catch
                        {
                            // best-effort
                        }
                    }
                }

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("Appium startup script not found: {ScriptPath}", scriptPath);
                    return false;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _installFolder
                };

                // Force UTF8 reading of child process output and reduce ANSI/colour
                try
                {
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    psi.StandardErrorEncoding = Encoding.UTF8;
                    psi.EnvironmentVariables["NO_COLOR"] = "1";
                    psi.EnvironmentVariables["FORCE_COLOR"] = "0";
                    psi.EnvironmentVariables["TERM"] = "dumb";
                }
                catch
                {
                    // Best-effort; ignore if environment modifications are not allowed
                }

                // No environment variables needed - using fully qualified paths in scripts
                
                _logger.LogInformation("Starting Appium process for {DeviceName} on port {Port}", device.Name, appiumPort);
                
                var process = new Process { StartInfo = psi };
                
                var serviceLogDir = Path.Combine(logDir, serviceName);
                Directory.CreateDirectory(serviceLogDir);

                var stdoutLog = Path.Combine(serviceLogDir, "stdout.log");
                var stderrLog = Path.Combine(serviceLogDir, "stderr.log");
                var sessionJsonPath = Path.Combine(serviceLogDir, "session.json");
                
                // Strip ANSI sequences and control characters before writing logs
                var ansiRegex = new System.Text.RegularExpressions.Regex("\\x1B\\[[0-9;]*[A-Za-z]", System.Text.RegularExpressions.RegexOptions.Compiled);

                string CleanLine(string s)
                {
                    if (string.IsNullOrEmpty(s)) return s;
                    var cleaned = ansiRegex.Replace(s, string.Empty);
                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]", string.Empty);
                    return cleaned.Trim();
                }

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = CleanLine(e.Data);
                        if (!string.IsNullOrEmpty(line))
                        {
                            File.AppendAllText(stdoutLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}", Encoding.UTF8);
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = CleanLine(e.Data);
                        if (!string.IsNullOrEmpty(line))
                        {
                            File.AppendAllText(stderrLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}", Encoding.UTF8);
                        }
                    }
                };

                    if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    _runningProcesses[serviceName] = process;
                    
                    // Monitor for early exit
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(2000);
                        if (process.HasExited)
                        {
                            _logger.LogError("Appium process exited early with code {Code}. Check logs at {LogPath}", process.ExitCode, stderrLog);
                            _runningProcesses.TryRemove(serviceName, out _);
                        }
                    });
                    
                        try
                        {
                            var metadata = new
                            {
                                ServiceName = serviceName,
                                DeviceId = device.Id,
                                DeviceName = device.Name,
                                Platform = device.Platform.ToString(),
                                AppiumPort = appiumPort,
                                WdaLocalPort = wdaPort,
                                MjpegServerPort = mjpegPort,
                                SystemPort = systemPort,
                                ProcessId = process.Id,
                                StartedAt = DateTime.UtcNow.ToString("o")
                            };

#pragma warning disable IL2026 // Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.
                            var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
#pragma warning restore IL2026
                            File.WriteAllText(sessionJsonPath, json, Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to write session metadata for {ServiceName}", serviceName);
                        }

                        return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Appium process {ServiceName}", serviceName);
                return false;
            }
        }

        private async Task StopLocalProcessAsync(string serviceName)
        {
            if (_runningProcesses.TryRemove(serviceName, out var process))
            {
                try
                {
                    _logger.LogInformation("Stopping Appium process {ServiceName} (PID: {Pid})", serviceName, process.Id);
                    
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Kill entire process tree
                        await process.WaitForExitAsync(new CancellationTokenSource(5000).Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping process {ServiceName}", serviceName);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private async Task<int?> GetServiceProcessIdAsync(string serviceName)
        {
            if (_runningProcesses.TryGetValue(serviceName, out var process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        return process.Id;
                    }
                }
                catch
                {
                    // Process might have exited
                }
            }
            return await Task.FromResult<int?>(null);
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
        /// Allocates consecutive ports for Appium services using the PortManager.
        /// </summary>
        public async Task<int[]?> AllocateConsecutivePortsAsync(int count)
        {
            var ports = await _portManager.AllocateConsecutivePortsAsync(count);
            
            if (ports == null)
            {
                _metrics.RecordPortAllocationFailure();
            }
            
            return ports;
        }

        /// <summary>
        /// Releases previously allocated ports back to the PortManager.
        /// </summary>
        public async Task ReleasePortsAsync(int[] ports)
        {
            await _portManager.ReleasePortsAsync(ports);
        }

        /// <summary>
        /// Gets all currently allocated ports from the PortManager.
        /// </summary>
        public IReadOnlyList<int> GetAllocatedPorts()
        {
            return _portManager.GetAllocatedPorts();
        }

        /// <summary>
        /// Checks if a specific port is in use via the PortManager.
        /// </summary>
        public bool IsPortInUse(int port)
        {
            return _portManager.IsPortInUse(port);
        }
    }
}
