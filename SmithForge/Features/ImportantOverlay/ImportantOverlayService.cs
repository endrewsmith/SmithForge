using SmithForge.Features.ImportantOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using static Dapper.SqlMapper;

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

        private readonly AppSettings _settings;

        public ImportantOverlayService(AppSettings settings)
        {
            _settings = settings;
        }

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

        public void ShowImportantMessage(Chater chater, CommonMessage message)
        {
            string importantText = $"Важное сообщение от {chater.EffectiveName}: {message.Message}";

            // Получаем актуальные настройки
            var settings = ConfigService.Load();

            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Текущий режим: {settings.ImportantPlaybackMode}");
            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Auto = {ImportantPlaybackMode.Auto}, Manual = {ImportantPlaybackMode.Manual}");

            // Проверяем режим воспроизведения
            if (settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto)
            {
                System.Diagnostics.Debug.WriteLine("[ImportantOverlay] РЕЖИМ: АВТОМАТИЧЕСКИЙ");
                _ = Task.Run(async () =>
                {
                    await VoiceService.PlayImportantSoundAsync();
                    await VoiceService.SayAsync(importantText);
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ImportantOverlay] РЕЖИМ: РУЧНОЙ");

                // Добавляем в очередь (это потокобезопасно)
                ImportantQueueService.Enqueue(importantText);

                // Обновляем счетчик в UI потоке
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainViewModel = Application.Current.MainWindow?.DataContext as MainViewModel;
                    mainViewModel?.UpdateImportantQueueCount(ImportantQueueService.QueueCount);
                });
            }

            // Отображаем сообщение в оверлее - обязательно в UI потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel?.ShowMessage(chater, message);
            });
        }
        private void AddMessageToOverlay(Chater chater, CommonMessage message)
        {
            // Используйте существующий метод для отображения сообщения
            // Например, если у вас есть метод ShowMessage или AddMessage:

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Вариант 1: Если есть ObservableCollection
                // Messages.Add(message);

                // Вариант 2: Если есть метод для добавления сообщения
                // AddMessage(message);

                // Вариант 3: Вызвать событие
                // MessageReceived?.Invoke(chater, message);

                // Если ничего из этого не подходит, просто покажите сообщение через Debug
                System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Показ сообщения: {chater.EffectiveName}: {message.Message}");
            });
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

                // Показываем текст в ViewModel
                await Application.Current.Dispatcher.InvokeAsync(() => _viewModel?.ShowMessage(item.user, item.msg));

                // Ждем время отображения (управляется в ViewModel)
                // Не очищаем сообщение здесь - это делает ViewModel после таймера

                // Небольшая пауза между сообщениями
                await Task.Delay(300);
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