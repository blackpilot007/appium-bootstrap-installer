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

using System.Net.Http.Json;
using System.Text.Json;
using AppiumBootstrapInstaller.Models;
using Microsoft.Extensions.Logging;

namespace AppiumBootstrapInstaller.Services
{
    /// <summary>
    /// Sends webhook notifications for device events
    /// </summary>
    public class WebhookNotifier
    {
        private readonly ILogger<WebhookNotifier> _logger;
        private readonly WebhookConfig _config;
        private readonly HttpClient _httpClient;

        public WebhookNotifier(ILogger<WebhookNotifier> logger, WebhookConfig config)
        {
            _logger = logger;
            _config = config;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            // Add custom headers
            foreach (var header in config.Headers)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        public async Task NotifyDeviceConnectedAsync(Device device)
        {
            if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.OnConnectUrl))
                return;

            var evt = new DeviceEvent
            {
                EventType = DeviceEventType.Connected,
                Device = device,
                Timestamp = DateTime.UtcNow
            };

            await SendWebhookAsync(_config.OnConnectUrl, evt, "device-connected");
        }

        public async Task NotifyDeviceDisconnectedAsync(Device device)
        {
            if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.OnDisconnectUrl))
                return;

            var evt = new DeviceEvent
            {
                EventType = DeviceEventType.Disconnected,
                Device = device,
                Timestamp = DateTime.UtcNow
            };

            await SendWebhookAsync(_config.OnDisconnectUrl, evt, "device-disconnected");
        }

        public async Task NotifySessionStartedAsync(Device device)
        {
            if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.OnSessionStartUrl))
                return;

            var evt = new DeviceEvent
            {
                EventType = DeviceEventType.SessionStarted,
                Device = device,
                Timestamp = DateTime.UtcNow
            };

            await SendWebhookAsync(_config.OnSessionStartUrl, evt, "session-started");
        }

        public async Task NotifySessionEndedAsync(Device device)
        {
            if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.OnSessionEndUrl))
                return;

            var evt = new DeviceEvent
            {
                EventType = DeviceEventType.SessionEnded,
                Device = device,
                Timestamp = DateTime.UtcNow
            };

            await SendWebhookAsync(_config.OnSessionEndUrl, evt, "session-ended");
        }

        private async Task SendWebhookAsync(string url, DeviceEvent evt, string eventName)
        {
            try
            {
                _logger.LogDebug("Sending webhook: {EventName} to {Url}", eventName, url);

                var response = await _httpClient.PostAsJsonAsync(url, evt, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Webhook {EventName} sent successfully to {Url}", eventName, url);
                }
                else
                {
                    _logger.LogWarning(
                        "Webhook {EventName} failed with status {StatusCode}: {Url}",
                        eventName, response.StatusCode, url
                    );
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Webhook {EventName} timed out: {Url}", eventName, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook {EventName} to {Url}", eventName, url);
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
