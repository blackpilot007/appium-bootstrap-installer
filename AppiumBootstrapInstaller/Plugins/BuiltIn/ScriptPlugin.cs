using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins.BuiltIn
{
    public class ScriptPlugin : AppiumBootstrapInstaller.Plugins.PluginBase
    {
        private readonly ILogger<ScriptPlugin> _logger;
        private readonly PluginConfig _config;
        private Process? _process;
        private AppiumBootstrapInstaller.Plugins.PluginContext? _startContext;

        public ScriptPlugin(string id, PluginConfig config, ILogger<ScriptPlugin> logger)
        {
            Id = id;
            Type = "script";
            _config = config;
            _logger = logger;
            this.Config = config;
        }

        public override Task<bool> CheckHealthAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_config.HealthCheckCommand))
                {
                    var cmd = _config.HealthCheckCommand!;
                    var args = _config.HealthCheckArguments != null ? string.Join(' ', _config.HealthCheckArguments) : string.Empty;

                    string fileName = cmd;
                    string arguments = args;

                    // If runtime hint is bash, run via bash -c
                    if (!string.IsNullOrEmpty(_config.HealthCheckRuntime) && _config.HealthCheckRuntime.Equals("bash", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = "bash";
                        arguments = $"-c \"{cmd} {args}\"";
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
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
                // fall through
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
                    _logger.LogInformation("Stopping script plugin {PluginId}", Id);
                    _process.Kill(true);
                    _process.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping script plugin {PluginId}", Id);
            }

            State = AppiumBootstrapInstaller.Plugins.PluginState.Stopped;
            return Task.CompletedTask;
        }

        public override Task<bool> StartAsync(AppiumBootstrapInstaller.Plugins.PluginContext context, CancellationToken cancellationToken)
        {
            _startContext = context;

            try
            {
                if (string.IsNullOrWhiteSpace(_config.Executable))
                {
                    _logger.LogWarning("ScriptPlugin {PluginId} has no script path configured", Id);
                    return Task.FromResult(false);
                }

                // Expand templates in executable, arguments and environment variables
                var exe = TemplateResolver.Expand(_config.Executable, context) ?? _config.Executable;
                var argsList = TemplateResolver.ExpandList(_config.Arguments ?? new System.Collections.Generic.List<string>(), context) ?? new System.Collections.Generic.List<string>();
                var workingDir = TemplateResolver.Expand(_config.WorkingDirectory ?? context.InstallFolder, context) ?? context.InstallFolder;

                // Determine runtime hint
                var runtimeHint = _config.Runtime;
                if (string.IsNullOrEmpty(runtimeHint) && _config.EnvironmentVariables != null && _config.EnvironmentVariables.TryGetValue("runtime", out var r)) runtimeHint = r;
                if (string.IsNullOrEmpty(runtimeHint) && !string.IsNullOrEmpty(exe) && exe.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)) runtimeHint = "powershell";

                string fileName;
                string arguments;

                if (!string.IsNullOrEmpty(runtimeHint) && runtimeHint.Equals("bash", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = "bash";
                    arguments = $"{exe} {string.Join(' ', argsList)}".Trim();
                }
                else
                {
                    fileName = "powershell.exe";
                    arguments = $"-ExecutionPolicy Bypass -File \"{exe}\" {string.Join(' ', argsList)}".Trim();
                }

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (_config.EnvironmentVariables != null)
                {
                    var env = TemplateResolver.ExpandDictionary(_config.EnvironmentVariables, context);
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
                _logger.LogInformation("ScriptPlugin {PluginId} started (pid={Pid})", Id, _process?.Id);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start ScriptPlugin {PluginId}", Id);
                State = AppiumBootstrapInstaller.Plugins.PluginState.Error;
                return Task.FromResult(false);
            }
        }
    }
}
