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
                .WriteTo.File("logs/installer-.log", rollingInterval: RollingInterval.Day)
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
                logger.LogInformation("==========================================");
                logger.LogInformation("  STEP 1/2: Installing Dependencies");
                logger.LogInformation("==========================================");

                int exitCode = executor.ExecuteScript(scriptPath, arguments, options.DryRun);

                if (exitCode != 0)
                {
                    logger.LogError("==========================================");
                    logger.LogError("  STEP 1/2 FAILED: Dependencies Installation");
                    logger.LogError("  Exit Code: {ExitCode}", exitCode);
                    logger.LogError("==========================================");
                    return exitCode;
                }

                logger.LogInformation("==========================================");
                logger.LogInformation("  STEP 1/2 COMPLETED: Dependencies Installed Successfully");
                logger.LogInformation("==========================================");

                // ============================================
                // STEP 2: Service Setup
                // ============================================
                logger.LogInformation("");
                logger.LogInformation("==========================================");
                logger.LogInformation("  STEP 2/2: Setting Up Service Manager");
                logger.LogInformation("==========================================");

                string serviceSetupScriptPath = executor.GetServiceSetupScriptPath(currentOS);
                logger.LogInformation("Service setup script: {ScriptPath}", serviceSetupScriptPath);

                // Service setup scripts accept optional install directory but can run without arguments
                // Pass install folder as argument for consistency
                string serviceSetupArgs = currentOS == ScriptExecutor.OperatingSystem.Windows
                    ? $"-InstallDir \"{config.InstallFolder}\""
                    : $"\"{config.InstallFolder}\"";

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

                logger.LogInformation("==========================================");
                logger.LogInformation("  STEP 2/2 COMPLETED: Service Manager Setup Successfully");
                logger.LogInformation("==========================================");

                // ============================================
                // STEP 3: Start Device Listener (if enabled)
                // ============================================
                if (config.EnableDeviceListener)
                {
                    logger.LogInformation("");
                    logger.LogInformation("==========================================");
                    logger.LogInformation("  STEP 3/3: Starting Device Listener");
                    logger.LogInformation("==========================================");
                    logger.LogInformation("Device listener is enabled in configuration.");
                    logger.LogInformation("Starting automatic device monitoring and Appium session management...");
                    logger.LogInformation("");
                    
                    return await RunDeviceListenerAsync(serviceProvider, config, logger);
                }

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
