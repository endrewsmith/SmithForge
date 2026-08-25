using SmithForge.ChatEngine.Platforms.Twitch.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Models;
using System.Linq;

namespace SmithForge.ChatEngine.Platforms.Twitch
{
    public class TwitchChatClient : IDisposable
    {
        private TwitchClient? _client;
        private string _channelName = string.Empty;
        private bool _isConnected = false;
        private readonly ConcurrentQueue<TwitchMessage> _messageQueue = new();
        private CancellationTokenSource? _cts;
        private Task? _processingTask;
        private bool _disposed = false;

        public event EventHandler<TwitchMessage>? OnMessageReceived;
        public event EventHandler<string>? OnLog;
        public event EventHandler<string>? OnStatusChanged;
        public event EventHandler<string>? OnMessageDeleted;

        public bool IsConnected => _isConnected;
        public string ChannelName => _channelName;

        public bool Connect(string channelName, string botName = "justinfan12345", string botPassword = "")
        {
            try
            {
                _channelName = channelName.ToLower();
                Log($"🔄 Подключение к чату: {channelName}");

                var credentials = new ConnectionCredentials(botName, botPassword);

                // ✅ Исправлено: убрали RateLimiter из ClientOptions
                var clientOptions = new ClientOptions
                {
                    // RateLimiter был удалён или перемещён
                    // Используем стандартные настройки
                };

                var webSocketClient = new WebSocketClient(clientOptions);
                _client = new TwitchClient(webSocketClient);

                _client.OnConnected += OnConnected;
                _client.OnJoinedChannel += OnJoinedChannel;
                _client.OnDisconnected += OnDisconnected;
                _client.OnConnectionError += OnConnectionError;
                _client.OnError += OnError;
                _client.OnMessageReceived += OnMessageReceivedHandler;
                _client.OnUserBanned += OnUserBanned;
                _client.OnUserTimedout += OnUserTimedout;

                _client.Initialize(credentials, _channelName);
                _client.Connect();

                _isConnected = true;

                _cts = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessQueue(_cts.Token));

                Log($"✅ Подключен к чату: {channelName}");
                OnStatusChanged?.Invoke(this, "connected");
                return true;
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подключения: {ex.Message}");
                OnStatusChanged?.Invoke(this, "error");
                return false;
            }
        }

        private void OnConnected(object? sender, OnConnectedArgs e)
        {
            Log("📡 Соединение установлено");
            OnStatusChanged?.Invoke(this, "connected");
        }

        private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
        {
            Log($"📺 Присоединился к каналу: {e.Channel}");
            OnStatusChanged?.Invoke(this, "joined");
        }

        private void OnDisconnected(object? sender, OnDisconnectedEventArgs e)
        {
            Log("⚠️ Отключен от Twitch");
            _isConnected = false;
            OnStatusChanged?.Invoke(this, "disconnected");
        }

        private void OnConnectionError(object? sender, OnConnectionErrorArgs e)
        {
            Log($"❌ Ошибка соединения: {e.Error?.Message ?? "Unknown"}");
            OnStatusChanged?.Invoke(this, "error");
        }

        private void OnError(object? sender, OnErrorEventArgs e)
        {
            Log($"❌ Ошибка: {e.Exception?.Message ?? "Unknown"}");
        }

        private void OnMessageReceivedHandler(object? sender, OnMessageReceivedArgs e)
        {
            try
            {
                var msg = e.ChatMessage;

                // ✅ ВЫВОДИМ ВСЁ, ЧТО ДОСТУПНО В TWITCHLIB
                Debug.WriteLine("═══════════════════════════════════════════════");
                Debug.WriteLine("[Twitch] СЫРЫЕ ДАННЫЕ (RAW):");
                Debug.WriteLine($"  EmoteSet: {msg.EmoteSet?.ToString() ?? "null"}");
                Debug.WriteLine($"  Id: {msg.Id ?? "null"}");
                Debug.WriteLine($"  Channel: {msg.Channel ?? "null"}");
                Debug.WriteLine($"  Message: {msg.Message ?? "null"}");
                Debug.WriteLine($"  Username: {msg.Username ?? "null"}");
                Debug.WriteLine($"  DisplayName: {msg.DisplayName ?? "null"}");
                Debug.WriteLine($"  UserId: {msg.UserId ?? "null"}");
                Debug.WriteLine($"  ColorHex: {msg.ColorHex ?? "null"}");
                Debug.WriteLine($"  IsBroadcaster: {msg.IsBroadcaster}");
                Debug.WriteLine($"  IsModerator: {msg.IsModerator}");
                Debug.WriteLine($"  IsSubscriber: {msg.IsSubscriber}");
                Debug.WriteLine($"  IsVip: {msg.IsVip}");
                Debug.WriteLine($"  IsMe: {msg.IsMe}");
                Debug.WriteLine($"  RoomId: {msg.RoomId ?? "null"}");
                Debug.WriteLine($"  TmiSentTs: {msg.TmiSentTs ?? "null"}");
                Debug.WriteLine($"  UserType: {msg.UserType}");

                if (msg.Badges != null && msg.Badges.Count > 0)
                {
                    Debug.WriteLine("  Badges:");
                    foreach (var badge in msg.Badges)
                    {
                        Debug.WriteLine($"    {badge.Key} = {badge.Value}");
                    }
                }

                // ✅ ВЫВОДИМ ЭМОДЗИ ИЗ EMOTESET
                if (msg.EmoteSet != null)
                {
                    Debug.WriteLine($"  EmoteSet.Emotes count: {msg.EmoteSet.Emotes?.Count ?? 0}");
                    if (msg.EmoteSet.Emotes != null)
                    {
                        foreach (var emote in msg.EmoteSet.Emotes)
                        {
                            Debug.WriteLine($"    Emote: {emote.Name} (ID: {emote.Id})");
                        }
                    }
                }
                Debug.WriteLine("═══════════════════════════════════════════════");

                var message = new TwitchMessage
                {
                    Id = msg.Id ?? Guid.NewGuid().ToString(),
                    Author = msg.DisplayName ?? msg.Username,
                    Login = msg.Username ?? "",
                    Text = msg.Message,
                    Timestamp = DateTime.Now,
                    Channel = msg.Channel,
                    ColorHex = msg.ColorHex ?? "#9146FF",
                    IsAction = false,
                    UserId = msg.UserId ?? "",
                    IsBroadcaster = msg.IsBroadcaster,
                    IsModerator = msg.IsModerator,
                    IsSubscriber = msg.IsSubscriber,
                    IsVip = msg.IsVip,
                    EmoteSet = msg.EmoteSet?.ToString() ?? string.Empty
                };

                // ✅ ЗАПОЛНЯЕМ EMOTES ИЗ EMOTESET
                if (msg.EmoteSet?.Emotes != null && msg.EmoteSet.Emotes.Count > 0)
                {
                    foreach (var emote in msg.EmoteSet.Emotes)
                    {
                        var emoteId = emote.Id.ToString();

                        // Проверяем, не добавили ли уже этот эмодзи
                        if (!message.Emotes.Any(e => e.Id == emoteId))
                        {
                            var emoteData = new TwitchEmoteData
                            {
                                Id = emoteId,
                                Code = emote.Name ?? string.Empty,
                                Positions = new List<int>() // В Emote нет Positions
                            };
                            message.Emotes.Add(emoteData);

                            Debug.WriteLine($"[Twitch] Добавлен эмодзи: {emoteData.Code} (ID: {emoteData.Id})");
                        }
                    }
                }

                Debug.WriteLine($"[Twitch] Итоговое количество эмодзи в сообщении: {message.Emotes.Count}");

                _messageQueue.Enqueue(message);
            }
            catch (Exception ex)
            {
                Log($"⚠️ Ошибка обработки сообщения: {ex.Message}");
                Debug.WriteLine($"[Twitch] Ошибка: {ex.Message}");
                Debug.WriteLine($"[Twitch] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Парсит EmoteSet вручную (формат: "25:0-4,5-9/26:10-14")
        /// </summary>
        private void ParseEmoteSet(string emoteSet, TwitchMessage message, string text)
        {
            if (string.IsNullOrEmpty(emoteSet)) return;

            var parts = emoteSet.Split('/');
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                var colonIndex = part.IndexOf(':');
                if (colonIndex <= 0) continue;

                var id = part.Substring(0, colonIndex);
                var positionsStr = part.Substring(colonIndex + 1);

                var emoteData = new TwitchEmoteData
                {
                    Id = id
                };

                // Парсим позиции: "0-4,5-9"
                var positionPairs = positionsStr.Split(',');
                foreach (var pair in positionPairs)
                {
                    if (string.IsNullOrEmpty(pair)) continue;

                    var dashIndex = pair.IndexOf('-');
                    if (dashIndex > 0)
                    {
                        if (int.TryParse(pair.Substring(0, dashIndex), out var start))
                        {
                            emoteData.Positions.Add(start);

                            // Извлекаем код эмодзи из текста по первой позиции
                            if (emoteData.Positions.Count == 1)
                            {
                                if (int.TryParse(pair.Substring(dashIndex + 1), out var end))
                                {
                                    if (start >= 0 && end < text.Length)
                                    {
                                        emoteData.Code = text.Substring(start, end - start + 1);
                                    }
                                }
                            }
                        }
                    }
                }

                if (emoteData.Positions.Count > 0)
                {
                    message.Emotes.Add(emoteData);
                }
            }
        }

        private void OnUserBanned(object? sender, OnUserBannedArgs e)
        {
            Log($"🔨 {e.UserBan?.Username} забанен");
            OnMessageDeleted?.Invoke(this, e.UserBan?.Username ?? "");
        }

        private void OnUserTimedout(object? sender, OnUserTimedoutArgs e)
        {
            Log($"⏱ {e.UserTimeout?.Username} затаймаутен");
            OnMessageDeleted?.Invoke(this, e.UserTimeout?.Username ?? "");
        }

        private async Task ProcessQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        OnMessageReceived?.Invoke(this, message);
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Log($"⚠️ Ошибка очереди: {ex.Message}");
                }
            }
        }

        public void SendMessage(string message)
        {
            if (_client != null && _client.IsConnected)
            {
                _client.SendMessage(_channelName, message);
                Log($"📤 Отправлено: {message}");
            }
            else
            {
                Log("❌ Не подключен к чату");
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _cts?.Cancel();
            _cts?.Dispose();

            if (_client != null && _client.IsConnected)
            {
                _client.Disconnect();
            }

            Log("⏹ Отключен от чата");
            OnStatusChanged?.Invoke(this, "disconnected");
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
            _client = null;
        }
    }
}