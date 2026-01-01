using System.Collections.Concurrent;
using System.Collections.Generic;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Plugins
{
    /// <summary>
    /// Registry holds plugin definitions (from configuration) and runtime instances created by the orchestrator.
    /// Definitions are registered at startup; runtime instances are added/removed as plugins are started/stopped.
    /// </summary>
    public class PluginRegistry
    {
        private readonly Dictionary<string, PluginConfig> _definitionsLookup = new();
        private readonly List<KeyValuePair<string, PluginConfig>> _definitions = new();
        private readonly ConcurrentDictionary<string, IPlugin> _instances = new();

        // Definitions (blueprints) - preserve registration order in _definitions list
        public void RegisterDefinition(string id, PluginConfig config)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            // If already registered, replace config but preserve original order
            if (_definitionsLookup.ContainsKey(id))
            {
                _definitionsLookup[id] = config;
                for (int i = 0; i < _definitions.Count; i++)
                {
                    if (_definitions[i].Key == id)
                    {
                        _definitions[i] = new KeyValuePair<string, PluginConfig>(id, config);
                        break;
                    }
                }
                return;
            }

            _definitionsLookup[id] = config;
            _definitions.Add(new KeyValuePair<string, PluginConfig>(id, config));
        }

        public virtual IEnumerable<KeyValuePair<string, PluginConfig>> GetDefinitions() => _definitions;

        public PluginConfig? GetDefinition(string id)
        {
            _definitionsLookup.TryGetValue(id, out var cfg);
            return cfg;
        }

        // Runtime instances
        public void RegisterInstance(IPlugin instance)
        {
            _instances[instance.Id] = instance;
        }

        public IEnumerable<IPlugin> GetInstances() => _instances.Values;

        public IPlugin? GetInstance(string id)
        {
            _instances.TryGetValue(id, out var p);
            return p;
        }

        public IEnumerable<IPlugin> GetInstancesByDefinitionId(string definitionId)
        {
            var prefix = definitionId + ":";
            foreach (var kv in _instances)
            {
                if (kv.Key == definitionId || kv.Key.StartsWith(prefix))
                    yield return kv.Value;
            }
        }

        public bool RemoveInstance(string id)
        {
            return _instances.TryRemove(id, out _);
        }
    }
}
