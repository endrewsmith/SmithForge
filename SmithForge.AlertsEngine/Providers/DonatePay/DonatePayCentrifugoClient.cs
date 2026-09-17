using SmithForge.AlertsEngine.Providers.DonatePay.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.AlertsEngine.Providers.DonatePay
{
    /// <summary>
    /// Клиент Centrifugo для DonatePay.
    /// Поддерживает два режима:
    /// 1. Через виджет (widget.donatepay.ru/socket/token) — если задан _widgetHash
    /// 2. Через API (donatepay.ru/api/v2/socket/token) — старый путь
    /// </summary>
    public class DonatePayCentrifugoClient : IDisposable
    {
        // ============================================================
        // === КОНСТАНТЫ ===
        // ============================================================
        private const string WsUrl = "wss://centrifugo.donatepay.ru:443/connection/websocket";
        private const string SocketTokenUrl = "https://donatepay.ru/api/v2/socket/token";
        private const string WidgetSocketTokenUrl = "https://widget.donatepay.ru/socket/token";

        // ============================================================
        // === ПОЛЯ ===
        // ============================================================
        private readonly string _apiKey;
        private readonly string _widgetHash;

        private long _userId;
        private string _socketToken = string.Empty;
        private string _centrifugeClientId = string.Empty;
        private string? _widgetChannel;

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _sendSemaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;
        private int _requestId = 1;

        // ============================================================
        // === СОБЫТИЯ ===
        // ============================================================
        public event EventHandler<DonatePayMessage>? OnDonationReceived;
        public event EventHandler? OnConnected;
        public event EventHandler<string>? OnDisconnected;
        public event EventHandler<Exception>? OnError;
        public event EventHandler<string>? OnLog;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;
        public long UserId => _userId;

        // ============================================================
        // === КОНСТРУКТОР ===
        // ============================================================
        public DonatePayCentrifugoClient(string apiKey, string widgetHash = "")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API-ключ не может быть пустым", nameof(apiKey));

            _apiKey = apiKey.Trim();

            // ✅ ХАРДКОД ХЭША ВИДЖЕТА (временно, для отладки)
            _widgetHash = !string.IsNullOrEmpty(widgetHash)
                ? widgetHash.Trim()
                : "0dfbc1e84b4908e6611e0eb6b7039eeb380d5b4c7c477f485e744d23259495cf";
        }

        // ============================================================
        // === ПОДКЛЮЧЕНИЕ ===
        // ============================================================
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_webSocket != null)
            {
                Log("⚠️ Уже подключён");
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                string? token = null;
                long userId = 0;

                // ✅ ВЫБОР РЕЖИМА: через виджет или через API
                if (!string.IsNullOrEmpty(_widgetHash))
                {
                    Log($"🔍 Используем виджет: {_widgetHash}");
                    var (widgetChannel, widgetToken) = await FetchWidgetSocketConfigAsync(_widgetHash, _cts.Token);

                    if (string.IsNullOrEmpty(widgetToken))
                        throw new Exception("Не удалось получить токен через виджет");

                    token = widgetToken;
                    _widgetChannel = widgetChannel;
                    Log($"✅ Токен виджета получен (длина {widgetToken.Length})");
                    Log($"✅ Канал виджета: {_widgetChannel ?? "(пусто)"}");

                    // User ID получаем отдельно (нужен для формирования каналов)
                    userId = await FetchUserIdOnlyAsync(_cts.Token);
                }
                else
                {
                    // Старый путь: /api/v2/socket/token
                    Log("🔍 Получаем socket-токен и User ID через API...");
                    var (apiToken, apiUserId) = await FetchSocketDataAsync(_cts.Token);
                    token = apiToken;
                    userId = apiUserId;
                }

                if (string.IsNullOrEmpty(token))
                    throw new Exception("Не удалось получить socket-токен");

                _socketToken = token;

                if (userId > 0)
                {
                    _userId = userId;
                    Log($"✅ User ID: {_userId}");
                }

                // 2. Подключаемся к Centrifugo
                Log($"🔄 Подключение к {WsUrl}...");
                _webSocket = new ClientWebSocket();
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await _webSocket.ConnectAsync(new Uri(WsUrl), _cts.Token);
                Log("✅ WebSocket подключён");

                // 3. Отправляем socket-токен для авторизации
                Log("📤 Отправка socket-токена...");
                var authPayload = new
                {
                    @params = new { token = _socketToken },
                    id = _requestId++
                };
                await SendJsonAsync(authPayload);

                // 4. Запускаем цикл прослушивания
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подключения: {ex.Message}");
                OnError?.Invoke(this, ex);
                throw;
            }
        }

        // ============================================================
        // === ПОЛУЧЕНИЕ USER ID (только для логов) ===
        // ============================================================
        private async Task<long> FetchUserIdOnlyAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "SmithForge");

                string userUrl = $"https://donatepay.ru/api/v1/user?access_token={Uri.EscapeDataString(_apiKey)}";
                var userResponse = await http.GetAsync(userUrl, cancellationToken);
                string userJson = await userResponse.Content.ReadAsStringAsync(cancellationToken);

                if (userResponse.IsSuccessStatusCode)
                {
                    try
                    {
                        using var userDoc = JsonDocument.Parse(userJson);
                        var userRoot = userDoc.RootElement;

                        if (userRoot.TryGetProperty("data", out var dataProp))
                        {
                            if (dataProp.TryGetProperty("id", out var idProp))
                                return idProp.GetInt64();
                            if (dataProp.TryGetProperty("user_id", out var uidProp))
                                return uidProp.GetInt64();
                        }
                        else if (userRoot.TryGetProperty("id", out var rootIdProp))
                        {
                            return rootIdProp.GetInt64();
                        }
                    }
                    catch (JsonException) { }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ Не удалось получить User ID: {ex.Message}");
            }

            return 0;
        }

        // ============================================================
        // === ПОЛУЧЕНИЕ КОНФИГА ЧЕРЕЗ ВИДЖЕТ (widget.donatepay.ru) ===
        // ============================================================
        private async Task<(string? channel, string? token)> FetchWidgetSocketConfigAsync(
            string widgetHash, CancellationToken ct)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    UseCookies = true,
                    CookieContainer = new System.Net.CookieContainer(),
                    AllowAutoRedirect = true
                };

                using var http = new HttpClient(handler);

                // ============================================================
                // ШАГ 1: GET страницы виджета — извлекаем _token из HTML
                // ============================================================
                string widgetPageUrl = $"https://widget.donatepay.ru/alert-box/widget/{widgetHash}";
                Log($"🔍 Загружаем страницу виджета: {widgetPageUrl}");

                var pageRequest = new HttpRequestMessage(HttpMethod.Get, widgetPageUrl);
                pageRequest.Headers.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36");
                pageRequest.Headers.Add("Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                var pageResponse = await http.SendAsync(pageRequest, ct);
                string pageHtml = await pageResponse.Content.ReadAsStringAsync(ct);

                Log($"📥 Страница виджета загружена: {pageResponse.StatusCode}, длина HTML {pageHtml.Length}");

                // ✅ ИЗВЛЕКАЕМ _token ИЗ HTML
                string? laravelToken = null;
                var tokenMatch = System.Text.RegularExpressions.Regex.Match(
                    pageHtml,
                    @"<input[^>]*name=[""']_token[""'][^>]*value=[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (tokenMatch.Success)
                {
                    laravelToken = tokenMatch.Groups[1].Value;
                    Log($"✅ Найден _token (input, длина {laravelToken.Length})");
                }
                else
                {
                    tokenMatch = System.Text.RegularExpressions.Regex.Match(
                        pageHtml,
                        @"<input[^>]*value=[""']([^""']+)[""'][^>]*name=[""']_token[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (tokenMatch.Success)
                    {
                        laravelToken = tokenMatch.Groups[1].Value;
                        Log($"✅ Найден _token (input alt, длина {laravelToken.Length})");
                    }
                }

                if (string.IsNullOrEmpty(laravelToken))
                {
                    var metaMatch = System.Text.RegularExpressions.Regex.Match(
                        pageHtml,
                        @"<meta[^>]*name=[""']csrf-token[""'][^>]*content=[""']([^""']+)[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (metaMatch.Success)
                    {
                        laravelToken = metaMatch.Groups[1].Value;
                        Log($"✅ Найден _token (meta, длина {laravelToken.Length})");
                    }
                }

                if (string.IsNullOrEmpty(laravelToken))
                {
                    Log("⚠️ _token не найден в HTML, пробуем без него");
                }

                // ============================================================
                // ШАГ 2: POST /socket/token с правильным телом
                // ============================================================
                var requestBody = new
                {
                    _token = laravelToken ?? "",
                    token = widgetHash
                };

                string jsonPayload = JsonSerializer.Serialize(requestBody);
                Log($"📤 POST body: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, WidgetSocketTokenUrl);
                request.Content = content;
                request.Headers.Add("Accept", "application/json, text/plain, */*");
                request.Headers.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/153.0.0.0 Safari/537.36");
                request.Headers.Referrer = new Uri(widgetPageUrl);
                request.Headers.Add("Origin", "https://widget.donatepay.ru");

                // X-XSRF-TOKEN из cookie
                var cookies = handler.CookieContainer.GetCookies(new Uri("https://widget.donatepay.ru"));
                foreach (System.Net.Cookie c in cookies)
                {
                    if (c.Name == "XSRF-TOKEN")
                    {
                        request.Headers.Add("X-XSRF-TOKEN", Uri.UnescapeDataString(c.Value));
                        break;
                    }
                }

                var response = await http.SendAsync(request, ct);
                string responseJson = await response.Content.ReadAsStringAsync(ct);

                Log($"📥 /socket/token (widget) ответ: {responseJson}");

                if (!response.IsSuccessStatusCode)
                {
                    Log($"❌ widget socket/token вернул {response.StatusCode}");
                    return (null, null);
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                string? channel = null;
                string? token = null;

                if (root.TryGetProperty("channel", out var chProp))
                    channel = chProp.GetString();
                if (root.TryGetProperty("token", out var tProp))
                    token = tProp.GetString();

                Log($"✅ Widget channel = {channel ?? "(пусто)"}, token длина {token?.Length ?? 0}");
                return (channel, token);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка FetchWidgetSocketConfigAsync: {ex.Message}");
                return (null, null);
            }
        }

        // ============================================================
        // === СТАРЫЙ ПУТЬ: ПОЛУЧЕНИЕ ТОКЕНА И USER ID ЧЕРЕЗ API ===
        // ============================================================
        private async Task<(string token, long userId)> FetchSocketDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "SmithForge");

                // ШАГ 1: User ID
                Log("🔍 Получаем User ID через GET /user...");
                string userUrl = $"https://donatepay.ru/api/v1/user?access_token={Uri.EscapeDataString(_apiKey)}";
                var userResponse = await http.GetAsync(userUrl, cancellationToken);
                string userJson = await userResponse.Content.ReadAsStringAsync(cancellationToken);

                Log($"📥 GET /user response (длина {userJson.Length}): {SafePreview(userJson, 150)}");

                long userId = 0;
                if (userResponse.IsSuccessStatusCode)
                {
                    try
                    {
                        using var userDoc = JsonDocument.Parse(userJson);
                        var userRoot = userDoc.RootElement;

                        if (userRoot.TryGetProperty("data", out var dataProp))
                        {
                            if (dataProp.TryGetProperty("id", out var idProp))
                                userId = idProp.GetInt64();
                            else if (dataProp.TryGetProperty("user_id", out var uidProp))
                                userId = uidProp.GetInt64();
                        }
                        else if (userRoot.TryGetProperty("id", out var rootIdProp))
                        {
                            userId = rootIdProp.GetInt64();
                        }
                    }
                    catch (JsonException)
                    {
                        Log("⚠️ Ответ /user не является JSON");
                    }
                }

                if (userId <= 0)
                {
                    Log("❌ Не удалось получить User ID из API");
                    return (string.Empty, 0);
                }

                Log($"✅ User ID: {userId}");

                // ШАГ 2: Connection token
                Log("🔍 Получаем socket-токен...");
                var tokenContent = new StringContent(
                    JsonSerializer.Serialize(new { access_token = _apiKey }),
                    Encoding.UTF8,
                    "application/json");

                var tokenResponse = await http.PostAsync(SocketTokenUrl, tokenContent, cancellationToken);
                string tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

                Log($"📥 /socket/token ПОЛНЫЙ ответ: {tokenJson}");

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    Log($"❌ /socket/token вернул {tokenResponse.StatusCode}");
                    return (string.Empty, userId);
                }

                using var tokenDoc = JsonDocument.Parse(tokenJson);
                var tokenRoot = tokenDoc.RootElement;

                string token = string.Empty;
                if (tokenRoot.TryGetProperty("token", out var tokenProp))
                    token = tokenProp.GetString() ?? string.Empty;
                else if (tokenRoot.TryGetProperty("data", out var tokenDataProp) &&
                         tokenDataProp.TryGetProperty("token", out var dataTokenProp))
                    token = dataTokenProp.GetString() ?? string.Empty;

                Log($"✅ Connection token длина {token.Length}, UserId: {userId}");
                return (token, userId);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка получения socket-данных: {ex.Message}");
                return (string.Empty, 0);
            }
        }

        // ============================================================
        // === ПРИЁМ СООБЩЕНИЙ ===
        // ============================================================
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            Log("🔁 ReceiveLoop запущен");

            var buffer = new byte[1024 * 8];

            try
            {
                while (_webSocket != null &&
                       _webSocket.State == WebSocketState.Open &&
                       !cancellationToken.IsCancellationRequested)
                {
                    using var ms = new MemoryStream();

                    var result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), cancellationToken);

                    do
                    {
                        ms.Write(buffer, 0, result.Count);

                        if (result.EndOfMessage) break;

                        result = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), cancellationToken);
                    }
                    while (true);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("⚠️ Сервер закрыл соединение");
                        OnDisconnected?.Invoke(this, "Connection closed");
                        break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    string jsonString = await reader.ReadToEndAsync();

                    ProcessMessage(jsonString, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Log("⏹ ReceiveLoop отменён");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка в ReceiveLoop: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }

        private void ProcessMessage(string jsonString, CancellationToken cancellationToken)
        {
            // 🟢 ПЕРЕХВАТ PING ДО ПАРСИНГА
            string trimmed = jsonString.Trim();
            if (trimmed == "{}")
            {
                Log("🏓 Ping от сервера → отправляем pong");
                _ = Task.Run(async () =>
                {
                    try { await SendRawJsonAsync("{}", cancellationToken); }
                    catch (Exception ex) { Log($"⚠️ Ошибка pong: {ex.Message}"); }
                });
                return;
            }

            // Логируем всё, что не пустой ping
            if (jsonString.Length > 3)
                Log($"📥 ВХОДЯЩЕЕ ({jsonString.Length}): {SafePreview(jsonString, 120)}");

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                // === 1. Ответ на авторизацию (id == 1) ===
                if (root.TryGetProperty("id", out var idProp) &&
                    idProp.ValueKind == JsonValueKind.Number)
                {
                    int responseId = idProp.GetInt32();

                    if (responseId == 1)
                    {
                        if (root.TryGetProperty("error", out var errorProp))
                        {
                            string errMsg = errorProp.ToString();
                            Log($"❌ Ошибка авторизации: {errMsg}");
                            OnError?.Invoke(this, new Exception($"Ошибка авторизации Centrifugo: {errMsg}"));
                            return;
                        }

                        if (root.TryGetProperty("result", out var resultProp) &&
                            resultProp.TryGetProperty("client", out var clientProp))
                        {
                            _centrifugeClientId = clientProp.GetString() ?? string.Empty;
                            Log($"✅ Авторизован! Centrifugo client = {_centrifugeClientId}");

                            OnConnected?.Invoke(this, EventArgs.Empty);

                            // Подписка на правильные каналы
                            _ = SubscribeAsync(cancellationToken);
                            return;
                        }
                    }
                }

                // === 2. Unsubscribe (type == 3) ===
                if (root.TryGetProperty("result", out var resProp) &&
                    resProp.TryGetProperty("type", out var typeProp) &&
                    typeProp.ValueKind == JsonValueKind.Number &&
                    typeProp.GetInt32() == 3)
                {
                    Log("⚠️ Подписка истекла → переподписка через 2 сек");
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        await SubscribeAsync(cancellationToken);
                    });
                    return;
                }

                // === 3. Notification (донат) ===
                var notification = FindPropertyRecursive(root, "notification");
                if (notification != null)
                {
                    Log("💰 Найден notification!");

                    var donation = JsonSerializer.Deserialize<DonatePayMessage>(notification.Value.GetRawText());
                    if (donation != null)
                        OnDonationReceived?.Invoke(this, donation);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка парсинга: {ex.Message}");
            }
        }

        private static JsonElement? FindPropertyRecursive(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(propertyName, out var found))
                    return found;

                foreach (var prop in element.EnumerateObject())
                {
                    var result = FindPropertyRecursive(prop.Value, propertyName);
                    if (result != null) return result;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var result = FindPropertyRecursive(item, propertyName);
                    if (result != null) return result;
                }
            }

            return null;
        }

        // ============================================================
        // === ПОДПИСКА НА КАНАЛЫ DONATEPAY ===
        // ============================================================
        private async Task SubscribeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string[] channels = new[]
                {
            $"widgets:LastEvents#{_userId}",
            $"widgets:AlertBox#{_userId}",
            $"events:events#{_userId}"
        };

                foreach (var channel in channels)
                {
                    Log($"📤 Подписка на канал {channel} БЕЗ токена (connection token уже даёт доступ)...");
                    await SendSubscribeAsync(channel, null, cancellationToken);
                    Log($"✅ Подписка отправлена: {channel}");
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подписки: {ex.Message}");
            }
        }

        // ============================================================
        // === ПОЛУЧЕНИЕ SUBSCRIPTION TOKENS ДЛЯ МАССИВА КАНАЛОВ ===
        // ============================================================
        private async Task<Dictionary<string, string>> FetchSubscriptionTokensAsync(
            string[] channels, CancellationToken ct)
        {
            var result = new Dictionary<string, string>();

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "SmithForge");

                var payload = new
                {
                    access_token = _apiKey,
                    client = _centrifugeClientId,
                    channels = channels
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await http.PostAsync(SocketTokenUrl, content, ct);
                string responseJson = await response.Content.ReadAsStringAsync(ct);

                Log($"📥 /socket/token (subscription) ответ: {responseJson}");

                if (!response.IsSuccessStatusCode)
                {
                    Log($"❌ /socket/token вернул {response.StatusCode}");
                    return result;
                }

                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("channels", out var chArr) &&
                    chArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in chArr.EnumerateArray())
                    {
                        string? name = ch.TryGetProperty("channel", out var n) ? n.GetString() : null;
                        string? token = ch.TryGetProperty("token", out var t) ? t.GetString() : null;

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(token))
                        {
                            result[name] = token;
                            Log($"✅ Токен для {name} (длина {token.Length})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка FetchSubscriptionTokensAsync: {ex.Message}");
            }

            return result;
        }

        // ============================================================
        // === ОТПРАВКА SUBSCRIBE ===
        // ============================================================
        private async Task SendSubscribeAsync(string channel, string? channelToken, CancellationToken cancellationToken)
        {
            var subscribePayload = new
            {
                method = 1,
                @params = new
                {
                    channel = channel,
                    token = channelToken
                },
                id = _requestId++
            };

            await SendJsonAsync(subscribePayload);
        }

        // ============================================================
        // === ОТПРАВКА (потокобезопасная) ===
        // ============================================================
        private async Task SendJsonAsync(object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            await SendRawJsonAsync(json, _cts?.Token ?? CancellationToken.None);
        }

        private async Task SendRawJsonAsync(string json, CancellationToken cancellationToken)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await _sendSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        cancellationToken);
                }
            }
            finally
            {
                _sendSemaphore.Release();
            }
        }

        // ============================================================
        // === ОТКЛЮЧЕНИЕ ===
        // ============================================================
        public async Task DisconnectAsync()
        {
            if (_webSocket == null) return;

            try
            {
                Log("⏹ Отключение...");

                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnect",
                        CancellationToken.None);
                }

                _webSocket.Dispose();
                _webSocket = null;

                Log("✅ Отключено");
            }
            catch (Exception ex)
            {
                Log($"⚠️ Ошибка при отключении: {ex.Message}");
            }
        }

        // ============================================================
        // === ВСПОМОГАТЕЛЬНЫЕ ===
        // ============================================================
        private void Log(string message)
        {
            Debug.WriteLine($"[DP Centrifugo] {message}");
            OnLog?.Invoke(this, message);
        }

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
            if (_disposed) return;
            _disposed = true;

            try
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _webSocket?.Dispose();
                _webSocket = null;
                _sendSemaphore.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DP Centrifugo] Ошибка Dispose: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}