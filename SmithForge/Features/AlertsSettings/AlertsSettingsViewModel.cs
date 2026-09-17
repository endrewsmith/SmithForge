using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace SmithForge.Features.AlertsSettings
{
    /// <summary>
    /// ViewModel окна настроек алертов.
    /// Содержит настройки для всех провайдеров (DonationAlerts, DonationPay и др.).
    /// </summary>
    public partial class AlertsSettingsViewModel : ObservableObject
    {
        // ============================================================
        // === ОБЩИЕ НАСТРОЙКИ ===
        // ============================================================
        [ObservableProperty]
        private bool _alertsOverlayVisible;

        [ObservableProperty]
        private int _alertsAlertDuration;

        // ============================================================
        // === DONATIONALERTS (OAuth) ===
        // ============================================================
        [ObservableProperty]
        private bool _donationAlertsEnabled;

        [ObservableProperty]
        private string _donationAlertsClientId = string.Empty;

        [ObservableProperty]
        private string _donationAlertsClientSecret = string.Empty;

        // Статус авторизации (заполняется программно из настроек)
        [ObservableProperty]
        private string _donationAlertsAuthStatus = "❌ Не авторизован";

        [ObservableProperty]
        private bool _donationAlertsIsAuthorized;

        [ObservableProperty]
        private long _donationAlertsUserId;

        // ============================================================
        // === DONATEPAY ===
        // ============================================================
        [ObservableProperty]
        private bool _donationPayEnabled;

        [ObservableProperty]
        private string _donationPayApiKey = string.Empty;

        [ObservableProperty]
        private string _donationPayAuthStatus = "❌ Не авторизован";

        [ObservableProperty]
        private bool _donationPayIsAuthorized;

        // ============================================================
        // === СОБЫТИЯ ===
        // ============================================================
        public event EventHandler<bool>? RequestClose;
        public event EventHandler? RequestAuthorize;

        // ============================================================
        // === КОМАНДЫ ===
        // ============================================================
        [RelayCommand]
        private void Save()
        {
            RequestClose?.Invoke(this, true);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(this, false);
        }

        /// <summary>
        /// Кнопка "Войти через DonationAlerts" — запускает OAuth
        /// </summary>
        [RelayCommand]
        private void Authorize()
        {
            RequestAuthorize?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void OpenDonationAlertsPage()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.donationalerts.com/application/clients",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlertsSettings] Ошибка открытия браузера: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenDonationPayPage()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://donationpay.ru",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlertsSettings] Ошибка открытия браузера: {ex.Message}");
            }
        }
    }
}