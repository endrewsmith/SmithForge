using SmithForge.Main.Models;
using SmithForge.Features.ImportantOverlay;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public class ImportantOverlayService
    {
        private ImportantOverlayWindow? _window;
        private ImportantOverlayViewModel? _viewModel;
        private bool _isInitialized = false;

        private Queue<(Chater user, CommonMessage msg)> _messageQueue = new();
        private bool _isShowingMessage = false;

        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;
        private double _savedWidth;
        private double _savedHeight;

        public void Initialize(double top, double left, bool isSetupMode)
        {
            _viewModel = new ImportantOverlayViewModel();
            _window = new ImportantOverlayWindow
            {
                DataContext = _viewModel,
                Top = top,
                Left = left,
                // Убираем принудительный Collapsed
            };

            _window.Show(); // Показываем сразу
            SetMode(isSetupMode);
            _isInitialized = true;
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;
            _window.Top = settings.ImportantOverlayTop;
            _window.Left = settings.ImportantOverlayLeft;

            // Теперь окно слушается настроек видимости, как и остальные
            _window.Visibility = settings.ImportantOverlayVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;

            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);

            System.Diagnostics.Debug.WriteLine($"[ImportantService] Режим настройки: {isSetupMode}");
        }

        public void SetHidden(bool isHidden)
        {
            if (_window == null) return;

            if (isHidden && !_isHidden)
            {
                _savedTop = _window.Top;
                _savedLeft = _window.Left;
                _savedWidth = _window.Width;
                _savedHeight = _window.Height;

                _window.Top = 0;
                _window.Left = 0;
                _window.Width = 10;
                _window.Height = 10;
                _window.Opacity = 0.01;

                _isHidden = true;
                System.Diagnostics.Debug.WriteLine("[ImportantService] Окно спрятано");
            }
            else if (!isHidden && _isHidden)
            {
                _window.Width = _savedWidth;
                _window.Height = _savedHeight;
                _window.Opacity = 1.0;
                _window.Top = _savedTop;
                _window.Left = _savedLeft;

                _isHidden = false;
                System.Diagnostics.Debug.WriteLine("[ImportantService] Окно показано");
            }
        }

        public void ShowImportantMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null) return;

            lock (_messageQueue)
            {
                _messageQueue.Enqueue((user, msg));
                System.Diagnostics.Debug.WriteLine($"[ImportantService] Сообщение добавлено в очередь. В очереди: {_messageQueue.Count}");
            }

            // Запускаем обработку очереди если она не запущена
            if (!_isShowingMessage)
            {
                _isShowingMessage = true;
                Task.Run(() => ProcessQueue());
            }
        }

        private async void ProcessQueue()
        {
            while (true)
            {
                (Chater user, CommonMessage msg) currentMessage = (null, null);
                bool hasMessage = false;

                lock (_messageQueue)
                {
                    if (_messageQueue.Count == 0)
                    {
                        _isShowingMessage = false;
                        hasMessage = false;
                    }
                    else
                    {
                        currentMessage = _messageQueue.Dequeue();
                        hasMessage = true;
                    }
                }

                if (!hasMessage) return; // Просто выходим, окно НЕ трогаем

                await ShowMessageAsync(currentMessage.user, currentMessage.msg);
            }
        }

        private async Task ShowMessageAsync(Chater user, CommonMessage msg)
        {
            try
            {
                // 1. Показываем в окне
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ShowImportantMessage(user, msg);
                });

                // 2. Озвучиваем и ЖДЕМ завершения речи (Task)
                // Мы вызываем VoiceService.SayAsync, который вернет задачу
                await VoiceService.SayAsync(msg.Message);

                // 3. Дополнительная пауза, чтобы зритель успел дочитать после озвучки
                await Task.Delay(2000);

                // 4. Очищаем окно перед следующим сообщением
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ClearMessage();
                });

                // Короткая пауза между алертами
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImportantService] Ошибка: {ex.Message}");
            }
        }


        public bool IsVisible => _window?.Visibility == Visibility.Visible;

        public void SavePosition(AppSettings settings)
        {
            if (_window == null) return;

            settings.ImportantOverlayTop = _isHidden ? _savedTop : _window.Top;
            settings.ImportantOverlayLeft = _isHidden ? _savedLeft : _window.Left;
            settings.ImportantOverlayWidth = _window.Width;
            settings.ImportantOverlayHeight = _window.Height;
            settings.ImportantOverlayVisible = _window.Visibility == Visibility.Visible;

            System.Diagnostics.Debug.WriteLine($"[ImportantService] Позиция сохранена");
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
    }
}