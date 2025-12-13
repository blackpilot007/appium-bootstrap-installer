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

using System.Net.Sockets;
using AppiumBootstrapInstaller.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Manages port allocation and availability for Appium sessions and plugins.
    /// Provides thread-safe port allocation with configurable range.
    /// </summary>
    public class PortManager : IPortManager
    {
        private readonly HashSet<int> _usedPorts = new();
        private readonly SemaphoreSlim _portLock = new(1, 1);
        private readonly int _minPort;
        private readonly int _maxPort;
        private readonly ILogger<PortManager> _logger;

        /// <summary>
        /// Initializes a new instance of the PortManager class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic messages.</param>
        /// <param name="minPort">Minimum port number in the allocation range (default: 4723).</param>
        /// <param name="maxPort">Maximum port number in the allocation range (default: 5000).</param>
        public PortManager(ILogger<PortManager> logger, int minPort = 4723, int maxPort = 5000)
        {
            _logger = logger;
            _minPort = minPort;
            _maxPort = maxPort;
            
            _logger.LogInformation("PortManager initialized with range {MinPort}-{MaxPort}", minPort, maxPort);
        }

        /// <summary>
        /// Allocates consecutive available ports from the configured range.
        /// </summary>
        /// <param name="count">Number of consecutive ports needed.</param>
        /// <returns>Array of allocated ports, or null if not enough consecutive ports available.</returns>
        public async Task<int[]?> AllocateConsecutivePortsAsync(int count)
        {
            if (count <= 0)
            {
                _logger.LogWarning("Invalid port count requested: {Count}", count);
                return null;
            }

            await _portLock.WaitAsync();
            try
            {
                // Find consecutive available ports
                for (int startPort = _minPort; startPort <= _maxPort - count; startPort++)
                {
                    if (ArePortsAvailable(startPort, count))
                    {
                        var ports = Enumerable.Range(startPort, count).ToArray();
                        
                        // Mark ports as used
                        foreach (var port in ports)
                        {
                            _usedPorts.Add(port);
                        }
                        
                        _logger.LogDebug("Allocated {Count} consecutive ports: {Ports}", 
                            count, string.Join(", ", ports));
                        
                        return ports;
                    }
                }
                
                _logger.LogWarning("Failed to allocate {Count} consecutive ports in range {MinPort}-{MaxPort}", 
                    count, _minPort, _maxPort);
                
                return null;
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// Releases previously allocated ports back to the available pool.
        /// </summary>
        /// <param name="ports">Array of ports to release.</param>
        public async Task ReleasePortsAsync(int[] ports)
        {
            if (ports == null || ports.Length == 0)
            {
                return;
            }

            await _portLock.WaitAsync();
            try
            {
                foreach (var port in ports)
                {
                    if (_usedPorts.Remove(port))
                    {
                        _logger.LogDebug("Released port {Port}", port);
                    }
                }
                
                _logger.LogDebug("Released {Count} ports: {Ports}", 
                    ports.Length, string.Join(", ", ports));
            }
            finally
            {
                _portLock.Release();
            }
        }

        /// <summary>
        /// Gets a read-only list of all currently allocated ports.
        /// </summary>
        /// <returns>Read-only list of allocated ports.</returns>
        public IReadOnlyList<int> GetAllocatedPorts()
        {
            lock (_usedPorts)
            {
                return _usedPorts.OrderBy(p => p).ToList();
            }
        }

        /// <summary>
        /// Checks if a specific port is currently in use (either allocated internally or bound by another process).
        /// </summary>
        /// <param name="port">Port number to check.</param>
        /// <returns>True if port is in use, false otherwise.</returns>
        public bool IsPortInUse(int port)
        {
            // Check if we've allocated this port
            lock (_usedPorts)
            {
                if (_usedPorts.Contains(port))
                {
                    return true;
                }
            }

            // Check if port is actually bound by trying to listen on it
            try
            {
                using var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return false; // Port is available
            }
            catch (SocketException)
            {
                return true; // Port is in use by another process
            }
        }

        /// <summary>
        /// Checks if a range of consecutive ports are available.
        /// </summary>
        /// <param name="startPort">Starting port number.</param>
        /// <param name="count">Number of consecutive ports to check.</param>
        /// <returns>True if all ports in the range are available, false otherwise.</returns>
        private bool ArePortsAvailable(int startPort, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int port = startPort + i;
                
                // Check if already allocated internally
                if (_usedPorts.Contains(port))
                {
                    return false;
                }
                
                // Check if port is actually available
                if (IsPortInUse(port))
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}
