using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Features.ImportantOverlay
{
    public class ImportantOverlayService
    {
        private bool _isPlaying = false; // флаг, что идет воспроизведение

        public bool IsPlaying => _isPlaying || _isAutoPlaying || _isPlayingManual;
        private bool _isAutoPlaying = false;
        private bool _isPlayingManual = false;
        private ImportantOverlayWindow? _window;
        private ImportantOverlayViewModel? _viewModel;
        private bool _isInitialized = false;
        private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;
        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;
        private readonly AppSettings _settings;

        private readonly Queue<(Chater chater, CommonMessage message, string text)> _messageQueue = new();
        private bool _isProcessing = false;
        private readonly object _queueLock = new object();

        // Событие для обновления счетчика (без прямого UI вызова)
        public event EventHandler<int>? QueueCountChanged;
        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;
        public int QueueSize { get { lock (_queueLock) return _messageQueue.Count; } }

        public ImportantOverlayService(AppSettings settings)
        {
            _settings = settings;
        }

        public void Initialize(double top, double left, double width, double height, bool isSetupMode)
        {
            if (_isInitialized) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
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

                SetSetupMode(isSetupMode);
                SetDisplayMode(_currentMode);
                _isInitialized = true;
                Debug.WriteLine("[ImportantService] Инициализация завершена");
            });
        }

        public void SetSetupMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;
            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);
        }

        public void SetDisplayMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            _viewModel?.SetMode(mode);
        }
        private bool _isAutoSwitchingEnabled = false;

        public bool IsAutoSwitchingEnabled
        {
            get => _isAutoSwitchingEnabled;
            set
            {
                _isAutoSwitchingEnabled = value;
                Debug.WriteLine($"[ImportantService] IsAutoSwitchingEnabled = {value}");
            }
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
            }
            else if (!isHidden && _isHidden)
            {
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
            }
        }

        public void ShowImportantMessage(Chater chater, CommonMessage message)
        {
            string importantText = $"Сообщение от {chater.EffectiveName}: {message.Message}";
            var settings = ConfigService.Load();

            Debug.WriteLine($"[ImportantOverlay] Режим: {(settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto ? "АВТО" : "РУЧНОЙ")}");
            Debug.WriteLine($"[ImportantOverlay] Текст: {importantText}");
            Debug.WriteLine($"[ImportantOverlay] IsAutoSwitchingEnabled: {_isAutoSwitchingEnabled}");
            Debug.WriteLine($"[ImportantOverlay] QueueSize до добавления: {_messageQueue.Count}");
            Debug.WriteLine($"[ImportantOverlay] IsPlaying: {_isPlaying}");

            // Добавляем в очередь
            int newCount;
            lock (_queueLock)
            {
                _messageQueue.Enqueue((chater, message, importantText));
                newCount = _messageQueue.Count;
                Debug.WriteLine($"[Queue] Добавлено. Всего: {newCount}");
            }

            // Вызываем событие обновления счетчика
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    QueueCountChanged?.Invoke(this, newCount);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImportantOverlay] Ошибка в QueueCountChanged: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);

            // Логика воспроизведения
            bool shouldPlay = false;

            if (!_isAutoSwitchingEnabled)
            {
                shouldPlay = settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto && !_isAutoPlaying;
                Debug.WriteLine($"[ImportantOverlay] Режим чтения ВЫКЛ, shouldPlay={shouldPlay}");
            }
            else
            {
                // Если уже идет воспроизведение И пришло новое сообщение
                if (_isPlaying && newCount > 1)
                {
                    Debug.WriteLine("[ImportantOverlay] Идет воспроизведение, пришло новое сообщение - переключаем на ручной режим");
                    shouldPlay = false;

                    // Переключаем режим на ручной
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                        mainVm?.SetImportantPlaybackMode(ImportantPlaybackMode.Manual);
                    }));

                    // Останавливаем текущее авто-воспроизведение
                    _isAutoPlaying = false;
                }
                // Если очередь пуста (была 0, стало 1) - воспроизводим
                else if (newCount == 1 && !_isPlaying)
                {
                    shouldPlay = settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto && !_isAutoPlaying;
                    Debug.WriteLine($"[ImportantOverlay] Режим чтения ВКЛ, первое сообщение, shouldPlay={shouldPlay}");
                }
                else
                {
                    shouldPlay = false;
                    Debug.WriteLine($"[ImportantOverlay] Режим чтения ВКЛ, newCount={newCount}, shouldPlay=False (накопление)");
                }
            }

            if (shouldPlay)
            {
                Debug.WriteLine("[ImportantOverlay] Запускаем авто-воспроизведение");
                _ = Task.Run(async () => await ProcessAutoQueueAsync());
            }
            else
            {
                Debug.WriteLine("[ImportantOverlay] Не запускаем авто-воспроизведение");
            }
        }
        private async Task ProcessAutoQueueAsync()
        {
            try
            {
                lock (_queueLock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }

                _isAutoPlaying = true;

                while (true)
                {
                    // Проверяем режим
                    var settings = ConfigService.Load();
                    if (_isAutoSwitchingEnabled && settings.ImportantPlaybackMode != ImportantPlaybackMode.Auto)
                    {
                        Debug.WriteLine("[ProcessAutoQueue] Режим изменился на ручной, останавливаем");
                        break;
                    }

                    (Chater chater, CommonMessage message, string text) item;
                    lock (_queueLock)
                    {
                        if (_messageQueue.Count == 0) break;
                        item = _messageQueue.Peek();
                    }

                    await ShowAndSpeakAsync(item.chater, item.message, item.text);

                    int newCount;
                    lock (_queueLock)
                    {
                        _messageQueue.Dequeue();
                        newCount = _messageQueue.Count;
                        Debug.WriteLine($"[Queue] Воспроизведено. Осталось: {newCount}");
                    }

                    // Вызываем событие обновления счетчика
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        QueueCountChanged?.Invoke(this, newCount);
                    }), System.Windows.Threading.DispatcherPriority.Normal);

                    // Если режим чтения включен, останавливаемся после воспроизведения одного сообщения
                    if (_isAutoSwitchingEnabled)
                    {
                        Debug.WriteLine("[ProcessAutoQueue] Режим чтения ВКЛ, останавливаем авто-воспроизведение после одного сообщения");
                        break;
                    }

                    // Если очередь пуста, выходим
                    if (newCount == 0)
                    {
                        Debug.WriteLine("[ProcessAutoQueue] Очередь пуста, останавливаем");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessAutoQueue] ОШИБКА: {ex.Message}");
            }
            finally
            {
                _isAutoPlaying = false;
                lock (_queueLock) { _isProcessing = false; }
            }
        }
        private async Task ShowAndSpeakAsync(Chater chater, CommonMessage message, string text)
        {
            try
            {
                _isPlaying = true;
                Debug.WriteLine($"[ShowAndSpeak] Начинаем: {text}");

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ShowMessage(chater, message);
                });

                await Task.Delay(200);
                await VoiceService.SayAsync(text);
                await Task.Delay(800);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ClearMessages();
                });

                Debug.WriteLine("[ShowAndSpeak] Сообщение отработано");

                // После воспроизведения проверяем очередь
                int remainingCount;
                lock (_queueLock)
                {
                    remainingCount = _messageQueue.Count;
                }

                Debug.WriteLine($"[ShowAndSpeak] После воспроизведения, осталось в очереди: {remainingCount}");

                // Если очередь пуста, вызываем событие для переключения режима
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    QueueCountChanged?.Invoke(this, remainingCount);
                    Debug.WriteLine($"[ShowAndSpeak] QueueCountChanged вызван с count={remainingCount}");

                    // ✅ Принудительная синхронизация, если очередь пуста и режим ручной
                    if (remainingCount == 0)
                    {
                        var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                        if (mainVm != null && mainVm.ImportantPlaybackMode == ImportantPlaybackMode.Manual && mainVm.IsAutoSwitchingEnabled)
                        {
                            Debug.WriteLine("[ShowAndSpeak] Принудительное переключение режима на Auto");
                            mainVm.ImportantPlaybackMode = ImportantPlaybackMode.Auto;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShowAndSpeak Error] {ex.Message}");
            }
            finally
            {
                _isPlaying = false;
            }
        }
        public async Task PlayNextFromQueueAsync()
        {
            if (_isPlayingManual)
            {
                Debug.WriteLine("[ManualQueue] Уже воспроизводится");
                return;
            }

            (Chater chater, CommonMessage message, string text) item;
            lock (_queueLock)
            {
                if (_messageQueue.Count == 0)
                {
                    Debug.WriteLine("[ManualQueue] Очередь пуста");
                    return;
                }
                item = _messageQueue.Dequeue();
            }

            _isPlayingManual = true;

            try
            {
                QueueCountChanged?.Invoke(this, _messageQueue.Count);
                await ShowAndSpeakAsync(item.chater, item.message, item.text);
                Debug.WriteLine("[ManualQueue] Воспроизведение завершено");
            }
            finally
            {
                _isPlayingManual = false;
            }
        }

        public void ClearQueue()
        {
            int newCount;
            lock (_queueLock)
            {
                _messageQueue.Clear();
                newCount = 0;
                Debug.WriteLine("[Queue] Очередь очищена");
            }

            // Вызываем событие в UI потоке
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                QueueCountChanged?.Invoke(this, newCount);
                Debug.WriteLine($"[ImportantOverlay] QueueCountChanged вызван после очистки, новый счетчик: {newCount}");
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
        public void SavePosition(AppSettings settings)
        {
            if (_window == null) return;
            settings.ImportantOverlayTop = _isHidden ? _savedTop : _window.Top;
            settings.ImportantOverlayLeft = _isHidden ? _savedLeft : _window.Left;
            settings.ImportantOverlayWidth = _window.Width;
            settings.ImportantOverlayHeight = _window.Height;
            settings.ImportantChatMode = _currentMode;
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;
            _window.Top = settings.ImportantOverlayTop;
            _window.Left = settings.ImportantOverlayLeft;
            _window.Width = settings.ImportantOverlayWidth;
            _window.Height = settings.ImportantOverlayHeight;
            SetDisplayMode(settings.ImportantChatMode);
        }

        public void SetAutoDisplay(bool isAuto)
        {
            if (_viewModel != null)
            {
                _viewModel.IsAutoDisplay = isAuto;
                Debug.WriteLine($"[ImportantService] AutoDisplay: {isAuto}");
            }
        }

        public void Show() { if (_window != null) _window.Visibility = Visibility.Visible; }
        public void Hide() { if (_window != null) _window.Visibility = Visibility.Collapsed; }
        public void Toggle() { if (_window != null) _window.Visibility = _window.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
    }
}