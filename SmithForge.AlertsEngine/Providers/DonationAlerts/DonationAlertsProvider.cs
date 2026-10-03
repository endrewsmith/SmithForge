using SmithForge.AlertsEngine.Core.Interfaces;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.AlertsEngine.Providers.DonationAlerts.Models;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts
{
    /// <summary>
    /// Провайдер DonationAlerts через Centrifugo WebSocket.
    /// Использует OAuth-токены.
    /// </summary>
    public class DonationAlertsProvider : IAlertProvider
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public AlertProviderType ProviderType => AlertProviderType.DonationAlerts;
        public AlertStatus Status { get; } = new();

        public event EventHandler<IncomingAlert>? AlertReceived;
        public event EventHandler<AlertStatus>? StatusChanged;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _accessToken;
        private readonly string _refreshToken;
        private readonly long _userId;

        private DonationAlertsCentrifugoClient? _client;

        public DonationAlertsProvider(
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            long userId)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _accessToken = accessToken;
            _refreshToken = refreshToken;
            _userId = userId;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                Status.MarkDisconnected("Access Token отсутствует. Требуется OAuth-авторизация.");
                StatusChanged?.Invoke(this, Status);
                return;
            }

            if (_userId <= 0)
            {
                Status.MarkDisconnected("User ID не определён. Требуется OAuth-авторизация.");
                StatusChanged?.Invoke(this, Status);
                return;
            }

            try
            {
                Debug.WriteLine($"[DA Provider] Подключение к Centrifugo (userId={_userId})...");

                _client = new DonationAlertsCentrifugoClient(_accessToken, _userId);

                _client.OnLog += (s, msg) => Debug.WriteLine($"[DA Provider] {msg}");
                _client.OnDonationReceived += OnDonationReceived;

                _client.OnConnected += (s, e) =>
                {
                    Status.MarkConnected();
                    StatusChanged?.Invoke(this, Status);
                };

                _client.OnDisconnected += (s, reason) =>
                {
                    Status.MarkDisconnected(reason);
                    StatusChanged?.Invoke(this, Status);
                };

                _client.OnError += (s, ex) =>
                {
                    Status.MarkDisconnected(ex.Message);
                    StatusChanged?.Invoke(this, Status);
                };

                await _client.ConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA Provider] Ошибка: {ex.Message}");
                Status.MarkDisconnected(ex.Message);
                StatusChanged?.Invoke(this, Status);
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_client != null)
                {
                    _client.OnDonationReceived -= OnDonationReceived;
                    await _client.DisconnectAsync();
                    _client.Dispose();
                    _client = null;
                }

                Status.MarkDisconnected();
                StatusChanged?.Invoke(this, Status);
                Debug.WriteLine("[DA Provider] Отключено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA Provider] Ошибка отключения: {ex.Message}");
            }
        }

        private void OnDonationReceived(object? sender, DonationAlertsMessage donation)
        {
            try
            {
                if (donation == null) return;

                var alert = new IncomingAlert
                {
                    ProviderId = donation.Id.ToString(),
                    ProviderType = AlertProviderType.DonationAlerts,
                    Type = AlertType.Donation,
                    UserName = donation.Username ?? "Аноним",
                    Message = donation.Message ?? string.Empty,
                    Amount = donation.Amount,
                    Currency = donation.Currency ?? "RUB",
                    Timestamp = DateTime.UtcNow,
                    IsCommissionCovered = false,   // DA не передаёт флаг, оставляем false
                    DisplayText = $"{donation.Username} задонатил {donation.Amount:F2} {donation.Currency}"
                };

                Status.MarkAlertReceived();
                StatusChanged?.Invoke(this, Status);

                AlertReceived?.Invoke(this, alert);

                Debug.WriteLine($"[DA Provider] ✅ Алерт: {alert.DisplayText}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA Provider] Ошибка обработки доната: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
            GC.SuppressFinalize(this);
        }
    }
}