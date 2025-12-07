# Architecture & Design

Understanding how Appium Bootstrap Installer works under the hood.

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   AppiumBootstrapInstaller                  │
│                        (Main Program)                        │
└──────────────────────┬──────────────────────────────────────┘
                       │
        ┌──────────────┼──────────────┐
        │              │              │
┌───────▼───────┐ ┌───▼──────┐ ┌────▼────────┐
│Configuration  │ │  Script  │ │   Device    │
│    Reader     │ │ Executor │ │  Listener   │
└───────┬───────┘ └───┬──────┘ └────┬────────┘
        │             │              │
        │             │              ├─► DeviceMetrics
        │             │              ├─► DeviceRegistry
        │             │              └─► AppiumSessionManager
        │             │                      │
        │             │                      ├─► NSSM (Windows)
        │             │                      ├─► Supervisor (macOS)
        │             │                      └─► systemd (Linux)
        │             │
        └─────────────┴──────────────────────┐
                                             │
                                    ┌────────▼────────┐
                                    │   Appium Server │
                                    │  (per device)   │
                                    └─────────────────┘
```

## Installation Flow

```
1. Start Application
   │
   ├─► Parse CLI Arguments
   │   ├─ --config <path>
   │   ├─ --listen
   │   ├─ --dry-run
   │   ├─ --generate-config
   │   └─ --help
   │
   ├─► Load Configuration
   │   ├─ Check --config argument
   │   ├─ Check ./config.json
   │   └─ Check ~/.appium-bootstrap/config.json
   │
   ├─► Detect Operating System
   │   ├─ Windows (RuntimeInformation)
   │   ├─ macOS (RuntimeInformation)
   │   └─ Linux (RuntimeInformation)
   │
   ├─► If --listen flag: Skip to Device Listener
   │
   ├─► STEP 1: Install Dependencies
   │   ├─ Execute Platform/{OS}/Scripts/InstallDependencies.{ext}
   │   ├─ Install Node.js via NVM
   │   ├─ Install Appium via npm
   │   ├─ Install configured drivers
   │   └─ Install configured plugins
   │
   ├─► STEP 2: Setup Service Manager
   │   ├─ Windows: Install NSSM
   │   ├─ macOS: Setup Supervisor
   │   └─ Linux: Setup systemd
   │
   └─► STEP 3: Start Device Listener (if EnableDeviceListener: true)
       └─ Enter device monitoring loop
```

## Device Listener Architecture

```
DeviceListenerService (BackgroundService)
│
├─► Initialization
│   ├─ Start metrics timer (5-minute interval)
│   ├─ Check tool availability (adb, idevice_id)
│   └─ Log configuration
│
└─► Main Loop (while not cancelled)
    │
    ├─► Monitor Android Devices
    │   ├─ Execute: adb devices
    │   ├─ Parse output for device IDs
    │   ├─ Compare with DeviceRegistry
    │   │   ├─ New device → HandleDeviceConnected()
    │   │   └─ Missing device → HandleDeviceDisconnected()
    │   └─ Update metrics
    │
    ├─► Monitor iOS Devices
    │   ├─ Execute: idevice_id -l
    │   ├─ Parse output for UDIDs
    │   ├─ Compare with DeviceRegistry
    │   │   ├─ New device → HandleDeviceConnected()
    │   │   └─ Missing device → HandleDeviceDisconnected()
    │   └─ Update metrics
    │
    └─► Wait for poll interval
        └─ Task.Delay(DeviceListenerPollInterval)
```

## Device Connection Handling

```
HandleDeviceConnected(deviceId, platform)
│
├─► Generate Correlation ID
│   └─ Format: CORR-{timestamp}-{random}
│
├─► BeginScope (Structured Logging)
│   └─ Include: DeviceId, Platform, CorrelationId
│
├─► Update DeviceRegistry
│   ├─ Check if device known
│   ├─ Create/update device record
│   └─ Set status: Connected
│
├─► Record Metrics
│   └─ Increment: TotalDevicesConnected
│
├─► If AutoStartAppium is true:
│   ├─ Allocate Port from AppiumSessionManager
│   ├─ Start Appium Server
│   │   └─ Via NSSM/Supervisor/systemd
│   ├─ Update DeviceRegistry with session info
│   ├─ Record success/failure metrics
│   └─ Log session details
│
└─► Save DeviceRegistry to disk
```

## Appium Session Management

```
AppiumSessionManager
│
├─► Port Pool Management
│   ├─ Initialize available ports queue
│   ├─ Track allocated ports (Dictionary)
│   ├─ Calculate usage percentage
│   └─ Warn at 70% and 90% thresholds
│
├─► Start Appium Server
│   ├─ Detect process manager (NSSM/Supervisor/systemd)
│   ├─ Allocate port from pool
│   ├─ Build Appium command
│   │   └─ appium --port {port} --log {logfile}
│   ├─ Create service via process manager
│   ├─ Start service
│   ├─ Record port allocation
│   └─ Update metrics
│
└─► Stop Appium Server
    ├─ Get port for device
    ├─ Stop service via process manager
    ├─ Remove service configuration
    ├─ Release port back to pool
    └─ Update metrics
```

## Observability Features

### 1. Correlation IDs

Every device operation gets a unique correlation ID for end-to-end tracing:

```csharp
string correlationId = $"CORR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
```

**Usage in logs:**
```
[INFO] Device android123 connected [CORR-20251207153045-abc123]
[INFO] Allocating port 4723 [CORR-20251207153045-abc123]
[INFO] Starting Appium server [CORR-20251207153045-abc123]
[INFO] Session started successfully [CORR-20251207153045-abc123]
```

### 2. Structured Logging

Uses Serilog with structured events and context scoping:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["DeviceId"] = deviceId,
    ["Platform"] = platform,
    ["CorrelationId"] = correlationId
}))
{
    _logger.LogInformation("Processing device connection");
    // All logs within this scope include context
}
```

### 3. Metrics Collection

```csharp
DeviceMetrics tracks:
├─ TotalDevicesConnected
├─ TotalDevicesDisconnected
├─ ActiveSessions
├─ SessionSuccessCount
├─ SessionFailureCount
├─ PortAllocationFailures
└─ GetSummary() - Periodic reporting
```

**Metrics output (every 5 minutes):**
```
[METRICS] Summary: Connected=15, Disconnected=3, Active=12, 
          Sessions(Success=45, Failed=2), Ports(Failed=0)
```

### 4. Port Pool Monitoring

Real-time tracking of port utilization:

```csharp
int totalPorts = PortRanges.AppiumEnd - PortRanges.AppiumStart + 1;
int usedPorts = _allocatedPorts.Count;
double usagePercent = (usedPorts / (double)totalPorts) * 100;

if (usagePercent >= 90)
    _logger.LogWarning("Port pool critically low: {Percent}%", usagePercent);
else if (usagePercent >= 70)
    _logger.LogWarning("Port pool usage high: {Percent}%", usagePercent);
```

## Data Models

### Device Model

```csharp
public class Device
{
    public string DeviceId { get; set; }
    public string Platform { get; set; }  // Android, iOS
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public DeviceStatus Status { get; set; }  // Connected, Disconnected
    public List<Session> Sessions { get; set; }
}
```

### Session Model

```csharp
public class Session
{
    public string SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int AppiumPort { get; set; }
    public string CorrelationId { get; set; }
    public SessionStatus Status { get; set; }  // Active, Completed, Failed
}
```

### InstallConfig Model

```csharp
public class InstallConfig
{
    public string InstallFolder { get; set; }
    public string NodeVersion { get; set; }
    public string AppiumVersion { get; set; }
    public bool EnableDeviceListener { get; set; }
    public bool AutoStartAppium { get; set; }
    public int DeviceListenerPollInterval { get; set; }
    public PortRanges PortRanges { get; set; }
    public DeviceRegistryConfig DeviceRegistry { get; set; }
    public List<DriverConfig> Drivers { get; set; }
    public List<PluginConfig> Plugins { get; set; }
}
```

## Service Managers

### Windows - NSSM

```powershell
# Install service
nssm install ServiceName "path/to/appium" --port 4723

# Configure service
nssm set ServiceName AppDirectory "path"
nssm set ServiceName Start SERVICE_AUTO_START

# Control service
nssm start ServiceName
nssm stop ServiceName
nssm remove ServiceName confirm
```

### macOS - Supervisor

```ini
[program:appium-4723]
command=/path/to/appium --port 4723
directory=/path/to/appium
autostart=true
autorestart=true
redirect_stderr=true
stdout_logfile=/path/to/logs/appium-4723.log
```

### Linux - systemd

```ini
[Unit]
Description=Appium Server (Port 4723)

[Service]
Type=simple
ExecStart=/path/to/appium --port 4723
WorkingDirectory=/path/to/appium
Restart=always

[Install]
WantedBy=multi-user.target
```

## Error Handling Strategy

1. **Configuration Errors**: Fail fast with clear error messages
2. **Installation Errors**: Log and return non-zero exit code
3. **Device Detection Errors**: Log warning, continue monitoring
4. **Session Start Errors**: Log error, record metric, continue
5. **Port Exhaustion**: Log error, record metric, queue devices

## Performance Considerations

### Polling Interval
- **Fast (3-5s)**: Better responsiveness, higher CPU usage
- **Normal (5-10s)**: Balanced performance
- **Slow (10-15s)**: Lower overhead, delayed detection

### Port Pool Size
- Calculate: `(Expected Devices × 1.5) + Buffer`
- Example: 20 devices → 30-40 port range

### Registry Auto-Save
- Frequent saves (30s): Better persistence, more I/O
- Infrequent saves (120s): Less I/O, risk of data loss

## Security Considerations

1. **Service Account**: Run with minimal required permissions
2. **File Permissions**: Restrict config and log file access
3. **Port Range**: Use high ports (>1024) to avoid privilege requirements
4. **Log Sanitization**: Avoid logging sensitive device information
5. **Configuration Validation**: Validate all user inputs

## Scalability

### Horizontal Scaling
- Run multiple instances on different machines
- Use separate port ranges per instance
- Centralize logging and metrics

### Vertical Scaling
- Increase poll interval for large device counts
- Optimize port pool management
- Use background tasks for registry saves

## Extension Points

1. **Custom Metrics Exporters**: Extend DeviceMetrics
2. **Alternative Service Managers**: Implement IServiceManager
3. **Custom Device Detection**: Extend DeviceListenerService
4. **Configuration Providers**: Extend ConfigurationReader
5. **Logging Sinks**: Add Serilog sinks (Elasticsearch, Application Insights)
