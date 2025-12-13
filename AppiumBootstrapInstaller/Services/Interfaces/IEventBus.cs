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

namespace AppiumBootstrapInstaller.Services.Interfaces
{
    /// <summary>
    /// Event bus for pub/sub messaging between services and plugins
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribe to an event type
        /// </summary>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

        /// <summary>
        /// Unsubscribe from an event type
        /// </summary>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        void Publish<TEvent>(TEvent eventData) where TEvent : class;
    }
}
