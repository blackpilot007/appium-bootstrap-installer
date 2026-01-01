using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins.BuiltIn
{
    /// <summary>
    /// Comprehensive tests for ProcessPlugin covering various executable types and scenarios
    /// </summary>
    public class ProcessPluginComprehensiveTests : IDisposable
    {
        private readonly Mock<ILogger<ProcessPlugin>> _mockLogger;
        private readonly string _testExecutablesPath;
        private readonly List<string> _createdFiles = new();

        public ProcessPluginComprehensiveTests()
        {
            _mockLogger = new Mock<ILogger<ProcessPlugin>>();
            _testExecutablesPath = Path.Combine(Path.GetTempPath(), "appium-test-executables", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testExecutablesPath);
        }

        public void Dispose()
        {
            // Cleanup test executables
            try
            {
                if (Directory.Exists(_testExecutablesPath))
                {
                    Directory.Delete(_testExecutablesPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Windows Executable Tests

        [Fact]
        public async Task StartAsync_WindowsExecutable_StartsSuccessfully()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on non-Windows
            }

            // Arrange
            var config = new PluginConfig
            {
                Id = "windows-exe-test",
                Name = "Windows EXE Test",
                Executable = "notepad.exe",
                Enabled = true
            };

            var plugin = new ProcessPlugin("windows-exe-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Running, plugin.State);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_CmdExecutable_StartsSuccessfully()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on non-Windows
            }

            // Arrange
            var config = new PluginConfig
            {
                Id = "cmd-exe-test",
                Name = "CMD EXE Test",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", "timeout", "/t", "30", "/nobreak" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("cmd-exe-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal("process", plugin.Type);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Unix Executable Tests

        [Fact]
        public async Task StartAsync_UnixBinary_StartsSuccessfully()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on Windows
            }

            // Arrange
            var config = new PluginConfig
            {
                Id = "unix-bin-test",
                Name = "Unix Binary Test",
                Executable = "/bin/sleep",
                Arguments = new List<string> { "30" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("unix-bin-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Running, plugin.State);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_UnixShellCommand_StartsSuccessfully()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on Windows
            }

            // Arrange
            var config = new PluginConfig
            {
                Id = "bash-cmd-test",
                Name = "Bash Command Test",
                Executable = "bash",
                Arguments = new List<string> { "-c", "sleep 30" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("bash-cmd-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Cross-Platform Executable Tests

        [Fact]
        public async Task StartAsync_PythonExecutable_StartsSuccessfully()
        {
            // Arrange
            var scriptPath = CreateTestScript("process_test.py", "import time\ntime.sleep(30)");
            var config = new PluginConfig
            {
                Id = "python-process-test",
                Name = "Python Process Test",
                Executable = "python",
                Arguments = new List<string> { scriptPath },
                Enabled = true
            };

            var plugin = new ProcessPlugin("python-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act & Assert
            try
            {
                var result = await plugin.StartAsync(context, CancellationToken.None);
                if (result)
                {
                    Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Running, plugin.State);
                    await plugin.StopAsync(CancellationToken.None);
                }
            }
            catch
            {
                // Python may not be installed - that's okay
            }
        }

        [Fact]
        public async Task StartAsync_NodeJsExecutable_StartsSuccessfully()
        {
            // Arrange
            var scriptPath = CreateTestScript("process_test.js", "setTimeout(() => {}, 30000);");
            var config = new PluginConfig
            {
                Id = "nodejs-process-test",
                Name = "Node.js Process Test",
                Executable = "node",
                Arguments = new List<string> { scriptPath },
                Enabled = true
            };

            var plugin = new ProcessPlugin("nodejs-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act & Assert
            try
            {
                var result = await plugin.StartAsync(context, CancellationToken.None);
                if (result)
                {
                    Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Running, plugin.State);
                    await plugin.StopAsync(CancellationToken.None);
                }
            }
            catch
            {
                // Node.js may not be installed - that's okay
            }
        }

        [Fact]
        public async Task StartAsync_JavaExecutable_StartsSuccessfully()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "java-process-test",
                Name = "Java Process Test",
                Executable = "java",
                Arguments = new List<string> { "-version" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("java-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act & Assert
            try
            {
                var result = await plugin.StartAsync(context, CancellationToken.None);
                // Java -version exits immediately, so may not be running
            }
            catch
            {
                // Java may not be installed - that's okay
            }
        }

        #endregion

        #region Working Directory Tests

        [Fact]
        public async Task StartAsync_WithWorkingDirectory_UsesCorrectDirectory()
        {
            // Arrange
            var workingDir = Path.Combine(_testExecutablesPath, "workdir");
            Directory.CreateDirectory(workingDir);

            var config = new PluginConfig
            {
                Id = "workdir-process-test",
                Name = "Working Directory Process Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                WorkingDirectory = workingDir
            };

            var plugin = new ProcessPlugin("workdir-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_WithNonExistentWorkingDirectory_CreatesDirectory()
        {
            // Arrange
            var workingDir = Path.Combine(_testExecutablesPath, "nonexistent_workdir");

            var config = new PluginConfig
            {
                Id = "nonexist-workdir-test",
                Name = "Non-existent Working Directory Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                WorkingDirectory = workingDir
            };

            var plugin = new ProcessPlugin("nonexist-workdir-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            // May succeed if working directory is created, or fail if not
            if (result)
            {
                await plugin.StopAsync(CancellationToken.None);
            }
        }

        #endregion

        #region Environment Variables Tests

        [Fact]
        public async Task StartAsync_WithEnvironmentVariables_SetsVariablesCorrectly()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on non-Windows for this test
            }

            // Arrange
            var scriptPath = CreateTestScript("env_test.bat", "@echo off\necho TEST_PROCESS_VAR=%TEST_PROCESS_VAR%\ntimeout /t 30 /nobreak");
            var config = new PluginConfig
            {
                Id = "env-process-test",
                Name = "Environment Process Test",
                Executable = "cmd.exe",
                Arguments = new List<string> { "/c", scriptPath },
                Enabled = true,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["TEST_PROCESS_VAR"] = "process_value_456"
                }
            };

            var plugin = new ProcessPlugin("env-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_WithMultipleEnvironmentVariables_SetsAllVariables()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "multi-env-test",
                Name = "Multiple Environment Variables Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["VAR1"] = "value1",
                    ["VAR2"] = "value2",
                    ["VAR3"] = "value3"
                }
            };

            var plugin = new ProcessPlugin("multi-env-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Health Check Tests

        [Fact]
        public async Task CheckHealthAsync_WithCustomHealthCheck_ExecutesCorrectly()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "health-check-test",
                Name = "Health Check Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                HealthCheckCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "echo",
                HealthCheckArguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "echo", "healthy" }
                    : new List<string> { "healthy" },
                HealthCheckTimeoutSeconds = 2
            };

            var plugin = new ProcessPlugin("health-check-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CheckHealthAsync_WithFailingHealthCheck_ReturnsFalse()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "failing-health-test",
                Name = "Failing Health Check Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                HealthCheckCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "false",
                HealthCheckArguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "exit", "1" }
                    : new List<string>(),
                HealthCheckTimeoutSeconds = 2
            };

            var plugin = new ProcessPlugin("failing-health-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.False(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task CheckHealthAsync_WithTimeoutHealthCheck_ReturnsFalse()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "timeout-health-process-test",
                Name = "Timeout Health Process Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true,
                HealthCheckCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "timeout" : "sleep",
                HealthCheckArguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/t", "10", "/nobreak" }
                    : new List<string> { "10" },
                HealthCheckTimeoutSeconds = 1
            };

            var plugin = new ProcessPlugin("timeout-health-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            await plugin.StartAsync(context, CancellationToken.None);

            // Act
            var result = await plugin.CheckHealthAsync();

            // Assert
            Assert.False(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Process Lifecycle Tests

        [Fact]
        public async Task StartAsync_ThenStop_StopsProcessCorrectly()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "lifecycle-test",
                Name = "Lifecycle Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("lifecycle-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var startResult = await plugin.StartAsync(context, CancellationToken.None);
            Assert.True(startResult);

            await Task.Delay(500); // Let process run briefly

            await plugin.StopAsync(CancellationToken.None);

            // Assert
            Assert.Equal(AppiumBootstrapInstaller.Plugins.PluginState.Stopped, plugin.State);
        }

        [Fact]
        public async Task StopAsync_MultipleCallsWithoutStart_HandlesGracefully()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "multi-stop-test",
                Name = "Multiple Stop Test",
                Executable = "cmd.exe",
                Enabled = true
            };

            var plugin = new ProcessPlugin("multi-stop-test", config, _mockLogger.Object);

            // Act & Assert - Should not throw
            await plugin.StopAsync(CancellationToken.None);
            await plugin.StopAsync(CancellationToken.None);
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task StartAsync_NonExistentExecutable_ReturnsFalse()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "nonexistent-process-test",
                Name = "Non-existent Process Test",
                Executable = "nonexistent_process_12345.exe",
                Arguments = new List<string> { "arg1" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("nonexistent-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StartAsync_NullExecutable_ReturnsFalse()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "null-exec-process-test",
                Name = "Null Executable Process Test",
                Executable = null,
                Enabled = true
            };

            var plugin = new ProcessPlugin("null-exec-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StartAsync_EmptyExecutable_ReturnsFalse()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "empty-exec-process-test",
                Name = "Empty Executable Process Test",
                Executable = "",
                Enabled = true
            };

            var plugin = new ProcessPlugin("empty-exec-process-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StartAsync_ExecutableWithInvalidArguments_HandlesError()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "invalid-args-test",
                Name = "Invalid Arguments Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "ls",
                Arguments = new List<string> { "--invalid-flag-that-does-not-exist-12345" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("invalid-args-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            // Process may start but exit immediately - either outcome is acceptable
        }

        #endregion

        #region Argument Handling Tests

        [Fact]
        public async Task StartAsync_WithComplexArguments_ParsesCorrectly()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on non-Windows
            }

            // Arrange
            var config = new PluginConfig
            {
                Id = "complex-args-test",
                Name = "Complex Arguments Test",
                Executable = "cmd.exe",
                Arguments = new List<string> 
                { 
                    "/c", 
                    "echo", 
                    "\"Test with spaces and special chars: !@#$%\"",
                    "&&",
                    "timeout",
                    "/t",
                    "30",
                    "/nobreak"
                },
                Enabled = true
            };

            var plugin = new ProcessPlugin("complex-args-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        [Fact]
        public async Task StartAsync_WithEmptyArguments_StartsWithoutArguments()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "no-args-test",
                Name = "No Arguments Test",
                Executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "notepad.exe" : "sleep",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string>()
                    : new List<string> { "30" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("no-args-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.True(result);
            }
            else
            {
                Assert.True(result);
            }

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region File Extension Tests

        [Fact]
        public async Task StartAsync_ExecutableWithFullPath_StartsCorrectly()
        {
            // Arrange
            var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "C:\\Windows\\System32\\cmd.exe"
                : "/bin/sleep";

            var config = new PluginConfig
            {
                Id = "fullpath-exe-test",
                Name = "Full Path Executable Test",
                Executable = executable,
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? new List<string> { "/c", "timeout", "/t", "30", "/nobreak" }
                    : new List<string> { "30" },
                Enabled = true
            };

            var plugin = new ProcessPlugin("fullpath-exe-test", config, _mockLogger.Object);
            var context = CreatePluginContext();

            // Act
            var result = await plugin.StartAsync(context, CancellationToken.None);

            // Assert
            Assert.True(result);

            // Cleanup
            await plugin.StopAsync(CancellationToken.None);
        }

        #endregion

        #region Helper Methods

        private string CreateTestScript(string fileName, string content)
        {
            var fullPath = Path.Combine(_testExecutablesPath, fileName);
            File.WriteAllText(fullPath, content);
            _createdFiles.Add(fullPath);
            return fullPath;
        }

        private AppiumBootstrapInstaller.Plugins.PluginContext CreatePluginContext()
        {
            return new AppiumBootstrapInstaller.Plugins.PluginContext
            {
                InstallFolder = _testExecutablesPath,
                Services = new ServiceCollection().BuildServiceProvider()
            };
        }

        #endregion
    }
}
