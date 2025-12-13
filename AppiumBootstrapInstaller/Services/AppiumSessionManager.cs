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
        private readonly string? _servyCliPath;
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
                // Check for Servy CLI in Program Files (default installation)
                _servyCliPath = @"C:\Program Files\Servy\servy-cli.exe";
                if (!File.Exists(_servyCliPath))
                {
                    // Fallback to local installation
                    _servyCliPath = Path.Combine(installFolder, "servy", "servy-cli.exe");
                    if (!File.Exists(_servyCliPath))
                    {
                        _logger.LogWarning("Servy CLI not found. Will use direct process execution.");
                        _servyCliPath = null;
                    }
                    else
                    {
                        _logger.LogInformation("Using Servy CLI for process management: {Path}", _servyCliPath);
                    }
                }
                else
                {
                    _logger.LogInformation("Using Servy CLI for process management: {Path}", _servyCliPath);
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
                        _metrics.RecordPortAllocationFailure();
                        
                        // Port exhaustion is likely not transient, don't retry
                        if (attempt == maxRetries)
                        {
                            return null;
                        }
                        
                        // Wait before retry in case ports are being released
                        await Task.Delay(baseDelayMs * attempt);
                        continue;
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
                var success = _isWindows
                    ? await StartWithServyAsync(serviceName, device, appiumPort, wdaPort, mjpegPort, systemPort)
                    : await StartWithSupervisordAsync(serviceName, device, appiumPort, wdaPort, mjpegPort, systemPort);

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
        public async Task StopSessionAsync(AppiumSession session)
        {
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
                        await StopWithServyAsync(serviceName);
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
                    _logger.LogInformation("Successfully stopped session {SessionId}", session.SessionId);
                    return;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning(
                        "Stop operation cancelled for session {SessionId}",
                        session.SessionId);
                    return;
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
        }

        private async Task<bool> StartWithServyAsync(
            string serviceName,
            Device device,
            int appiumPort,
            int? wdaPort,
            int? mjpegPort,
            int? systemPort)
        {
            if (_servyCliPath == null)
            {
                _logger.LogWarning("Servy CLI not available, cannot start service");
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

                // Get logs folder relative to executable (same location as Serilog installer logs)
                var executableDir = AppDomain.CurrentDomain.BaseDirectory;
                var logDir = Path.Combine(executableDir, "logs");
                Directory.CreateDirectory(logDir); // Ensure it exists

                // Build PowerShell service command arguments
                var psArguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                                $"-AppiumHomePath \"{appiumHome}\" " +
                                $"-AppiumBinPath \"{appiumBin}\" " +
                                $"-AppiumPort {appiumPort} " +
                                $"-WdaLocalPort {wdaPort ?? 0} " +
                                $"-MpegLocalPort {mjpegPort ?? 0}";

                // Build Servy install command with health monitoring and log rotation
                var servyArgs = $"install --quiet " +
                    $"--name=\"{serviceName}\" " +
                    $"--displayName=\"Appium - {device.Name}\" " +
                    $"--description=\"Appium server for device {device.Id}\" " +
                    $"--path=\"powershell.exe\" " +
                    $"--startupDir=\"{_installFolder}\" " +
                    $"--params=\"{psArguments}\" " +
                    $"--startupType=Automatic " +
                    $"--priority=Normal " +
                    $"--stdout=\"{logDir}\\{serviceName}_stdout.log\" " +
                    $"--stderr=\"{logDir}\\{serviceName}_stderr.log\" " +
                    $"--enableSizeRotation " +
                    $"--rotationSize=10 " +
                    $"--maxRotations=5 " +
                    $"--enableHealth " +
                    $"--heartbeatInterval=30 " +
                    $"--maxFailedChecks=3 " +
                    $"--recoveryAction=RestartProcess " +
                    $"--maxRestartAttempts=5";

                // Install service
                _logger.LogDebug("Installing Servy service: {ServiceName}", serviceName);
                var installResult = await RunCommandAsync(_servyCliPath, servyArgs);

                if (installResult.ExitCode != 0)
                {
                    _logger.LogError("Failed to install Servy service: {Error}", installResult.Error);
                    return false;
                }

                // Start service
                _logger.LogDebug("Starting Servy service: {ServiceName}", serviceName);
                var startResult = await RunCommandAsync(_servyCliPath, $"start --quiet --name=\"{serviceName}\"");

                if (startResult.ExitCode != 0)
                {
                    _logger.LogError("Failed to start Servy service: {Error}", startResult.Error);
                    await RunCommandAsync(_servyCliPath, $"uninstall --quiet --name=\"{serviceName}\"");
                    return false;
                }

                // Wait a bit for service to initialize
                await Task.Delay(2000);

                _logger.LogInformation("Servy service {ServiceName} started successfully with health monitoring", serviceName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Servy service {ServiceName}", serviceName);
                return false;
            }
        }

        private async Task StopWithServyAsync(string serviceName)
        {
            if (_servyCliPath == null) return;

            try
            {
                _logger.LogDebug("Stopping Servy service: {ServiceName}", serviceName);
                await RunCommandAsync(_servyCliPath, $"stop --quiet --name=\"{serviceName}\"");
                
                await Task.Delay(1000);
                
                _logger.LogDebug("Uninstalling Servy service: {ServiceName}", serviceName);
                await RunCommandAsync(_servyCliPath, $"uninstall --quiet --name=\"{serviceName}\"");
                
                _logger.LogInformation("Servy service {ServiceName} stopped and uninstalled", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Servy service {ServiceName}", serviceName);
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
                if (_isWindows && _servyCliPath != null)
                {
                    var result = await RunCommandAsync(_servyCliPath, $"status --name=\"{serviceName}\"");
                    // Parse PID from Servy status output if available
                    // For now, return null as Servy manages the process
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
