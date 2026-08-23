using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.ChatEngine.Platforms.GoodGame.Models;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class GgWebSocketClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly string _host;
        private readonly string _scheme;
        private CancellationTokenSource? _cts;
        private bool _isConnected = false;
        private string? _chatToken;

        public event EventHandler<string>? OnLog;
        public event EventHandler<GgMessage>? OnMessageReceived;
        public event EventHandler<long>? OnMessageDeleted;
        public event EventHandler? OnConnected;
        public event EventHandler<string>? OnDisconnected;
        public event EventHandler<Exception>? OnError;

        public GgWebSocketClient(string host = "chat-1.goodgame.ru", string scheme = "wss")
        {
            _host = host;
            _scheme = scheme;
        }

        public async Task ConnectAsync(long channelId, string chatToken = "")
        {
            try
            {
                _chatToken = chatToken;
                var wsUri = $"{_scheme}://{_host}/chat/websocket";
                OnLog?.Invoke(this, $"🔄 Подключение к WebSocket: {wsUri}");

                _webSocket = new ClientWebSocket();
                _cts = new CancellationTokenSource();

                _webSocket.Options.SetRequestHeader("Origin", "https://goodgame.ru");
                _webSocket.Options.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                await _webSocket.ConnectAsync(new Uri(wsUri), _cts.Token);
                _isConnected = true;

                OnConnected?.Invoke(this, EventArgs.Empty);
                OnLog?.Invoke(this, "✅ Подключен к WebSocket GoodGame");

                var joinMessage = new
                {
                    type = "join",
                    data = new
                    {
                        channel_id = channelId,
                        isHidden = false,
                        token = _chatToken
                    }
                };

                var json = JsonSerializer.Serialize(joinMessage);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                OnLog?.Invoke(this, $"📤 Отправлен join для канала {channelId}");

                _ = Task.Run(() => ReceiveMessagesAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                OnLog?.Invoke(this, $"❌ Ошибка подключения: {ex.Message}");
                throw;
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_webSocket == null) break;

                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                        _isConnected = false;
                        OnDisconnected?.Invoke(this, "Соединение закрыто");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(message);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                    OnLog?.Invoke(this, $"❌ Ошибка получения: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            // ✅ ОТЛАДКА: выводим сырое сообщение
            Debug.WriteLine($"[GgWebSocket] RAW: {message}");

            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();
                    Debug.WriteLine($"[GgWebSocket] Тип: {type}");

                    // Проверяем наличие поля data
                    if (!root.TryGetProperty("data", out var data))
                    {
                        Debug.WriteLine($"[GgWebSocket] ⚠️ Нет поля 'data' в сообщении");
                        OnLog?.Invoke(this, "⚠️ Нет поля 'data' в сообщении");
                        return;
                    }

                    switch (type)
                    {
                        case "message":
                            await HandleUserMessageAsync(data);
                            break;
                        case "remove_message":
                            await HandleRemoveMessageAsync(data);
                            break;
                        case "welcome":
                            Debug.WriteLine($"[GgWebSocket] 👋 Welcome получено");
                            OnLog?.Invoke(this, "👋 Получено welcome от сервера");
                            break;
                        default:
                            Debug.WriteLine($"[GgWebSocket] 📨 Неизвестный тип: {type}");
                            OnLog?.Invoke(this, $"📨 Неизвестный тип: {type}");
                            break;
                    }
                }
                else
                {
                    Debug.WriteLine($"[GgWebSocket] ⚠️ Нет поля 'type' в сообщении");
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[GgWebSocket] ❌ Ошибка JSON: {ex.Message}");
                Debug.WriteLine($"[GgWebSocket] ❌ Сообщение: {message}");
                OnLog?.Invoke(this, $"⚠️ Ошибка парсинга JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GgWebSocket] ❌ Ошибка парсинга: {ex.Message}");
                Debug.WriteLine($"[GgWebSocket] ❌ Сообщение: {message}");
                OnLog?.Invoke(this, $"⚠️ Ошибка парсинга: {ex.Message}");
            }
        }

        private Task HandleUserMessageAsync(JsonElement data)
        {
            try
            {
                Debug.WriteLine($"[GgWebSocket] HandleUserMessageAsync: {data}");

                // Проверяем наличие обязательных полей
                if (!data.TryGetProperty("message_id", out var msgId))
                {
                    Debug.WriteLine("[GgWebSocket] ⚠️ Нет поля message_id");
                    return Task.CompletedTask;
                }

                if (!data.TryGetProperty("user_name", out var userName))
                {
                    Debug.WriteLine("[GgWebSocket] ⚠️ Нет поля user_name");
                    return Task.CompletedTask;
                }

                if (!data.TryGetProperty("text", out var text))
                {
                    Debug.WriteLine("[GgWebSocket] ⚠️ Нет поля text");
                    return Task.CompletedTask;
                }

                // ✅ Получаем user_id
                long userId = 0;
                if (data.TryGetProperty("user_id", out var userIdElement))
                {
                    userId = userIdElement.GetInt64();
                    Debug.WriteLine($"[GgWebSocket] UserId: {userId}");
                }

                // Парсим подписки
                var subscriptionDuration = new Dictionary<long, int>();
                if (data.TryGetProperty("resubs", out var resubs))
                {
                    foreach (var prop in resubs.EnumerateObject())
                    {
                        if (long.TryParse(prop.Name, out var channelId))
                        {
                            var duration = prop.Value.GetInt32();
                            subscriptionDuration[channelId] = duration;
                        }
                    }
                }

                // Получаем цвет
                string colorHex = "#FFFFFF";
                if (data.TryGetProperty("color", out var color))
                {
                    colorHex = color.GetString() ?? "#FFFFFF";
                    if (!colorHex.StartsWith("#"))
                        colorHex = "#" + colorHex;
                }

                var ggMessage = new GgMessage
                {
                    GgId = msgId.GetInt64(),
                    UserId = userId,  // ← Сохраняем ID пользователя
                    Author = userName.GetString() ?? "Unknown",
                    Text = text.GetString() ?? string.Empty,
                    Timestamp = DateTime.Now,
                    SubscriptionDuration = subscriptionDuration,
                    BadgeName = data.TryGetProperty("icon", out var icon) ? icon.GetString() ?? "" : "",
                    AuthorColorName = colorHex,
                    SponsorLevel = data.TryGetProperty("payments", out var payments) ? payments.GetInt32() : 0,
                    AuthorRights = data.TryGetProperty("user_rights", out var rights) ? rights.GetInt32() : 0,
                    ColorHex = colorHex
                };

                Debug.WriteLine($"[GgWebSocket] ✅ Сообщение: {ggMessage.Author} (ID: {ggMessage.UserId}): {ggMessage.Text}");
                OnMessageReceived?.Invoke(this, ggMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GgWebSocket] ❌ Ошибка обработки сообщения: {ex.Message}");
                Debug.WriteLine($"[GgWebSocket] ❌ Data: {data}");
                OnLog?.Invoke(this, $"⚠️ Ошибка обработки сообщения: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private Task HandleRemoveMessageAsync(JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("message_id", out var msgId))
                {
                    Debug.WriteLine("[GgWebSocket] ⚠️ Нет поля message_id для удаления");
                    return Task.CompletedTask;
                }

                var messageId = msgId.GetInt64();
                Debug.WriteLine($"[GgWebSocket] 🗑 Удалено сообщение {messageId}");
                OnMessageDeleted?.Invoke(this, messageId);
                OnLog?.Invoke(this, $"🗑 Удалено сообщение {messageId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GgWebSocket] ❌ Ошибка удаления: {ex.Message}");
                OnLog?.Invoke(this, $"⚠️ Ошибка удаления: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            _isConnected = false;
            _cts?.Cancel();

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);
            }

            _webSocket?.Dispose();
            _webSocket = null;

            OnLog?.Invoke(this, "⏹ Отключен от WebSocket");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _webSocket?.Dispose();
        }
    }
}