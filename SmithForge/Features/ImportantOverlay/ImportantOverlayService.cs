using SmithForge.Features.ImportantOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Features.ImportantOverlay
{
    public class ImportantOverlayService
    {
        private ImportantOverlayWindow? _window;
        private ImportantOverlayViewModel? _viewModel;
        private bool _isInitialized = false;
        private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;
        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;

        private Queue<(Chater user, CommonMessage msg)> _messageQueue = new();
        private bool _isShowingQueue = false;

        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;

        public void Initialize(double top, double left, double width, double height, bool isSetupMode)
        {
            if (_isInitialized) return;

            System.Diagnostics.Debug.WriteLine($"[ImportantService] ===== ИНИЦИАЛИЗАЦИЯ =====");
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Получено из settings: top={top}, left={left}, width={width}, height={height}");

            _viewModel = new ImportantOverlayViewModel();
            _window = new ImportantOverlayWindow
            {
                DataContext = _viewModel,
                Top = top,
                Left = left,
                Width = width > 0 ? width : 450,
                Height = height > 0 ? height : 600,
                Visibility = Visibility.Visible
            };

            System.Diagnostics.Debug.WriteLine($"[ImportantService] Окно создано: {_window.Width}x{_window.Height}");

            SetSetupMode(isSetupMode);
            SetDisplayMode(_currentMode);
            _isInitialized = true;
        }

        public void SetSetupMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;
            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Режим настройки: {isSetupMode}");
        }

        public void SetDisplayMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            if (_viewModel != null)
            {
                _viewModel.SetMode(mode);
            }
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Установлен режим отображения: {mode}");
        }

        public void SetHidden(bool isHidden)
        {
            if (_window == null) return;
            if (isHidden && !_isHidden)
            {
                _savedTop = _window.Top;
                _savedLeft = _window.Left;
                _window.Top = -2000;
                _window.Left = -2000;
                _isHidden = true;
                System.Diagnostics.Debug.WriteLine("[ImportantService] Окно спрятано за экран");
            }
            else if (!isHidden && _isHidden)
            {
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[ImportantService] Окно возвращено на позицию");
            }
        }

        public void ShowImportantMessage(Chater user, CommonMessage msg)
        {
            lock (_messageQueue)
            {
                _messageQueue.Enqueue((user, msg));
                System.Diagnostics.Debug.WriteLine($"[ImportantService] Добавлено в очередь: {msg.Message}");
            }
            if (!_isShowingQueue) Task.Run(() => ProcessQueue());
        }

        private async Task ProcessQueue()
        {
            _isShowingQueue = true;
            while (true)
            {
                (Chater user, CommonMessage msg) item;
                lock (_messageQueue)
                {
                    if (_messageQueue.Count == 0) break;
                    item = _messageQueue.Dequeue();
                }

                // 1. Показываем текст (используем ShowMessage, а не ShowImportantMessage)
                await Application.Current.Dispatcher.InvokeAsync(() => _viewModel?.ShowMessage(item.user, item.msg));

                // 2. Озвучиваем
                await VoiceService.SayAsync(item.msg.Message);

                // 3. Ждем, пока сообщение висит на экране
                await Task.Delay(4000);

                // 4. Очищаем сообщение
                await Application.Current.Dispatcher.InvokeAsync(() => _viewModel?.ClearMessages());

                await Task.Delay(500);
            }
            _isShowingQueue = false;
        }

        public void SavePosition(AppSettings settings)
        {
            if (_window == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ImportantService] ОШИБКА: _window = null");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ImportantService] ===== СОХРАНЕНИЕ ПОЗИЦИИ =====");
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Текущая позиция: Top={_window.Top}, Left={_window.Left}");
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Текущие размеры: Width={_window.Width}, Height={_window.Height}");

            settings.ImportantOverlayTop = _isHidden ? _savedTop : _window.Top;
            settings.ImportantOverlayLeft = _isHidden ? _savedLeft : _window.Left;
            settings.ImportantOverlayWidth = _window.Width;
            settings.ImportantOverlayHeight = _window.Height;
            settings.ImportantChatMode = _currentMode;

            System.Diagnostics.Debug.WriteLine($"[ImportantService] СОХРАНЕНО в settings: {settings.ImportantOverlayWidth}x{settings.ImportantOverlayHeight}");
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;

            System.Diagnostics.Debug.WriteLine($"[ImportantService] ===== ЗАГРУЗКА ПОЗИЦИИ =====");
            System.Diagnostics.Debug.WriteLine($"[ImportantService] До загрузки: {_window.Width}x{_window.Height}");
            System.Diagnostics.Debug.WriteLine($"[ImportantService] Из settings: width={settings.ImportantOverlayWidth}, height={settings.ImportantOverlayHeight}");

            _window.Top = settings.ImportantOverlayTop;
            _window.Left = settings.ImportantOverlayLeft;
            _window.Width = settings.ImportantOverlayWidth;
            _window.Height = settings.ImportantOverlayHeight;

            SetDisplayMode(settings.ImportantChatMode);

            System.Diagnostics.Debug.WriteLine($"[ImportantService] После загрузки: {_window.Width}x{_window.Height}");
        }

        public void Show()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
            System.Diagnostics.Debug.WriteLine("[ImportantService] Окно показано");
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[ImportantService] Окно скрыто");
        }

        public void Toggle()
        {
            if (_window == null) return;
            if (_window.Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }
    }
}