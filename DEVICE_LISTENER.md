# Device Listener Feature

## Overview

The Device Listener is an integrated background service within AppiumBootstrapInstaller that automatically monitors Android and iOS device connections and manages Appium server sessions with dynamic port allocation.

## Architecture

### Components

1. **DeviceListenerService** - Background worker that polls for device connections
2. **DeviceRegistry** - Manages device state and persists to JSON database
3. **AppiumSessionManager** - Handles Appium server lifecycle with NSSM (Windows) or Supervisord (Linux/macOS)
4. **WebhookNotifier** - Sends HTTP notifications for device events

### Process Management Strategy

- **Windows**: Uses **NSSM** (Non-Sucking Service Manager) to run each Appium server as a managed Windows service
  - Auto-restart on crash
  - Dedicated stdout/stderr logs per device
  - Service-level process isolation
  
- **Linux/macOS**: Uses **Supervisord** to manage Appium server processes
  - Dynamic config generation per device
  - Auto-restart capabilities
  - Centralized process supervision

### How It Works

```
Device Connected → Detect via ADB/idevice → Registry Update → Create Service/Program
                                                                      ↓
                                                          Windows: NSSM Service
                                                          Linux/Mac: Supervisor Program
                                                                      ↓
                                                          Allocate Dynamic Ports:
                                                          - Appium Server Port (4723-4823)
                                                          - WDA Local Port (iOS: 8100-8200)
                                                          - MJPEG Stream Port (iOS: 9100-9200)
                                                          - System Port (Android: 8200-8300)
                                                                      ↓
                                                          Start Process → Send Webhook

Device Disconnected → Detect Absence → Stop Service/Program → Release Ports → Send Webhook
```

## Configuration

### Enable Device Listener

Add to your `config.json`:

```json
{
  "installFolder": "${USERPROFILE}/AppiumBootstrap",
  "nodeVersion": "22",
  "appiumVersion": "2.17.1",
  "nvmVersion": "0.40.1",
  "drivers": [
    {
      "name": "uiautomator2",
      "version": "3.8.3",
      "enabled": true
    },
    {
      "name": "xcuitest",
      "version": "7.24.3",
      "enabled": true
    }
  ],
  "plugins": [
    {
      "name": "device-farm",
      "version": "8.3.5",
      "enabled": true
    }
  ],
  "enableDeviceListener": true,
  "deviceListenerPollInterval": 5,
  "autoStartAppium": true,
  "portRanges": {
    "appiumStart": 4723,
    "appiumEnd": 4823,
    "wdaStart": 8100,
    "wdaEnd": 8200,
    "mjpegStart": 9100,
    "mjpegEnd": 9200,
    "systemPortStart": 8200,
    "systemPortEnd": 8300
  },
  "webhooks": {
    "enabled": true,
    "onConnectUrl": "http://your-server/api/device/connected",
    "onDisconnectUrl": "http://your-server/api/device/disconnected",
    "onSessionStartUrl": "http://your-server/api/session/started",
    "onSessionEndUrl": "http://your-server/api/session/ended",
    "headers": {
      "Authorization": "Bearer YOUR_TOKEN",
      "Content-Type": "application/json"
    },
    "timeoutSeconds": 30
  },
  "deviceRegistry": {
    "enabled": true,
    "filePath": "device-registry.json",
    "autoSave": true,
    "saveIntervalSeconds": 30
  }
}
```

### Configuration Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `enableDeviceListener` | boolean | `false` | Enable/disable device monitoring |
| `deviceListenerPollInterval` | number | `5` | Polling interval in seconds |
| `autoStartAppium` | boolean | `true` | Auto-start Appium on device connect |
| `portRanges.appiumStart` | number | `4723` | Start of Appium port range |
| `portRanges.appiumEnd` | number | `4823` | End of Appium port range |
| `portRanges.wdaStart` | number | `8100` | Start of WDA port range (iOS) |
| `portRanges.wdaEnd` | number | `8200` | End of WDA port range (iOS) |
| `portRanges.mjpegStart` | number | `9100` | Start of MJPEG port range (iOS) |
| `portRanges.mjpegEnd` | number | `9200` | End of MJPEG port range (iOS) |
| `portRanges.systemPortStart` | number | `8200` | Start of system port range (Android) |
| `portRanges.systemPortEnd` | number | `8300` | End of system port range (Android) |
| `webhooks.enabled` | boolean | `false` | Enable webhook notifications |
| `webhooks.onConnectUrl` | string | `null` | URL for device connect events |
| `webhooks.onDisconnectUrl` | string | `null` | URL for device disconnect events |
| `webhooks.onSessionStartUrl` | string | `null` | URL for session start events |
| `webhooks.onSessionEndUrl` | string | `null` | URL for session end events |
| `webhooks.headers` | object | `{}` | Custom HTTP headers for webhooks |
| `webhooks.timeoutSeconds` | number | `30` | Webhook request timeout |
| `deviceRegistry.enabled` | boolean | `true` | Enable device registry persistence |
| `deviceRegistry.filePath` | string | `"device-registry.json"` | Registry file path |
| `deviceRegistry.autoSave` | boolean | `true` | Auto-save registry changes |
| `deviceRegistry.saveIntervalSeconds` | number | `30` | Auto-save interval |

## Usage

### Running in Listen Mode

```bash
# Windows
AppiumBootstrapInstaller.exe --listen --config config.json

# Linux/macOS
./AppiumBootstrapInstaller --listen --config config.json
```

### What Happens

1. **Device Detection**
   - Polls `adb devices` every 5 seconds for Android devices
   - Polls `idevice_id -l` for iOS devices
   - Detects emulators vs physical devices

2. **Automatic Appium Session via Process Manager**
   - **Windows (NSSM)**:
     - Creates Windows service: `AppiumBootstrap_<device_id>`
     - Installs service with NSSM: `nssm install <service> powershell.exe <script>`
     - Configures auto-restart on failure
     - Starts service: `nssm start <service>`
     - Each device gets independent service with dedicated logs
   
   - **Linux/macOS (Supervisord)**:
     - Creates Supervisor program config in `services/supervisor/conf.d/`
     - Reloads Supervisor: `supervisorctl reread && supervisorctl update`
     - Starts program: `supervisorctl start <program>`
     - Auto-restart enabled with 3 retries
   
   - **Port Allocation**: Dynamically allocates from configured ranges
   - **Process Monitoring**: Process manager handles health checks and restarts

3. **Device Registry**
   - Stores device info with service name as session ID
   - Tracks connection times, session ports, process status
   - Auto-saves every 30 seconds

4. **Webhook Notifications**
   - Sends POST requests for all device lifecycle events
   ```json
   {
     "eventType": "Connected",
     "device": {
       "id": "emulator-5554",
       "platform": "Android",
       "type": "Emulator",
       "name": "Pixel_5_API_31",
       "state": "Connected",
       "connectedAt": "2025-12-07T10:30:00Z",
       "appiumSession": {
         "sessionId": "abc-123",
         "appiumPort": 4723,
         "systemPort": 8200,
         "startedAt": "2025-12-07T10:30:05Z",
         "processId": 12345,
         "status": "Running"
       }
     },
     "timestamp": "2025-12-07T10:30:05Z"
   }
   ```

## Service Management

### Windows (NSSM)

Each connected device gets its own Windows service managed by NSSM:

```powershell
# List all Appium device services
Get-Service | Where-Object { $_.Name -like "AppiumBootstrap_*" }

# Check specific device service
nssm status AppiumBootstrap_emulator_5554

# View service logs
Get-Content "$InstallFolder\services\logs\AppiumBootstrap_emulator_5554_stdout.log" -Tail 50 -Wait

# Manually restart a device service
nssm restart AppiumBootstrap_emulator_5554

# Stop and remove service (normally handled by device listener)
nssm stop AppiumBootstrap_emulator_5554
nssm remove AppiumBootstrap_emulator_5554 confirm
```

### Linux/macOS (Supervisord)

Each device gets a Supervisor program:

```bash
# List all Appium device programs
supervisorctl status | grep AppiumBootstrap

# Check specific device program
supervisorctl status AppiumBootstrap_emulator_5554

# View program logs
tail -f $InstallFolder/services/logs/AppiumBootstrap_emulator_5554_stdout.log

# Manually restart a device program
supervisorctl restart AppiumBootstrap_emulator_5554

# Stop program (normally handled by device listener)
supervisorctl stop AppiumBootstrap_emulator_5554
supervisorctl remove AppiumBootstrap_emulator_5554
```

### Service Lifecycle

1. **Device Connects** → Service/Program created and started
2. **Service Crashes** → Process manager auto-restarts
3. **Device Disconnects** → Service/Program stopped and removed
4. **Listener Stops** → All active services cleaned up

`device-registry.json` example:

```json
{
  "lastUpdated": "2025-12-07T10:35:00Z",
  "devices": [
    {
      "id": "emulator-5554",
      "platform": "Android",
      "type": "Emulator",
      "name": "Pixel_5_API_31",
      "state": "Connected",
      "connectedAt": "2025-12-07T10:30:00Z",
      "lastSeen": "2025-12-07T10:35:00Z",
      "appiumSession": {
        "sessionId": "abc-123",
        "appiumPort": 4723,
        "systemPort": 8200,
        "startedAt": "2025-12-07T10:30:05Z",
        "processId": 12345,
        "status": "Running"
      }
    },
    {
      "id": "00008030-001234567890ABCD",
      "platform": "iOS",
      "type": "Physical",
      "name": "John's iPhone",
      "state": "Connected",
      "connectedAt": "2025-12-07T10:32:00Z",
      "lastSeen": "2025-12-07T10:35:00Z",
      "appiumSession": {
        "sessionId": "def-456",
        "appiumPort": 4724,
        "wdaLocalPort": 8100,
        "mjpegServerPort": 9100,
        "startedAt": "2025-12-07T10:32:05Z",
        "processId": 12346,
        "status": "Running"
      }
    }
  ]
}
```

## Port Allocation Strategy

- **Sequential Allocation**: Ports allocated sequentially from start to end
- **Availability Check**: TCP socket test before allocation
- **Conflict Prevention**: Tracks used ports in memory
- **Automatic Release**: Ports freed when device disconnects

## Webhook Events

### 1. Device Connected
- **Trigger**: New device detected
- **Payload**: Device info with empty session

### 2. Session Started
- **Trigger**: Appium process started successfully
- **Payload**: Device info with session details (ports, PID)

### 3. Device Disconnected
- **Trigger**: Device no longer detected
- **Payload**: Device info with final state

### 4. Session Ended
- **Trigger**: Appium process terminated (device disconnect or manual stop)
- **Payload**: Device info with session status = Stopped

## Integration Examples

### 1. Slack Notifications

```javascript
// Express.js webhook receiver
app.post('/api/device/connected', (req, res) => {
  const { device } = req.body;
  slack.send({
    text: `📱 Device Connected: ${device.name} (${device.platform})`,
    channel: '#device-farm'
  });
  res.sendStatus(200);
});
```

### 2. Test Orchestration

```javascript
app.post('/api/session/started', async (req, res) => {
  const { device } = req.body;
  const { appiumPort, sessionId } = device.appiumSession;
  
  // Queue test suite for this device
  await testQueue.add({
    deviceId: device.id,
    appiumUrl: `http://localhost:${appiumPort}/wd/hub`,
    platform: device.platform
  });
  
  res.sendStatus(200);
});
```

### 3. Database Tracking

```javascript
app.post('/api/device/connected', async (req, res) => {
  const { device } = req.body;
  
  await db.devices.upsert({
    id: device.id,
    name: device.name,
    platform: device.platform,
    lastConnected: new Date(),
    status: 'online'
  });
  
  res.sendStatus(200);
});
```

## Logs

Device listener and individual Appium services log to:

### Main Listener Logs
- Console output (stdout)
- `logs/installer-<date>.log`

### Per-Device Service Logs

**Windows**:
- `services/logs/AppiumBootstrap_<device_id>_stdout.log`
- `services/logs/AppiumBootstrap_<device_id>_stderr.log`

**Linux/macOS**:
- `services/logs/AppiumBootstrap_<device_id>_stdout.log`
- `services/logs/AppiumBootstrap_<device_id>_stderr.log`

Example main listener log:

```
[2025-12-07 10:30:00] [INFO] Device listener service starting...
[2025-12-07 10:30:00] [INFO] Poll interval: 5 seconds
[2025-12-07 10:30:00] [INFO] ADB available: True
[2025-12-07 10:30:00] [INFO] idevice_id available: True
[2025-12-07 10:30:05] [INFO] Device connected: emulator-5554 (Android, Emulator) - Pixel_5_API_31
[2025-12-07 10:30:05] [INFO] Starting Appium session for device emulator-5554
[2025-12-07 10:30:05] [DEBUG] Installing NSSM service: AppiumBootstrap_emulator_5554
[2025-12-07 10:30:06] [DEBUG] Starting NSSM service: AppiumBootstrap_emulator_5554
[2025-12-07 10:30:08] [INFO] NSSM service AppiumBootstrap_emulator_5554 started successfully
[2025-12-07 10:30:08] [INFO] Appium session started for emulator-5554 on port 4723 (Service: AppiumBootstrap_emulator_5554)
[2025-12-07 10:30:08] [INFO] Webhook device-connected sent successfully
```

Example device-specific Appium log (`AppiumBootstrap_emulator_5554_stdout.log`):

```
========================================
Starting Appium Server (Windows)
========================================
Appium Home: C:\Users\Localuser\AppiumBootstrap
Appium Bin: C:\Users\Localuser\AppiumBootstrap\bin
Appium Port: 4723
WDA Local Port: 0
MPEG Local Port: 0
========================================
Detected Appium version: 2.17.1
✅ DeviceFarm plugin is installed
[Appium] Welcome to Appium v2.17.1
[Appium] Appium REST http interface listener started on 0.0.0.0:4723
```

## Troubleshooting

### Device Not Detected

1. **Android**: Ensure `adb` is in PATH
   ```bash
   adb devices
   ```

2. **iOS**: Ensure `idevice_id` is installed
   ```bash
   idevice_id -l
   ```

### Appium Session Fails to Start

**Windows**:
- Check NSSM is installed: `$InstallFolder\nssm\nssm.exe`
- Verify service creation: `Get-Service AppiumBootstrap_*`
- Check service logs in `services/logs/`
- Manually test NSSM: `nssm install test powershell.exe "-Command echo test"`

**Linux/macOS**:
- Check Supervisord is running: `supervisorctl status`
- Verify config directory: `services/supervisor/conf.d/`
- Check Supervisor logs: `tail -f /var/log/supervisor/supervisord.log`
- Test Supervisord: `supervisorctl reread && supervisorctl update`

**General**:
- Verify `StartAppiumServer.ps1` or `.sh` exists in Platform scripts
- Check Node.js and Appium are installed in installFolder
- Ensure port ranges don't conflict with other services
- Check permissions (services may need elevated privileges)

### Service Won't Start

**Windows**:
```powershell
# Check service status
nssm status AppiumBootstrap_emulator_5554

# View service details
nssm dump AppiumBootstrap_emulator_5554

# Check Windows Event Log
Get-EventLog -LogName Application -Source nssm -Newest 10
```

**Linux/macOS**:
```bash
# Check program status
supervisorctl status AppiumBootstrap_emulator_5554

# View detailed status
supervisorctl tail AppiumBootstrap_emulator_5554 stderr

# Check Supervisor main log
tail -f /var/log/supervisor/supervisord.log
```

### Multiple Devices, Port Conflicts

- Verify port ranges are large enough: `(appiumEnd - appiumStart) >= max_devices`
- Check for external processes using ports: `netstat -ano | findstr :4723` (Windows) or `lsof -i :4723` (Linux/macOS)
- Increase port ranges in config if needed

### Webhooks Not Sending

- Verify webhook URL is accessible
- Check network/firewall settings
- Review timeout configuration (default 30s)
- Test webhook endpoint: `curl -X POST <webhook_url> -d '{"test": true}'`

### Service Cleanup After Crash

**Windows**:
```powershell
# List orphaned services
Get-Service | Where-Object { $_.Name -like "AppiumBootstrap_*" }

# Remove all device services
Get-Service | Where-Object { $_.Name -like "AppiumBootstrap_*" } | ForEach-Object {
    nssm stop $_.Name
    nssm remove $_.Name confirm
}
```

**Linux/macOS**:
```bash
# List all programs
supervisorctl status | grep AppiumBootstrap

# Stop all device programs
supervisorctl stop AppiumBootstrap_*

# Remove configs and reload
rm -f services/supervisor/conf.d/AppiumBootstrap_*.conf
supervisorctl reread
supervisorctl update
```

## Performance Considerations

- **Poll Interval**: Lower = faster detection, higher CPU usage
- **Port Ranges**: Ensure enough ports for max concurrent devices
- **Auto-Save Interval**: Balance between data persistence and I/O

## Security Notes

- Webhook URLs should use HTTPS in production
- Use authentication tokens in webhook headers
- Device registry may contain sensitive device identifiers
- Consider encrypting registry file at rest

## Extending

### Custom Actions on Device Connect

Edit `DeviceListenerService.OnDeviceConnectedAsync()`:

```csharp
private async Task OnDeviceConnectedAsync(Device device)
{
    _logger.LogInformation("Device connected: {DeviceId}", device.Id);
    
    _registry.AddOrUpdateDevice(device);
    await _webhookNotifier.NotifyDeviceConnectedAsync(device);
    
    // Your custom logic here
    if (device.Platform == DevicePlatform.Android)
    {
        await ConfigureAndroidDeviceAsync(device);
    }
    
    if (_config.AutoStartAppium)
    {
        var session = await _sessionManager.StartSessionAsync(device);
        // ...
    }
}
```

## Future Enhancements

- [ ] REST API for querying device status
- [ ] Web dashboard for device monitoring
- [ ] Graceful session handoff on reconnect
- [ ] Device capability caching
- [ ] Custom device filters/rules
- [ ] Multi-tenant support with device pools
