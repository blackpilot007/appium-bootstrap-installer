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

using System;
using System.Runtime.InteropServices;
using AppiumBootstrapInstaller.Services;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace AppiumBootstrapInstaller
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Configure Serilog with adaptive ANSI color theme that works with both dark and light terminals
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Literate,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path: "logs/installer-.log",
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: 10_485_760, // 10 MB
                    retainedFileCountLimit: 30,
                    rollOnFileSizeLimit: true,
                    shared: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            try
            {
                var services = new ServiceCollection();
                ConfigureServices(services);
                using var serviceProvider = services.BuildServiceProvider();

                // Get logger for Program
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("===========================================");
                logger.LogInformation("  Appium Bootstrap Installer");
                logger.LogInformation("  Configuration-Driven Service");
                logger.LogInformation("===========================================");

                // Parse command-line arguments
                var options = ParseArguments(args);

                if (options.ShowHelp)
                {
                    ShowHelp();
                    return 0;
                }

                // Resolve services
                var configReader = serviceProvider.GetRequiredService<ConfigurationReader>();
                var executorFactory = serviceProvider.GetRequiredService<Func<string, ScriptExecutor>>();

                if (options.GenerateSampleConfig)
                {
                    configReader.CreateSampleConfig(options.ConfigPath ?? "config.sample.json");
                    return 0;
                }

                // Detect OS and create Executor
                var platformScriptsPath = GetPlatformScriptsPath();
                var executor = executorFactory(platformScriptsPath);

                var currentOS = executor.DetectOperatingSystem();
                logger.LogInformation("Detected OS: {OS}", currentOS);
                logger.LogInformation("Architecture: {Architecture}", RuntimeInformation.ProcessArchitecture);
                logger.LogInformation(".NET Runtime: {Runtime}", RuntimeInformation.FrameworkDescription);

                if (currentOS == ScriptExecutor.OperatingSystem.Unknown)
                {
                    logger.LogError("ERROR: Unsupported operating system");
                    return 1;
                }

                // Load configuration
                var config = configReader.LoadConfiguration(options.ConfigPath);

                logger.LogInformation("Configuration loaded successfully:");
                logger.LogInformation("  Install Folder: {InstallFolder}", config.InstallFolder);
                logger.LogInformation("  Node Version: {NodeVersion}", config.NodeVersion);
                logger.LogInformation("  Appium Version: {AppiumVersion}", config.AppiumVersion);
                logger.LogInformation("  NVM Version: {NvmVersion}", config.NvmVersion);
                logger.LogInformation("  Drivers: {DriversCount} enabled", config.Drivers.Count(d => d.Enabled));
                logger.LogInformation("  Plugins: {PluginsCount} enabled", config.Plugins.Count(p => p.Enabled));

                // Run in listen-only mode if requested (skip installation)
                if (options.ListenMode)
                {
                    logger.LogInformation("Running in listen-only mode (skipping installation)");
                    return await RunDeviceListenerAsync(serviceProvider, config, logger);
                }

                // Acquire a folder-level lock to prevent concurrent installers
                using var installLock = AcquireInstallFolderLock(config.InstallFolder, logger, TimeSpan.FromSeconds(30));

                // Clean installation folder before starting (if configured)
                if (!options.DryRun && config.CleanInstallFolder)
                {
                    logger.LogInformation("Cleaning installation folder before starting...");
                    executor.CleanInstallationFolder(config.InstallFolder);
                }
                else if (!options.DryRun)
                {
                    logger.LogInformation("Preserving existing installation folder (cleanInstallFolder: false)");
                }

                // Get script path
                string scriptPath = executor.GetInstallationScriptPath(currentOS);
                logger.LogInformation("Installation script: {ScriptPath}", scriptPath);

                // Build arguments
                string arguments = executor.BuildArguments(config, currentOS);

                // ============================================
                // STEP 1: Install Dependencies
                // ============================================
                logger.LogWarning("==========================================");
                logger.LogWarning("  STEP 1/2: Installing Dependencies");
                logger.LogWarning("==========================================");

                int exitCode = executor.ExecuteScript(scriptPath, arguments, options.DryRun);

                if (exitCode != 0)
                {
                    logger.LogError("==========================================");
                    logger.LogError("  STEP 1/2 FAILED: Dependencies Installation");
                    logger.LogError("  Exit Code: {ExitCode}", exitCode);
                    logger.LogError("==========================================");
                    return exitCode;
                }

                logger.LogWarning("==========================================");
                logger.LogWarning("  STEP 1/2 COMPLETED: Dependencies Installed Successfully");
                logger.LogWarning("==========================================");

                // Default behavior: if device listener is enabled in config, run it
                // inline (in-process) immediately after dependencies are installed.
                // This avoids creating a separate service and reduces complexity
                // for non-admin installs. If later you want a persistent service,
                // we can add an explicit opt-in flag to create one.
                if (config.EnableDeviceListener)
                {
                    logger.LogInformation("");
                    logger.LogInformation("Device listener is enabled; starting listener in-process (default behavior). Skipping service setup.");
                    // Ensure Platform scripts are available in the install folder so
                    // StartAppiumServer.ps1 and related runtime scripts exist when
                    // the device listener attempts to start Appium sessions.
                    try
                    {
                        // Prefer the discovered platform scripts path (may be in a parent publish folder)
                        var platformSource = platformScriptsPath;
                        var platformDest = Path.Combine(config.InstallFolder, "Platform");

                        if (!string.IsNullOrEmpty(platformSource) && Directory.Exists(platformSource))
                        {
                            logger.LogInformation("Copying Platform scripts to installation folder before starting listener...");
                            CopyDirectory(platformSource, platformDest, recursive: true);
                            logger.LogInformation("Platform scripts copied to: {Destination}", platformDest);
                        }
                        else
                        {
                            logger.LogWarning("Platform directory not found at: {Source}. Device listener may not be able to start Appium servers.", platformSource ?? "(null)");
                        }

                        // Verify StartAppiumServer script existence for the current OS
                        if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                        {
                            string startScript = Path.Combine(platformDest, "Windows", "Scripts", "StartAppiumServer.ps1");
                            if (!File.Exists(startScript))
                            {
                                logger.LogWarning("Appium startup script not found at: {Script}. Device listener will retry starting sessions but Appium may fail.", startScript);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to copy Platform scripts before starting device listener");
                    }

                    return await RunDeviceListenerAsync(serviceProvider, config, logger);
                }

                // ============================================
                // STEP 2: Service Setup
                // ============================================
                logger.LogInformation("");
                logger.LogWarning("==========================================");
                logger.LogWarning("  STEP 2/2: Setting Up Service Manager");
                logger.LogWarning("==========================================");

                string serviceSetupScriptPath = executor.GetServiceSetupScriptPath(currentOS);
                logger.LogInformation("Service setup script: {ScriptPath}", serviceSetupScriptPath);

                // Service setup scripts accept optional install directory but can run without arguments
                // Pass install folder as argument for consistency. Also pass a best-effort path to the published exe
                string exeCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name + ".exe");
                string serviceSetupArgs = currentOS == ScriptExecutor.OperatingSystem.Windows
                    ? $"-InstallDir \"{config.InstallFolder}\" -ExeSource \"{exeCandidate}\""
                    : $"\"{config.InstallFolder}\"";

                // Ensure the running executable is available in the install folder so ServiceSetup can create the agent wrapper
                    try
                    {
                        string[] candidateSources = new[] {
                            System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName,
                            System.Reflection.Assembly.GetEntryAssembly()?.Location,
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name + ".exe"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.FriendlyName)
                        };

                        string found = null;
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
                            var exeDest = Path.Combine(config.InstallFolder, Path.GetFileName(found));
                            File.Copy(found, exeDest, true);
                            logger.LogInformation("Copied executable to: {ExeDest}", exeDest);
                        }
                        else
                        {
                            logger.LogDebug("No executable file found among candidates; skipping copy to install folder");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not copy running executable to install folder");
                    }

                int serviceSetupExitCode = executor.ExecuteScript(serviceSetupScriptPath, serviceSetupArgs, options.DryRun);

                if (serviceSetupExitCode != 0)
                {
                    logger.LogError("==========================================");
                    logger.LogError("  STEP 2/2 FAILED: Service Setup");
                    logger.LogError("  Exit Code: {ExitCode}", serviceSetupExitCode);
                    logger.LogError("==========================================");
                    logger.LogWarning("Dependencies were installed successfully, but service setup failed.");
                    logger.LogWarning("You may need to run the service setup script manually:");
                    logger.LogWarning("  {ScriptPath}", serviceSetupScriptPath);
                    return serviceSetupExitCode;
                }

                logger.LogWarning("==========================================");
                logger.LogWarning("  STEP 2/2 COMPLETED: Service Manager Setup Successfully");
                logger.LogWarning("==========================================");

                // Copy Platform scripts to installation folder for StartAppiumServer.ps1 and other runtime scripts
                try
                {
                    var platformSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Platform");
                    var platformDest = Path.Combine(config.InstallFolder, "Platform");
                    
                    if (Directory.Exists(platformSource))
                    {
                        logger.LogInformation("Copying Platform scripts to installation folder...");
                        CopyDirectory(platformSource, platformDest, recursive: true);
                        logger.LogInformation("Platform scripts copied to: {Destination}", platformDest);
                    }
                    else
                    {
                        logger.LogWarning("Platform directory not found at: {Source}", platformSource);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to copy Platform scripts");
                }

                // NOTE: Device listener is started inline by default after dependency
                // installation above when `EnableDeviceListener` is true. We do not
                // start it here because this code path is for after service setup
                // which we skip for inline default behavior.

                // ============================================
                // ALL STEPS COMPLETED (without device listener)
                // ============================================
                logger.LogInformation("");
                logger.LogInformation("==========================================");
                logger.LogInformation("  ALL INSTALLATION STEPS COMPLETED SUCCESSFULLY!");
                logger.LogInformation("==========================================");
                logger.LogInformation("Next steps:");
                logger.LogInformation("  1. Appium is installed in: {InstallFolder}", config.InstallFolder);

                if (currentOS == ScriptExecutor.OperatingSystem.Windows)
                {
                    logger.LogInformation("  2. Services are configured with NSSM");
                    logger.LogInformation("  3. Device listener is disabled. Enable it in config.json:");
                    logger.LogInformation("     \"EnableDeviceListener\": true");
                    logger.LogInformation("  4. Or run manually: {InstallFolder}\\bin\\appium.bat", config.InstallFolder);
                }
                else if (currentOS == ScriptExecutor.OperatingSystem.MacOS)
                {
                    logger.LogInformation("  2. Services are configured with Supervisor");
                    logger.LogInformation("  3. Source NVM: source {InstallFolder}/.nvm/nvm.sh", config.InstallFolder);
                    logger.LogInformation("  4. Use Node version: nvm use {NodeVersion}", config.NodeVersion);
                    logger.LogInformation("  5. Device listener is disabled. Enable it in config.json");
                }
                else // Linux
                {
                    logger.LogInformation("  2. Services are configured with systemd");
                    logger.LogInformation("  3. Source NVM: source {InstallFolder}/.nvm/nvm.sh", config.InstallFolder);
                    logger.LogInformation("  4. Use Node version: nvm use {NodeVersion}", config.NodeVersion);
                    logger.LogInformation("  5. Device listener is disabled. Enable it in config.json");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CRITICAL ERROR");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static FileStream AcquireInstallFolderLock(string installFolder, Microsoft.Extensions.Logging.ILogger logger, TimeSpan timeout)
        {
            // Ensure install folder exists so we can place a lock file inside it
            try
            {
                Directory.CreateDirectory(installFolder);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create install folder to acquire lock");
                throw;
            }

            string lockPath = Path.Combine(installFolder, ".install.lock");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    // Open the file exclusively. This will fail if another process holds it.
                    var fs = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    // Write basic info to the lock file to aid debugging
                    fs.SetLength(0);
                    var info = System.Text.Encoding.UTF8.GetBytes($"PID:{Environment.ProcessId}\nAcquired:{DateTime.UtcNow:O}\n");
                    fs.Write(info, 0, info.Length);
                    fs.Flush(true);
                    logger.LogInformation("Acquired install-folder lock: {LockPath}", lockPath);
                    return fs;
                }
                catch (IOException)
                {
                    logger.LogWarning("Install folder appears locked by another installer. Waiting to acquire lock...");
                    Thread.Sleep(1000);
                }
            }

            throw new TimeoutException($"Timed out waiting to acquire install-folder lock at {lockPath}");
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Add logging
            services.AddLogging(configure =>
            {
                configure.AddSerilog(); // Uses static Log.Logger
            });

            // Register services
            services.AddTransient<ConfigurationReader>();

            // Factory for ScriptExecutor to injecting path
            services.AddTransient<Func<string, ScriptExecutor>>(provider => path =>
                new ScriptExecutor(path, provider.GetRequiredService<ILogger<ScriptExecutor>>()));
        }

        private static async Task<int> RunDeviceListenerAsync(
            IServiceProvider serviceProvider,
            InstallConfig config,
            ILogger<Program> logger)
        {
            try
            {
                logger.LogInformation("===========================================");
                logger.LogInformation("  Starting Device Listener Mode");
                logger.LogInformation("===========================================");

                if (!config.EnableDeviceListener)
                {
                    logger.LogWarning("Device listener is disabled in configuration");
                    return 1;
                }

                // Create device listener services
                var metrics = new DeviceMetrics();
                
                var registry = new DeviceRegistry(
                    serviceProvider.GetRequiredService<ILogger<DeviceRegistry>>(),
                    config.DeviceRegistry
                );

                var sessionManager = new AppiumSessionManager(
                    serviceProvider.GetRequiredService<ILogger<AppiumSessionManager>>(),
                    config.InstallFolder,
                    config.PortRanges,
                    metrics
                );

                var deviceListener = new DeviceListenerService(
                    serviceProvider.GetRequiredService<ILogger<DeviceListenerService>>(),
                    config,
                    config.InstallFolder,
                    registry,
                    sessionManager,
                    metrics
                );

                logger.LogInformation("Device listener configured:");
                logger.LogInformation("  Install Folder: {InstallFolder}", config.InstallFolder);
                logger.LogInformation("  Poll Interval: {Interval}s", config.DeviceListenerPollInterval);
                logger.LogInformation("  Auto Start Appium: {AutoStart}", config.AutoStartAppium);
                logger.LogInformation("  Port Allocation: Dynamic (consecutive 4-digit ports)");
                logger.LogInformation("");
                logger.LogInformation("Press Ctrl+C to stop...");

                // Run the service
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    logger.LogInformation("Shutting down...");
                };

                await deviceListener.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);

                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Device listener stopped");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Device listener failed");
                return 1;
            }
        }

        static CommandLineOptions ParseArguments(string[] args)
        {
            var options = new CommandLineOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg.ToLower())
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        options.ShowHelp = true;
                        break;

                    case "--config":
                    case "-c":
                        if (i + 1 < args.Length)
                        {
                            options.ConfigPath = args[++i];
                        }
                        else
                        {
                            throw new ArgumentException("--config requires a file path");
                        }
                        break;

                    case "--dry-run":
                    case "-d":
                        options.DryRun = true;
                        break;

                    case "--generate-config":
                    case "-g":
                        options.GenerateSampleConfig = true;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            options.ConfigPath = args[++i];
                        }
                        break;

                    case "--listen":
                    case "-l":
                        options.ListenMode = true;
                        break;

                    default:
                        Console.WriteLine($"Warning: Unknown argument '{arg}' (use --help for usage)");
                        break;
                }
            }

            return options;
        }

        static void ShowHelp()
        {
            Console.WriteLine("Appium Bootstrap Installer - Configuration-Driven Service");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  AppiumBootstrapInstaller [options]");
            Console.WriteLine();
            Console.WriteLine("DESCRIPTION:");
            Console.WriteLine("  By default, this application will:");
            Console.WriteLine("    1. Install Node.js, Appium, and configured drivers/plugins");
            Console.WriteLine("    2. Set up the service manager (NSSM/Supervisor/systemd)");
            Console.WriteLine("    3. Start device listener (if EnableDeviceListener: true in config)");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  --config, -c <path>       Path to configuration file (JSON)");
            Console.WriteLine("                            If not specified, searches in:");
            Console.WriteLine("                              1. ./config.json (current directory)");
            Console.WriteLine("                              2. ~/.appium-bootstrap/config.json");
            Console.WriteLine();
            Console.WriteLine("  --dry-run, -d             Show what would be executed without running");
            Console.WriteLine();
            Console.WriteLine("  --generate-config, -g [path]");
            Console.WriteLine("                            Generate a sample configuration file");
            Console.WriteLine("                            Default: config.sample.json");
            Console.WriteLine();
            Console.WriteLine("  --listen, -l              Skip installation and run device listener only");
            Console.WriteLine("                            Use this if dependencies are already installed");
            Console.WriteLine();
            Console.WriteLine("  --help, -h                Show this help message");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Generate sample config");
            Console.WriteLine("  AppiumBootstrapInstaller --generate-config");
            Console.WriteLine();
            Console.WriteLine("  # Full setup: Install + Start device listener");
            Console.WriteLine("  AppiumBootstrapInstaller --config my-config.json");
            Console.WriteLine();
            Console.WriteLine("  # Run device listener only (skip installation)");
            Console.WriteLine("  AppiumBootstrapInstaller --listen");
            Console.WriteLine();
            Console.WriteLine("  # Dry run to preview execution");
            Console.WriteLine("  AppiumBootstrapInstaller --config my-config.json --dry-run");
        }

        static string GetPlatformScriptsPath()
        {
            // Get the directory where the executable is located
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? dirInfo = new DirectoryInfo(currentDir);

            // Walk up the directory tree until we find the "Platform" folder or hit the root
            while (dirInfo != null)
            {
                string platformPath = Path.Combine(dirInfo.FullName, "Platform");
                if (Directory.Exists(platformPath))
                {
                    return platformPath;
                }
                dirInfo = dirInfo.Parent;
            }

            // If not found, throw exception with helpful message
            throw new DirectoryNotFoundException(
                "Platform scripts directory not found. \n" +
                "The application searches for a 'Platform' directory in the current directory and all parent directories.\n" +
                "Please ensure the Platform folder is available relative to the executable."
            );
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
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

    class CommandLineOptions
    {
        public bool ShowHelp { get; set; }
        public string? ConfigPath { get; set; }
        public bool DryRun { get; set; }
        public bool GenerateSampleConfig { get; set; }
        public bool ListenMode { get; set; } // New: Run in device listener mode
    }
}
