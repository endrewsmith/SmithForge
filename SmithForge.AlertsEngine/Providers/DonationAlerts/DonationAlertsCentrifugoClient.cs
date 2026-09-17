using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.AlertsEngine.Providers.DonationAlerts.Models;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts
{
    /// <summary>
    /// Клиент Centrifugo для DonationAlerts.
    /// Работает через ClientWebSocket .NET. Использует OAuth-токены.
    /// </summary>
    public class DonationAlertsCentrifugoClient : IDisposable
    {
        private const string WsUrl = "wss://centrifugo.donationalerts.com/connection/websocket";

        private string _centrifugeClientId = string.Empty;

        private ClientWebSocket? _webSocket;
        private readonly string _accessToken;
        private readonly long _userId;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public event EventHandler<DonationAlertsMessage>? OnDonationReceived;
        public event EventHandler? OnConnected;
        public event EventHandler<string>? OnDisconnected;
        public event EventHandler<Exception>? OnError;
        public event EventHandler<string>? OnLog;

        private int _requestId = 1;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public DonationAlertsCentrifugoClient(string accessToken, long userId)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access Token не может быть пустым", nameof(accessToken));
            if (userId <= 0)
                throw new ArgumentException("User ID должен быть > 0", nameof(userId));

            _accessToken = accessToken;
            _userId = userId;
        }

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
                Log($"🔄 Подключение к Centrifugo (userId={_userId})...");

                _webSocket = new ClientWebSocket();
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

                await _webSocket.ConnectAsync(new Uri(WsUrl), _cts.Token);
                Log("✅ WebSocket подключён");

                // Получаем socket_connection_token через API
                string? socketToken = null;
                using (var api = new DonationAlertsApiClient(_accessToken))
                {
                    socketToken = await api.GetSocketConnectionTokenAsync(_cts.Token);
                }

                if (string.IsNullOrEmpty(socketToken))
                {
                    throw new Exception("Не удалось получить socket_connection_token");
                }

                Log($"📤 Отправка socket_connection_token (длина {socketToken.Length})...");

                var authPayload = new
                {
                    @params = new { token = socketToken },
                    id = _requestId++
                };

                await SendJsonAsync(authPayload);

                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подключения: {ex.Message}");
                OnError?.Invoke(this, ex);
                throw;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 8];

            try
            {
                while (_webSocket != null &&
                       _webSocket.State == WebSocketState.Open &&
                       !cancellationToken.IsCancellationRequested)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), cancellationToken);
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

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

            Log($"📥 Ответ сервера (длина {jsonString.Length}): {SafePreview(jsonString, 80)}");

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                // Ответ на авторизацию
                if (root.TryGetProperty("id", out var idProp))
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
                            // ✅ Centrifugo возвращает client как строку (GUID)
                            _centrifugeClientId = clientProp.GetString() ?? string.Empty;

                            Log($"✅ Авторизован! Centrifugo client = {_centrifugeClientId}");

                            OnConnected?.Invoke(this, EventArgs.Empty);

                            // Подписка на персональный канал
                            _ = SubscribeAsync(cancellationToken);
                            return;
                        }
                    }
                }

                // Данные доната
                if (root.TryGetProperty("result", out var resProp) &&
                    resProp.TryGetProperty("data", out var dataProp) &&
                    dataProp.TryGetProperty("data", out var donationProp))
                {
                    Log($"💰 Получен донат!");

                    var donation = JsonSerializer.Deserialize<DonationAlertsMessage>(donationProp.GetRawText());
                    if (donation != null)
                    {
                        OnDonationReceived?.Invoke(this, donation);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка парсинга: {ex.Message}");
            }
        }

        private async Task SubscribeAsync(CancellationToken cancellationToken)
        {
            try
            {
                string channel = $"$alerts:donation_{_userId}";
                Log($"📡 Получаем subscription_token для канала: {channel}");

                // Получаем subscription_token через API
                string? subscriptionToken = null;
                using (var api = new DonationAlertsApiClient(_accessToken))
                {
                    subscriptionToken = await api.GetSubscriptionTokenAsync(channel, _centrifugeClientId, cancellationToken);
                }

                if (string.IsNullOrEmpty(subscriptionToken))
                {
                    Log("❌ Не удалось получить subscription_token");
                    OnError?.Invoke(this, new Exception("Не удалось получить subscription_token"));
                    return;
                }

                Log($"📤 Подписка на канал (token длина {subscriptionToken.Length})...");

                var subscribePayload = new
                {
                    method = 1,
                    @params = new
                    {
                        channel = channel,
                        token = subscriptionToken
                    },
                    id = _requestId++
                };

                await SendJsonAsync(subscribePayload);
                Log("✅ Запрос на подписку отправлен");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подписки: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }

        private async Task SendJsonAsync(object payload)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket не подключён");
            }

            string json = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts?.Token ?? CancellationToken.None);
        }

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

        private void Log(string message)
        {
            Debug.WriteLine($"[DA Centrifugo] {message}");
            OnLog?.Invoke(this, message);
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA Centrifugo] Ошибка Dispose: {ex.Message}");
            }

            GC.SuppressFinalize(this);
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
    }
}