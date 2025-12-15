using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins
{
    public static class TemplateResolver
    {
        private static readonly Regex BraceToken = new("\\{([^}]+)\\}", RegexOptions.Compiled);
        private static readonly Regex DollarToken = new("\\$\\{([^}]+)\\}", RegexOptions.Compiled);

        public static string? Expand(string? input, PluginContext ctx)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // First expand {name} tokens from ctx.Variables (case-insensitive)
            string result = BraceToken.Replace(input, match =>
            {
                var key = match.Groups[1].Value;
                if (ctx?.Variables != null && ctx.Variables.TryGetValue(key, out var val))
                {
                    return val?.ToString() ?? string.Empty;
                }
                // fallback: check context properties
                if (string.Equals(key, "installFolder", StringComparison.OrdinalIgnoreCase) && ctx != null)
                {
                    return ctx.InstallFolder ?? string.Empty;
                }
                return match.Value; // leave as-is
            });

            // Then expand ${VAR} from environment or context
            result = DollarToken.Replace(result, match =>
            {
                var name = match.Groups[1].Value;
                // check common context properties
                if (string.Equals(name, "INSTALL_FOLDER", StringComparison.OrdinalIgnoreCase) && ctx != null)
                {
                    return ctx.InstallFolder ?? string.Empty;
                }

                // try environment variable
                var env = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(env)) return env;

                // try ctx variables with case-insensitive match
                if (ctx?.Variables != null)
                {
                    var kv = ctx.Variables.FirstOrDefault(k => string.Equals(k.Key, name, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(kv.Key)) return kv.Value?.ToString() ?? string.Empty;
                }

                return match.Value; // leave as-is
            });

            return result;
        }

        public static List<string>? ExpandList(List<string>? items, PluginContext ctx)
        {
            if (items == null) return null;
            return items.Select(i => Expand(i, ctx) ?? string.Empty).ToList();
        }

        public static Dictionary<string, string>? ExpandDictionary(Dictionary<string, string>? dict, PluginContext ctx)
        {
            if (dict == null) return null;
            var outd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
            {
                var key = Expand(kv.Key, ctx) ?? kv.Key;
                var val = Expand(kv.Value, ctx) ?? string.Empty;
                outd[key] = val;
            }
            return outd;
        }
    }
}
