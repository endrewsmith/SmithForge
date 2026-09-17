using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts
{
    /// <summary>
    /// Клиент для запросов к API DonationAlerts.
    /// Используется для получения socket_connection_token и subscription_token.
    /// </summary>
    public class DonationAlertsApiClient : IDisposable
    {
        private const string ApiBaseUrl = "https://www.donationalerts.com/api/v1";

        private readonly HttpClient _httpClient;
        private readonly string _accessToken;

        public DonationAlertsApiClient(string accessToken)
        {
            _accessToken = accessToken;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SmithForge");
        }

        /// <summary>
        /// Получить socket_connection_token для подключения к Centrifugo
        /// </summary>
        /// <param name="socketConnectionToken">Токен, полученный от /user/oauth</param>
        public async Task<string?> GetSocketConnectionTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Согласно документации DA, /user/oauth возвращает данные, включая socket_connection_token
                var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/user/oauth");
                var response = await _httpClient.SendAsync(request, cancellationToken);

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[DA API] user/oauth response (длина {json.Length}): {SafePreview(json, 40)}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[DA API] Ошибка: {response.StatusCode}");
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Формат: { "data": { "socket_connection_token": "..." } }
                if (root.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("socket_connection_token", out var tokenProp))
                {
                    string? token = tokenProp.GetString();
                    Debug.WriteLine($"[DA API] ✅ Получен socket_connection_token (длина {token?.Length ?? 0})");
                    return token;
                }

                Debug.WriteLine("[DA API] socket_connection_token не найден в ответе");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA API] Ошибка: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получить subscription_token для подписки на канал
        /// </summary>
        /// <param name="channel">Канал, например $alerts:donation_123</param>
        /// <summary>
        /// Получить subscription_token для подписки на канал
        /// </summary>
        /// <param name="channel">Канал, например $alerts:donation_123</param>
        /// <param name="clientId">Centrifugo client ID, полученный после WebSocket-авторизации</param>
        public async Task<string?> GetSubscriptionTokenAsync(
            string channel,
            string clientId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ✅ ОБЯЗАТЕЛЬНО передаём client
                var payload = new
                {
                    client = clientId,
                    channels = new[] { channel }
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{ApiBaseUrl}/centrifuge/subscribe", content, cancellationToken);
                string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

                Debug.WriteLine($"[DA API] centrifuge/subscribe response (длина {responseJson.Length}): {SafePreview(responseJson, 40)}");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[DA API] Ошибка: {response.StatusCode}");
                    return null;
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                // Формат: { "channels": [ { "channel": "...", "token": "..." } ] }
                if (root.TryGetProperty("channels", out var channelsProp) &&
                    channelsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in channelsProp.EnumerateArray())
                    {
                        if (ch.TryGetProperty("token", out var tokenProp))
                        {
                            string? token = tokenProp.GetString();
                            Debug.WriteLine($"[DA API] ✅ Получен subscription_token (длина {token?.Length ?? 0})");
                            return token;
                        }
                    }
                }

                Debug.WriteLine("[DA API] subscription_token не найден");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA API] Ошибка: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Безопасный предпросмотр строки для логов
        /// </summary>
        private static string SafePreview(string? value, int previewLength = 5)
        {
            if (string.IsNullOrEmpty(value))
                return "(пусто)";

            if (value.Length <= previewLength)
                return $"[{value.Length}] {value.Substring(0, Math.Min(2, value.Length))}...";

            return $"[длина {value.Length}] {value.Substring(0, previewLength)}...";
        }
        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}