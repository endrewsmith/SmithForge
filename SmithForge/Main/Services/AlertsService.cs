using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SmithForge.AlertsEngine;
using SmithForge.AlertsEngine.Core.Interfaces;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Сервис управления алертами.
    /// Поддерживает несколько провайдеров одновременно (DonationAlerts, DonationPay и др.).
    /// </summary>
    public class AlertsService : IDisposable
    {
        private readonly IAlertProviderFactory _providerFactory;

        /// <summary>
        /// Активные провайдеры (ключ — тип провайдера)
        /// </summary>
        private readonly Dictionary<AlertProviderType, IAlertProvider> _providers = new();

        private readonly object _lockObject = new();

        /// <summary>
        /// Событие: получен новый алерт (от любого провайдера)
        /// </summary>
        public event EventHandler<IncomingAlert>? AlertReceived;

        /// <summary>
        /// Событие: изменился статус какого-либо провайдера
        /// </summary>
        public event EventHandler<AlertStatus>? StatusChanged;

        public AlertsService()
        {
            _providerFactory = new AlertProviderFactory();
        }

        /// <summary>
        /// Запустить ВСЕ провайдеры на основе настроек.
        /// </summary>
        public async Task StartAsync(AppSettings settings)
        {
            Debug.WriteLine("[AlertsService] Запуск провайдеров алертов...");

            await StopAsync();

            // === DonationAlerts ===
            if (settings.DonationAlertsEnabled)
            {
                // Проверяем: есть ли ClientId, ClientSecret и уже полученный AccessToken
                if (string.IsNullOrWhiteSpace(settings.DonationAlertsClientId) ||
                    string.IsNullOrWhiteSpace(settings.DonationAlertsClientSecret))
                {
                    Debug.WriteLine("[AlertsService] DonationAlerts: Client ID / Client Secret не указаны.");
                    NotifyStatusError("DonationAlerts: укажите Client ID и Client Secret в настройках.");
                }
                else if (string.IsNullOrWhiteSpace(settings.DonationAlertsAccessToken))
                {
                    Debug.WriteLine("[AlertsService] DonationAlerts: Access Token отсутствует — требуется OAuth-авторизация.");
                    NotifyStatusError("DonationAlerts: нажмите 'Войти через DonationAlerts' в настройках.");
                }
                else
                {
                    await StartProviderAsync(AlertProviderType.DonationAlerts, settings);
                }
            }

            // === DonatePay ===
            if (settings.DonationPayEnabled)
            {
                if (!string.IsNullOrWhiteSpace(settings.DonationPayApiKey))
                {
                    await StartProviderAsync(AlertProviderType.DonationPay, settings);
                }
                else
                {
                    Debug.WriteLine("[AlertsService] DonatePay: API-ключ не указан.");
                    NotifyStatusError("DonatePay: укажите API-ключ в настройках.");
                }
            }

            Debug.WriteLine($"[AlertsService] Запущено провайдеров: {_providers.Count}");
        }

        /// <summary>
        /// Запустить конкретный провайдер
        /// </summary>
        private async Task StartProviderAsync(AlertProviderType providerType, AppSettings settings)
        {
            try
            {
                Debug.WriteLine($"[AlertsService] Запуск провайдера: {providerType}");

                IAlertProvider? provider = providerType switch
                {
                    AlertProviderType.DonationAlerts => _providerFactory.CreateProvider(
                        providerType,
                        settings.DonationAlertsClientId,
                        settings.DonationAlertsClientSecret,
                        settings.DonationAlertsAccessToken,
                        settings.DonationAlertsRefreshToken,
                        settings.DonationAlertsUserId),

                    AlertProviderType.DonationPay => _providerFactory.CreateProvider(
                        providerType,
                        settings.DonationPayApiKey),

                    _ => null
                };

                if (provider == null)
                {
                    Debug.WriteLine($"[AlertsService] Не удалось создать провайдер {providerType}");
                    NotifyStatusError($"{providerType}: не удалось создать провайдер.");
                    return;
                }

                provider.AlertReceived += OnProviderAlertReceived;
                provider.StatusChanged += OnProviderStatusChanged;

                await provider.ConnectAsync();

                lock (_lockObject)
                {
                    _providers[providerType] = provider;
                }

                Debug.WriteLine($"[AlertsService] ✅ Провайдер {providerType} запущен");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AlertsService] Ошибка запуска провайдера {providerType}: {ex.Message}");
                NotifyStatusError($"{providerType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Остановить ВСЕ активные провайдеры
        /// </summary>
        public async Task StopAsync()
        {
            List<IAlertProvider> providersToStop;

            lock (_lockObject)
            {
                providersToStop = new List<IAlertProvider>(_providers.Values);
                _providers.Clear();
            }

            foreach (var provider in providersToStop)
            {
                try
                {
                    Debug.WriteLine($"[AlertsService] Остановка провайдера {provider.ProviderType}...");

                    provider.AlertReceived -= OnProviderAlertReceived;
                    provider.StatusChanged -= OnProviderStatusChanged;

                    await provider.DisconnectAsync();
                    provider.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AlertsService] Ошибка остановки провайдера {provider.ProviderType}: {ex.Message}");
                }
            }

            if (providersToStop.Count > 0)
            {
                Debug.WriteLine($"[AlertsService] Остановлено провайдеров: {providersToStop.Count}");
            }
        }

        /// <summary>
        /// Получить статус конкретного провайдера
        /// </summary>
        public AlertStatus? GetStatus(AlertProviderType providerType)
        {
            lock (_lockObject)
            {
                if (_providers.TryGetValue(providerType, out var provider))
                    return provider.Status;
            }
            return null;
        }

        /// <summary>
        /// Получить все активные провайдеры
        /// </summary>
        public IReadOnlyDictionary<AlertProviderType, IAlertProvider> GetActiveProviders()
        {
            lock (_lockObject)
            {
                return new Dictionary<AlertProviderType, IAlertProvider>(_providers);
            }
        }

        /// <summary>
        /// Есть ли активные провайдеры
        /// </summary>
        public bool HasActiveProviders
        {
            get
            {
                lock (_lockObject)
                {
                    return _providers.Count > 0;
                }
            }
        }

        // ============================================================
        // ОБРАБОТЧИКИ СОБЫТИЙ ОТ ПРОВАЙДЕРОВ
        // ============================================================

        private void OnProviderAlertReceived(object? sender, IncomingAlert alert)
        {
            Debug.WriteLine($"[AlertsService] [{alert.ProviderType}] Получен алерт: {alert.DisplayText}");
            AlertReceived?.Invoke(this, alert);
        }

        private void OnProviderStatusChanged(object? sender, AlertStatus status)
        {
            Debug.WriteLine($"[AlertsService] Статус изменился: {status}");
            StatusChanged?.Invoke(this, status);
        }

        private void NotifyStatusError(string message)
        {
            StatusChanged?.Invoke(this, new AlertStatus
            {
                ErrorMessage = message
            });
        }

        public void Dispose()
        {
            _ = StopAsync();
            GC.SuppressFinalize(this);
        }
    }
}