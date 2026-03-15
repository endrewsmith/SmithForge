using SmithForge.Main.Models;
using SmithForge.Features.ChatOverlay;
using System;
using System.Windows;

namespace SmithForge.Main.Services
{
    public class ChatOverlayService
    {
        private ChatOverlayWindow? _overlayWindow;
        private ChatOverlayViewModel? _viewModel;
        private bool _isInitialized = false;

        // Новые поля для скрытия за экран
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

                _overlayWindow.Show();
                _overlayWindow.Hide();

                _isInitialized = true;
            }
        }

        public void Initialize(double top, double left, bool isSetupMode)
        {
            if (!_isInitialized || _overlayWindow == null) return;

            _overlayWindow.Top = top;
            _overlayWindow.Left = left;

            SetMode(isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Инициализирован, режим настройки: {isSetupMode}");
        }

        public void SetMode(bool isSetupMode)
        {
            if (_overlayWindow == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _overlayWindow.SetClickThrough(!isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Режим настройки: {isSetupMode}");
        }

        // НОВЫЙ МЕТОД для скрытия за экран
        public void SetHidden(bool isHidden)
        {
            if (_overlayWindow == null) return;

            if (isHidden && !_isHidden)
            {
                // Сохраняем текущую позицию
                _savedTop = _overlayWindow.Top;
                _savedLeft = _overlayWindow.Left;

                // Оставляем окно на месте, но делаем его полностью прозрачным
                //_overlayWindow.Opacity = 0.01; // Почти прозрачно, но окно существует
                //_overlayWindow.Topmost = false; // Убираем поверх всех
                                                // Выносим за экран (окно остается видимым для OBS)
                _overlayWindow.Top = 1 - _overlayWindow.Height; 
                _overlayWindow.Left = 1 - _overlayWindow.Width;  

                // ВАЖНО: окно должно оставаться Visible
                //_overlayWindow.Visibility = Visibility.Visible;

                _isHidden = true;
                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                // Возвращаем видимость
                //_overlayWindow.Opacity = 1.0;
                //_overlayWindow.Topmost = true;
                // Возвращаем на сохраненную позицию
                _overlayWindow.Top = _savedTop;
                _overlayWindow.Left = _savedLeft;
                //_overlayWindow.Visibility = Visibility.Visible;
                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[ChatOverlayService] Окно возвращено на позицию");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null) return;
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

            // Сохраняем реальную позицию (не спрятанную)
            settings.OverlayTop = _isHidden ? _savedTop : _overlayWindow.Top;
            settings.OverlayLeft = _isHidden ? _savedLeft : _overlayWindow.Left;
            settings.OverlayWidth = _overlayWindow.Width;
            settings.OverlayHeight = _overlayWindow.Height;
            settings.OverlayVisible = _overlayWindow.Visibility == Visibility.Visible;

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Позиция сохранена");
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_overlayWindow == null) return;

            _overlayWindow.Top = settings.OverlayTop;
            _overlayWindow.Left = settings.OverlayLeft;
            _overlayWindow.Width = settings.OverlayWidth;
            _overlayWindow.Height = settings.OverlayHeight;

            if (settings.OverlayVisible)
            {
                _overlayWindow.Visibility = Visibility.Visible;
            }

            System.Diagnostics.Debug.WriteLine($"[ChatOverlayService] Позиция загружена");
        }
    }
}