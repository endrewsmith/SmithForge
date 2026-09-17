using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.AlertsEngine.Core.Interfaces;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.AlertsEngine.Providers.DonatePay.Models;

namespace SmithForge.AlertsEngine.Providers.DonatePay
{
    /// <summary>
    /// Провайдер DonatePay через Centrifugo WebSocket.
    /// Использует API-ключ (вводит пользователь).
    /// </summary>
    public class DonatePayProvider : IAlertProvider
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public AlertProviderType ProviderType => AlertProviderType.DonationPay;
        public AlertStatus Status { get; } = new();

        public event EventHandler<IncomingAlert>? AlertReceived;
        public event EventHandler<AlertStatus>? StatusChanged;

        private readonly string _apiKey;
        private DonatePayCentrifugoClient? _client;

        public DonatePayProvider(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Status.MarkDisconnected("API-ключ не указан");
                StatusChanged?.Invoke(this, Status);
                return;
            }

            try
            {
                Debug.WriteLine("[DP Provider] Подключение к Centrifugo...");

                _client = new DonatePayCentrifugoClient(_apiKey);



                _client.OnLog += (s, msg) => Debug.WriteLine($"[DP Provider] {msg}");
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
                Debug.WriteLine($"[DP Provider] Ошибка: {ex.Message}");
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
                Debug.WriteLine("[DP Provider] Отключено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DP Provider] Ошибка отключения: {ex.Message}");
            }
        }

        private void OnDonationReceived(object? sender, DonatePayMessage donation)
        {
            try
            {
                if (donation == null || donation.Vars == null) return;

                var vars = donation.Vars;

                // ✅ Парсим флаг покрытия комиссии
                bool isCommissionCovered = vars.IsCommissionCovered == 1;

                var alert = new IncomingAlert
                {
                    ProviderId = donation.Id.ToString(),
                    ProviderType = AlertProviderType.DonationPay,
                    Type = AlertType.Donation,
                    UserName = vars.Name ?? "Аноним",
                    Message = vars.Comment ?? string.Empty,
                    Amount = vars.Sum,
                    Currency = vars.Currency ?? "RUB",
                    Timestamp = DateTime.UtcNow,
                    IsCommissionCovered = isCommissionCovered,
                    DisplayText = $"{vars.Name} задонатил {vars.Sum:F2} {vars.Currency}"
                };

                Status.MarkAlertReceived();
                StatusChanged?.Invoke(this, Status);

                AlertReceived?.Invoke(this, alert);

                Debug.WriteLine($"[DP Provider] ✅ Алерт: {alert.DisplayText} (комиссия покрыта: {isCommissionCovered})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DP Provider] Ошибка обработки доната: {ex.Message}");
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