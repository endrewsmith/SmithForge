using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class GoodGameConnector : IChatConnector
    {
        private readonly ILogger<GoodGameConnector>? _logger;
        private readonly string _connectorId;
        private bool _isConnected = false;
        public string Id => _connectorId;
        public ChannelType Platform => ChannelType.GoodGame;
        public ConnectorStatus Status { get; private set; } = new();

        public event EventHandler<IncomingChatMessage>? MessageReceived;
        public event EventHandler<ConnectorStatus>? StatusChanged;

        public GoodGameConnector(ILogger<GoodGameConnector>? logger = null, string channelId = "", string connectorId = "")
        {
            _logger = logger;
            _connectorId = string.IsNullOrEmpty(connectorId) ? Guid.NewGuid().ToString() : connectorId;
            Status.IsConnected = false;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogWarning("GoodGame коннектор еще не реализован");
            await Task.Delay(100, cancellationToken);
            throw new NotImplementedException("GoodGame коннектор еще не реализован");
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _isConnected = false;
            Status.IsConnected = false;
            StatusChanged?.Invoke(this, Status);
            await Task.CompletedTask;
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("GoodGame отправка еще не реализована");
        }

        public Task<bool> ValidateSettingsAsync()
        {
            return Task.FromResult(true);
        }

        public string? GetVideoId() => null;

        public void Dispose()
        {
            // nothing yet
        }
    }
}