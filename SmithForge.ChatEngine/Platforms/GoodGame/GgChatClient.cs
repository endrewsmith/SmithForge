using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SmithForge.ChatEngine.Platforms.GoodGame.Models;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class GgChatClient : IDisposable
    {
        private readonly GgApiClient _apiClient;
        private GgWebSocketClient? _webSocketClient;
        private GgChannel? _channel;
        private bool _isConnected = false;
        private string _chatToken = string.Empty;
        private bool _disposed = false;

        public event EventHandler<GgMessage>? OnMessageReceived;
        public event EventHandler<long>? OnMessageDeleted;
        public event EventHandler<string>? OnLog;
        public event EventHandler<string>? OnStatusChanged;

        public GgChatClient(GgApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<bool> ConnectAsync(string channelName, string chatToken = "")
        {
            try
            {
                _chatToken = chatToken;
                OnLog?.Invoke(this, $"🔄 Подключение к каналу: {channelName}");
                OnStatusChanged?.Invoke(this, "connecting");

                _apiClient.OnLog += (s, msg) => OnLog?.Invoke(this, msg);
                _channel = await _apiClient.RequestChannelInfoAsync(channelName);

                OnLog?.Invoke(this, $"📺 Канал: {_channel.Name} (ID: {_channel.Id})");
                OnLog?.Invoke(this, $"📊 Статус: {_channel.Status}, Зрителей: {_channel.Viewers}");

                _webSocketClient = new GgWebSocketClient("chat-1.goodgame.ru", "wss");

                _webSocketClient.OnLog += (s, msg) => OnLog?.Invoke(this, msg);
                _webSocketClient.OnMessageReceived += (s, msg) =>
                {
                    Debug.WriteLine($"[GgChatClient] OnMessageReceived: {msg.Author}: {msg.Text}");
                    OnMessageReceived?.Invoke(this, msg);
                };
                _webSocketClient.OnMessageDeleted += (s, id) => OnMessageDeleted?.Invoke(this, id);
                _webSocketClient.OnConnected += (s, e) =>
                {
                    _isConnected = true;
                    OnStatusChanged?.Invoke(this, "connected");
                    OnLog?.Invoke(this, "✅ Подключен к чату GoodGame (WebSocket)");
                };
                _webSocketClient.OnDisconnected += (s, reason) =>
                {
                    _isConnected = false;
                    OnStatusChanged?.Invoke(this, "disconnected");
                    OnLog?.Invoke(this, $"⚠️ Отключен: {reason}");
                };
                _webSocketClient.OnError += (s, ex) =>
                {
                    OnLog?.Invoke(this, $"❌ Ошибка WebSocket: {ex.Message}");
                    OnStatusChanged?.Invoke(this, "error");
                };

                await _webSocketClient.ConnectAsync(_channel.Id, _chatToken);

                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke(this, $"❌ Ошибка подключения: {ex.Message}");
                OnStatusChanged?.Invoke(this, "error");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocketClient != null)
            {
                await _webSocketClient.DisconnectAsync();
                _webSocketClient.Dispose();
                _webSocketClient = null;
            }

            _isConnected = false;
            OnStatusChanged?.Invoke(this, "disconnected");
            OnLog?.Invoke(this, "⏹ Отключен от чата");
        }

        public bool IsConnected => _isConnected;
        public GgChannel? CurrentChannel => _channel;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _webSocketClient?.Dispose();
        }
    }
}