# Plugin Architecture: Quick Start Implementation Guide

## Overview
This guide provides a **step-by-step path** to implement the plugin system incrementally, allowing you to validate each component before moving to the next.

---

## Phase 1: Foundation (Week 1) - Core Interfaces

### Step 1.1: Create Plugin Models
**File:** `AppiumBootstrapInstaller/Models/PluginConfig.cs`

```csharp
using System.Text.Json.Serialization;

namespace AppiumBootstrapInstaller.Models;

public class PluginSystemConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("configPath")]
    public string ConfigPath { get; set; } = "./plugins.json";

    [JsonPropertyName("hotReloadEnabled")]
    public bool HotReloadEnabled { get; set; } = false;

    [JsonPropertyName("hotReloadIntervalSeconds")]
    public int HotReloadIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("pluginDirectory")]
    public string PluginDirectory { get; set; } = string.Empty;

    [JsonPropertyName("maxConcurrentPlugins")]
    public int MaxConcurrentPlugins { get; set; } = 50;
}

public class PluginConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("dependsOn")]
    public List<string> DependsOn { get; set; } = new();

    [JsonPropertyName("triggerOn")]
    public string TriggerOn { get; set; } = "startup";

    [JsonPropertyName("restartPolicy")]
    public string RestartPolicy { get; set; } = "on-failure";

    [JsonPropertyName("startDelaySeconds")]
    public int StartDelaySeconds { get; set; } = 0;

    [JsonPropertyName("stopTimeoutSeconds")]
    public int StopTimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("environmentVariables")]
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    [JsonPropertyName("healthCheck")]
    public HealthCheckConfig? HealthCheck { get; set; }

    [JsonPropertyName("logging")]
    public PluginLoggingConfig? Logging { get; set; }

    // Process plugin specific
    [JsonPropertyName("executable")]
    public string? Executable { get; set; }

    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = new();

    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; set; }

    // Script plugin specific
    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    [JsonPropertyName("script")]
    public string? Script { get; set; }

    // HTTP plugin specific
    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("handler")]
    public string? Handler { get; set; }

    // Pipeline plugin specific
    [JsonPropertyName("steps")]
    public List<PipelineStepConfig> Steps { get; set; } = new();
}

public class HealthCheckConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "none";

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("httpEndpoint")]
    public string? HttpEndpoint { get; set; }

    [JsonPropertyName("intervalSeconds")]
    public int IntervalSeconds { get; set; } = 30;

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 5;

    [JsonPropertyName("retries")]
    public int Retries { get; set; } = 3;
}

public class PluginLoggingConfig
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "info";

    [JsonPropertyName("outputPath")]
    public string? OutputPath { get; set; }

    [JsonPropertyName("maxFileSizeMB")]
    public int MaxFileSizeMB { get; set; } = 10;

    [JsonPropertyName("maxFiles")]
    public int MaxFiles { get; set; } = 5;
}

public class PipelineStepConfig
{
    [JsonPropertyName("plugin")]
    public string Plugin { get; set; } = string.Empty;

    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; } = false;
}

public enum PluginState
{
    Disabled,
    Starting,
    Running,
    Stopping,
    Stopped,
    Failed
}

public class PluginStartResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public static PluginStartResult Success() => new() { IsSuccess = true };
    public static PluginStartResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
}

public class PluginHealthStatus
{
    public bool IsHealthy { get; set; }
    public string? Message { get; set; }

    public static PluginHealthStatus Healthy() => new() { IsHealthy = true };
    public static PluginHealthStatus Unhealthy(string message) => new() { IsHealthy = false, Message = message };
}
```

### Step 1.2: Create Plugin Interfaces
**File:** `AppiumBootstrapInstaller/Plugins/IPlugin.cs`

```csharp
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins;

public interface IPlugin
{
    string Id { get; }
    string Type { get; }
    PluginState State { get; }

    Task<PluginStartResult> StartAsync(PluginContext context, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<PluginHealthStatus> CheckHealthAsync();

    event EventHandler<PluginStateChangedEventArgs>? StateChanged;
}

public class PluginContext
{
    public required PluginConfig Config { get; set; }
    public required IServiceProvider Services { get; set; }
    public required ILogger Logger { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public required string InstallFolder { get; set; }
    public CancellationToken CancellationToken { get; set; }
    
    // NEW: Runtime context for accessing live system state
    public IAppiumRuntimeContext? Runtime { get; set; }
}

/// <summary>
/// Provides plugins access to runtime Appium infrastructure state
/// </summary>
public interface IAppiumRuntimeContext
{
    // Device information
    IReadOnlyList<Device> GetConnectedDevices();
    Device? GetDevice(string deviceId);
    IReadOnlyList<Device> GetDevicesByPlatform(DevicePlatform platform);
    
    // Appium session information
    IReadOnlyList<AppiumSession> GetActiveSessions();
    AppiumSession? GetSessionForDevice(string deviceId);
    IReadOnlyDictionary<string, AppiumSession> GetAllSessions();
    
    // Port allocations
    IReadOnlyList<int> GetAllocatedPorts();
    IReadOnlyList<int> GetAvailablePorts(int count);
    bool IsPortInUse(int port);
    
    // Service status
    ServiceHealthStatus GetServiceHealth();
    TimeSpan GetUptime();
    
    // Metrics
    DeviceMetrics GetMetrics();
    
    // Event subscription (for reactive plugins)
    void SubscribeToDeviceEvents(Action<DeviceEventArgs> handler);
    void SubscribeToSessionEvents(Action<SessionEventArgs> handler);
    void UnsubscribeFromDeviceEvents(Action<DeviceEventArgs> handler);
    void UnsubscribeFromSessionEvents(Action<SessionEventArgs> handler);
}

public class ServiceHealthStatus
{
    public bool IsHealthy { get; set; }
    public int ConnectedDevices { get; set; }
    public int ActiveSessions { get; set; }
    public int RunningPlugins { get; set; }
    public Dictionary<string, string> ComponentStatus { get; set; } = new();
}

public class DeviceEventArgs : EventArgs
{
    public required Device Device { get; set; }
    public string EventType { get; set; } = string.Empty; // "connected", "disconnected"
}

public class SessionEventArgs : EventArgs
{
    public required AppiumSession Session { get; set; }
    public required Device Device { get; set; }
    public string EventType { get; set; } = string.Empty; // "started", "stopped", "failed"
}

public class PluginStateChangedEventArgs : EventArgs
{
    public PluginState OldState { get; set; }
    public PluginState NewState { get; set; }
    public string? Message { get; set; }
}
```

### Step 1.3: Create Base Plugin Implementation
**File:** `AppiumBootstrapInstaller/Plugins/PluginBase.cs`

```csharp
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Plugins;

public abstract class PluginBase : IPlugin
{
    protected ILogger Logger { get; private set; } = null!;
    protected PluginConfig Config { get; private set; } = null!;
    protected string InstallFolder { get; private set; } = string.Empty;

    public string Id => Config.Id;
    public string Type => Config.Type;
    public PluginState State { get; protected set; } = PluginState.Disabled;

    public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

    public abstract Task<PluginStartResult> StartAsync(PluginContext context, CancellationToken cancellationToken);
    public abstract Task StopAsync(CancellationToken cancellationToken);
    public abstract Task<PluginHealthStatus> CheckHealthAsync();

    protected void Initialize(PluginContext context)
    {
        Logger = context.Logger;
        Config = context.Config;
        InstallFolder = context.InstallFolder;
    }

    protected void ChangeState(PluginState newState, string? message = null)
    {
        var oldState = State;
        State = newState;
        
        Logger.LogInformation("Plugin {PluginId} state changed: {OldState} -> {NewState}", Id, oldState, newState);
        
        StateChanged?.Invoke(this, new PluginStateChangedEventArgs
        {
            OldState = oldState,
            NewState = newState,
            Message = message
        });
    }

    protected string ExpandVariables(string? input, PluginContext context)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var result = input;
        
        // Expand InstallFolder
        result = result.Replace("${INSTALL_FOLDER}", context.InstallFolder, StringComparison.OrdinalIgnoreCase);
        
        // Expand custom variables
        foreach (var kvp in context.Variables)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        
        // Expand environment variables
        result = Environment.ExpandEnvironmentVariables(result);
        
        return result;
    }

    protected async Task<PluginHealthStatus> CheckPortHealthAsync(int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            return PluginHealthStatus.Healthy();
        }
        catch
        {
            return PluginHealthStatus.Unhealthy($"Port {port} is not accessible");
        }
    }
}
```

---

## Phase 2: Process Plugin (Week 2) - First Plugin Type

### Step 2.1: Implement ProcessPlugin
**File:** `AppiumBootstrapInstaller/Plugins/BuiltIn/ProcessPlugin.cs`

```csharp
using System.Diagnostics;
using System.Text;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins.BuiltIn;

public class ProcessPlugin : PluginBase
{
    private Process? _process;
    private readonly SemaphoreSlim _processLock = new(1, 1);

    public override async Task<PluginStartResult> StartAsync(PluginContext context, CancellationToken cancellationToken)
    {
        Initialize(context);
        ChangeState(PluginState.Starting);

        try
        {
            if (string.IsNullOrEmpty(Config.Executable))
            {
                return PluginStartResult.Failure("Executable path is required for process plugin");
            }

            var executable = ExpandVariables(Config.Executable, context);
            var workingDir = ExpandVariables(Config.WorkingDirectory, context) ?? Path.GetDirectoryName(executable);

            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = string.Join(" ", Config.Arguments.Select(a => ExpandVariables(a, context))),
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Set environment variables
            foreach (var kvp in Config.EnvironmentVariables)
            {
                psi.EnvironmentVariables[kvp.Key] = ExpandVariables(kvp.Value, context);
            }

            // Force UTF-8
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            Logger.LogInformation("Starting plugin {PluginId}: {Executable} {Arguments}", 
                Id, executable, psi.Arguments);

            _process = Process.Start(psi);

            if (_process == null)
            {
                return PluginStartResult.Failure("Failed to start process");
            }

            // Attach logging
            AttachLogging(_process);

            // Monitor for early exit
            _ = MonitorProcessAsync(cancellationToken);

            // Wait for startup delay
            if (Config.StartDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(Config.StartDelaySeconds), cancellationToken);
            }

            ChangeState(PluginState.Running);
            return PluginStartResult.Success();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start plugin {PluginId}", Id);
            ChangeState(PluginState.Failed, ex.Message);
            return PluginStartResult.Failure(ex.Message);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processLock.WaitAsync(cancellationToken);
        try
        {
            if (_process == null || _process.HasExited)
            {
                ChangeState(PluginState.Stopped);
                return;
            }

            ChangeState(PluginState.Stopping);

            Logger.LogInformation("Stopping plugin {PluginId} (PID: {Pid})", Id, _process.Id);

            // Try graceful shutdown first
            try
            {
                _process.Kill(entireProcessTree: false);
                await _process.WaitForExitAsync(cancellationToken);
            }
            catch
            {
                // Force kill if graceful fails
                _process.Kill(entireProcessTree: true);
            }

            ChangeState(PluginState.Stopped);
        }
        finally
        {
            _processLock.Release();
        }
    }

    public override async Task<PluginHealthStatus> CheckHealthAsync()
    {
        if (_process == null || _process.HasExited)
        {
            return PluginHealthStatus.Unhealthy("Process is not running");
        }

        // Port-based health check
        if (Config.HealthCheck?.Type == "port" && Config.HealthCheck.Port.HasValue)
        {
            return await CheckPortHealthAsync(Config.HealthCheck.Port.Value);
        }

        return PluginHealthStatus.Healthy();
    }

    private void AttachLogging(Process process)
    {
        var logPath = Config.Logging?.OutputPath ?? 
            Path.Combine(InstallFolder, "logs", "plugins", $"{Id}.log");
        
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.LogInformation("[{PluginId}] {Output}", Id, e.Data);
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [OUT] {e.Data}{Environment.NewLine}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.LogWarning("[{PluginId}] {Error}", Id, e.Data);
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERR] {e.Data}{Environment.NewLine}");
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private async Task MonitorProcessAsync(CancellationToken cancellationToken)
    {
        if (_process == null) return;

        await _process.WaitForExitAsync(cancellationToken);

        if (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogWarning("Plugin {PluginId} process exited unexpectedly with code {ExitCode}", 
                Id, _process.ExitCode);
            
            ChangeState(PluginState.Failed, $"Process exited with code {_process.ExitCode}");

            // Handle restart policy
            if (Config.RestartPolicy == "always" || 
                (Config.RestartPolicy == "on-failure" && _process.ExitCode != 0))
            {
                Logger.LogInformation("Restarting plugin {PluginId} due to restart policy", Id);
                await Task.Delay(5000, cancellationToken); // Wait 5s before restart
                // TODO: Trigger restart via orchestrator
            }
        }
    }
}
```

---

## Phase 3: Plugin Orchestrator (Week 3) - Lifecycle Management

### Step 3.1: Plugin Registry
**File:** `AppiumBootstrapInstaller/Plugins/PluginRegistry.cs`

```csharp
using System.Collections.Concurrent;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins.BuiltIn;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Plugins;

public class PluginRegistry
{
    private readonly ILogger<PluginRegistry> _logger;
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<string, IPlugin> _plugins = new();
    private readonly Dictionary<string, Func<IPlugin>> _pluginFactories = new();

    public PluginRegistry(ILogger<PluginRegistry> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;

        // Register built-in plugin types
        RegisterPluginType("process", () => new ProcessPlugin());
        // Future: RegisterPluginType("script", () => new ScriptPlugin());
        // Future: RegisterPluginType("http", () => new HttpPlugin());
        // Future: RegisterPluginType("pipeline", () => new PipelinePlugin());
    }

    public void RegisterPluginType(string type, Func<IPlugin> factory)
    {
        _pluginFactories[type.ToLowerInvariant()] = factory;
        _logger.LogInformation("Registered plugin type: {Type}", type);
    }

    public IPlugin CreatePlugin(PluginConfig config)
    {
        var type = config.Type.ToLowerInvariant();
        
        if (!_pluginFactories.TryGetValue(type, out var factory))
        {
            throw new InvalidOperationException($"Unknown plugin type: {config.Type}");
        }

        var plugin = factory();
        _plugins[config.Id] = plugin;
        
        _logger.LogInformation("Created plugin {PluginId} of type {Type}", config.Id, config.Type);
        return plugin;
    }

    public IPlugin? GetPlugin(string pluginId)
    {
        _plugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    public IEnumerable<IPlugin> GetAllPlugins() => _plugins.Values;

    public bool RemovePlugin(string pluginId)
    {
        return _plugins.TryRemove(pluginId, out _);
    }
}
```

### Step 3.2: Plugin Orchestrator
**File:** `AppiumBootstrapInstaller/Plugins/PluginOrchestrator.cs`

```csharp
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Plugins;

public class PluginOrchestrator
{
    private readonly ILogger<PluginOrchestrator> _logger;
    private readonly PluginRegistry _registry;
    private readonly string _installFolder;
    private readonly IServiceProvider _services;

    public PluginOrchestrator(
        ILogger<PluginOrchestrator> logger,
        PluginRegistry registry,
        string installFolder,
        IServiceProvider services)
    {
        _logger = logger;
        _registry = registry;
        _installFolder = installFolder;
        _services = services;
    }

    public async Task<List<IPlugin>> StartPluginsAsync(
        IEnumerable<PluginConfig> configs,
        CancellationToken cancellationToken)
    {
        var startedPlugins = new List<IPlugin>();

        try
        {
            // Build dependency graph
            var sortedConfigs = TopologicalSort(configs.ToList());

            _logger.LogInformation("Starting {Count} plugins in dependency order", sortedConfigs.Count);

            foreach (var config in sortedConfigs)
            {
                if (!config.Enabled)
                {
                    _logger.LogDebug("Skipping disabled plugin {PluginId}", config.Id);
                    continue;
                }

                var plugin = _registry.GetPlugin(config.Id) ?? _registry.CreatePlugin(config);
                
                var context = new PluginContext
                {
                    Config = config,
                    Services = _services,
                    Logger = _logger,
                    InstallFolder = _installFolder,
                    CancellationToken = cancellationToken
                };

                var result = await plugin.StartAsync(context, cancellationToken);

                if (result.IsSuccess)
                {
                    startedPlugins.Add(plugin);
                    _logger.LogInformation("✅ Plugin {PluginId} started successfully", config.Id);
                }
                else
                {
                    _logger.LogError("❌ Failed to start plugin {PluginId}: {Error}", 
                        config.Id, result.ErrorMessage);
                    throw new PluginStartException($"Plugin {config.Id} failed to start: {result.ErrorMessage}");
                }
            }

            return startedPlugins;
        }
        catch
        {
            // Rollback: stop all started plugins
            _logger.LogWarning("Rolling back: stopping {Count} started plugins", startedPlugins.Count);
            await StopPluginsAsync(startedPlugins, cancellationToken);
            throw;
        }
    }

    public async Task StopPluginsAsync(IEnumerable<IPlugin> plugins, CancellationToken cancellationToken)
    {
        var pluginList = plugins.Reverse().ToList(); // Stop in reverse order

        foreach (var plugin in pluginList)
        {
            try
            {
                await plugin.StopAsync(cancellationToken);
                _logger.LogInformation("Stopped plugin {PluginId}", plugin.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping plugin {PluginId}", plugin.Id);
            }
        }
    }

    private List<PluginConfig> TopologicalSort(List<PluginConfig> configs)
    {
        var graph = configs.ToDictionary(c => c.Id, c => c.DependsOn);
        var inDegree = configs.ToDictionary(c => c.Id, c => 0);

        // Calculate in-degrees
        foreach (var deps in graph.Values)
        {
            foreach (var dep in deps)
            {
                if (inDegree.ContainsKey(dep))
                {
                    inDegree[dep]++;
                }
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var result = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            if (graph.TryGetValue(node, out var deps))
            {
                foreach (var dep in deps)
                {
                    if (inDegree.ContainsKey(dep))
                    {
                        inDegree[dep]--;
                        if (inDegree[dep] == 0)
                        {
                            queue.Enqueue(dep);
                        }
                    }
                }
            }
        }

        if (result.Count != configs.Count)
        {
            throw new CircularDependencyException("Circular dependency detected in plugin configuration");
        }

        return result.Select(id => configs.First(c => c.Id == id)).ToList();
    }
}

public class PluginStartException : Exception
{
    public PluginStartException(string message) : base(message) { }
}

public class CircularDependencyException : Exception
{
    public CircularDependencyException(string message) : base(message) { }
}
```

---

## Integration with Existing Code

### Update `InstallConfig.cs`
```csharp
[JsonPropertyName("pluginSystem")]
public PluginSystemConfig? PluginSystem { get; set; }
```

### Update `Program.cs` (simplified)
```csharp
// After device listener setup
if (config.PluginSystem?.Enabled == true)
{
    logger.LogInformation("Starting plugin system...");
    
    var pluginRegistry = new PluginRegistry(
        serviceProvider.GetRequiredService<ILogger<PluginRegistry>>(),
        serviceProvider
    );
    
    var pluginOrchestrator = new PluginOrchestrator(
        serviceProvider.GetRequiredService<ILogger<PluginOrchestrator>>(),
        pluginRegistry,
        config.InstallFolder,
        serviceProvider
    );

    // Load plugin configs
    var pluginConfigs = LoadPluginConfigs(config.PluginSystem.ConfigPath);
    
    // Start plugins
    var startedPlugins = await pluginOrchestrator.StartPluginsAsync(pluginConfigs, cts.Token);
    logger.LogInformation("Started {Count} plugins", startedPlugins.Count);
}
```

---

## Sample Configuration

### `config.json` (add to existing)
```json
{
  "pluginSystem": {
    "enabled": true,
    "configPath": "./plugins.json",
    "hotReloadEnabled": false,
    "maxConcurrentPlugins": 50
  }
}
```

### `plugins.json` (new file)
```json
{
  "plugins": [
    {
      "id": "custom-monitor",
      "type": "process",
      "enabled": true,
      "displayName": "Custom Device Monitor",
      "executable": "${INSTALL_FOLDER}/bin/custom-monitor.exe",
      "arguments": ["--config", "${INSTALL_FOLDER}/monitor-config.json"],
      "workingDirectory": "${INSTALL_FOLDER}",
      "restartPolicy": "always",
      "healthCheck": {
        "type": "port",
        "port": 9999,
        "intervalSeconds": 30
      },
      "logging": {
        "level": "info",
        "outputPath": "${INSTALL_FOLDER}/logs/plugins/custom-monitor.log"
      }
    }
  ]
}
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task ProcessPlugin_StartsSuccessfully()
{
    var config = new PluginConfig
    {
        Id = "test-plugin",
        Type = "process",
        Executable = "cmd.exe",
        Arguments = new List<string> { "/c", "timeout", "10" }
    };
    
    var plugin = new ProcessPlugin();
    var context = CreateTestContext(config);
    
    var result = await plugin.StartAsync(context, CancellationToken.None);
    
    Assert.True(result.IsSuccess);
    Assert.Equal(PluginState.Running, plugin.State);
    
    await plugin.StopAsync(CancellationToken.None);
}
```

### Integration Test
```bash
# Create test plugin executable
echo "sleep 60" > test-plugin.sh
chmod +x test-plugin.sh

# Test config
cat > plugins.json <<EOF
{
  "plugins": [
    {
      "id": "test",
      "type": "process",
      "enabled": true,
      "executable": "./test-plugin.sh"
    }
  ]
}
EOF

# Run installer
./AppiumBootstrapInstaller
```

---

## Next Steps

1. **Implement Phase 1**: Core interfaces and models (1-2 days)
2. **Implement Phase 2**: ProcessPlugin (2-3 days)
3. **Implement Phase 3**: PluginOrchestrator with dependency resolution (3-4 days)
4. **Test & Validate**: End-to-end testing with real plugins (2-3 days)
5. **Documentation**: Update README and USER_GUIDE (1 day)

**Total Estimated Time**: 2-3 weeks for basic working implementation.

---

## Questions?

Let me know which phase you'd like to start with, or if you'd like me to:
- Create a minimal proof-of-concept implementation right now
- Focus on a specific plugin type first
- Prioritize service manager integration over portable mode
