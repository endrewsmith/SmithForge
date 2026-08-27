using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.GoodGame.Models;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class GgConnector : IChatConnector
    {
        private readonly ILogger<GgConnector>? _logger;
        private GgChatClient? _chatClient;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource? _cts;
        private bool _disposed;
        private string _channelName = string.Empty;
        private readonly string _connectorId;
        private bool _isConnected = false;
        private long _channelId;  // ← Сохраняем ID канала

        public string Id => _connectorId;
        public ChannelType Platform => ChannelType.GoodGame;
        public ConnectorStatus Status { get; private set; } = new();

        public event EventHandler<IncomingChatMessage>? MessageReceived;
        public event EventHandler<ConnectorStatus>? StatusChanged;

        public GgConnector(
            ILogger<GgConnector>? logger = null,
            string channelName = "",
            string connectorId = "")
        {
            _logger = logger;
            _channelName = channelName;
            _connectorId = string.IsNullOrEmpty(connectorId) ? Guid.NewGuid().ToString() : connectorId;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_channelName))
                {
                    throw new Exception("Не указан канал GoodGame");
                }

                _logger?.LogInformation($"▶ Подключение к GoodGame чату: {_channelName}");

                var apiClient = new GgApiClient(_httpClient);
                _chatClient = new GgChatClient(apiClient);

                _chatClient.OnMessageReceived += OnMessageReceived;
                _chatClient.OnMessageDeleted += OnMessageDeleted;
                _chatClient.OnLog += (s, msg) => _logger?.LogInformation(msg);
                _chatClient.OnStatusChanged += OnStatusChanged;

                var success = await _chatClient.ConnectAsync(_channelName);

                if (success)
                {
                    // ✅ Сохраняем ID канала
                    _channelId = _chatClient.CurrentChannel?.Id ?? 0;

                    _isConnected = true;
                    Status.IsConnected = true;
                    Status.ErrorMessage = null;
                    Status.MarkConnected();
                    StatusChanged?.Invoke(this, Status);

                    _logger?.LogInformation($"✅ Подключен к GoodGame чату: {_channelName} (ID: {_channelId})");
                }
                else
                {
                    throw new Exception("Не удалось подключиться к GoodGame чату");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Status.IsConnected = false;
                Status.ErrorMessage = ex.Message;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogError(ex, $"❌ Ошибка подключения к GoodGame: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _cts?.Cancel();
                if (_chatClient != null)
                {
                    await _chatClient.DisconnectAsync();
                    _chatClient.Dispose();
                    _chatClient = null;
                }

                _isConnected = false;
                Status.IsConnected = false;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogInformation($"⏹ Отключен от GoodGame чата: {_channelName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ Ошибка отключения от GoodGame: {ex.Message}");
            }
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            // GoodGame не поддерживает отправку через API для сторонних приложений
            throw new NotSupportedException("Отправка сообщений в GoodGame чат не поддерживается");
        }

        public Task<bool> ValidateSettingsAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_channelName));
        }

        public string? GetVideoId() => null;

        public void SetChannelName(string channelName)
        {
            _channelName = channelName;
            _logger?.LogInformation($"📺 Установлен канал: {_channelName}");
        }

        private void OnMessageReceived(object? sender, GgMessage message)
        {
            Debug.WriteLine($"[GgConnector] ====== ПОЛУЧЕНО СООБЩЕНИЕ ОТ GOODGAME ======");
            Debug.WriteLine($"[GgConnector] Author: {message.Author}");
            Debug.WriteLine($"[GgConnector] Text: {message.Text}");
            Debug.WriteLine($"[GgConnector] UserId: {message.UserId}");
            Debug.WriteLine("=====================================================");

            Debug.WriteLine($"[GgConnector] OnMessageReceived: {message?.Author}: {message?.Text}");

            if (message == null) return;

            // ✅ Используем UserId как уникальный идентификатор
            string userId = message.UserId > 0 ? message.UserId.ToString() : message.Author;

            var incomingMessage = new IncomingChatMessage
            {
                Platform = ChannelType.GoodGame,
                ChannelId = _channelId.ToString(),
                UserId = userId,  // ← Используем числовой ID
                UserName = message.Author,
                Text = message.Text,
                Timestamp = message.Timestamp.ToUniversalTime(),
                ConnectorId = Id
            };

            Status.MarkMessageReceived();
            MessageReceived?.Invoke(this, incomingMessage);
        }

        private void OnMessageDeleted(object? sender, long messageId)
        {
            _logger?.LogInformation($"🗑 Удалено сообщение {messageId}");
        }

        private void OnStatusChanged(object? sender, string status)
        {
            if (status == "error")
            {
                _isConnected = false;
                Status.IsConnected = false;
                Status.ErrorMessage = "Ошибка чата";
                StatusChanged?.Invoke(this, Status);
            }
            else if (status == "connected")
            {
                _isConnected = true;
                Status.IsConnected = true;
                Status.ErrorMessage = null;
                StatusChanged?.Invoke(this, Status);
            }
            else if (status == "disconnected")
            {
                _isConnected = false;
                Status.IsConnected = false;
                StatusChanged?.Invoke(this, Status);
            }
            else if (status == "offline")
            {
                _isConnected = true;
                Status.IsConnected = true;
                Status.ErrorMessage = "Канал офлайн";
                StatusChanged?.Invoke(this, Status);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts?.Cancel();
            _cts?.Dispose();

            _chatClient?.Dispose();
            _httpClient.Dispose();

            _disposed = true;
        }
    }
}