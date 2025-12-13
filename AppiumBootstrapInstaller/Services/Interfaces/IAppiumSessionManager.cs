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
    /// Interface for managing Appium server sessions
    /// </summary>
    public interface IAppiumSessionManager
    {
        /// <summary>
        /// Starts an Appium session for a device
        /// </summary>
        Task<AppiumSession?> StartSessionAsync(Device device);

        /// <summary>
        /// Stops an Appium session for a device
        /// </summary>
        Task<bool> StopSessionAsync(Device device);

        /// <summary>
        /// Allocates consecutive ports for Appium services
        /// </summary>
        Task<int[]?> AllocateConsecutivePortsAsync(int count);

        /// <summary>
        /// Releases previously allocated ports
        /// </summary>
        Task ReleasePortsAsync(int[] ports);

        /// <summary>
        /// Gets all currently allocated ports
        /// </summary>
        IReadOnlyList<int> GetAllocatedPorts();

        /// <summary>
        /// Checks if a specific port is in use
        /// </summary>
        bool IsPortInUse(int port);
    }
}
