using SmithForge.Main.Models;
using System;
using System.Windows;

namespace SmithForge.Features.ChatOverlayShorts
{
    public class ChatOverlayShortsService
    {
        private ChatOverlayShortsWindow? _window;
        private ChatOverlayShortsViewModel? _viewModel;
        private bool _isInitialized = false;

        // Поля для скрытия за экран
        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;

        public void Initialize(double top, double left, bool isSetupMode)
        {
            if (_isInitialized) return;

            try
            {
                _viewModel = new ChatOverlayShortsViewModel();
                _window = new ChatOverlayShortsWindow
                {
                    DataContext = _viewModel,
                    Top = top,
                    Left = left,
                    Visibility = Visibility.Collapsed
                };

                _window.Show();
                _window.Hide();

                SetMode(isSetupMode);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"[ShortsService] Инициализирован, режим настройки: {isSetupMode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortsService] Ошибка: {ex.Message}");
            }
        }

        public void SetMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[ShortsService] Режим настройки: {isSetupMode}");
        }

        // МЕТОД ДЛЯ СКРЫТИЯ ЗА ЭКРАН
        public void SetHidden(bool isHidden)
        {
            if (_window == null) return;

            if (isHidden && !_isHidden)
            {
                // Сохраняем текущую позицию
                _savedTop = _window.Top;
                _savedLeft = _window.Left;

                // Выносим за экран (окно остается видимым для OBS)
                _window.Top = -2000;
                _window.Left = -2000;
                _isHidden = true;
                System.Diagnostics.Debug.WriteLine("[ShortsService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                // Возвращаем на сохраненную позицию
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[ShortsService] Окно возвращено на позицию");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null) return;
            _viewModel.AddMessage(user, msg);
        }

        public void Show()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
            System.Diagnostics.Debug.WriteLine("[ShortsService] Окно показано");
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[ShortsService] Окно скрыто");
        }

        public void Toggle()
        {
            if (_window == null) return;

            if (_window.Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }

        public bool IsVisible => _window?.Visibility == Visibility.Visible;

        public void SavePosition(AppSettings settings)
        {
            if (_window == null) return;

            // Сохраняем реальную позицию (не спрятанную)
            settings.ShortsWindowTop = _isHidden ? _savedTop : _window.Top;
            settings.ShortsWindowLeft = _isHidden ? _savedLeft : _window.Left;
            settings.ShortsWindowWidth = _window.Width;
            settings.ShortsWindowHeight = _window.Height;
            settings.ShortsWindowVisible = _window.Visibility == Visibility.Visible;

            System.Diagnostics.Debug.WriteLine($"[ShortsService] Позиция сохранена");
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;

            _window.Top = settings.ShortsWindowTop;
            _window.Left = settings.ShortsWindowLeft;
            _window.Width = settings.ShortsWindowWidth;
            _window.Height = settings.ShortsWindowHeight;

            if (settings.ShortsWindowVisible)
            {
                _window.Visibility = Visibility.Visible;
            }

            System.Diagnostics.Debug.WriteLine($"[ShortsService] Позиция загружена");
        }
    }
}