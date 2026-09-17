using System;
using System.Windows;
using System.Windows.Controls;
using SmithForge.Main.Models;
using SmithForge.Main.Services;

namespace SmithForge.Features.AlertsSettings
{
    /// <summary>
    /// Окно настроек алертов.
    /// </summary>
    public partial class AlertsSettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly AlertsSettingsViewModel _viewModel;

        public AlertsSettingsWindow(AppSettings settings)
        {
            InitializeComponent();

            _settings = settings;
            _viewModel = new AlertsSettingsViewModel();
            DataContext = _viewModel;

            // Загружаем текущие настройки в VM
            LoadSettingsIntoViewModel();

            // Подписываемся на события
            _viewModel.RequestClose += OnRequestClose;
            _viewModel.RequestAuthorize += OnRequestAuthorize;
        }

        /// <summary>
        /// Загрузить текущие настройки в ViewModel
        /// </summary>
        private void LoadSettingsIntoViewModel()
        {
            // Общие
            _viewModel.AlertsOverlayVisible = _settings.AlertsOverlayVisible;
            _viewModel.AlertsAlertDuration = _settings.AlertsAlertDuration;

            // DonationAlerts
            _viewModel.DonationAlertsEnabled = _settings.DonationAlertsEnabled;
            _viewModel.DonationAlertsClientId = _settings.DonationAlertsClientId;
            _viewModel.DonationAlertsClientSecret = _settings.DonationAlertsClientSecret;

            // DonatePay
            _viewModel.DonationPayEnabled = _settings.DonationPayEnabled;
            _viewModel.DonationPayApiKey = _settings.DonationPayApiKey;
            _viewModel.DonationPayIsAuthorized = !string.IsNullOrWhiteSpace(_settings.DonationPaySocketToken);
            _viewModel.DonationPayAuthStatus = _viewModel.DonationPayIsAuthorized
                ? "✅ Готов к подключению"
                : "❌ Не авторизован";

            // ✅ СНАЧАЛА заполняем VM, ПОТОМ синхронизируем PasswordBox
            if (ClientSecretBox != null)
            {
                ClientSecretBox.Password = _viewModel.DonationAlertsClientSecret ?? string.Empty;
            }

            if (DonatePayApiKeyBox != null)
            {
                DonatePayApiKeyBox.Password = _viewModel.DonationPayApiKey ?? string.Empty;
            }

            // Статус авторизации DonationAlerts
            UpdateAuthStatus();
        }

        /// <summary>
        /// Обновить статус авторизации
        /// </summary>
        private void UpdateAuthStatus()
        {
            bool isAuthorized = !string.IsNullOrWhiteSpace(_settings.DonationAlertsAccessToken)
                                && _settings.DonationAlertsUserId > 0;

            if (isAuthorized)
            {
                _viewModel.DonationAlertsIsAuthorized = true;
                _viewModel.DonationAlertsUserId = _settings.DonationAlertsUserId;
                _viewModel.DonationAlertsAuthStatus = $"✅ Авторизован (User ID: {_settings.DonationAlertsUserId})";
            }
            else
            {
                _viewModel.DonationAlertsIsAuthorized = false;
                _viewModel.DonationAlertsUserId = 0;
                _viewModel.DonationAlertsAuthStatus = "❌ Не авторизован";
            }
        }

        /// <summary>
        /// Сохранить настройки из ViewModel в AppSettings
        /// </summary>
        private void SaveViewModelToSettings()
        {
            // Общие
            _settings.AlertsOverlayVisible = _viewModel.AlertsOverlayVisible;
            _settings.AlertsAlertDuration = _viewModel.AlertsAlertDuration;

            // DonationAlerts
            _settings.DonationAlertsEnabled = _viewModel.DonationAlertsEnabled;
            _settings.DonationAlertsClientId = _viewModel.DonationAlertsClientId?.Trim() ?? string.Empty;
            _settings.DonationAlertsClientSecret = _viewModel.DonationAlertsClientSecret?.Trim() ?? string.Empty;

            // DonatePay
            _settings.DonationPayEnabled = _viewModel.DonationPayEnabled;
            _settings.DonationPayApiKey = _viewModel.DonationPayApiKey?.Trim() ?? string.Empty;

            ConfigService.Save(_settings);
        }

        /// <summary>
        /// Синхронизация PasswordBox с ViewModel (DonatePay)
        /// </summary>
        private void DonatePayApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && DataContext is AlertsSettingsViewModel vm)
            {
                vm.DonationPayApiKey = pb.Password;
            }
        }
        /// <summary>
        /// Синхронизация PasswordBox с ViewModel
        /// </summary>
        private void ClientSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb && DataContext is AlertsSettingsViewModel vm)
            {
                vm.DonationAlertsClientSecret = pb.Password;
            }
        }

        /// <summary>
        /// Закрытие окна (Сохранить / Отмена)
        /// </summary>
        private void OnRequestClose(object? sender, bool shouldSave)
        {
            if (shouldSave)
            {
                SaveViewModelToSettings();
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }

            Close();
        }

        /// <summary>
        /// Запуск OAuth-авторизации DonationAlerts
        /// </summary>
        private async void OnRequestAuthorize(object? sender, EventArgs e)
        {
            try
            {
                // Сначала фиксируем ClientId и ClientSecret, чтобы OAuth их использовал
                _settings.DonationAlertsClientId = _viewModel.DonationAlertsClientId?.Trim() ?? string.Empty;
                _settings.DonationAlertsClientSecret = _viewModel.DonationAlertsClientSecret?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(_settings.DonationAlertsClientId) ||
                    string.IsNullOrWhiteSpace(_settings.DonationAlertsClientSecret))
                {
                    MessageBox.Show(
                        "Сначала укажите Client ID и Client Secret.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _viewModel.DonationAlertsAuthStatus = "⏳ Ожидание авторизации в браузере...";

                // Запускаем OAuth-флоу
                var oauth = new SmithForge.AlertsEngine.Providers.DonationAlerts.DonationAlertsOAuth(
                    _settings.DonationAlertsClientId,
                    _settings.DonationAlertsClientSecret);

                var tokens = await oauth.AuthorizeAsync();

                if (tokens == null)
                {
                    _viewModel.DonationAlertsAuthStatus = "❌ Авторизация отменена";
                    return;
                }

                // Сохраняем полученные токены
                _settings.DonationAlertsAccessToken = tokens.AccessToken;
                _settings.DonationAlertsRefreshToken = tokens.RefreshToken;
                _settings.DonationAlertsTokenExpiresAt = tokens.ExpiresAt;
                _settings.DonationAlertsUserId = tokens.UserId;

                ConfigService.Save(_settings);

                UpdateAuthStatus();

                MessageBox.Show(
                    $"Авторизация успешна!\nUser ID: {tokens.UserId}",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _viewModel.DonationAlertsAuthStatus = $"❌ Ошибка: {ex.Message}";
                MessageBox.Show(
                    $"Ошибка авторизации: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.RequestClose -= OnRequestClose;
            _viewModel.RequestAuthorize -= OnRequestAuthorize;
            base.OnClosed(e);
        }
    }
}