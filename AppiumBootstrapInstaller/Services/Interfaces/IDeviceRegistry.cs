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

using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Services.Interfaces
{
    /// <summary>
    /// Interface for managing device registry and persistence
    /// </summary>
    public interface IDeviceRegistry
    {
        /// <summary>
        /// Gets all devices (connected and disconnected)
        /// </summary>
        IReadOnlyCollection<Device> GetAllDevices();

        /// <summary>
        /// Gets a specific device by ID
        /// </summary>
        Device? GetDevice(string deviceId);

        /// <summary>
        /// Gets only connected devices
        /// </summary>
        IReadOnlyCollection<Device> GetConnectedDevices();

        /// <summary>
        /// Adds or updates a device in the registry
        /// </summary>
        void AddOrUpdateDevice(Device device);

        /// <summary>
        /// Removes a device from the registry
        /// </summary>
        void RemoveDevice(string deviceId);

        /// <summary>
        /// Persists the registry to disk
        /// </summary>
        void SaveToDisk();
    }
}
