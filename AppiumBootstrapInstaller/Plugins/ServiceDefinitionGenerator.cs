using System;
using System.IO;
using System.Linq;
using System.Text;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins
{
    /// <summary>
    /// Generates service manager unit files (systemd and supervisor) for plugin definitions.
    /// These files are intended as templates/operators can install them if they choose.
    /// This generator also writes a helper `install-generated-services.sh` script that copies units
    /// into system locations and reloads systemd/supervisor. Generated files are written under
    /// `generated-services/systemd` and `generated-services/supervisor`.
    /// </summary>
    public class ServiceDefinitionGenerator
    {
        private readonly string _outputRoot;

        public ServiceDefinitionGenerator()
        {
            _outputRoot = Path.Combine(AppContext.BaseDirectory, "generated-services");
            Directory.CreateDirectory(_outputRoot);
            Directory.CreateDirectory(Path.Combine(_outputRoot, "systemd"));
            Directory.CreateDirectory(Path.Combine(_outputRoot, "supervisor"));
        }

        private string SafeId(PluginConfig cfg)
        {
            return !string.IsNullOrWhiteSpace(cfg.Id) ? cfg.Id : "plugin";
        }

        public string GenerateSystemdUnit(PluginConfig cfg, string installFolder)
        {
            var id = SafeId(cfg);
            var unitName = id + ".service";
            var path = Path.Combine(_outputRoot, "systemd", unitName);

            var ctx = new PluginContext { InstallFolder = installFolder, Variables = new System.Collections.Generic.Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) };
            ctx.Variables["installFolder"] = installFolder;

            var exe = TemplateResolver.Expand(cfg.Executable, ctx) ?? cfg.Executable ?? string.Empty;
            var argsList = TemplateResolver.ExpandList(cfg.Arguments, ctx) ?? new System.Collections.Generic.List<string>();
            var command = string.IsNullOrWhiteSpace(argsList.Any() ? string.Join(' ', argsList) : string.Empty) ? exe : exe + " " + string.Join(' ', argsList);

            var sb = new StringBuilder();
            sb.AppendLine("[Unit]");
            sb.AppendLine($"Description=Appium Plugin - {id}");
            sb.AppendLine("After=network.target");
            sb.AppendLine();
            sb.AppendLine("[Service]");
            sb.AppendLine("Type=simple");
            sb.AppendLine($"ExecStart={command}");
            sb.AppendLine("Restart=on-failure");
            sb.AppendLine($"RestartSec={Math.Max(1, cfg.HealthCheckIntervalSeconds ?? 5)}");
            sb.AppendLine();
            sb.AppendLine("[Install]");
            sb.AppendLine("WantedBy=multi-user.target");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        public string GenerateSupervisorConf(PluginConfig cfg, string installFolder)
        {
            var id = SafeId(cfg);
            var confName = id + ".conf";
            var path = Path.Combine(_outputRoot, "supervisor", confName);

            var ctx = new PluginContext { InstallFolder = installFolder, Variables = new System.Collections.Generic.Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) };
            ctx.Variables["installFolder"] = installFolder;

            var exe = TemplateResolver.Expand(cfg.Executable, ctx) ?? cfg.Executable ?? string.Empty;
            var argsList = TemplateResolver.ExpandList(cfg.Arguments, ctx) ?? new System.Collections.Generic.List<string>();
            var command = string.IsNullOrWhiteSpace(argsList.Any() ? string.Join(' ', argsList) : string.Empty) ? exe : exe + " " + string.Join(' ', argsList);

            var stdoutLog = Path.Combine("/var/log", id + ".log");
            var stderrLog = Path.Combine("/var/log", id + ".err.log");

            var sb = new StringBuilder();
            sb.AppendLine($"[program:{id}]");
            sb.AppendLine($"command={command}");
            sb.AppendLine("autostart=true");
            sb.AppendLine("autorestart=true");
            if (!string.IsNullOrWhiteSpace(cfg.WorkingDirectory))
            {
                sb.AppendLine($"directory={cfg.WorkingDirectory}");
            }
            if (cfg.EnvironmentVariables != null && cfg.EnvironmentVariables.Count > 0)
            {
                var envVars = string.Join(",", cfg.EnvironmentVariables.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
                sb.AppendLine($"environment={envVars}");
            }
            sb.AppendLine($"stdout_logfile={stdoutLog}");
            sb.AppendLine($"stderr_logfile={stderrLog}");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        /// <summary>
        /// Generate templates for all provided plugins and return list of generated files.
        /// Also writes `install-generated-services.sh` helper script in the output root.
        /// </summary>
        public string[] GenerateAll(System.Collections.Generic.IEnumerable<PluginConfig> plugins, string installFolder)
        {
            var outputs = new System.Collections.Generic.List<string>();
            var serviceNames = new System.Collections.Generic.List<string>();
            foreach (var p in plugins)
            {
                try
                {
                    outputs.Add(GenerateSystemdUnit(p, installFolder));
                    outputs.Add(GenerateSupervisorConf(p, installFolder));
                    serviceNames.Add(SafeId(p) + ".service");
                }
                catch
                {
                    // ignore per-plugin errors
                }
            }

            // Write helper install script
            var installScriptPath = Path.Combine(_outputRoot, "install-generated-services.sh");
            var installSb = new StringBuilder();
            installSb.AppendLine("#!/bin/bash");
            installSb.AppendLine("set -euo pipefail");
            installSb.AppendLine("GEN_DIR=\"$(cd \"$(dirname \"$0\")\" && pwd)\"");
            installSb.AppendLine("echo \"Installing generated services from $GEN_DIR\"");

            if (serviceNames.Count == 0)
            {
                installSb.AppendLine("echo \"No plugin services to install\"");
            }
            else
            {
                installSb.AppendLine($"echo \"Installing {serviceNames.Count} plugin service(s): {string.Join(", ", serviceNames)}\"");
            }
            installSb.AppendLine();

            installSb.AppendLine("if command -v systemctl >/dev/null 2>&1; then");
            installSb.AppendLine("  echo \"Installing systemd unit files...\"");
            installSb.AppendLine("  sudo cp \"$GEN_DIR/systemd/*.service\" /etc/systemd/system/ || true");
            installSb.AppendLine("  sudo systemctl daemon-reload || true");
            installSb.AppendLine("  for f in \"$GEN_DIR/systemd/" + "*.service\"; do\n    sv=$(basename \"$f\")\n    echo \"Enabling $sv\"\n    sudo systemctl enable \"$sv\" || true\n    sudo systemctl start \"$sv\" || true\n  done");
            installSb.AppendLine("fi\n");
            installSb.AppendLine("if command -v supervisorctl >/dev/null 2>&1; then");
            installSb.AppendLine("  echo \"Installing supervisor configs...\"");
            installSb.AppendLine("  sudo cp \"$GEN_DIR/supervisor/*.conf\" /etc/supervisor/conf.d/ || true");
            installSb.AppendLine("  sudo supervisorctl update || true");
            installSb.AppendLine("fi\n");
            installSb.AppendLine("echo \"Install complete\"");

            File.WriteAllText(installScriptPath, installSb.ToString());
            try { File.SetAttributes(installScriptPath, File.GetAttributes(installScriptPath) | FileAttributes.Normal); } catch { }
            outputs.Add(installScriptPath);

            // Also emit a PowerShell helper that describes what to do on Windows
            var pwScript = Path.Combine(_outputRoot, "install-generated-services.ps1");
            var pw = new StringBuilder();
            pw.AppendLine("Write-Host 'This repository generated service templates for Linux (systemd/supervisor).'\n");
            pw.AppendLine("Write-Host 'On Windows, review the generated service definitions and create equivalent services (NSSM or sc.exe).'");
            File.WriteAllText(pwScript, pw.ToString());
            outputs.Add(pwScript);

            return outputs.ToArray();
        }
    }
}
