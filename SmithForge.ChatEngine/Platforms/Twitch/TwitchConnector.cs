using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.Twitch.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Platforms.Twitch
{
    public class TwitchConnector : IChatConnector
    {
        private readonly ILogger<TwitchConnector>? _logger;
        private TwitchChatClient? _chatClient;
        private CancellationTokenSource? _cts;
        private bool _disposed;
        private string _channelName = string.Empty;
        private string _botName = "justinfan12345";
        private string _botPassword = string.Empty;
        private readonly string _connectorId;
        private bool _isConnected = false;

        public string Id => _connectorId;
        public ChannelType Platform => ChannelType.Twitch;
        public ConnectorStatus Status { get; private set; } = new();

        public event EventHandler<IncomingChatMessage>? MessageReceived;
        public event EventHandler<ConnectorStatus>? StatusChanged;

        public TwitchConnector(
            ILogger<TwitchConnector>? logger = null,
            string channelName = "",
            string botName = "justinfan12345",
            string botPassword = "",
            string connectorId = "")
        {
            _logger = logger;
            _channelName = channelName;
            _botName = string.IsNullOrEmpty(botName) ? "justinfan12345" : botName;
            _botPassword = botPassword;
            _connectorId = string.IsNullOrEmpty(connectorId) ? Guid.NewGuid().ToString() : connectorId;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_channelName))
                {
                    throw new Exception("Не указан канал Twitch");
                }

                _logger?.LogInformation($"▶ Подключение к Twitch чату: {_channelName}");

                _chatClient = new TwitchChatClient();
                _chatClient.OnMessageReceived += OnMessageReceived;
                _chatClient.OnLog += (s, msg) => _logger?.LogInformation(msg);
                _chatClient.OnStatusChanged += OnStatusChanged;

                var success = await Task.Run(() => _chatClient.Connect(_channelName, _botName, _botPassword));

                if (success)
                {
                    _isConnected = true;
                    Status.IsConnected = true;
                    Status.ErrorMessage = null;
                    Status.MarkConnected();
                    StatusChanged?.Invoke(this, Status);

                    _logger?.LogInformation($"✅ Подключен к Twitch чату: {_channelName}");
                }
                else
                {
                    throw new Exception("Не удалось подключиться к Twitch чату");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Status.IsConnected = false;
                Status.ErrorMessage = ex.Message;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogError(ex, $"❌ Ошибка подключения к Twitch: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _cts?.Cancel();
                _chatClient?.Disconnect();

                _isConnected = false;
                Status.IsConnected = false;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogInformation($"⏹ Отключен от Twitch чата: {_channelName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ Ошибка отключения от Twitch: {ex.Message}");
            }
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (_chatClient != null && _isConnected)
            {
                _chatClient.SendMessage(message);
            }
            else
            {
                throw new InvalidOperationException("Не подключен к Twitch чату");
            }
            return Task.CompletedTask;
        }

        public Task<bool> ValidateSettingsAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_channelName));
        }

        public string? GetVideoId()
        {
            return null;
        }

        public void SetChannelName(string channelName)
        {
            _channelName = channelName.ToLower();
            _logger?.LogInformation($"📺 Установлен канал: {_channelName}");
        }

        public void SetCredentials(string botName, string botPassword)
        {
            _botName = botName;
            _botPassword = botPassword;
            _logger?.LogInformation($"🔑 Установлены учетные данные бота");
        }

        private void OnMessageReceived(object? sender, TwitchMessage message)
        {
            if (message == null) return;

            var incomingMessage = new IncomingChatMessage
            {
                Platform = ChannelType.Twitch,
                ChannelId = message.Channel,
                UserId = message.UserId ?? message.Login,
                UserName = message.Author,
                Text = message.Text,
                Timestamp = message.Timestamp.ToUniversalTime(),
                ConnectorId = Id
            };

            Status.MarkMessageReceived();
            MessageReceived?.Invoke(this, incomingMessage);
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
            else if (status == "connected" || status == "joined")
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
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cts?.Cancel();
            _cts?.Dispose();

            _chatClient?.Dispose();

            _disposed = true;
        }
    }
}