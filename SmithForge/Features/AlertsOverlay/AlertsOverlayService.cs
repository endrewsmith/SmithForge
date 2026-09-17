using SmithForge.AlertsEngine.Core.Models;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Windows;

namespace SmithForge.Features.AlertsOverlay
{
    /// <summary>
    /// Сервис управления окном оверлея алертов.
    /// По аналогии с ChatOverlayService, ImportantOverlayService и др.
    /// </summary>
    public class AlertsOverlayService
    {
        private AlertsOverlayWindow? _window;
        private AlertsOverlayViewModel? _viewModel;
        private bool _isInitialized;
        private bool _isHidden;
        private double _savedTop;
        private double _savedLeft;

        public AlertsOverlayService()
        {
            CreateOverlay();
        }

        /// <summary>
        /// Создать окно оверлея (скрытое, "в тени")
        /// </summary>
        private void CreateOverlay()
        {
            if (_window != null) return;

            _viewModel = new AlertsOverlayViewModel();
            _window = new AlertsOverlayWindow
            {
                DataContext = _viewModel,
                Visibility = Visibility.Collapsed
            };

            _window.Show();
            _window.Hide();

            _isInitialized = true;

            Debug.WriteLine("[Alerts OverlayService] Окно создано");
        }

        /// <summary>
        /// Инициализация с параметрами (позиция, размер, режим настройки)
        /// </summary>
        public void Initialize(double top, double left, double width, double height, bool isSetupMode)
        {
            if (!_isInitialized || _window == null) return;

            _window.Top = top;
            _window.Left = left;
            _window.Width = width;
            _window.Height = height;

            SetSetupMode(isSetupMode);

            Debug.WriteLine($"[Alerts OverlayService] Инициализирован: {top},{left} {width}x{height}, setup={isSetupMode}");
        }

        /// <summary>
        /// Включить / выключить режим настройки (перетаскивание, клики)
        /// </summary>
        public void SetSetupMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);

            Debug.WriteLine($"[Alerts OverlayService] SetupMode: {isSetupMode}");
        }

        /// <summary>
        /// Установить длительность показа алерта (в секундах)
        /// </summary>
        public void SetAlertDuration(int seconds)
        {
            _viewModel?.SetAlertDuration(seconds);
        }

        /// <summary>
        /// Показать алерт от любого провайдера
        /// </summary>
        public void ShowAlert(IncomingAlert alert)
        {
            if (!_isInitialized || _viewModel == null)
            {
                Debug.WriteLine("[Alerts OverlayService] Невозможно показать: сервис не инициализирован");
                return;
            }

            if (alert == null)
            {
                Debug.WriteLine("[Alerts OverlayService] Алерт = null, пропускаем");
                return;
            }

            Debug.WriteLine($"[Alerts OverlayService] ShowAlert: {alert.DisplayText}");
            _viewModel.ShowAlert(alert);
        }

        /// <summary>
        /// Спрятать окно за экран / вернуть обратно
        /// </summary>
        public void SetHidden(bool isHidden)
        {
            if (_window == null) return;

            if (isHidden && !_isHidden)
            {
                _savedTop = _window.Top;
                _savedLeft = _window.Left;

                _window.Top = 1 - _window.Height;
                _window.Left = 1 - _window.Width;

                _isHidden = true;
                Debug.WriteLine("[Alerts OverlayService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
                Debug.WriteLine("[Alerts OverlayService] Окно возвращено");
            }
        }

        /// <summary>
        /// Показать / скрыть окно оверлея
        /// </summary>
        public void SetVisible(bool isVisible)
        {
            if (_window == null) return;

            _window.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (isVisible) _window.Topmost = true;

            Debug.WriteLine($"[Alerts OverlayService] Visible: {isVisible}");
        }

        public bool IsVisible => _window?.Visibility == Visibility.Visible;

        /// <summary>
        /// Сохранить позицию и размер в настройки
        /// </summary>
        public void SavePosition(AppSettings settings)
        {
            if (_window == null) return;

            settings.AlertsOverlayTop = _isHidden ? _savedTop : _window.Top;
            settings.AlertsOverlayLeft = _isHidden ? _savedLeft : _window.Left;
            settings.AlertsOverlayWidth = _window.Width;
            settings.AlertsOverlayHeight = _window.Height;
            settings.AlertsOverlayVisible = _window.Visibility == Visibility.Visible;

            Debug.WriteLine("[Alerts OverlayService] Позиция сохранена");
        }

        /// <summary>
        /// Загрузить позицию и размер из настроек
        /// </summary>
        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;

            _window.Top = settings.AlertsOverlayTop;
            _window.Left = settings.AlertsOverlayLeft;
            _window.Width = settings.AlertsOverlayWidth;
            _window.Height = settings.AlertsOverlayHeight;

            if (settings.AlertsOverlayVisible)
            {
                _window.Visibility = Visibility.Visible;
            }

            Debug.WriteLine("[Alerts OverlayService] Позиция загружена");
        }

        /// <summary>
        /// Очистить очередь и скрыть текущий алерт
        /// </summary>
        public void ClearAll()
        {
            _viewModel?.ClearAll();
        }
    }
}