using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.AlertsEngine.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Features.AlertsOverlay
{
    /// <summary>
    /// ViewModel оверлея алертов.
    /// Поддерживает мульти-провайдерность (DonationAlerts, DonationPay и др.).
    /// </summary>
    public partial class AlertsOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        [ObservableProperty]
        private AlertDisplayModel? _currentAlert;

        /// <summary>
        /// Очередь алертов (если пришло несколько подряд)
        /// </summary>
        private readonly Queue<IncomingAlert> _alertQueue = new();

        private readonly object _queueLock = new();
        private CancellationTokenSource? _hideCts;
        private bool _isShowing;
        private int _alertDurationSeconds = 10;

        /// <summary>
        /// Установить длительность показа алерта
        /// </summary>
        public void SetAlertDuration(int seconds)
        {
            _alertDurationSeconds = Math.Max(1, seconds);
        }

        /// <summary>
        /// Показать алерт. Если уже показывается — добавит в очередь.
        /// </summary>
        public void ShowAlert(IncomingAlert alert)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowAlert(alert));
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[AlertsOverlay] ShowAlert: {alert.DisplayText}");

            lock (_queueLock)
            {
                _alertQueue.Enqueue(alert);
            }

            if (!_isShowing)
            {
                _ = ProcessQueueAsync();
            }
        }

        /// <summary>
        /// Обработка очереди алертов (показывает по одному)
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            _isShowing = true;

            try
            {
                while (true)
                {
                    IncomingAlert? nextAlert = null;

                    lock (_queueLock)
                    {
                        if (_alertQueue.Count > 0)
                        {
                            nextAlert = _alertQueue.Dequeue();
                        }
                    }

                    if (nextAlert == null) break;

                    await ShowSingleAlertAsync(nextAlert);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlertsOverlay] Ошибка очереди: {ex.Message}");
            }
            finally
            {
                _isShowing = false;
                CurrentAlert = null;
            }
        }

        /// <summary>
        /// Показать один алерт на _alertDurationSeconds секунд
        /// </summary>
        private async Task ShowSingleAlertAsync(IncomingAlert alert)
        {
            _hideCts?.Cancel();
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;

            CurrentAlert = new AlertDisplayModel(alert);

            try
            {
                await Task.Delay(_alertDurationSeconds * 1000, token);
            }
            catch (TaskCanceledException)
            {
                // Новый алерт пришёл — прерываем текущий
            }

            CurrentAlert = null;

            // Небольшая пауза между алертами
            await Task.Delay(300);
        }

        /// <summary>
        /// Очистить очередь и скрыть текущий алерт
        /// </summary>
        public void ClearAll()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(ClearAll);
                return;
            }

            _hideCts?.Cancel();

            lock (_queueLock)
            {
                _alertQueue.Clear();
            }

            CurrentAlert = null;
            _isShowing = false;
        }
    }

    /// <summary>
    /// Модель для отображения алерта в UI
    /// </summary>
    public class AlertDisplayModel
    {
        public string UserName { get; }
        public string Message { get; }
        public string DisplayAmount { get; }
        public bool HasMessage { get; }
        public string ProviderName { get; }
        public AlertProviderType ProviderType { get; }
        public AlertType AlertType { get; }

        // ✅ НОВОЕ
        public bool IsCommissionCovered { get; }

        public AlertDisplayModel(IncomingAlert alert)
        {
            UserName = string.IsNullOrWhiteSpace(alert.UserName) ? "Аноним" : alert.UserName;
            Message = alert.Message ?? string.Empty;
            HasMessage = !string.IsNullOrWhiteSpace(Message);

            ProviderType = alert.ProviderType;
            AlertType = alert.Type;

            // ✅ НОВОЕ
            IsCommissionCovered = alert.IsCommissionCovered;

            ProviderName = alert.ProviderType switch
            {
                AlertProviderType.DonationAlerts => "DonationAlerts",
                AlertProviderType.DonationPay => "DonationPay",
                _ => "Alerts"
            };

            DisplayAmount = alert.Type switch
            {
                AlertType.Donation => $"{alert.Amount:F0} {alert.Currency}",
                AlertType.Subscription => "Подписка",
                AlertType.Follow => "Фолловер",
                _ => alert.Type.ToString()
            };
        }
    }
}