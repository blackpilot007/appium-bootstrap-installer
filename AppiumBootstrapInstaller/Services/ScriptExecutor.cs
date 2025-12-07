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

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Executes platform-specific installation scripts
    /// </summary>
    public class ScriptExecutor
    {
        private readonly string _platformScriptsRoot;
        private readonly ILogger<ScriptExecutor> _logger;

        public enum OperatingSystem
        {
            Windows,
            MacOS,
            Linux,
            Unknown
        }

        public ScriptExecutor(string platformScriptsRoot, ILogger<ScriptExecutor> logger)
        {
            _platformScriptsRoot = platformScriptsRoot;
            _logger = logger;
        }

        /// <summary>
        /// Detects the current operating system
        /// </summary>
        public OperatingSystem DetectOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OperatingSystem.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return OperatingSystem.MacOS;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OperatingSystem.Linux;
            else
                return OperatingSystem.Unknown;
        }

        /// <summary>
        /// Gets the appropriate installation script path for the current OS
        /// </summary>
        public string GetInstallationScriptPath(OperatingSystem os)
        {
            string scriptName = os switch
            {
                OperatingSystem.Windows => "InstallDependencies.ps1",
                OperatingSystem.MacOS => "InstallDependencies.sh",
                OperatingSystem.Linux => "InstallDependencies.sh",
                _ => throw new PlatformNotSupportedException($"Unsupported operating system: {os}")
            };

            string osFolder = os switch
            {
                OperatingSystem.Windows => "Windows",
                OperatingSystem.MacOS => "MacOS",
                OperatingSystem.Linux => "Linux",
                _ => throw new PlatformNotSupportedException($"Unsupported operating system: {os}")
            };

            string scriptPath = Path.Combine(_platformScriptsRoot, osFolder, "Scripts", scriptName);

            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Installation script not found at path: {ScriptPath}", scriptPath);
                throw new FileNotFoundException($"Installation script not found: {scriptPath}");
            }

            return scriptPath;
        }

        /// <summary>
        /// Gets the appropriate service setup script path for the current OS
        /// </summary>
        public string GetServiceSetupScriptPath(OperatingSystem os)
        {
            string scriptName = os switch
            {
                OperatingSystem.Windows => "ServiceSetup.ps1",
                OperatingSystem.MacOS => "SupervisorSetup.sh",
                OperatingSystem.Linux => "SystemdSetup.sh",
                _ => throw new PlatformNotSupportedException($"Unsupported operating system: {os}")
            };

            string osFolder = os switch
            {
                OperatingSystem.Windows => "Windows",
                OperatingSystem.MacOS => "MacOS",
                OperatingSystem.Linux => "Linux",
                _ => throw new PlatformNotSupportedException($"Unsupported operating system: {os}")
            };

            string scriptPath = Path.Combine(_platformScriptsRoot, osFolder, "Scripts", scriptName);

            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Service setup script not found at path: {ScriptPath}", scriptPath);
                throw new FileNotFoundException($"Service setup script not found: {scriptPath}");
            }

            return scriptPath;
        }

        /// <summary>
        /// Builds command-line arguments from configuration
        /// </summary>
        public string BuildArguments(InstallConfig config, OperatingSystem os)
        {
            var args = new List<string>();

            // Common arguments for all platforms
            if (os == OperatingSystem.Windows)
            {
                // Determine NVM version (prefer platform-specific, fallback to root)
                string nvmVersion = config.PlatformSpecific?.Windows?.NvmVersion ?? config.NvmVersion;


                // PowerShell parameters
                args.Add($"-InstallFolder \"{config.InstallFolder}\"");
                args.Add($"-NodeVersion \"{config.NodeVersion}\"");
                args.Add($"-AppiumVersion \"{config.AppiumVersion}\"");
                args.Add($"-NvmVersion \"{nvmVersion}\"");

                // Drivers
                var xcuitestDriver = config.Drivers.FirstOrDefault(d => d.Name.Equals("xcuitest", StringComparison.OrdinalIgnoreCase));
                var uiautomator2Driver = config.Drivers.FirstOrDefault(d => d.Name.Equals("uiautomator2", StringComparison.OrdinalIgnoreCase));

                if (xcuitestDriver != null && !string.IsNullOrEmpty(xcuitestDriver.Version))
                {
                    args.Add($"-XCUITestVersion \"{xcuitestDriver.Version}\"");
                }

                if (uiautomator2Driver != null && !string.IsNullOrEmpty(uiautomator2Driver.Version))
                {
                    args.Add($"-DriverVersion \"{uiautomator2Driver.Version}\"");
                }

                // Platform-specific settings
                if (config.PlatformSpecific?.Windows != null)
                {
                    var winConfig = config.PlatformSpecific.Windows;
                    if (winConfig.InstallIOSSupport)
                        args.Add("-InstallIOSSupport");
                    if (!winConfig.InstallAndroidSupport)
                        args.Add("-InstallAndroidSupport:$false");
                }

                // Driver installation flags based on enabled status
                if (xcuitestDriver != null && !xcuitestDriver.Enabled)
                    args.Add("-InstallXCUITest:$false");
                if (uiautomator2Driver != null && !uiautomator2Driver.Enabled)
                    args.Add("-InstallUiAutomator:$false");

                // Plugin installation flags based on enabled status
                var deviceFarmPlugin = config.Plugins.FirstOrDefault(p => p.Name.Equals("device-farm", StringComparison.OrdinalIgnoreCase));
                if (deviceFarmPlugin != null && !deviceFarmPlugin.Enabled)
                    args.Add("-InstallDeviceFarm:$false");
            }
            else // macOS and Linux use bash-style arguments
            {
                // Determine NVM version (prefer platform-specific, fallback to root)
                string nvmVersion = config.NvmVersion;

                if (os == OperatingSystem.MacOS && config.PlatformSpecific?.MacOS?.NvmVersion != null)
                {
                    nvmVersion = config.PlatformSpecific.MacOS.NvmVersion;
                }
                else if (os == OperatingSystem.Linux && config.PlatformSpecific?.Linux?.NvmVersion != null)
                {
                    nvmVersion = config.PlatformSpecific.Linux.NvmVersion;
                }

                args.Add($"--install_folder=\"{config.InstallFolder}\"");
                args.Add($"--node_version=\"{config.NodeVersion}\"");
                args.Add($"--appium_version=\"{config.AppiumVersion}\"");
                args.Add($"--nvm_version=\"{nvmVersion}\"");

                // Drivers
                var xcuitestDriver = config.Drivers.FirstOrDefault(d => d.Name.Equals("xcuitest", StringComparison.OrdinalIgnoreCase));
                var uiautomator2Driver = config.Drivers.FirstOrDefault(d => d.Name.Equals("uiautomator2", StringComparison.OrdinalIgnoreCase));

                if (xcuitestDriver != null && !string.IsNullOrEmpty(xcuitestDriver.Version))
                {
                    args.Add($"--xcuitest_version=\"{xcuitestDriver.Version}\"");
                }

                if (uiautomator2Driver != null && !string.IsNullOrEmpty(uiautomator2Driver.Version))
                {
                    args.Add($"--uiautomator2_version=\"{uiautomator2Driver.Version}\"");
                }

                // Platform-specific settings for macOS
                if (os == OperatingSystem.MacOS && config.PlatformSpecific?.MacOS != null)
                {
                    var macConfig = config.PlatformSpecific.MacOS;
                    if (!string.IsNullOrEmpty(macConfig.LibimobiledeviceVersion))
                    {
                        args.Add($"--libimobiledevice_version=\"{macConfig.LibimobiledeviceVersion}\"");
                    }
                }

                // Driver installation flags based on enabled status (macOS/Linux)
                if (xcuitestDriver != null && !xcuitestDriver.Enabled)
                    args.Add("--install_xcuitest=false");
                if (uiautomator2Driver != null && !uiautomator2Driver.Enabled)
                    args.Add("--install_uiautomator=false");

                // Plugin installation flags based on enabled status (macOS/Linux)
                var deviceFarmPlugin = config.Plugins.FirstOrDefault(p => p.Name.Equals("device-farm", StringComparison.OrdinalIgnoreCase));
                if (deviceFarmPlugin != null && !deviceFarmPlugin.Enabled)
                    args.Add("--install_device_farm=false");

                // Platform-specific settings for Linux
                if (os == OperatingSystem.Linux && config.PlatformSpecific?.Linux != null)
                {
                    var linuxConfig = config.PlatformSpecific.Linux;
                    if (linuxConfig.InstallIOSSupport)
                        args.Add("--install_ios_support=true");
                    if (!linuxConfig.InstallAndroidSupport)
                        args.Add("--install_android_support=false");
                }
            }

            return string.Join(" ", args);
        }

        /// <summary>
        /// Sets execute permissions on a script file (Unix-like systems only)
        /// </summary>
        public void SetExecutePermissions(string scriptPath)
        {
            var os = DetectOperatingSystem();
            if (os == OperatingSystem.Windows)
            {
                // Windows doesn't need execute permissions
                return;
            }

            try
            {
                _logger.LogInformation("Setting execute permissions on: {ScriptPath}", scriptPath);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{scriptPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    _logger.LogWarning("Failed to set execute permissions: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set execute permissions");
            }
        }

        /// <summary>
        /// Deletes the installation folder if it exists
        /// </summary>
        public void CleanInstallationFolder(string installFolder)
        {
            if (Directory.Exists(installFolder))
            {
                _logger.LogInformation("Cleaning installation folder: {InstallFolder}", installFolder);
                try
                {
                    Directory.Delete(installFolder, true);
                    _logger.LogInformation("Installation folder cleaned successfully.");
                }
                catch (IOException ex)
                {
                    _logger.LogWarning("Failed to fully clean installation folder (files in use): {Message}. Continuing installation...", ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning("Failed to clean installation folder (access denied): {Message}. Continuing installation...", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to clean installation folder: {Message}. Continuing installation...", ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("Installation folder does not exist, skipping cleanup: {InstallFolder}", installFolder);
            }
        }

        /// <summary>
        /// Executes the installation script with real-time output streaming
        /// </summary>
        public int ExecuteScript(string scriptPath, string arguments, bool dryRun = false)
        {
            var os = DetectOperatingSystem();

            if (dryRun)
            {
                _logger.LogInformation("DRY RUN MODE - No scripts will execute");
                _logger.LogInformation("OS: {OS}", os);
                _logger.LogInformation("Script: {ScriptPath}", scriptPath);
                _logger.LogInformation("Arguments: {Arguments}", arguments);
                return 0;
            }

            // Set execute permissions for Unix-like systems
            if (os != OperatingSystem.Windows)
            {
                SetExecutePermissions(scriptPath);
            }

            string fileName;
            string processArguments;

            if (os == OperatingSystem.Windows)
            {
                fileName = "powershell.exe";
                processArguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}";
            }
            else
            {
                fileName = "/bin/bash";
                processArguments = $"\"{scriptPath}\" {arguments}";
            }

            _logger.LogInformation("Executing Installation Script");
            _logger.LogInformation("OS: {OS}", os);
            _logger.LogInformation("Shell: {Shell}", fileName);
            _logger.LogInformation("Script: {ScriptPath}", scriptPath);
            // Hide arguments when logging if they contain secrets (none for now)
            _logger.LogDebug("Arguments: {Arguments}", arguments);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = processArguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Stream output in real-time
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogInformation("{Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogError("{Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            _logger.LogInformation("Script Execution Completed. Exit Code: {ExitCode}", process.ExitCode);

            return process.ExitCode;
        }
    }
}
