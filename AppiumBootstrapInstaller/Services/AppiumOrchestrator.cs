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
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Orchestrates installation, service setup, and device listener operations
    /// </summary>
    public class AppiumOrchestrator
    {
        private readonly ILogger<AppiumOrchestrator> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly InstallConfig _config;
        private readonly Func<string, ScriptExecutor> _executorFactory;
        private readonly string _platformScriptsPath;

        public AppiumOrchestrator(
            ILogger<AppiumOrchestrator> logger,
            IServiceProvider serviceProvider,
            InstallConfig config,
            Func<string, ScriptExecutor> executorFactory,
            string platformScriptsPath)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config;
            _executorFactory = executorFactory;
            _platformScriptsPath = platformScriptsPath;
        }

        /// <summary>
        /// Runs the full installation workflow (dependencies + optional service setup)
        /// </summary>
        public async Task<int> RunInstallationAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            try
            {
                var executor = _executorFactory(_platformScriptsPath);
                var currentOS = executor.DetectOperatingSystem();

                if (currentOS == ScriptExecutor.OperatingSystem.Unknown)
                {
                    _logger.LogError("ERROR: Unsupported operating system");
                    return 1;
                }

                // Acquire folder lock
                using var installLock = AcquireInstallFolderLock(_config.InstallFolder, TimeSpan.FromSeconds(30));

                // Clean installation folder if requested
                if (!options.DryRun && _config.CleanInstallFolder)
                {
                    _logger.LogInformation("Cleaning installation folder before starting...");
                    executor.CleanInstallationFolder(_config.InstallFolder);
                }
                else if (!options.DryRun)
                {
                    _logger.LogInformation("Preserving existing installation folder (cleanInstallFolder: false)");
                }

                // STEP 1: Install Dependencies
                _logger.LogWarning("==========================================");
                _logger.LogWarning("  STEP 1/2: Installing Dependencies");
                _logger.LogWarning("==========================================");

                string scriptPath = executor.GetInstallationScriptPath(currentOS);
                string arguments = executor.BuildArguments(_config, currentOS);
                _logger.LogInformation("Installation script: {ScriptPath}", scriptPath);

                int exitCode = executor.ExecuteScript(scriptPath, arguments, options.DryRun);

                if (exitCode != 0)
                {
                    _logger.LogError("==========================================");
                    _logger.LogError("  STEP 1/2 FAILED: Dependencies Installation");
                    _logger.LogError("  Exit Code: {ExitCode}", exitCode);
                    _logger.LogError("==========================================");
                    return exitCode;
                }

                _logger.LogWarning("==========================================");
                _logger.LogWarning("  STEP 1/2 COMPLETED: Dependencies Installed Successfully");
                _logger.LogWarning("==========================================");

                // Start any configured plugins (optional plugin system)
                try
                {
                    var pluginOrchestrator = _serviceProvider.GetService<AppiumBootstrapInstaller.Plugins.PluginOrchestrator>();
                    var pluginRegistry = _serviceProvider.GetService<AppiumBootstrapInstaller.Plugins.PluginRegistry>();
                    if (pluginOrchestrator != null && pluginRegistry != null && pluginRegistry.GetDefinitions().Any())
                    {
                        var ctx = new AppiumBootstrapInstaller.Plugins.PluginContext
                        {
                            InstallFolder = _config.InstallFolder,
                            Services = _serviceProvider,
                            Logger = _serviceProvider.GetService(typeof(Microsoft.Extensions.Logging.ILogger<AppiumBootstrapInstaller.Plugins.PluginOrchestrator>)) as Microsoft.Extensions.Logging.ILogger<AppiumBootstrapInstaller.Plugins.PluginOrchestrator> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AppiumBootstrapInstaller.Plugins.PluginOrchestrator>.Instance
                        };

                        await pluginOrchestrator.StartEnabledPluginsAsync(ctx, cancellationToken);
                            // Start background plugin health monitor
                            try
                            {
                                // Use configured monitor interval and backoff from InstallConfig
                                int monitorInterval = _config.PluginMonitorIntervalSeconds;
                                int restartBackoff = _config.PluginRestartBackoffSeconds;
                                pluginOrchestrator.StartMonitoring(ctx, monitorInterval, restartBackoff, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to start plugin health monitor");
                            }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Plugin orchestrator failed to start configured plugins");
                }

                // If device listener is enabled, start it inline and skip service setup
                if (_config.EnableDeviceListener)
                {
                    _logger.LogInformation("");
                    _logger.LogInformation("Device listener is enabled; starting STEP 2/2 in-process (default behavior). Skipping service setup.");
                    
                    await CopyPlatformScriptsAsync();
                    return await RunDeviceListenerAsync(cancellationToken);
                }

                // STEP 2: Optional Startup Configuration (only if device listener is not enabled inline)
                _logger.LogInformation("");
                _logger.LogWarning("==========================================");
                _logger.LogWarning("  STEP 2/2: Configuring Optional Auto-Start");
                _logger.LogWarning("==========================================");

                string serviceSetupScriptPath = executor.GetServiceSetupScriptPath(currentOS);
                _logger.LogInformation("Service setup script: {ScriptPath}", serviceSetupScriptPath);

                string exeCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name + ".exe");
                string serviceSetupArgs = currentOS == ScriptExecutor.OperatingSystem.Windows
                    ? $"-InstallDir \"{_config.InstallFolder}\" -ExeSource \"{exeCandidate}\""
                    : $"\"{_config.InstallFolder}\"";

                // Copy executable to install folder
                await CopyExecutableToInstallFolderAsync();

                int serviceSetupExitCode = executor.ExecuteScript(serviceSetupScriptPath, serviceSetupArgs, options.DryRun);

                if (serviceSetupExitCode != 0)
                {
                    _logger.LogError("==========================================");
                    _logger.LogError("  STEP 2/2 FAILED: Startup Configuration");
                    _logger.LogError("  Exit Code: {ExitCode}", serviceSetupExitCode);
                    _logger.LogError("==========================================");
                    _logger.LogWarning("Dependencies were installed successfully, but startup configuration failed.");
                    _logger.LogWarning("You can manually run the device listener:");
                    _logger.LogWarning("  {ExePath} --listen --config {ConfigPath}", 
                        Path.Combine(_config.InstallFolder, "AppiumBootstrapInstaller.exe"),
                        "path/to/config.json");
                    return serviceSetupExitCode;
                }

                _logger.LogWarning("==========================================");
                _logger.LogWarning("  STEP 2/2 COMPLETED: Service Manager Setup Successfully");
                _logger.LogWarning("==========================================");

                // Copy Platform scripts
                await CopyPlatformScriptsAsync();

                // Show completion message
                ShowCompletionMessage(currentOS);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Installation failed");
                return 1;
            }
        }

        /// <summary>
        /// Runs device listener mode (skips installation)
        /// </summary>
        public async Task<int> RunDeviceListenerAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("");
                _logger.LogWarning("==========================================");
                _logger.LogWarning("  STEP 2/2: Starting Device Listener Mode");
                _logger.LogWarning("==========================================");

                if (!_config.EnableDeviceListener)
                {
                    _logger.LogWarning("Device listener is disabled in configuration");
                    return 1;
                }

                // Get services from DI container (not manual instantiation)
                var eventBus = _serviceProvider.GetRequiredService<IEventBus>();
                var metrics = _serviceProvider.GetRequiredService<IDeviceMetrics>();
                var registry = _serviceProvider.GetRequiredService<IDeviceRegistry>();
                var sessionManager = _serviceProvider.GetRequiredService<IAppiumSessionManager>();

                var deviceListener = new DeviceListenerService(
                    _serviceProvider.GetRequiredService<ILogger<DeviceListenerService>>(),
                    _config,
                    _config.InstallFolder,
                    registry,
                    sessionManager,
                    metrics,
                    eventBus
                );

                _logger.LogInformation("Device listener configured:");
                _logger.LogInformation("  Install Folder: {InstallFolder}", _config.InstallFolder);
                _logger.LogInformation("  Poll Interval: {Interval}s", _config.DeviceListenerPollInterval);
                _logger.LogInformation("  Auto Start Appium: {AutoStart}", _config.AutoStartAppium);
                _logger.LogInformation("  Port Allocation: Dynamic (consecutive 4-digit ports)");
                _logger.LogInformation("");
                _logger.LogInformation("Press Ctrl+C to stop...");

                // Start the device listener
                await deviceListener.StartAsync(cancellationToken);

                // Wait indefinitely
                await Task.Delay(Timeout.Infinite, cancellationToken);

                return 0;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Device listener stopped");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device listener failed");
                return 1;
            }
        }

        private FileStream AcquireInstallFolderLock(string installFolder, TimeSpan timeout)
        {
            try
            {
                Directory.CreateDirectory(installFolder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create install folder to acquire lock");
                throw;
            }

            string lockPath = Path.Combine(installFolder, ".install.lock");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            while (sw.Elapsed < timeout)
            {
                try
                {
                    var fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    fs.SetLength(0);
                    var info = System.Text.Encoding.UTF8.GetBytes($"PID:{Environment.ProcessId}\nAcquired:{DateTime.UtcNow:O}\n");
                    fs.Write(info, 0, info.Length);
                    fs.Flush(true);
                    _logger.LogInformation("Acquired install-folder lock: {LockPath}", lockPath);
                    return fs;
                }
                catch (IOException)
                {
                    _logger.LogWarning("Install folder appears locked by another installer. Waiting to acquire lock...");
                    Thread.Sleep(1000);
                }
            }

            throw new TimeoutException($"Timed out waiting to acquire install-folder lock at {lockPath}");
        }

        private async Task CopyPlatformScriptsAsync()
        {
            try
            {
                var platformDest = Path.Combine(_config.InstallFolder, "Platform");

                if (!string.IsNullOrEmpty(_platformScriptsPath) && Directory.Exists(_platformScriptsPath))
                {
                    _logger.LogInformation("Copying Platform scripts to installation folder...");
                    CopyDirectory(_platformScriptsPath, platformDest, recursive: true);
                    _logger.LogInformation("Platform scripts copied to: {Destination}", platformDest);

                    // Verify StartAppiumServer script exists
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        string startScript = Path.Combine(platformDest, "Windows", "Scripts", "StartAppiumServer.ps1");
                        if (!File.Exists(startScript))
                        {
                            _logger.LogWarning("Appium startup script not found at: {Script}", startScript);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Platform directory not found at: {Source}", _platformScriptsPath ?? "(null)");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy Platform scripts");
            }
        }

        private async Task CopyExecutableToInstallFolderAsync()
        {
            try
            {
                string[] candidateSources = new[] {
                    System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName,
                    System.AppContext.BaseDirectory,
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name + ".exe"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName)
                }.Where(s => !string.IsNullOrEmpty(s)).ToArray()!;

                string? found = null;
                foreach (var candidate in candidateSources)
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    {
                        found = candidate;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(found))
                {
                    var exeDest = Path.Combine(_config.InstallFolder, Path.GetFileName(found));
                    File.Copy(found, exeDest, true);
                    _logger.LogInformation("Copied executable to: {ExeDest}", exeDest);
                }
                else
                {
                    _logger.LogDebug("No executable file found among candidates; skipping copy to install folder");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not copy running executable to install folder");
            }
        }

        private void ShowCompletionMessage(ScriptExecutor.OperatingSystem currentOS)
        {
            _logger.LogInformation("");
            _logger.LogInformation("==========================================");
            _logger.LogInformation("  ALL INSTALLATION STEPS COMPLETED SUCCESSFULLY!");
            _logger.LogInformation("==========================================");
            _logger.LogInformation("Next steps:");
            _logger.LogInformation("  1. Appium is installed in: {InstallFolder}", _config.InstallFolder);

            if (currentOS == ScriptExecutor.OperatingSystem.Windows)
            {
                _logger.LogInformation("  2. Services are configured with NSSM");
                _logger.LogInformation("  3. Device listener is disabled. Enable it in config.json:");
                _logger.LogInformation("     \"EnableDeviceListener\": true");
                _logger.LogInformation("  4. Or run manually: {InstallFolder}\\bin\\appium.bat", _config.InstallFolder);
            }
            else if (currentOS == ScriptExecutor.OperatingSystem.MacOS)
            {
                _logger.LogInformation("  2. Services are configured with Supervisor");
                _logger.LogInformation("  3. Source NVM: source {InstallFolder}/.nvm/nvm.sh", _config.InstallFolder);
                _logger.LogInformation("  4. Use Node version: nvm use {NodeVersion}", _config.NodeVersion);
                _logger.LogInformation("  5. Device listener is disabled. Enable it in config.json");
            }
            else // Linux
            {
                _logger.LogInformation("  2. Services are configured with systemd");
                _logger.LogInformation("  3. Source NVM: source {InstallFolder}/.nvm/nvm.sh", _config.InstallFolder);
                _logger.LogInformation("  4. Use Node version: nvm use {NodeVersion}", _config.NodeVersion);
                _logger.LogInformation("  5. Device listener is disabled. Enable it in config.json");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, overwrite: true);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }
    }

    public class CommandLineOptions
    {
        public bool ShowHelp { get; set; }
        public string? ConfigPath { get; set; }
        public bool DryRun { get; set; }
        public bool GenerateSampleConfig { get; set; }
        public bool ListenMode { get; set; }
        // If set, prints warnings as JSON to stdout
        public bool WarningsJson { get; set; }
        // Optional path to write warnings JSON for CI consumption
        public string? WarningsFile { get; set; }
    }
}
