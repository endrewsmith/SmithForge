using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.AlertsEngine.Providers.DonationAlerts.Models;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts
{
    /// <summary>
    /// OAuth-авторизация для DonationAlerts.
    /// Открывает браузер, ловит code на локальном порту 17845, обменивает на токены.
    /// </summary>
    public class DonationAlertsOAuth
    {
        // ============================================================
        // Константы OAuth
        // ============================================================
        private const string AuthUrl = "https://www.donationalerts.com/oauth/authorize";
        private const string TokenUrl = "https://www.donationalerts.com/oauth/token";

        // ✅ Должно СОВПАДАТЬ с тем, что указано в настройках приложения на DA
        private const string RedirectUri = "http://localhost:17845/";
        private const int RedirectPort = 17845;

        // Scopes (какие права нужны)
        private const string Scope = "oauth-user-show oauth-donation-subscribe";

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly HttpClient _httpClient = new();

        public DonationAlertsOAuth(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("Client ID не указан", nameof(clientId));
            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new ArgumentException("Client Secret не указан", nameof(clientSecret));

            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        /// <summary>
        /// Запускает OAuth-флоу и возвращает токены.
        /// null, если пользователь отменил или произошла ошибка.
        /// </summary>
        public async Task<DonationAlertsTokens?> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            // 1. Создаём локальный HTTP-сервер для перехвата редиректа
            var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);

            try
            {
                listener.Start();
                Debug.WriteLine($"[DA OAuth] Локальный сервер запущен: {RedirectUri}");

                // 2. Формируем URL авторизации
                string state = Guid.NewGuid().ToString("N");
                string authUri = $"{AuthUrl}" +
                                 $"?client_id={Uri.EscapeDataString(_clientId)}" +
                                 $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                                 $"&response_type=code" +
                                 $"&scope={Uri.EscapeDataString(Scope)}" +
                                 $"&state={state}";

                // 3. Открываем браузер
                Process.Start(new ProcessStartInfo(authUri) { UseShellExecute = true });
                Debug.WriteLine($"[DA OAuth] Открыт браузер: {authUri}");

                // 4. Ждём входящий запрос на редирект
                var contextTask = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(
                    contextTask,
                    Task.Delay(TimeSpan.FromMinutes(5), cancellationToken));

                if (completedTask != contextTask)
                {
                    Debug.WriteLine("[DA OAuth] Таймаут ожидания авторизации (5 мин)");
                    return null;
                }

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                string? code = request.QueryString["code"];
                string? error = request.QueryString["error"];
                string? returnedState = request.QueryString["state"];

                // 5. Отправляем ответ в браузер (красивую страничку)
                string htmlResponse = BuildResponseHtml(error, code);
                var htmlBytes = Encoding.UTF8.GetBytes(htmlResponse);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = htmlBytes.Length;
                await response.OutputStream.WriteAsync(htmlBytes);
                response.Close();

                // 6. Проверяем ответ
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.WriteLine($"[DA OAuth] Сервер вернул ошибку: {error}");
                    return null;
                }

                if (string.IsNullOrEmpty(code))
                {
                    Debug.WriteLine("[DA OAuth] Код авторизации не получен");
                    return null;
                }

                if (returnedState != state)
                {
                    Debug.WriteLine($"[DA OAuth] ⚠️ State не совпадает! Ожидался {state}, получен {returnedState}");
                    return null;
                }

                Debug.WriteLine($"[DA OAuth] Получен code (длина {code.Length}): {SafePreview(code, 5)}");

                // 7. Обмениваем code на токены
                var tokens = await ExchangeCodeForTokensAsync(code, cancellationToken);

                return tokens;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA OAuth] Ошибка: {ex.Message}");
                throw;
            }
            finally
            {
                try { listener.Stop(); } catch { }
                listener.Close();
            }
        }

        /// <summary>
        /// Обмен кода авторизации на access_token + refresh_token
        /// </summary>
        private async Task<DonationAlertsTokens?> ExchangeCodeForTokensAsync(
            string code,
            CancellationToken cancellationToken)
        {
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["redirect_uri"] = RedirectUri,
                ["code"] = code
            };

            var content = new FormUrlEncodedContent(payload);

            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            Debug.WriteLine($"[DA OAuth] Ответ токена: {SafePreview(json, 40)}");

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[DA OAuth] Ошибка получения токена: {response.StatusCode} - {json}");
                return null;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponseDto>(json);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                Debug.WriteLine("[DA OAuth] Не удалось распарсить токен");
                return null;
            }

            var tokens = new DonationAlertsTokens
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                UserId = 0 // заполним через API
            };

            // 8. Получаем user_id через API
            try
            {
                long userId = await FetchUserIdAsync(tokens.AccessToken, cancellationToken);
                tokens.UserId = userId;
                Debug.WriteLine($"[DA OAuth] ✅ User ID: {userId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA OAuth] ⚠️ Не удалось получить User ID: {ex.Message}");
            }

            return tokens;
        }

        /// <summary>
        /// Безопасный предпросмотр строки для логов (не раскрывает секреты)
        /// </summary>
        private static string SafePreview(string? value, int previewLength = 5)
        {
            if (string.IsNullOrEmpty(value))
                return "(пусто)";

            if (value.Length <= previewLength)
                return $"[{value.Length}] {value.Substring(0, Math.Min(2, value.Length))}...";

            return $"[длина {value.Length}] {value.Substring(0, previewLength)}...";
        }
        /// <summary>
        /// Получить ID пользователя через API
        /// </summary>
        private async Task<long> FetchUserIdAsync(string accessToken, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.donationalerts.com/api/v1/user/oauth");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            string json = await response.Content.ReadAsStringAsync(cancellationToken);

            Debug.WriteLine($"[DA OAuth] User info: {json}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Не удалось получить user info: {response.StatusCode}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Формат ответа: { "data": { "id": 123, ... } }
            if (root.TryGetProperty("data", out var dataProp) &&
                dataProp.TryGetProperty("id", out var idProp))
            {
                return idProp.GetInt64();
            }

            throw new Exception("В ответе API нет поля data.id");
        }

        /// <summary>
        /// HTML-страница, которая показывается пользователю после авторизации
        /// </summary>
        private static string BuildResponseHtml(string? error, string? code)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Ошибка авторизации</title></head>
<body style='font-family: Segoe UI, sans-serif; text-align: center; padding: 50px; background: #1E1E2E; color: #fff;'>
    <h1 style='color: #F44336;'>❌ Ошибка авторизации</h1>
    <p>Попробуйте снова в SmithForge.</p>
    <p style='color: #888; font-size: 12px;'>Можете закрыть эту вкладку.</p>
</body>
</html>";
            }

            return @"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Авторизация успешна</title></head>
<body style='font-family: Segoe UI, sans-serif; text-align: center; padding: 50px; background: #1E1E2E; color: #fff;'>
    <h1 style='color: #4CAF50;'>✅ Авторизация успешна!</h1>
    <p>Теперь можете вернуться в SmithForge.</p>
    <p style='color: #888; font-size: 12px;'>Эта вкладка закроется автоматически через 3 секунды.</p>
    <script>setTimeout(() => window.close(), 3000);</script>
</body>
</html>";
        }

        /// <summary>
        /// DTO для ответа с токенами от DonationAlerts
        /// </summary>
        private class TokenResponseDto
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }
        }
    }
}