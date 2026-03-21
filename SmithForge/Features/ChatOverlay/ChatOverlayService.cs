using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Features.ChatOverlay;
using System;
using System.Windows;

namespace SmithForge.Features.ChatOverlay
{
    public class ChatOverlayService
    {
        private ChatOverlayWindow? _overlayWindow;
        private ChatOverlayViewModel? _viewModel;
        private bool _isInitialized = false;
        private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        // Поля для скрытия за экран
        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;

        public ChatOverlayService()
        {
            CreateOverlay();
        }

        private void CreateOverlay()
        {
            if (_overlayWindow == null)
            {
                _viewModel = new ChatOverlayViewModel();
                _overlayWindow = new ChatOverlayWindow
                {
                    DataContext = _viewModel,
                    Visibility = Visibility.Collapsed
                };

                // ВАЖНО: передаем окно в ViewModel
                _viewModel.SetWindow(_overlayWindow);

                _overlayWindow.Show();
                _overlayWindow.Hide();

                _isInitialized = true;

                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно создано");
            }
        }

        public void Initialize(double top, double left)
        {
            if (!_isInitialized || _overlayWindow == null) return;

            _overlayWindow.Top = top;
            _overlayWindow.Left = left;

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Инициализирован, позиция: {top}, {left}");
        }

        public void SetSetupMode(bool isSetupMode)
        {
            if (_overlayWindow == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _overlayWindow.SetClickThrough(!isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Режим настройки: {isSetupMode}");
        }

        public void SetDisplayMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            if (_viewModel != null)
            {
                _viewModel.SetMode(mode);
            }
            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Установлен режим отображения: {mode}");
        }

        public void SetHidden(bool isHidden)
        {
            if (_overlayWindow == null) return;

            if (isHidden && !_isHidden)
            {
                _savedTop = _overlayWindow.Top;
                _savedLeft = _overlayWindow.Left;

                _overlayWindow.Top = 1 - _overlayWindow.Height;
                _overlayWindow.Left = 1 - _overlayWindow.Width;

                _isHidden = true;
                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                _overlayWindow.Top = _savedTop;
                _overlayWindow.Left = _savedLeft;
                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно возвращено на позицию");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Невозможно добавить сообщение: сервис не инициализирован");
                return;
            }

            _viewModel.AddMessage(user, msg);
        }

        public void Show()
        {
            if (_overlayWindow == null) return;
            _overlayWindow.Visibility = Visibility.Visible;
            _overlayWindow.Topmost = true;
            System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно показано");
        }

        public void Hide()
        {
            if (_overlayWindow == null) return;
            _overlayWindow.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно скрыто");
        }

        public void Toggle()
        {
            if (_overlayWindow == null) return;

            if (_overlayWindow.Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }

        public bool IsVisible => _overlayWindow?.Visibility == Visibility.Visible;

        public void SavePosition(AppSettings settings)
        {
            if (_overlayWindow == null) return;

            settings.OverlayTop = _isHidden ? _savedTop : _overlayWindow.Top;
            settings.OverlayLeft = _isHidden ? _savedLeft : _overlayWindow.Left;
            settings.OverlayWidth = _overlayWindow.Width;
            settings.OverlayHeight = _overlayWindow.Height;
            settings.OverlayVisible = _overlayWindow.Visibility == Visibility.Visible;
            settings.MainChatMode = _currentMode;

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Позиция сохранена, режим: {_currentMode}");
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_overlayWindow == null) return;

            _overlayWindow.Top = settings.OverlayTop;
            _overlayWindow.Left = settings.OverlayLeft;
            _overlayWindow.Width = settings.OverlayWidth;
            _overlayWindow.Height = settings.OverlayHeight;

            SetDisplayMode(settings.MainChatMode);

            if (settings.OverlayVisible)
            {
                _overlayWindow.Visibility = Visibility.Visible;
            }

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Позиция загружена, режим: {_currentMode}");
        }
    }
}