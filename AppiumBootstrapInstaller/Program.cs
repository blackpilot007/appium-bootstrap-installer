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
using AppiumBootstrapInstaller.Services.Interfaces;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.Text.Json;

namespace AppiumBootstrapInstaller
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Configure Serilog
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
                    fileSizeLimitBytes: 10_485_760,
                    retainedFileCountLimit: 30,
                    rollOnFileSizeLimit: true,
                    shared: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            try
            {
                // Parse arguments
                var options = ParseArguments(args);

                if (options.ShowHelp)
                {
                    ShowHelp();
                    return 0;
                }

                // Build DI container
                var services = new ServiceCollection();
                var config = ConfigureServices(services, options);
                using var serviceProvider = services.BuildServiceProvider();

                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("===========================================");
                logger.LogInformation("  Appium Bootstrap Installer");
                logger.LogInformation("  Configuration-Driven Service");
                logger.LogInformation("===========================================");
                logger.LogInformation("Detected OS: {OS}", RuntimeInformation.OSDescription);
                logger.LogInformation("Architecture: {Architecture}", RuntimeInformation.ProcessArchitecture);
                logger.LogInformation(".NET Runtime: {Runtime}", RuntimeInformation.FrameworkDescription);

                // Handle special modes
                if (options.GenerateSampleConfig)
                {
                    var configReader = serviceProvider.GetRequiredService<ConfigurationReader>();
                    configReader.CreateSampleConfig(options.ConfigPath ?? "config.sample.json");
                    return 0;
                }

                // Log configuration
                logger.LogInformation("Configuration loaded successfully:");
                logger.LogInformation("  Install Folder: {InstallFolder}", config.InstallFolder);
                logger.LogInformation("  Node Version: {NodeVersion}", config.NodeVersion);
                logger.LogInformation("  Appium Version: {AppiumVersion}", config.AppiumVersion);
                logger.LogInformation("  Drivers: {DriversCount} enabled", config.Drivers.Count(d => d.Enabled));
                logger.LogInformation("  Plugins: {PluginsCount} enabled", config.Plugins.Count(p => p.Enabled));

                // Validate configuration (collect errors + warnings)
                var validator = serviceProvider.GetRequiredService<ConfigurationValidator>();
                List<string> errors;
                List<string> warnings;
                if (!validator.Validate(config, out errors, out warnings))
                {
                    logger.LogError("Configuration validation failed. Please fix the errors and try again.");

                    if (warnings != null && warnings.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("Configuration warnings:");
                        foreach (var w in warnings)
                        {
                            Console.WriteLine("  - {0}", w);
                        }
                        Console.WriteLine();
                    }

                    return 1;
                }

                // Print warnings to the caller (stdout) so external callers can observe them.
                // Support machine-readable JSON output for CI via --warnings-json or --warnings-file / WARNINGS_FILE env var.
                if (warnings != null && warnings.Any())
                {
                    // Always print human-readable warnings for interactive use
                    Console.WriteLine();
                    Console.WriteLine("Configuration warnings:");
                    foreach (var w in warnings)
                    {
                        Console.WriteLine("  - {0}", w);
                    }
                    Console.WriteLine();

                    // Prepare JSON payload
                    var payload = new
                    {
                        TimestampUtc = DateTime.UtcNow,
                        ConfigPath = options.ConfigPath,
                        Warnings = warnings
                    };

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
#pragma warning disable IL2026 // Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code.
                    string json = JsonSerializer.Serialize(payload, jsonOptions);
#pragma warning restore IL2026

                    // If --warnings-json flag is set or WARNINGS_JSON env var is truthy, print JSON to stdout.
                    var envWarningsJson = Environment.GetEnvironmentVariable("WARNINGS_JSON");
                    if (options.WarningsJson || (!string.IsNullOrWhiteSpace(envWarningsJson) && (
                        envWarningsJson == "1" || envWarningsJson.Equals("true", StringComparison.OrdinalIgnoreCase))))
                    {
                        Console.WriteLine(json);
                    }

                    // If --warnings-file provided or WARNINGS_FILE env var set, write JSON to file for CI.
                    var warningsFilePath = options.WarningsFile ?? Environment.GetEnvironmentVariable("WARNINGS_FILE");
                    if (!string.IsNullOrWhiteSpace(warningsFilePath))
                    {
                        try
                        {
                            File.WriteAllText(warningsFilePath, json);
                            logger.LogInformation("Wrote configuration warnings JSON to {Path}", warningsFilePath);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to write warnings JSON to {Path}", warningsFilePath);
                        }
                    }
                }

                // Populate plugin registry from configuration (register simple ProcessPlugins for now)
                try
                {
                    var pluginRegistry = serviceProvider.GetRequiredService<AppiumBootstrapInstaller.Plugins.PluginRegistry>();

                    // Register definitions (blueprints) from configuration. Runtime instances will be created per-device by the orchestrator.
                    foreach (var p in config.Plugins)
                    {
                        var id = p.Id;
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            logger.LogWarning("Skipping plugin with empty id in configuration");
                            continue;
                        }

                        pluginRegistry.RegisterDefinition(id, p);
                        logger.LogDebug("Registered plugin definition {PluginId} (type={Type}, enabled={Enabled})", id, p.Type ?? "process", p.Enabled);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to populate plugin registry from configuration");
                }

                // Instantiate DeviceEventTrigger so it subscribes to event bus
                try
                {
                    var deviceTrigger = serviceProvider.GetService<AppiumBootstrapInstaller.Plugins.Triggers.DeviceEventTrigger>();
                    if (deviceTrigger != null)
                    {
                        logger.LogDebug("DeviceEventTrigger initialized");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to initialize DeviceEventTrigger");
                }

                // Get orchestrator and run
                var orchestrator = serviceProvider.GetRequiredService<AppiumOrchestrator>();
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    logger.LogInformation("Shutting down...");
                };

                int exitCode;
                if (options.ListenMode)
                {
                    logger.LogInformation("Running in listen-only mode (skipping installation)");
                    exitCode = await orchestrator.RunDeviceListenerAsync(cts.Token);
                }
                else
                {
                    exitCode = await orchestrator.RunInstallationAsync(options, cts.Token);
                }

                return exitCode;
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

        private static InstallConfig ConfigureServices(IServiceCollection services, CommandLineOptions options)
        {
            // Logging
            services.AddLogging(configure => configure.AddSerilog());

            // Configuration
            services.AddSingleton<ConfigurationReader>();
            var configReader = new ConfigurationReader(services.BuildServiceProvider().GetRequiredService<ILogger<ConfigurationReader>>());
            var config = configReader.LoadConfiguration(options.ConfigPath);
            services.AddSingleton(config);

            // Core services (Singleton for long-running process)
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<IDeviceMetrics, DeviceMetrics>();
            services.AddSingleton<IDeviceRegistry>(sp => new DeviceRegistry(
                sp.GetRequiredService<ILogger<DeviceRegistry>>(),
                config.DeviceRegistry));
            services.AddSingleton<IPortManager, PortManager>();
            services.AddSingleton<IAppiumSessionManager>(sp => new AppiumSessionManager(
                sp.GetRequiredService<ILogger<AppiumSessionManager>>(),
                config.InstallFolder,
                config.PortRanges,
                sp.GetRequiredService<IDeviceMetrics>(),
                sp.GetRequiredService<IPortManager>(),
                config.PrebuiltWdaPath));
            services.AddSingleton<IHealthCheckService, HealthCheckService>();

            // Validation
            services.AddSingleton<ConfigurationValidator>();

            // Script executor factory
            var platformScriptsPath = GetPlatformScriptsPath();
            services.AddTransient<Func<string, ScriptExecutor>>(provider => path =>
                new ScriptExecutor(path, provider.GetRequiredService<ILogger<ScriptExecutor>>()));

            // Orchestrator
            services.AddSingleton(sp => new AppiumOrchestrator(
                sp.GetRequiredService<ILogger<AppiumOrchestrator>>(),
                sp,
                config,
                sp.GetRequiredService<Func<string, ScriptExecutor>>(),
                platformScriptsPath));

            // Plugin subsystem
            services.AddSingleton<AppiumBootstrapInstaller.Plugins.PluginRegistry>();
            services.AddSingleton<AppiumBootstrapInstaller.Plugins.IPluginOrchestrator, AppiumBootstrapInstaller.Plugins.PluginOrchestrator>();
            // Service definition generator (writes systemd/supervisor templates)
            services.AddSingleton<AppiumBootstrapInstaller.Plugins.ServiceDefinitionGenerator>();
            // Device event trigger listens for device connect/disconnect and starts plugins
            services.AddSingleton<AppiumBootstrapInstaller.Plugins.Triggers.DeviceEventTrigger>();

            return config;
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

                    case "--warnings-json":
                        options.WarningsJson = true;
                        break;

                    case "--warnings-file":
                        if (i + 1 < args.Length)
                        {
                            options.WarningsFile = args[++i];
                        }
                        else
                        {
                            throw new ArgumentException("--warnings-file requires a file path");
                        }
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
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo? dirInfo = new DirectoryInfo(currentDir);

            while (dirInfo != null)
            {
                string platformPath = Path.Combine(dirInfo.FullName, "Platform");
                if (Directory.Exists(platformPath))
                {
                    return platformPath;
                }
                dirInfo = dirInfo.Parent;
            }

            throw new DirectoryNotFoundException(
                "Platform scripts directory not found. \n" +
                "The application searches for a 'Platform' directory in the current directory and all parent directories.\n" +
                "Please ensure the Platform folder is available relative to the executable."
            );
        }
    }
}
