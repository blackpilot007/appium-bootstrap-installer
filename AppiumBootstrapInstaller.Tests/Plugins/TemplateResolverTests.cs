using System;
using System.Collections.Generic;
using AppiumBootstrapInstaller.Models;
using AppiumBootstrapInstaller.Plugins;
using Xunit;

namespace AppiumBootstrapInstaller.Tests.Plugins
{
    public class TemplateResolverTests
    {
        [Fact]
        public void Expand_NullInput_ReturnsNull()
        {
            // Arrange
            var ctx = new PluginContext();

            // Act
            var result = TemplateResolver.Expand(null, ctx);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Expand_EmptyInput_ReturnsEmpty()
        {
            // Arrange
            var ctx = new PluginContext();

            // Act
            var result = TemplateResolver.Expand(string.Empty, ctx);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Expand_NoTokens_ReturnsInput()
        {
            // Arrange
            var ctx = new PluginContext();
            var input = "simple string without tokens";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void Expand_BraceToken_VariableFound_ReplacesToken()
        {
            // Arrange
            var ctx = new PluginContext
            {
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = "test-value"
                }
            };
            var input = "Hello {name}!";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Hello test-value!", result);
        }

        [Fact]
        public void Expand_BraceToken_VariableNotFound_LeavesToken()
        {
            // Arrange
            var ctx = new PluginContext();
            var input = "Hello {missing}!";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Hello {missing}!", result);
        }

        [Fact]
        public void Expand_BraceToken_InstallFolderProperty_ReplacesToken()
        {
            // Arrange
            var ctx = new PluginContext
            {
                InstallFolder = "/opt/appium"
            };
            var input = "Install path: {installFolder}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Install path: /opt/appium", result);
        }

        [Fact]
        public void Expand_DollarToken_EnvironmentVariable_ReplacesToken()
        {
            // Arrange
            Environment.SetEnvironmentVariable("TEST_VAR", "env-value");
            var ctx = new PluginContext();
            var input = "Value: ${TEST_VAR}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Value: env-value", result);

            // Cleanup
            Environment.SetEnvironmentVariable("TEST_VAR", null);
        }

        [Fact]
        public void Expand_DollarToken_InstallFolder_ReplacesToken()
        {
            // Arrange
            var ctx = new PluginContext
            {
                InstallFolder = "/custom/path"
            };
            var input = "Path: ${INSTALL_FOLDER}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Path: /custom/path", result);
        }

        [Fact]
        public void Expand_DollarToken_VariableFromContext_ReplacesToken()
        {
            // Arrange
            var ctx = new PluginContext
            {
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["custom_var"] = "context-value"
                }
            };
            var input = "Value: ${custom_var}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("Value: context-value", result);
        }

        [Fact]
        public void Expand_MultipleTokens_ReplacesAll()
        {
            // Arrange
            Environment.SetEnvironmentVariable("ENV_VAR", "env-val");
            var ctx = new PluginContext
            {
                InstallFolder = "/install/path",
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ctx_var"] = "context-val"
                }
            };
            var input = "{ctx_var} at ${INSTALL_FOLDER} with ${ENV_VAR}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("context-val at /install/path with env-val", result);

            // Cleanup
            Environment.SetEnvironmentVariable("ENV_VAR", null);
        }

        [Fact]
        public void Expand_CaseInsensitive_Variables()
        {
            // Arrange
            var ctx = new PluginContext
            {
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TestVar"] = "value"
                }
            };
            var input = "{testvar} and {TESTVAR}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("value and value", result);
        }

        [Fact]
        public void Expand_NestedTokens_ProcessesInOrder()
        {
            // Arrange
            var ctx = new PluginContext
            {
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["inner"] = "replaced"
                }
            };
            var input = "${inner} and {inner}";

            // Act
            var result = TemplateResolver.Expand(input, ctx);

            // Assert
            Assert.Equal("replaced and replaced", result);
        }

        [Fact]
        public void ExpandList_NullInput_ReturnsNull()
        {
            // Arrange
            var ctx = new PluginContext();

            // Act
            var result = TemplateResolver.ExpandList(null, ctx);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExpandList_EmptyList_ReturnsEmptyList()
        {
            // Arrange
            var ctx = new PluginContext();
            var input = new List<string>();

            // Act
            var result = TemplateResolver.ExpandList(input, ctx);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ExpandList_WithTokens_ReplacesInEachItem()
        {
            // Arrange
            var ctx = new PluginContext
            {
                Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["port"] = "4723"
                }
            };
            var input = new List<string> { "--port", "{port}", "--host", "localhost" };

            // Act
            var result = TemplateResolver.ExpandList(input, ctx);

            // Assert
            Assert.Equal(new[] { "--port", "4723", "--host", "localhost" }, result);
        }

        [Fact]
        public void ExpandList_MixedTokensAndPlainText_ProcessesCorrectly()
        {
            // Arrange
            var ctx = new PluginContext
            {
                InstallFolder = "/opt/appium"
            };
            var input = new List<string> { "start", "${INSTALL_FOLDER}/bin/appium", "--config", "{installFolder}/config.json" };

            // Act
            var result = TemplateResolver.ExpandList(input, ctx);

            // Assert
            Assert.Equal(new[] { "start", "/opt/appium/bin/appium", "--config", "/opt/appium/config.json" }, result);
        }
    }
}