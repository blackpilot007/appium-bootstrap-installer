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

using System.Collections.Concurrent;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Simple event bus for pub/sub messaging
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
        private readonly ILogger<EventBus> _logger;
        private readonly object _lock = new();

        public EventBus(ILogger<EventBus> logger)
        {
            _logger = logger;
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var list))
                {
                    list = new List<Delegate>();
                    _handlers[eventType] = list;
                }
                list.Add(handler);
                _logger.LogDebug("Subscribed handler for event type {EventType}", eventType.Name);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventType, out var list))
                {
                    list.Remove(handler);
                    _logger.LogDebug("Unsubscribed handler for event type {EventType}", eventType.Name);
                }
            }
        }

        public void Publish<TEvent>(TEvent eventData) where TEvent : class
        {
            var eventType = typeof(TEvent);
            List<Delegate>? handlersCopy = null;

            lock (_lock)
            {
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlersCopy = new List<Delegate>(handlers);
                }
            }

            if (handlersCopy != null && handlersCopy.Count > 0)
            {
                _logger.LogDebug("Publishing event {EventType} to {HandlerCount} handlers", 
                    eventType.Name, handlersCopy.Count);

                foreach (Action<TEvent> handler in handlersCopy)
                {
                    try
                    {
                        handler(eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Event handler failed for {EventType}", eventType.Name);
                    }
                }
            }
        }
    }
}
