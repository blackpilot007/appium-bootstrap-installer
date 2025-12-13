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
    /// Manages port allocation and availability for Appium sessions and plugins.
    /// </summary>
    public interface IPortManager
    {
        /// <summary>
        /// Allocates consecutive available ports.
        /// </summary>
        /// <param name="count">Number of consecutive ports needed.</param>
        /// <returns>Array of allocated ports, or null if not enough consecutive ports available.</returns>
        Task<int[]?> AllocateConsecutivePortsAsync(int count);

        /// <summary>
        /// Releases previously allocated ports back to the pool.
        /// </summary>
        /// <param name="ports">Array of ports to release.</param>
        Task ReleasePortsAsync(int[] ports);

        /// <summary>
        /// Gets a read-only list of all currently allocated ports.
        /// </summary>
        /// <returns>Read-only list of allocated ports.</returns>
        IReadOnlyList<int> GetAllocatedPorts();

        /// <summary>
        /// Checks if a specific port is currently in use (either allocated or bound by another process).
        /// </summary>
        /// <param name="port">Port number to check.</param>
        /// <returns>True if port is in use, false otherwise.</returns>
        bool IsPortInUse(int port);
    }
}
