# Troubleshooting Guide

Common issues and their solutions.

## Installation Issues

### Configuration file not found

**Error:**
```
Error: Configuration file not found
```

**Solutions:**
```bash
# Specify config explicitly
AppiumBootstrapInstaller --config /full/path/to/config.json

# Or copy to expected location
cp config.sample.json config.json
```

### Platform scripts not found

**Error:**
```
Error: Platform scripts directory not found
```

**Solution:** Ensure `Platform/` folder exists alongside the executable:
```
your-app-folder/
├── AppiumBootstrapInstaller.exe
└── Platform/
    ├── Windows/
    ├── MacOS/
    └── Linux/
```

### Permission denied (Linux/macOS)

**Error:**
```
Error: Permission denied
```

**Solutions:**
```bash
# Make executable
chmod +x AppiumBootstrapInstaller

# Run with sudo if needed
sudo ./AppiumBootstrapInstaller
```

### PowerShell execution policy (Windows)

**Error:**
```
running scripts is disabled on this system
```

**Solutions:**
```powershell
# Temporary bypass
powershell -ExecutionPolicy Bypass -File .\build-all.ps1

# Or set execution policy (requires admin)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Device Detection Issues

### Android devices not detected

**Diagnosis:**
```bash
# Check ADB installation
adb version

# Check connected devices
adb devices

# Check USB debugging enabled on device
```

**Solutions:**
```bash
# Restart ADB server
adb kill-server
adb start-server

# Check USB drivers (Windows)
# Install from device manufacturer

# Verify device authorization
# Check device screen for authorization prompt
```

### iOS devices not detected (macOS/Linux)

**Diagnosis:**
```bash
# Check libimobiledevice installation
idevice_id --version

# List connected iOS devices
idevice_id -l

# Check usbmuxd status (Linux)
sudo systemctl status usbmuxd
```

**Solutions:**
```bash
# Install libimobiledevice (macOS)
brew install libimobiledevice

# Install libimobiledevice (Ubuntu/Debian)
sudo apt-get install libimobiledevice-tools usbmuxd

# Restart usbmuxd (Linux)
sudo systemctl restart usbmuxd

# Trust computer on iOS device
# Check device screen for trust prompt
```

### Devices detected but Appium not starting

**Diagnosis:**
```bash
# Check logs
tail -f logs/device-listener-*.log

# Verify Appium installation
{InstallFolder}/node_modules/.bin/appium --version

# Check port availability
netstat -an | grep 4723
```

**Solutions:**
1. Check port ranges in config.json
2. Verify NSSM/Supervisor service status
3. Check Appium installation logs
4. Ensure sufficient available ports

## Service Issues

### Windows - NSSM Service Won't Start

**Diagnosis:**
```powershell
# Check NSSM installation
Test-Path "C:\path\to\nssm\nssm.exe"

# Check service configuration
nssm status AppiumDeviceListener

# View service logs
Get-Content "C:\path\to\logs\device-listener.log" -Tail 50
```

**Solutions:**
```powershell
# Reinstall service
nssm remove AppiumDeviceListener confirm
nssm install AppiumDeviceListener "C:\path\to\AppiumBootstrapInstaller.exe" --listen

# Check service account permissions
nssm set AppiumDeviceListener ObjectName LocalSystem

# Verify executable path
nssm get AppiumDeviceListener Application
```

### macOS - Supervisor Issues

**Diagnosis:**
```bash
# Check supervisor installation
which supervisord

# Check configuration
supervisorctl status

# View logs
tail -f /path/to/logs/supervisord.log
```

**Solutions:**
```bash
# Reload configuration
supervisorctl reread
supervisorctl update

# Restart supervisor
brew services restart supervisor

# Check configuration syntax
supervisord -c /path/to/supervisord.conf -n
```

### Linux - systemd Issues

**Diagnosis:**
```bash
# Check service status
sudo systemctl status appium-device-listener

# View detailed logs
sudo journalctl -u appium-device-listener --no-pager -n 100

# Check service file
systemctl cat appium-device-listener
```

**Solutions:**
```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service
sudo systemctl enable appium-device-listener

# Restart service
sudo systemctl restart appium-device-listener

# Check service dependencies
systemctl list-dependencies appium-device-listener
```

## Port Issues

### Port already in use

**Error:**
```
Warning: Port 4723 is already in use
```

**Diagnosis:**
```bash
# Check what's using the port (Linux/macOS)
lsof -i :4723

# Check what's using the port (Windows)
netstat -ano | findstr :4723
```

**Solutions:**

1. **Adjust port ranges in config.json:**
```json
{
  "PortRanges": {
    "AppiumStart": 5000,
    "AppiumEnd": 5100
  }
}
```

2. **Kill conflicting process:**
```bash
# Linux/macOS
kill -9 <PID>

# Windows
taskkill /PID <PID> /F
```

### Port pool exhausted

**Warning:**
```
WARNING: Port pool usage at 90%
```

**Solutions:**

1. **Increase port range:**
```json
{
  "PortRanges": {
    "AppiumStart": 4723,
    "AppiumEnd": 5000  // Increase upper limit
  }
}
```

2. **Check for zombie sessions:**
```bash
# Check logs for disconnected devices
grep "disconnected" logs/device-listener-*.log

# Restart device listener to clean up
```

## Build Issues

### .NET SDK not found

**Error:**
```
The command 'dotnet' is not found
```

**Solutions:**
```bash
# Install .NET SDK
# Download from: https://dotnet.microsoft.com/download

# Verify installation
dotnet --version
```

### Build fails with trim warnings

**Warning:**
```
warning IL2026: JSON serialization warnings
```

**Note:** These are expected warnings for trimmed builds and can be safely ignored. The application functions correctly despite these warnings.

## Runtime Issues

### High memory usage

**Symptoms:** Application consuming excessive memory

**Solutions:**
1. Reduce `DeviceListenerPollInterval` to decrease frequency
2. Disable `DeviceRegistry` if not needed
3. Check for memory leaks in logs
4. Restart device listener service

### Slow device detection

**Symptoms:** Delays in detecting connected/disconnected devices

**Solutions:**
1. Decrease `DeviceListenerPollInterval` (but increases CPU usage)
2. Check system USB performance
3. Verify ADB/libimobiledevice performance
4. Check for system resource constraints

### Metrics not appearing

**Symptoms:** No metrics in logs

**Solutions:**
1. Ensure logging level is set to Information or higher
2. Check log file permissions
3. Verify metrics collection is enabled (built-in)
4. Wait for 5-minute metric interval

## Logging Issues

### Log files not created

**Diagnosis:**
```bash
# Check logs directory
ls -la logs/

# Check permissions
ls -ld logs/
```

**Solutions:**
```bash
# Create logs directory
mkdir -p logs

# Fix permissions
chmod 755 logs

# On Windows, ensure write permissions for service account
```

### Cannot find correlation ID

**Search for specific device operation:**
```bash
# Linux/macOS
grep "CORR-20251207-abc123" logs/device-listener-*.log

# Windows PowerShell
Select-String -Path "logs\device-listener-*.log" -Pattern "CORR-20251207-abc123"
```

## Getting Help

If you're still experiencing issues:

1. **Check logs:** `logs/` directory contains detailed information
2. **Enable debug logging:** Set log level to Debug in code
3. **Search issues:** [GitHub Issues](https://github.com/blackpilot007/appium-bootstrap-installer/issues)
4. **Create new issue:** Include:
   - Operating system and version
   - Appium Bootstrap Installer version
   - Configuration file (sanitized)
   - Relevant log snippets
   - Steps to reproduce

## Diagnostic Commands

### Collect diagnostic information

**Linux/macOS:**
```bash
#!/bin/bash
echo "=== System Info ==="
uname -a
echo ""
echo "=== .NET Version ==="
dotnet --version
echo ""
echo "=== ADB Version ==="
adb version
echo ""
echo "=== Connected Devices ==="
adb devices
echo ""
echo "=== Recent Logs ==="
tail -n 50 logs/device-listener-*.log
```

**Windows PowerShell:**
```powershell
Write-Host "=== System Info ==="
Get-ComputerInfo | Select-Object OsName, OsVersion, OsArchitecture

Write-Host "`n=== ADB Version ==="
adb version

Write-Host "`n=== Connected Devices ==="
adb devices

Write-Host "`n=== Recent Logs ==="
Get-Content logs\device-listener-*.log -Tail 50
```
