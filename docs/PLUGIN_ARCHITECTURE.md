# Plugin Architecture Design

## Executive Summary

This document proposes a **modern, extensible plugin architecture** for the Appium Bootstrap Installer that enables:
- Custom service orchestration (executables, scripts, HTTP endpoints)
- Cross-platform plugin system (Windows/Linux/macOS)
- Sequential and parallel plugin execution
- Declarative configuration with minimal code changes
- Hot-reload and runtime plugin management

## Design Principles

1. **Configuration-Driven**: All plugins defined in JSON/YAML
2. **Platform-Agnostic**: Unified API across Windows, macOS, Linux
3. **Process-First**: Use child processes (portable mode) by default; optional service manager integration
4. **Composable**: Plugins can depend on other plugins or run independently
5. **Observable**: Rich logging, metrics, and health checks
6. **Zero-Downtime**: Hot-reload plugins without restarting the main process

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Appium Bootstrap Installer                   │
│                     (Existing C# .NET 8 App)                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │            Plugin Orchestrator (New)                       │ │
│  │  - Plugin Lifecycle Manager                                │ │
│  │  - Dependency Resolver (DAG)                               │ │
│  │  - Execution Engine (Sequential/Parallel)                  │ │
│  │  - Health Monitor & Auto-Restart                           │ │
│  └───────────────────────────────────────────────────────────┘ │
│                          ▲                                      │
│                          │                                      │
│  ┌───────────────────────┴───────────────────────────────────┐ │
│  │         Plugin Configuration Loader                        │ │
│  │  - JSON/YAML Schema Validation                             │ │
│  │  - Environment Variable Expansion                          │ │
│  │  - Hot-Reload Support                                      │ │
│  └────────────────────────────────────────────────────────────┘ │
│                          ▲                                      │
│                          │                                      │
│  ┌───────────────────────┴───────────────────────────────────┐ │
│  │              Plugin Registry                               │ │
│  │  - Built-in Plugins (Process, HTTP, Script)               │ │
│  │  - Custom User Plugins (Dynamically Loaded)               │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
                          ▲
                          │
         ┌────────────────┴────────────────┐
         │                                  │
┌────────▼────────┐              ┌─────────▼──────────┐
│   config.json   │              │  plugins.json      │
│  (Existing)     │              │  (New)             │
└─────────────────┘              └────────────────────┘
```

---

## Plugin Types

### 1. **Process Plugin** (Execute Binaries/Scripts)
Run executables as managed child processes.

**Use Cases:**
- Custom monitoring scripts
- Log aggregators (e.g., Fluent Bit, Vector)
- Proxy servers (e.g., mitmproxy, Charles Proxy)
- Test orchestrators

**Configuration Example:**
```json
{
  "plugins": [
    {
      "id": "log-forwarder",
      "type": "process",
      "enabled": true,
      "executable": "${INSTALL_FOLDER}/bin/fluent-bit",
      "arguments": ["-c", "${INSTALL_FOLDER}/config/fluent-bit.conf"],
      "workingDirectory": "${INSTALL_FOLDER}",
      "environmentVariables": {
        "LOG_LEVEL": "info"
      },
      "restartPolicy": "always",
      "healthCheck": {
        "type": "port",
        "port": 24224,
        "intervalSeconds": 30
      }
    }
  ]
}
```

### 2. **HTTP Plugin** (REST API Services)
Manage HTTP-based services with health checks.

**Use Cases:**
- Webhook receivers
- Test result collectors
- Remote control APIs
- WebSocket servers

**Configuration Example:**
```json
{
  "plugins": [
    {
      "id": "webhook-server",
      "type": "http",
      "enabled": true,
      "port": 9090,
      "handler": "builtin:webhook",
      "routes": [
        {
          "path": "/device-connected",
          "method": "POST",
          "action": "trigger-plugin",
          "target": "custom-notification"
        }
      ],
      "cors": {
        "enabled": true,
        "allowedOrigins": ["*"]
      }
    }
  ]
}
```

### 3. **Script Plugin** (PowerShell/Bash/Python)
Execute scripts with output capture.

**Use Cases:**
- Setup/teardown scripts
- Environment validation
- Data collection
- Custom automation

**Configuration Example:**
```json
{
  "plugins": [
    {
      "id": "device-provisioner",
      "type": "script",
      "enabled": true,
      "runtime": "powershell",
      "script": "${INSTALL_FOLDER}/scripts/provision-device.ps1",
      "arguments": ["-DeviceId", "{deviceId}"],
      "triggerOn": "device-connected",
      "timeout": 300
    }
  ]
}
```

### 4. **Pipeline Plugin** (Sequential Execution)
Chain multiple plugins together.

**Use Cases:**
- Multi-step workflows
- Complex automation
- Build pipelines

**Configuration Example:**
```json
{
  "plugins": [
    {
      "id": "device-onboarding-pipeline",
      "type": "pipeline",
      "enabled": true,
      "steps": [
        {
          "plugin": "device-provisioner",
          "continueOnError": false
        },
        {
          "plugin": "appium-session-starter",
          "continueOnError": true
        },
        {
          "plugin": "send-slack-notification",
          "continueOnError": true
        }
      ],
      "triggerOn": "device-connected"
    }
  ]
}
```

---

## Plugin Lifecycle

```
┌──────────┐
│ DISABLED │
└────┬─────┘
     │ Enable (config change)
     ▼
┌──────────┐
│ STARTING │◄─────┐
└────┬─────┘      │
     │            │ Restart
     ▼            │
┌──────────┐      │
│ RUNNING  │──────┤
└────┬─────┘      │
     │            │
     ▼            │
┌──────────┐      │
│ STOPPING │──────┘
└────┬─────┘
     │
     ▼
┌──────────┐
│ STOPPED  │
└──────────┘
```

### State Transitions
- **DISABLED → STARTING**: Configuration changed to `enabled: true`
- **STARTING → RUNNING**: Process started successfully and health check passed
- **RUNNING → STOPPING**: Manual stop, config disable, or fatal error
- **STOPPING → STOPPED**: Process gracefully terminated
- **STOPPED → STARTING**: Auto-restart (if restartPolicy = "always")

---

## Configuration Schema

### Complete Plugin Configuration
```json
{
  "pluginSystem": {
    "enabled": true,
    "configPath": "./plugins.json",
    "hotReloadEnabled": true,
    "hotReloadIntervalSeconds": 30,
    "pluginDirectory": "${INSTALL_FOLDER}/plugins",
    "maxConcurrentPlugins": 50
  },
  "plugins": [
    {
      "id": "string (required) - Unique plugin identifier",
      "type": "process|http|script|pipeline (required)",
      "enabled": "boolean (default: true)",
      "displayName": "string (optional) - Human-readable name",
      "description": "string (optional)",
      "dependsOn": ["plugin-id-1", "plugin-id-2"],
      "triggerOn": "startup|device-connected|device-disconnected|manual|cron",
      "cronExpression": "string (optional) - For cron triggers",
      "restartPolicy": "never|on-failure|always (default: on-failure)",
      "startDelaySeconds": "number (default: 0)",
      "stopTimeoutSeconds": "number (default: 30)",
      "environmentVariables": {
        "KEY": "value"
      },
      "healthCheck": {
        "type": "port|http|process|none",
        "port": "number (for port/http checks)",
        "httpEndpoint": "string (for http checks)",
        "intervalSeconds": "number (default: 30)",
        "timeoutSeconds": "number (default: 5)",
        "retries": "number (default: 3)"
      },
      "metrics": {
        "enabled": "boolean (default: true)",
        "endpoint": "/metrics",
        "port": "number (optional)"
      },
      "logging": {
        "level": "debug|info|warn|error",
        "outputPath": "${INSTALL_FOLDER}/logs/plugins/{pluginId}.log",
        "maxFileSizeMB": "number (default: 10)",
        "maxFiles": "number (default: 5)"
      }
    }
  ]
}
```

---

## Implementation Plan

### Phase 1: Core Plugin Infrastructure (Week 1-2)

#### New Files
```
AppiumBootstrapInstaller/
├── Plugins/
│   ├── IPlugin.cs                      # Plugin interface
│   ├── PluginBase.cs                   # Base implementation
│   ├── PluginContext.cs                # Execution context
│   ├── PluginRegistry.cs               # Plugin discovery/registration
│   ├── PluginOrchestrator.cs           # Lifecycle & execution
│   ├── PluginHealthMonitor.cs          # Health checks & restarts
│   └── Triggers/
│       ├── ITrigger.cs
│       ├── StartupTrigger.cs
│       ├── DeviceEventTrigger.cs
│       └── CronTrigger.cs
├── Plugins/BuiltIn/
│   ├── ProcessPlugin.cs                # Execute binaries
│   ├── ScriptPlugin.cs                 # Run scripts
│   ├── HttpPlugin.cs                   # HTTP services
│   └── PipelinePlugin.cs               # Sequential execution
└── Models/
    ├── PluginConfig.cs                 # Configuration models
    └── PluginState.cs                  # Runtime state
```

#### Key Interfaces

**IPlugin.cs**
```csharp
public interface IPlugin
{
    string Id { get; }
    string Type { get; }
    PluginState State { get; }
    
    Task<PluginStartResult> StartAsync(PluginContext context, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task<PluginHealthStatus> CheckHealthAsync();
    
    event EventHandler<PluginStateChangedEventArgs> StateChanged;
}
```

**PluginContext.cs**
```csharp
public class PluginContext
{
    public PluginConfig Config { get; set; }
    public IServiceProvider Services { get; set; }
    public ILogger Logger { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public string InstallFolder { get; set; }
    public CancellationToken CancellationToken { get; set; }
}
```

### Phase 2: Built-In Plugin Types (Week 3)

#### ProcessPlugin Implementation
```csharp
public class ProcessPlugin : PluginBase
{
    private Process? _process;
    
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        var config = context.Config;
        var psi = new ProcessStartInfo
        {
            FileName = ExpandVariables(config.Executable, context),
            Arguments = string.Join(" ", config.Arguments.Select(a => ExpandVariables(a, context))),
            WorkingDirectory = ExpandVariables(config.WorkingDirectory, context),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        // Set environment variables
        foreach (var kvp in config.EnvironmentVariables)
        {
            psi.EnvironmentVariables[kvp.Key] = ExpandVariables(kvp.Value, context);
        }
        
        _process = Process.Start(psi);
        
        // Attach logging
        AttachLogging(_process, context.Logger);
        
        State = PluginState.Running;
        return PluginStartResult.Success();
    }
    
    public override async Task<PluginHealthStatus> CheckHealthAsync()
    {
        if (_process == null || _process.HasExited)
        {
            return PluginHealthStatus.Unhealthy("Process not running");
        }
        
        // Port-based health check
        if (Config.HealthCheck?.Type == "port")
        {
            return await CheckPortHealthAsync(Config.HealthCheck.Port);
        }
        
        return PluginHealthStatus.Healthy();
    }
}
```

### Phase 3: Plugin Orchestrator (Week 4)

#### Dependency Resolution (DAG)
```csharp
public class PluginOrchestrator
{
    private readonly PluginRegistry _registry;
    private readonly ILogger<PluginOrchestrator> _logger;
    private readonly ConcurrentDictionary<string, IPlugin> _runningPlugins = new();
    
    public async Task StartPluginsAsync(
        IEnumerable<string> pluginIds, 
        CancellationToken cancellationToken)
    {
        // Build dependency graph
        var graph = BuildDependencyGraph(pluginIds);
        
        // Topological sort for execution order
        var executionOrder = TopologicalSort(graph);
        
        // Start plugins in dependency order
        foreach (var pluginId in executionOrder)
        {
            var plugin = _registry.GetPlugin(pluginId);
            if (plugin == null) continue;
            
            var context = CreatePluginContext(plugin);
            var result = await plugin.StartAsync(context, cancellationToken);
            
            if (result.IsSuccess)
            {
                _runningPlugins[pluginId] = plugin;
                _logger.LogInformation("Plugin {PluginId} started successfully", pluginId);
            }
            else
            {
                _logger.LogError("Failed to start plugin {PluginId}: {Error}", 
                    pluginId, result.ErrorMessage);
                
                // Stop any plugins that were started
                await StopPluginsAsync(_runningPlugins.Keys, cancellationToken);
                throw new PluginStartException($"Plugin {pluginId} failed to start");
            }
        }
    }
    
    private List<string> TopologicalSort(Dictionary<string, List<string>> graph)
    {
        // Kahn's algorithm for topological sort
        var inDegree = graph.Keys.ToDictionary(k => k, k => 0);
        foreach (var deps in graph.Values)
        {
            foreach (var dep in deps)
            {
                inDegree[dep]++;
            }
        }
        
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var result = new List<string>();
        
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);
            
            foreach (var dep in graph[node])
            {
                inDegree[dep]--;
                if (inDegree[dep] == 0)
                {
                    queue.Enqueue(dep);
                }
            }
        }
        
        if (result.Count != graph.Count)
        {
            throw new CircularDependencyException("Circular dependency detected in plugin graph");
        }
        
        return result;
    }
}
```

### Phase 4: Trigger System (Week 5)

#### Device Event Trigger
```csharp
public class DeviceEventTrigger : ITrigger
{
    private readonly DeviceListenerService _deviceListener;
    private readonly PluginOrchestrator _orchestrator;
    
    public DeviceEventTrigger(
        DeviceListenerService deviceListener,
        PluginOrchestrator orchestrator)
    {
        _deviceListener = deviceListener;
        _orchestrator = orchestrator;
        
        // Subscribe to device events
        _deviceListener.DeviceConnected += OnDeviceConnected;
        _deviceListener.DeviceDisconnected += OnDeviceDisconnected;
    }
    
    private async void OnDeviceConnected(object? sender, DeviceEventArgs e)
    {
        var plugins = GetPluginsForTrigger("device-connected");
        
        foreach (var plugin in plugins)
        {
            var context = CreateContext(e.Device);
            await _orchestrator.StartPluginAsync(plugin.Id, context, CancellationToken.None);
        }
    }
}
```

---

## Service Manager Integration (Optional)

For users who want persistent system services:

### Windows (Servy/NSSM)
```json
{
  "plugins": [
    {
      "id": "custom-monitor",
      "type": "process",
      "serviceManager": {
        "enabled": true,
        "type": "servy",
        "serviceName": "AppiumCustomMonitor",
        "displayName": "Appium Custom Monitor",
        "description": "Custom device monitoring service",
        "startType": "automatic"
      }
    }
  ]
}
```

### Linux/macOS (Systemd/Supervisor)
```json
{
  "plugins": [
    {
      "id": "custom-monitor",
      "type": "process",
      "serviceManager": {
        "enabled": true,
        "type": "systemd",
        "unitName": "appium-custom-monitor.service",
        "user": "appium",
        "restart": "always"
      }
    }
  ]
}
```

**Implementation Strategy:**
- Default to **portable process mode** (child processes) for simplicity
- Provide optional `serviceManager` config for production deployments
- Generate service definition files (systemd units, supervisord config, etc.)

---

## Configuration Examples

### Example 1: Log Aggregator + Notification Pipeline
```json
{
  "plugins": [
    {
      "id": "fluent-bit",
      "type": "process",
      "enabled": true,
      "executable": "${INSTALL_FOLDER}/bin/fluent-bit",
      "arguments": ["-c", "${INSTALL_FOLDER}/config/fluent-bit.conf"],
      "restartPolicy": "always",
      "healthCheck": {
        "type": "port",
        "port": 24224
      }
    },
    {
      "id": "slack-notifier",
      "type": "script",
      "enabled": true,
      "runtime": "python",
      "script": "${INSTALL_FOLDER}/scripts/slack-notify.py",
      "triggerOn": "device-connected",
      "arguments": [
        "--device-id", "{deviceId}",
        "--webhook-url", "${SLACK_WEBHOOK_URL}"
      ]
    },
    {
      "id": "onboarding-pipeline",
      "type": "pipeline",
      "enabled": true,
      "triggerOn": "device-connected",
      "steps": [
        {"plugin": "slack-notifier", "continueOnError": true},
        {"plugin": "device-provisioner", "continueOnError": false}
      ]
    }
  ]
}
```

### Example 2: Proxy + Test Recorder
```json
{
  "plugins": [
    {
      "id": "mitmproxy",
      "type": "process",
      "enabled": true,
      "executable": "mitmdump",
      "arguments": [
        "-p", "8888",
        "--set", "flow_detail=3",
        "-w", "${INSTALL_FOLDER}/logs/traffic.mitm"
      ],
      "restartPolicy": "always",
      "healthCheck": {
        "type": "port",
        "port": 8888
      }
    },
    {
      "id": "test-recorder",
      "type": "http",
      "enabled": true,
      "port": 9091,
      "handler": "builtin:webhook",
      "routes": [
        {
          "path": "/record-test",
          "method": "POST",
          "action": "save-json",
          "target": "${INSTALL_FOLDER}/test-results/{timestamp}.json"
        }
      ]
    }
  ]
}
```

---

## Benefits of This Architecture

### 1. **Flexibility**
- Add custom services via configuration (no code changes)
- Mix and match plugin types
- Hot-reload configuration changes

### 2. **Scalability**
- Run 50+ plugins concurrently
- Dependency resolution ensures correct startup order
- Parallel execution where possible

### 3. **Reliability**
- Auto-restart failed plugins
- Health checks prevent zombie processes
- Graceful shutdown and cleanup

### 4. **Observability**
- Structured logging per plugin
- Prometheus metrics endpoint
- Real-time plugin state monitoring

### 5. **Cross-Platform**
- Unified API across Windows/macOS/Linux
- Platform-specific optimizations hidden
- Optional service manager integration

---

## Migration Path

### Existing Users
1. **No Breaking Changes**: Existing `config.json` continues to work
2. **Opt-In**: Set `pluginSystem.enabled: true` to enable plugins
3. **Gradual Adoption**: Start with simple process plugins, add complexity over time

### New Users
- Recommended to use plugin system from day 1
- Sample configurations provided for common scenarios
- Plugin marketplace (future: community-contributed plugins)

---

## Technical Recommendations

### 1. **Use .NET 8 Dependency Injection**
- Register plugins as scoped services
- Leverage `IHostedService` for lifecycle management
- Use `IOptions<T>` for configuration binding

### 2. **Implement Plugin Versioning**
```json
{
  "plugins": [
    {
      "id": "my-plugin",
      "version": "1.2.0",
      "minInstallerVersion": "0.10.1"
    }
  ]
}
```

### 3. **Use System.Text.Json Source Generators**
Already using `JsonSerializerContext` — extend for plugin configs:
```csharp
[JsonSerializable(typeof(PluginConfig))]
[JsonSerializable(typeof(PluginSystemConfig))]
public partial class PluginJsonContext : JsonSerializerContext { }
```

### 4. **Implement Plugin Sandboxing (Future)**
- Run untrusted plugins in separate AppDomains or containers
- Resource limits (CPU, memory, network)
- Permission model for file system access

### 5. **Consider gRPC for Plugin Communication**
For advanced scenarios (e.g., plugin-to-plugin communication):
```proto
service PluginService {
  rpc SendEvent(PluginEvent) returns (PluginEventResponse);
  rpc GetState(PluginStateRequest) returns (PluginState);
}
```

---

## Performance Considerations

### Resource Limits
```json
{
  "plugins": [
    {
      "id": "resource-heavy-plugin",
      "resourceLimits": {
        "maxMemoryMB": 512,
        "maxCpuPercent": 50,
        "maxOpenFiles": 1024
      }
    }
  ]
}
```

### Concurrency Control
```json
{
  "pluginSystem": {
    "maxConcurrentPlugins": 50,
    "maxConcurrentStartups": 10,
    "startupTimeoutSeconds": 60
  }
}
```

---

## Security Considerations

### 1. **Plugin Validation**
- Schema validation for plugin configs
- Signature verification for binaries (future)
- Allowlist of permitted executables

### 2. **Environment Isolation**
- Plugins inherit minimal environment variables
- Explicit environment variable passing required
- No access to sensitive credentials by default

### 3. **File System Permissions**
```json
{
  "plugins": [
    {
      "id": "restricted-plugin",
      "permissions": {
        "fileSystem": {
          "allowedPaths": [
            "${INSTALL_FOLDER}/data",
            "/tmp"
          ],
          "readOnly": false
        }
      }
    }
  ]
}
```

---

## Next Steps

### Immediate (Phase 1-2)
1. Implement `IPlugin` interface and `PluginBase`
2. Build `PluginOrchestrator` with dependency resolution
3. Create `ProcessPlugin` as first built-in type
4. Add basic health monitoring

### Short-Term (Phase 3-4)
1. Implement trigger system (startup, device events)
2. Add `ScriptPlugin` and `PipelinePlugin`
3. Hot-reload configuration support
4. Comprehensive logging and metrics

### Long-Term (Future Releases)
1. Plugin marketplace (community plugins)
2. Web UI for plugin management
3. Remote plugin execution (multi-host)
4. Advanced security (sandboxing, signatures)

---

## Questions for Discussion

1. **Preferred Configuration Format**: JSON (current) or YAML (more readable for complex configs)?
2. **Plugin Discovery**: Auto-discover plugins in `plugins/` directory, or require explicit registration?
3. **Service Manager Priority**: Should we prioritize portable process mode or service manager integration?
4. **Plugin Communication**: Do you need plugins to communicate with each other, or are they independent?
5. **UI Requirements**: Command-line only, or would a web UI for plugin management be valuable?

---

## Conclusion

This architecture provides:
- ✅ **Cross-platform**: Windows, macOS, Linux support
- ✅ **Flexible**: Configuration-driven plugin system
- ✅ **Scalable**: Handle dozens of concurrent plugins
- ✅ **Easy to configure**: Declarative JSON/YAML config
- ✅ **Production-ready**: Health checks, auto-restart, metrics
- ✅ **Extensible**: Add new plugin types without core changes

**Estimated Implementation Time**: 4-6 weeks for full Phase 1-4 delivery.

Let me know your thoughts and which aspects you'd like me to prioritize!
