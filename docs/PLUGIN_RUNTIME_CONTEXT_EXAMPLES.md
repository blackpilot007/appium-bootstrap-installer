# Plugin Runtime Context: Usage Examples

## Overview

Plugins can access **live system state** through the `IAppiumRuntimeContext` interface, enabling rich integrations with device monitoring, session management, and infrastructure metrics.

---

## Accessing Runtime Context

### In Your Plugin

```csharp
public class MyCustomPlugin : PluginBase
{
    private IAppiumRuntimeContext? _runtime;

    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        _runtime = context.Runtime; // Store for later use
        
        // Now you can query live state
        var devices = _runtime?.GetConnectedDevices() ?? new List<Device>();
        Logger.LogInformation("Found {Count} connected devices", devices.Count);
        
        return PluginStartResult.Success();
    }
}
```

---

## Common Use Cases

### 1. Query Connected Devices

```csharp
// Get all connected devices
var allDevices = _runtime.GetConnectedDevices();
foreach (var device in allDevices)
{
    Logger.LogInformation("Device: {Name} ({Id}) - Platform: {Platform}", 
        device.Name, device.Id, device.Platform);
}

// Get specific device
var device = _runtime.GetDevice("00008120-0019452C1A31A01E");
if (device != null)
{
    Logger.LogInformation("Found device: {Name}", device.Name);
}

// Get devices by platform
var iosDevices = _runtime.GetDevicesByPlatform(DevicePlatform.iOS);
var androidDevices = _runtime.GetDevicesByPlatform(DevicePlatform.Android);

Logger.LogInformation("iOS: {iOS}, Android: {Android}", 
    iosDevices.Count, androidDevices.Count);
```

### 2. Access Appium Session Details

```csharp
// Get all active Appium sessions
var sessions = _runtime.GetActiveSessions();
foreach (var session in sessions)
{
    Logger.LogInformation(
        "Session {Id}: Port {Port}, WDA {WDA}, MJPEG {MJPEG}",
        session.SessionId,
        session.AppiumPort,
        session.WdaLocalPort,
        session.MjpegServerPort
    );
}

// Get session for specific device
var session = _runtime.GetSessionForDevice("DEVICE123");
if (session != null)
{
    Logger.LogInformation("Device DEVICE123 is running on port {Port}", 
        session.AppiumPort);
}

// Get all sessions (includes stopped/failed)
var allSessions = _runtime.GetAllSessions();
Logger.LogInformation("Total sessions tracked: {Count}", allSessions.Count);
```

### 3. Check Port Availability

```csharp
// Check if specific port is in use
if (_runtime.IsPortInUse(4723))
{
    Logger.LogWarning("Port 4723 is already in use");
}

// Get allocated ports
var allocatedPorts = _runtime.GetAllocatedPorts();
Logger.LogInformation("Ports in use: {Ports}", string.Join(", ", allocatedPorts));

// Find available ports
var availablePorts = _runtime.GetAvailablePorts(3); // Get 3 consecutive ports
if (availablePorts.Count >= 3)
{
    Logger.LogInformation("Available ports: {Ports}", 
        string.Join(", ", availablePorts));
}
```

### 4. Monitor Service Health

```csharp
var health = _runtime.GetServiceHealth();

Logger.LogInformation(
    "Service Health: {Status}\n" +
    "  Connected Devices: {Devices}\n" +
    "  Active Sessions: {Sessions}\n" +
    "  Running Plugins: {Plugins}",
    health.IsHealthy ? "HEALTHY" : "UNHEALTHY",
    health.ConnectedDevices,
    health.ActiveSessions,
    health.RunningPlugins
);

foreach (var component in health.ComponentStatus)
{
    Logger.LogInformation("  {Component}: {Status}", 
        component.Key, component.Value);
}
```

### 5. Track Uptime

```csharp
var uptime = _runtime.GetUptime();
Logger.LogInformation("System uptime: {Days}d {Hours}h {Minutes}m", 
    uptime.Days, uptime.Hours, uptime.Minutes);
```

### 6. Access Metrics

```csharp
var metrics = _runtime.GetMetrics();
var summary = metrics.GetSummary();

Logger.LogInformation("Metrics: {Summary}", summary);
// Example output: "Devices: 2 Android, 3 iOS | Sessions: 5 active, 10 started, 1 failed (90.9% success)"
```

---

## Reactive Plugins: Event Subscriptions

### Subscribe to Device Events

```csharp
public class DeviceMonitorPlugin : PluginBase
{
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        
        // Subscribe to device events
        context.Runtime?.SubscribeToDeviceEvents(OnDeviceEvent);
        
        return PluginStartResult.Success();
    }
    
    private void OnDeviceEvent(DeviceEventArgs e)
    {
        if (e.EventType == "connected")
        {
            Logger.LogInformation("Device connected: {Name} ({Id})", 
                e.Device.Name, e.Device.Id);
            
            // Take action - send notification, run provisioning, etc.
            _ = SendSlackNotificationAsync(e.Device);
        }
        else if (e.EventType == "disconnected")
        {
            Logger.LogWarning("Device disconnected: {Name} ({Id})", 
                e.Device.Name, e.Device.Id);
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Clean up subscription
        context.Runtime?.UnsubscribeFromDeviceEvents(OnDeviceEvent);
        await Task.CompletedTask;
    }
}
```

### Subscribe to Session Events

```csharp
public class SessionTrackerPlugin : PluginBase
{
    private readonly Dictionary<string, DateTime> _sessionStartTimes = new();
    
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        context.Runtime?.SubscribeToSessionEvents(OnSessionEvent);
        return PluginStartResult.Success();
    }
    
    private void OnSessionEvent(SessionEventArgs e)
    {
        switch (e.EventType)
        {
            case "started":
                _sessionStartTimes[e.Session.SessionId] = DateTime.UtcNow;
                Logger.LogInformation(
                    "Session started: {DeviceName} on port {Port}",
                    e.Device.Name, e.Session.AppiumPort
                );
                break;
                
            case "stopped":
                if (_sessionStartTimes.TryGetValue(e.Session.SessionId, out var startTime))
                {
                    var duration = DateTime.UtcNow - startTime;
                    Logger.LogInformation(
                        "Session stopped: {DeviceName} - Duration: {Duration}",
                        e.Device.Name, duration
                    );
                    _sessionStartTimes.Remove(e.Session.SessionId);
                }
                break;
                
            case "failed":
                Logger.LogError("Session failed: {DeviceName}", e.Device.Name);
                break;
        }
    }
}
```

---

## Advanced Examples

### Example 1: Dynamic Load Balancer

```csharp
public class LoadBalancerPlugin : PluginBase
{
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        
        // Monitor session distribution
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sessions = context.Runtime?.GetActiveSessions() ?? new List<AppiumSession>();
                var iosCount = sessions.Count(s => s.WdaLocalPort.HasValue); // iOS has WDA port
                var androidCount = sessions.Count - iosCount;
                
                Logger.LogInformation(
                    "Load distribution - iOS: {iOS}, Android: {Android}",
                    iosCount, androidCount
                );
                
                // Implement load balancing logic here
                if (iosCount > androidCount * 2)
                {
                    Logger.LogWarning("iOS sessions overloaded - consider scaling");
                }
                
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }, cancellationToken);
        
        return PluginStartResult.Success();
    }
}
```

### Example 2: Port Conflict Detector

```csharp
public class PortMonitorPlugin : PluginBase
{
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var allocatedPorts = context.Runtime?.GetAllocatedPorts() ?? new List<int>();
                
                // Check for external port conflicts
                foreach (var port in allocatedPorts)
                {
                    if (context.Runtime?.IsPortInUse(port) == true)
                    {
                        // Port is in use by something else
                        Logger.LogWarning("Port conflict detected: {Port}", port);
                    }
                }
                
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
        }, cancellationToken);
        
        return PluginStartResult.Success();
    }
}
```

### Example 3: Device Provisioning Workflow

```csharp
public class AutoProvisionPlugin : PluginBase
{
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        
        context.Runtime?.SubscribeToDeviceEvents(async (e) =>
        {
            if (e.EventType == "connected")
            {
                await ProvisionDeviceAsync(e.Device, context);
            }
        });
        
        return PluginStartResult.Success();
    }
    
    private async Task ProvisionDeviceAsync(Device device, PluginContext context)
    {
        Logger.LogInformation("Auto-provisioning device: {Name}", device.Name);
        
        // Wait for Appium session to start
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        var session = context.Runtime?.GetSessionForDevice(device.Id);
        if (session == null)
        {
            Logger.LogWarning("No Appium session found for device {Id}", device.Id);
            return;
        }
        
        Logger.LogInformation(
            "Device {Name} ready - Appium port: {Port}",
            device.Name, session.AppiumPort
        );
        
        // Run provisioning commands via Appium
        var appiumUrl = $"http://localhost:{session.AppiumPort}";
        await InstallAppsAsync(appiumUrl, device);
        await ConfigureSettingsAsync(appiumUrl, device);
        
        Logger.LogInformation("Provisioning complete for {Name}", device.Name);
    }
}
```

### Example 4: Metrics Exporter

```csharp
public class PrometheusExporterPlugin : PluginBase
{
    private HttpListener? _listener;
    
    public override async Task<PluginStartResult> StartAsync(
        PluginContext context, 
        CancellationToken cancellationToken)
    {
        Initialize(context);
        
        // Start HTTP server for Prometheus metrics
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:9090/metrics/");
        _listener.Start();
        
        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var ctx = await _listener.GetContextAsync();
                var metrics = GeneratePrometheusMetrics(context.Runtime);
                
                var buffer = Encoding.UTF8.GetBytes(metrics);
                ctx.Response.ContentLength64 = buffer.Length;
                await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                ctx.Response.Close();
            }
        }, cancellationToken);
        
        return PluginStartResult.Success();
    }
    
    private string GeneratePrometheusMetrics(IAppiumRuntimeContext? runtime)
    {
        if (runtime == null) return string.Empty;
        
        var health = runtime.GetServiceHealth();
        var uptime = runtime.GetUptime();
        
        return $@"
# HELP appium_connected_devices Number of connected devices
# TYPE appium_connected_devices gauge
appium_connected_devices {health.ConnectedDevices}

# HELP appium_active_sessions Number of active Appium sessions
# TYPE appium_active_sessions gauge
appium_active_sessions {health.ActiveSessions}

# HELP appium_uptime_seconds Service uptime in seconds
# TYPE appium_uptime_seconds counter
appium_uptime_seconds {uptime.TotalSeconds}

# HELP appium_running_plugins Number of running plugins
# TYPE appium_running_plugins gauge
appium_running_plugins {health.RunningPlugins}
";
    }
}
```

---

## Configuration with Runtime Context

### Using Variables in Plugin Config

```json
{
  "plugins": [
    {
      "id": "device-notifier",
      "type": "script",
      "runtime": "powershell",
      "script": "${INSTALL_FOLDER}/scripts/notify.ps1",
      "arguments": [
        "--device-count", "${CONNECTED_DEVICES}",
        "--session-count", "${ACTIVE_SESSIONS}",
        "--uptime", "${UPTIME_SECONDS}"
      ],
      "triggerOn": "device-connected"
    }
  ]
}
```

**Available Variables:**
- `${CONNECTED_DEVICES}` - Number of connected devices
- `${ACTIVE_SESSIONS}` - Number of active Appium sessions
- `${UPTIME_SECONDS}` - Service uptime in seconds

These are expanded automatically when the plugin starts.

---

## Implementation: AppiumRuntimeContext

### Complete Implementation

```csharp
public class AppiumRuntimeContext : IAppiumRuntimeContext
{
    private readonly DeviceRegistry _deviceRegistry;
    private readonly AppiumSessionManager _sessionManager;
    private readonly DeviceMetrics _metrics;
    private readonly DateTime _startTime;
    private readonly List<Action<DeviceEventArgs>> _deviceEventHandlers = new();
    private readonly List<Action<SessionEventArgs>> _sessionEventHandlers = new();
    
    public AppiumRuntimeContext(
        DeviceRegistry deviceRegistry,
        AppiumSessionManager sessionManager,
        DeviceMetrics metrics)
    {
        _deviceRegistry = deviceRegistry;
        _sessionManager = sessionManager;
        _metrics = metrics;
        _startTime = DateTime.UtcNow;
    }
    
    // Device information
    public IReadOnlyList<Device> GetConnectedDevices()
        => _deviceRegistry.GetConnectedDevices().ToList();
    
    public Device? GetDevice(string deviceId)
        => _deviceRegistry.GetDevice(deviceId);
    
    public IReadOnlyList<Device> GetDevicesByPlatform(DevicePlatform platform)
        => _deviceRegistry.GetConnectedDevices()
            .Where(d => d.Platform == platform)
            .ToList();
    
    // Appium session information
    public IReadOnlyList<AppiumSession> GetActiveSessions()
        => _deviceRegistry.GetConnectedDevices()
            .Where(d => d.AppiumSession != null && d.AppiumSession.Status == SessionStatus.Running)
            .Select(d => d.AppiumSession!)
            .ToList();
    
    public AppiumSession? GetSessionForDevice(string deviceId)
    {
        var device = _deviceRegistry.GetDevice(deviceId);
        return device?.AppiumSession;
    }
    
    public IReadOnlyDictionary<string, AppiumSession> GetAllSessions()
        => _deviceRegistry.GetAllDevices()
            .Where(d => d.AppiumSession != null)
            .ToDictionary(d => d.Id, d => d.AppiumSession!);
    
    // Port allocations
    public IReadOnlyList<int> GetAllocatedPorts()
        => GetActiveSessions()
            .SelectMany(s => new[] { s.AppiumPort, s.WdaLocalPort ?? 0, s.MjpegServerPort ?? 0, s.SystemPort ?? 0 })
            .Where(p => p > 0)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
    
    public IReadOnlyList<int> GetAvailablePorts(int count)
    {
        var allocatedPorts = GetAllocatedPorts().ToHashSet();
        var availablePorts = new List<int>();
        
        for (int port = 4723; port < 65535 && availablePorts.Count < count; port++)
        {
            if (!allocatedPorts.Contains(port) && !IsPortInUse(port))
            {
                availablePorts.Add(port);
            }
        }
        
        return availablePorts;
    }
    
    public bool IsPortInUse(int port)
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch
        {
            return true;
        }
    }
    
    // Service status
    public ServiceHealthStatus GetServiceHealth()
    {
        var devices = GetConnectedDevices();
        var sessions = GetActiveSessions();
        
        return new ServiceHealthStatus
        {
            IsHealthy = true,
            ConnectedDevices = devices.Count,
            ActiveSessions = sessions.Count,
            RunningPlugins = 0, // Will be set by orchestrator
            ComponentStatus = new Dictionary<string, string>
            {
                ["DeviceListener"] = "Running",
                ["SessionManager"] = "Running",
                ["PluginSystem"] = "Running"
            }
        };
    }
    
    public TimeSpan GetUptime() => DateTime.UtcNow - _startTime;
    
    public DeviceMetrics GetMetrics() => _metrics;
    
    // Event subscriptions
    public void SubscribeToDeviceEvents(Action<DeviceEventArgs> handler)
        => _deviceEventHandlers.Add(handler);
    
    public void SubscribeToSessionEvents(Action<SessionEventArgs> handler)
        => _sessionEventHandlers.Add(handler);
    
    public void UnsubscribeFromDeviceEvents(Action<DeviceEventArgs> handler)
        => _deviceEventHandlers.Remove(handler);
    
    public void UnsubscribeFromSessionEvents(Action<SessionEventArgs> handler)
        => _sessionEventHandlers.Remove(handler);
    
    // Internal: Trigger events (called by DeviceListenerService)
    internal void RaiseDeviceEvent(Device device, string eventType)
    {
        var args = new DeviceEventArgs { Device = device, EventType = eventType };
        foreach (var handler in _deviceEventHandlers.ToList())
        {
            try { handler(args); }
            catch { /* Ignore handler exceptions */ }
        }
    }
    
    internal void RaiseSessionEvent(AppiumSession session, Device device, string eventType)
    {
        var args = new SessionEventArgs { Session = session, Device = device, EventType = eventType };
        foreach (var handler in _sessionEventHandlers.ToList())
        {
            try { handler(args); }
            catch { /* Ignore handler exceptions */ }
        }
    }
}
```

---

## Integration Example

### In Program.cs

```csharp
// Create runtime context
var runtimeContext = new AppiumRuntimeContext(
    deviceRegistry,
    sessionManager,
    metrics
);

// Pass to plugin orchestrator
var pluginOrchestrator = new PluginOrchestrator(
    logger,
    pluginRegistry,
    installFolder,
    serviceProvider,
    runtimeContext  // NEW parameter
);

// Start plugins with runtime context
var startedPlugins = await pluginOrchestrator.StartPluginsAsync(
    pluginConfigs, 
    cancellationToken
);
```

### In PluginOrchestrator.cs

```csharp
public async Task<List<IPlugin>> StartPluginsAsync(
    IEnumerable<PluginConfig> configs,
    CancellationToken cancellationToken)
{
    // ...
    var context = new PluginContext
    {
        Config = config,
        Services = _services,
        Logger = _logger,
        InstallFolder = _installFolder,
        CancellationToken = cancellationToken,
        Runtime = _runtimeContext  // Pass runtime context
    };
    
    await plugin.StartAsync(context, cancellationToken);
    // ...
}
```

---

## Summary

**Runtime context provides plugins with:**

✅ **Device Information** - Query connected devices, filter by platform  
✅ **Session Details** - Access Appium ports, WDA ports, session status  
✅ **Port Management** - Check allocations, find available ports  
✅ **Service Health** - Monitor overall system health  
✅ **Metrics** - Access success rates, timing data  
✅ **Event Subscriptions** - React to device/session events in real-time  

**This enables plugins to:**
- Build custom dashboards
- Implement load balancers
- Create provisioning workflows
- Export metrics to external systems
- Trigger actions on device events
- Monitor port conflicts
- Track session lifetimes

The runtime context makes plugins **fully integrated** with the Appium infrastructure!
