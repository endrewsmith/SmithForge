using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Features.StickersOverlay;
using System;
using System.Windows;

namespace SmithForge.Main.Services
{
    public class StickersOverlayService
    {
        private StickersOverlayWindow? _window;
        private StickersOverlayViewModel? _viewModel;
        private bool _isInitialized = false;
        private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        // Поля для скрытия за экран
        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;

        public StickersOverlayService()
        {
            CreateOverlay();
        }

        private void CreateOverlay()
        {
            if (_window == null)
            {
                _viewModel = new StickersOverlayViewModel();
                _window = new StickersOverlayWindow
                {
                    DataContext = _viewModel,
                    Visibility = Visibility.Collapsed
                };

                _window.Show();
                _window.Hide();

                _isInitialized = true;

                System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Окно создано");
            }
        }

        public void Initialize(double top, double left, double width, double height, bool isSetupMode)
        {
            if (!_isInitialized || _window == null) return;

            _window.Top = top;
            _window.Left = left;
            _window.Width = width;
            _window.Height = height;

            SetSetupMode(isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[StickersOverlayService] Инициализирован, режим настройки: {isSetupMode}");
        }

        public void SetSetupMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[StickersOverlayService] Режим настройки: {isSetupMode}");
        }

        public void SetDisplayMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            if (_viewModel != null)
            {
                _viewModel.SetMode(mode);
            }
            System.Diagnostics.Debug.WriteLine($"[StickersOverlayService] Установлен режим отображения: {mode}");
        }

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
                System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Окно возвращено на позицию");
            }
        }

        public void ShowSticker(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Невозможно показать стикер: сервис не инициализирован");
                return;
            }

            _viewModel.ShowSticker(user, msg);
        }

        public void Show()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
            System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Окно показано");
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[StickersOverlayService] Окно скрыто");
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

            settings.StickersWindowTop = _isHidden ? _savedTop : _window.Top;
            settings.StickersWindowLeft = _isHidden ? _savedLeft : _window.Left;
            settings.StickersWindowWidth = _window.Width;
            settings.StickersWindowHeight = _window.Height;
            settings.StickersWindowVisible = _window.Visibility == Visibility.Visible;
            settings.StickersChatMode = _currentMode;

            System.Diagnostics.Debug.WriteLine($"[StickersOverlayService] Позиция сохранена, режим: {_currentMode}");
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;

            _window.Top = settings.StickersWindowTop;
            _window.Left = settings.StickersWindowLeft;
            _window.Width = settings.StickersWindowWidth;
            _window.Height = settings.StickersWindowHeight;

            SetDisplayMode(settings.StickersChatMode);

            if (settings.StickersWindowVisible)
            {
                _window.Visibility = Visibility.Visible;
            }

            System.Diagnostics.Debug.WriteLine($"[StickersOverlayService] Позиция загружена, режим: {_currentMode}");
        }

        public void SetDisplayTime(int milliseconds)
        {
            if (_viewModel != null)
            {
                _viewModel.SetDisplayTime(milliseconds);
            }
        }
    }
}