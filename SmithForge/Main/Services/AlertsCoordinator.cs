using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.Features.StatsRotation;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор алертов и важных сообщений.
    /// Управляет AlertsService и предоставляет UI-команды.
    /// </summary>
    public partial class AlertsCoordinator : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly OverlayManagerService _overlayManager;
        private readonly WebServerService? _webServer;
        private readonly AppSettings _settings;
        private readonly AlertsService _alertsService = new();

        // ============================================================
        // СВОЙСТВА ДЛЯ UI
        // ============================================================
        [ObservableProperty]
        private int _importantQueueCount = 0;

        [ObservableProperty]
        private ImportantPlaybackMode _importantPlaybackMode = ImportantPlaybackMode.Auto;

        [ObservableProperty]
        private string _importantPlaybackHotkey = "F8";

        [ObservableProperty]
        private bool _isAutoSwitchingEnabled = true;


        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public AlertsCoordinator(
            SettingsService settingsService,
            OverlayManagerService overlayManager,
            WebServerService? webServer,
            AppSettings settings)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
            _webServer = webServer;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Загружаем начальные значения
            _importantPlaybackMode = settings.ImportantPlaybackMode;
            _importantPlaybackHotkey = settings.ImportantPlaybackHotkey;

            // Подписываемся на события AlertsService
            _alertsService.AlertReceived += OnAlertReceived;
            _alertsService.StatusChanged += OnAlertStatusChanged;
        }

        // ============================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ
        // ============================================================
        public async Task StartAsync()
        {
            await _alertsService.StartAsync(_settings);
            _overlayManager.SetAlertsVisible(_settings.AlertsOverlayVisible);

            Debug.WriteLine("[AlertsCoordinator] AlertsService запущен");
        }

        public async Task StopAsync()
        {

            await _alertsService.StopAsync();
            Debug.WriteLine("[AlertsCoordinator] AlertsService остановлен");
        }

        /// <summary>Прокидываем активность из MainViewModel.</summary>
        public void NotifyUserActivity()
        {
            
        }

        public void SetImportantPlaybackMode(ImportantPlaybackMode mode)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => SetImportantPlaybackMode(mode));
                return;
            }

            if (ImportantPlaybackMode != mode)
            {
                ImportantPlaybackMode = mode;
                Debug.WriteLine($"[AlertsCoordinator] Режим принудительно установлен: {mode}");
            }
        }

        public void UpdateImportantQueueCount(int count)
        {
            _settingsService.UpdateImportantQueueCount(count, IsAutoSwitchingEnabled, ImportantPlaybackMode,
                (c, mode) =>
                {
                    ImportantQueueCount = c;
                    ImportantPlaybackMode = mode;
                });
        }

        public void SavePosition()
        {
            _overlayManager.SaveAllPositions(_settings);
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ С НАСТРОЙКАМИ
        // ============================================================
        partial void OnImportantPlaybackModeChanged(ImportantPlaybackMode value)
            => _settingsService.SetImportantPlaybackMode(value);

        partial void OnImportantPlaybackHotkeyChanged(string value)
            => _settingsService.SetImportantPlaybackHotkey(value);

        partial void OnIsAutoSwitchingEnabledChanged(bool value)
            => _settingsService.SetIsAutoSwitchingEnabled(value, ImportantQueueCount, ImportantPlaybackMode);

        // ============================================================
        // ОБРАБОТЧИКИ СОБЫТИЙ ALERTS SERVICE
        // ============================================================
        private void OnAlertReceived(object? sender, IncomingAlert alert)
        {
            if (alert == null) return;

            Debug.WriteLine($"[AlertsCoordinator] Получен алерт: [{alert.ProviderType}] {alert.DisplayText}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlayManager.ShowAlert(alert);
            });

            if (_webServer != null)
            {
                try
                {
                    string providerName = alert.ProviderType switch
                    {
                        AlertProviderType.DonationAlerts => "DonationAlerts",
                        AlertProviderType.DonationPay => "DonationPay",
                        _ => "Alert"
                    };

                    string displayAmount = alert.Type switch
                    {
                        AlertType.Donation => $"{alert.Amount:F0} {alert.Currency}",
                        AlertType.Subscription => "Подписка",
                        AlertType.Follow => "Фолловер",
                        _ => alert.Type.ToString()
                    };

                    _webServer.SendAlertToWeb(
                        userName: alert.UserName,
                        message: alert.Message,
                        displayAmount: displayAmount,
                        providerType: alert.ProviderType.ToString().ToLower(),
                        providerName: providerName,
                        durationSeconds: _settings.AlertsAlertDuration
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AlertsCoordinator] Ошибка отправки алерта в веб: {ex.Message}");
                }
            }
        }

        private void OnAlertStatusChanged(object? sender, AlertStatus status)
        {
            Debug.WriteLine($"[AlertsCoordinator] Статус алертов: {status}");
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private async Task PlayNextImportant()
        {
            if (ImportantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                await _overlayManager.PlayNextFromQueueAsync();
                UpdateImportantQueueCount(_overlayManager.QueueSize);
            }
        }

        [RelayCommand]
        private void ToggleAlertsOverlay()
        {
            _settings.AlertsOverlayVisible = !_settings.AlertsOverlayVisible;
            _overlayManager.SetAlertsVisible(_settings.AlertsOverlayVisible);
            ConfigService.Save(_settings);
            Debug.WriteLine($"[AlertsCoordinator] AlertsOverlayVisible: {_settings.AlertsOverlayVisible}");
        }

        [RelayCommand]
        private void OpenAlertsWebOverlay()
        {
            try
            {
                string url = $"http://localhost:{_settings.NetworkPort}/alerts";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Debug.WriteLine($"[AlertsCoordinator] Открыт веб-оверлей алертов: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AlertsCoordinator] Ошибка открытия веб-оверлея: {ex.Message}");
                MessageBox.Show($"Не удалось открыть браузер: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task OpenAlertsSettings()
        {
            try
            {
                var window = new SmithForge.Features.AlertsSettings.AlertsSettingsWindow(_settings)
                {
                    Owner = Application.Current.MainWindow
                };

                var result = window.ShowDialog();

                if (result == true)
                {
                    Debug.WriteLine("[AlertsCoordinator] Настройки алертов сохранены, перезапускаем AlertsService...");

                    await _alertsService.StartAsync(_settings);

                    _overlayManager.SetAlertsVisible(_settings.AlertsOverlayVisible);
                    _overlayManager.SetAlertsDuration(_settings.AlertsAlertDuration);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AlertsCoordinator] Ошибка открытия настроек алертов: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}