using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Plugins.BuiltIn
{
    public class ProcessPlugin : AppiumBootstrapInstaller.Plugins.PluginBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger<ProcessPlugin> _logger;
        private readonly AppiumBootstrapInstaller.Models.PluginConfig _config;
        private AppiumBootstrapInstaller.Plugins.PluginContext? _startContext;
        private Process? _process;

        public ProcessPlugin(string id, AppiumBootstrapInstaller.Models.PluginConfig config, Microsoft.Extensions.Logging.ILogger<ProcessPlugin> logger)
        {
            Id = id;
            Type = "process";
            _config = config;
            _logger = logger;
            // populate base Config for introspection
            this.Config = config;
        }

        public override Task<bool> CheckHealthAsync()
        {
            // If a custom health-check command is configured, execute it and consider exit code 0 as healthy.
            try
            {
                if (!string.IsNullOrWhiteSpace(_config.HealthCheckCommand))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _config.HealthCheckCommand!,
                        Arguments = _config.HealthCheckArguments != null ? string.Join(' ', _config.HealthCheckArguments) : string.Empty,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null)
                        return Task.FromResult(false);

                    int timeoutSeconds = _config.HealthCheckTimeoutSeconds ?? _startContext?.HealthCheckTimeoutSeconds ?? 5;
                    int timeoutMs = Math.Max(100, timeoutSeconds * 1000);
                    bool exited = proc.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { proc.Kill(true); } catch { }
                        return Task.FromResult(false);
                    }

                    return Task.FromResult(proc.ExitCode == 0);
                }
            }
            catch (Exception)
            {
                // fall through to process-based check
            }

            var healthy = _process != null && !_process.HasExited;
            return Task.FromResult(healthy);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _logger.LogInformation("Killing process for plugin {PluginId}", Id);
                    _process.Kill(true);
                    _process.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping process plugin {PluginId}", Id);
            }

            State = AppiumBootstrapInstaller.Plugins.PluginState.Stopped;
            return Task.CompletedTask;
        }

        public override Task<bool> StartAsync(AppiumBootstrapInstaller.Plugins.PluginContext context, CancellationToken cancellationToken)
        {
            _startContext = context;

            try
            {
                var cfg = _config;
                if (cfg == null || string.IsNullOrWhiteSpace(cfg.Executable))
                {
                    _logger.LogWarning("ProcessPlugin {PluginId} has no executable configured", Id);
                    return Task.FromResult(false);
                }
                // Expand templates in executable, arguments and environment variables
                var exe = TemplateResolver.Expand(cfg.Executable, context) ?? cfg.Executable;
                var argsList = TemplateResolver.ExpandList(cfg.Arguments ?? new List<string>(), context) ?? new List<string>();
                var workingDir = TemplateResolver.Expand(cfg.WorkingDirectory ?? context.InstallFolder, context) ?? context.InstallFolder;

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = argsList != null ? string.Join(' ', argsList) : string.Empty,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (cfg.EnvironmentVariables != null)
                {
                    var env = TemplateResolver.ExpandDictionary(cfg.EnvironmentVariables, context);
                    foreach (var kv in env)
                    {
                        psi.Environment[kv.Key] = kv.Value;
                    }
                }

                _process = Process.Start(psi);
                if (_process != null)
                {
                    _process.OutputDataReceived += (s, e) => { if (e.Data != null) _logger.LogInformation("[{Plugin}] {Line}", Id, e.Data); };
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();
                }

                State = AppiumBootstrapInstaller.Plugins.PluginState.Running;
                _logger.LogInformation("ProcessPlugin {PluginId} started (pid={Pid})", Id, _process?.Id);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start ProcessPlugin {PluginId}", Id);
                State = AppiumBootstrapInstaller.Plugins.PluginState.Error;
                return Task.FromResult(false);
            }
        }
    }
}
