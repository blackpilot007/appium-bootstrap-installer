using System;
using System.IO;
using System.Linq;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins
{
    public class ServiceDefinitionGeneratorTests : IDisposable
    {
        private readonly string _testOutputRoot;
        private readonly ServiceDefinitionGenerator _generator;

        public ServiceDefinitionGeneratorTests()
        {
            _testOutputRoot = Path.Combine(Path.GetTempPath(), "ServiceDefTest_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputRoot);
            _generator = new ServiceDefinitionGenerator();
            // Override the private output root for testing
            typeof(ServiceDefinitionGenerator)
                .GetField("_outputRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_generator, _testOutputRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testOutputRoot))
            {
                Directory.Delete(_testOutputRoot, true);
            }
        }

        [Fact]
        public void GenerateSystemdUnit_ValidConfig_GeneratesCorrectUnit()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "test-plugin",
                Name = "Test Plugin",
                Executable = "/usr/bin/node",
                Arguments = new List<string> { "server.js", "--port", "8080" },
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var unitPath = _generator.GenerateSystemdUnit(config, installFolder);
            var unitContent = File.ReadAllText(unitPath);

            // Assert
            Assert.Contains("[Unit]", unitContent);
            Assert.Contains("Description=Appium Plugin - test-plugin", unitContent);
            Assert.Contains("[Service]", unitContent);
            Assert.Contains("ExecStart=/usr/bin/node server.js --port 8080", unitContent);
            Assert.Contains("[Install]", unitContent);
            Assert.Contains("WantedBy=multi-user.target", unitContent);
        }

        [Fact]
        public void GenerateSystemdUnit_WithTemplateVariables_ExpandsVariables()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "templated-plugin",
                Name = "Templated Plugin",
                Executable = "{installFolder}/bin/node",
                Arguments = new List<string> { "--config", "{installFolder}/config.json" },
                Enabled = true
            };
            var installFolder = "/custom/install/path";

            // Act
            var unitPath = _generator.GenerateSystemdUnit(config, installFolder);
            var unitContent = File.ReadAllText(unitPath);

            // Assert
            Assert.Contains("ExecStart=/custom/install/path/bin/node --config /custom/install/path/config.json", unitContent);
        }

        [Fact]
        public void GenerateSystemdUnit_EmptyId_UsesDefaultId()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "",
                Name = "Plugin Without ID",
                Executable = "/bin/echo",
                Arguments = new List<string> { "hello" },
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var unitPath = _generator.GenerateSystemdUnit(config, installFolder);
            var unitContent = File.ReadAllText(unitPath);

            // Assert
            Assert.Contains("Description=Appium Plugin - plugin", unitContent);
        }

        [Fact]
        public void GenerateSupervisorConf_ValidConfig_GeneratesCorrectConfig()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "supervisor-plugin",
                Name = "Supervisor Plugin",
                Executable = "/usr/bin/python3",
                Arguments = new List<string> { "app.py" },
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var confPath = _generator.GenerateSupervisorConf(config, installFolder);
            var unitContent = File.ReadAllText(confPath);

            // Assert
            Assert.Contains("[program:supervisor-plugin]", unitContent);
            Assert.Contains("command=/usr/bin/python3 app.py", unitContent);
            Assert.Contains("autostart=true", unitContent);
            Assert.Contains("autorestart=true", unitContent);
        }

        [Fact]
        public void GenerateSupervisorConf_WithEnvironmentVariables_IncludesThem()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "env-plugin",
                Name = "Environment Plugin",
                Executable = "/bin/bash",
                Arguments = new List<string> { "-c", "echo $MY_VAR" },
                EnvironmentVariables = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["MY_VAR"] = "test-value",
                    ["PATH"] = "/custom/path"
                },
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var confPath = _generator.GenerateSupervisorConf(config, installFolder);
            var configContent = File.ReadAllText(confPath);

            // Assert
            Assert.Contains("environment=MY_VAR=\"test-value\",PATH=\"/custom/path\"", configContent);
        }

        [Fact]
        public void GenerateSupervisorConf_WithWorkingDirectory_UsesCustomDirectory()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "wd-plugin",
                Name = "Working Directory Plugin",
                Executable = "./run.sh",
                WorkingDirectory = "/custom/work/dir",
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var confPath = _generator.GenerateSupervisorConf(config, installFolder);
            var configContent = File.ReadAllText(confPath);

            // Assert
            Assert.Contains("directory=/custom/work/dir", configContent);
        }

        [Fact]
        public void GenerateSupervisorConf_WithRestartPolicy_ConfiguresCorrectly()
        {
            // Arrange
            var config = new PluginConfig
            {
                Id = "restart-plugin",
                Name = "Restart Policy Plugin",
                Executable = "/bin/true",
                RestartPolicy = RestartPolicy.Always,
                Enabled = true
            };
            var installFolder = "/opt/appium";

            // Act
            var confPath = _generator.GenerateSupervisorConf(config, installFolder);
            var configContent = File.ReadAllText(confPath);

            // Assert
            Assert.Contains("autorestart=true", configContent);
        }

        [Fact]
        public void GenerateAllUnits_MultiplePlugins_GeneratesAllFiles()
        {
            // Arrange
            var configs = new[]
            {
                new PluginConfig { Id = "plugin1", Executable = "/bin/echo", Enabled = true },
                new PluginConfig { Id = "plugin2", Executable = "/bin/true", Enabled = true }
            };
            var installFolder = "/opt/appium";

            // Act
            _generator.GenerateAll(configs, installFolder);

            // Assert
            var systemdDir = Path.Combine(_testOutputRoot, "systemd");
            var supervisorDir = Path.Combine(_testOutputRoot, "supervisor");

            Assert.True(Directory.Exists(systemdDir));
            Assert.True(Directory.Exists(supervisorDir));

            Assert.True(File.Exists(Path.Combine(systemdDir, "plugin1.service")));
            Assert.True(File.Exists(Path.Combine(systemdDir, "plugin2.service")));
            Assert.True(File.Exists(Path.Combine(supervisorDir, "plugin1.conf")));
            Assert.True(File.Exists(Path.Combine(supervisorDir, "plugin2.conf")));
        }

        [Fact]
        public void GenerateInstallScript_ValidConfigs_GeneratesScript()
        {
            // Arrange
            var configs = new[]
            {
                new PluginConfig { Id = "test1", Enabled = true },
                new PluginConfig { Id = "test2", Enabled = true }
            };

            // Act
            var result = _generator.GenerateAll(configs, "/opt/appium");

            // Assert
            var scriptPath = Path.Combine(_testOutputRoot, "install-generated-services.sh");
            Assert.True(File.Exists(scriptPath));

            var content = File.ReadAllText(scriptPath);
            Assert.Contains("#!/bin/bash", content);
            Assert.Contains("test1.service", content);
            Assert.Contains("test2.service", content);
        }

        [Fact]
        public void GenerateInstallScript_EmptyConfigs_GeneratesBasicScript()
        {
            // Arrange
            var configs = Array.Empty<PluginConfig>();

            // Act
            var result = _generator.GenerateAll(configs, "/opt/appium");

            // Assert
            var scriptPath = Path.Combine(_testOutputRoot, "install-generated-services.sh");
            Assert.True(File.Exists(scriptPath));

            var content = File.ReadAllText(scriptPath);
            Assert.Contains("#!/bin/bash", content);
            Assert.Contains("No plugin services to install", content);
        }
    }
}
