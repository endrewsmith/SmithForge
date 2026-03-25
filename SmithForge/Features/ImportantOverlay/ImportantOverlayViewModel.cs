using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private bool _isAutoDisplay = true;

        [ObservableProperty] private double _messageDisplayDelay = 800;
        [ObservableProperty] private int _maxQueueSize = 20;
        [ObservableProperty] private int _queueSize;

        [ObservableProperty] private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        private readonly Queue<DisplayMessageViewModel> _messageQueue = new();
        private Timer? _displayTimer;
        private bool _isProcessing = false;
        private readonly object _queueLock = new object();
        private readonly HashSet<DisplayMessageViewModel> _removingMessages = new();

        public ImportantOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
            StartDisplayTimer();
        }

        #region Управление режимом

        public void SetMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }
            System.Diagnostics.Debug.WriteLine($"[ImportantOverlayViewModel] Установлен режим: {mode}");
        }

        public ChatDisplayMode GetCurrentMode()
        {
            return _currentMode;
        }

        private void ApplyModeSettings(DisplayMessageViewModel msgVm)
        {
            switch (_currentMode)
            {
                case ChatDisplayMode.Compact:
                    msgVm.ShowAvatar = false;
                    msgVm.ShowRank = false;
                    msgVm.AnimationDuration = 300;
                    break;
                case ChatDisplayMode.AppearAndFade:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    msgVm.AnimationDuration = 400;
                    break;
                case ChatDisplayMode.AppearOnly:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    msgVm.AnimationDuration = 400;
                    break;
                case ChatDisplayMode.Slideshow:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    msgVm.AnimationDuration = 400;
                    break;
                default:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    break;
            }
        }

        #endregion

        #region Управление таймером

        public void StartDisplayTimer()
        {
            _displayTimer?.Dispose();
            _displayTimer = new Timer(ProcessQueue, null, 0, (int)MessageDisplayDelay);
        }

        public void StopDisplayTimer()
        {
            _displayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void SetDisplayDelay(int milliseconds)
        {
            MessageDisplayDelay = milliseconds;
            _displayTimer?.Change(0, milliseconds);
        }

        #endregion

        #region Добавление сообщений

        public void ShowMessage(Chater user, CommonMessage msg)
        {
            System.Diagnostics.Debug.WriteLine($"[ImportantOverlayViewModel] ShowMessage вызван: {user.EffectiveName}: {msg.Message}");
            if (!IsEnabled) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                    if (string.IsNullOrEmpty(cleanText)) return;

                    msgVm.MessageText = cleanText;

                    if (_currentMode == ChatDisplayMode.AppearAndFade ||
                        _currentMode == ChatDisplayMode.ScrollAndFade ||
                        _currentMode == ChatDisplayMode.Compact)
                    {
                        msgVm.DisplayTimeMs = msg.DisplayTimeMs;
                    }
                    else
                    {
                        msgVm.DisplayTimeMs = 0;
                    }

                    ApplyModeSettings(msgVm);

                    lock (_queueLock)
                    {
                        if (_messageQueue.Count >= MaxQueueSize)
                        {
                            var removed = _messageQueue.Dequeue();
                            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Очередь переполнена, удалено: {removed.MessageText}");
                        }
                        _messageQueue.Enqueue(msgVm);
                        QueueSize = _messageQueue.Count;
                        System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Добавлено в очередь: {cleanText}, размер: {QueueSize}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay AddMessage Error] {ex.Message}");
                }
            });
        }

        #endregion

        #region Обработка очереди

        private void ProcessQueue(object? state)
        {
            if (_isProcessing || !IsAutoDisplay || !IsEnabled) return;
            _isProcessing = true;

            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    DisplayMessageViewModel? nextMessage = null;
                    lock (_queueLock)
                    {
                        if (_messageQueue.Count > 0)
                        {
                            nextMessage = _messageQueue.Dequeue();
                            QueueSize = _messageQueue.Count;
                            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Извлечено из очереди: {nextMessage?.MessageText}, осталось: {QueueSize}");
                        }
                    }
                    if (nextMessage != null)
                    {
                        // 1. ПОКАЗЫВАЕМ СООБЩЕНИЕ
                        await ShowMessageInternal(nextMessage);

                        // 2. ВОСПРОИЗВОДИМ ЗВУК
                        await VoiceService.PlayImportantSoundAsync();
                        // 2. ЖДЕМ 500 МС ДЛЯ АНИМАЦИИ ПОЯВЛЕНИЯ
                        await Task.Delay(500);
                        // 3. ОЗВУЧИВАЕМ ТЕКСТ
                        await VoiceService.SayAsync(nextMessage.MessageText);

                        // 4. УДАЛЯЕМ СООБЩЕНИЕ ПОСЛЕ ОЗВУЧКИ
                        await HideMessageInternal(nextMessage);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay ProcessQueue Error] {ex.Message}");
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }

        private Task ShowMessageInternal(DisplayMessageViewModel msgVm)
        {
            var tcs = new TaskCompletionSource<bool>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_currentMode == ChatDisplayMode.Slideshow)
                    {
                        DisplayMessages.Clear();
                    }

                    DisplayMessages.Add(msgVm);
                    tcs.SetResult(true);
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Сообщение показано: {msgVm.MessageText}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay ShowMessage Error] {ex.Message}");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        private Task HideMessageInternal(DisplayMessageViewModel msgVm)
        {
            var tcs = new TaskCompletionSource<bool>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (DisplayMessages.Contains(msgVm))
                    {
                        DisplayMessages.Remove(msgVm);
                        System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Сообщение удалено: {msgVm.MessageText}");
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay HideMessage Error] {ex.Message}");
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public void DisplayNextMessage()
        {
            if (_isProcessing) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessageViewModel? nextMessage = null;
                lock (_queueLock)
                {
                    if (_messageQueue.Count > 0)
                    {
                        nextMessage = _messageQueue.Dequeue();
                        QueueSize = _messageQueue.Count;
                    }
                }
                if (nextMessage != null)
                {
                    ShowMessageInternal(nextMessage);
                    VoiceService.PlayImportantSound();
                    VoiceService.SayAsync(nextMessage.MessageText);
                    Task.Delay(3000).ContinueWith(_ => HideMessageInternal(nextMessage));
                }
            });
        }

        public void DisplayAllMessages()
        {
            bool wasAutoDisplay = IsAutoDisplay;
            IsAutoDisplay = false;

            Application.Current.Dispatcher.Invoke(async () =>
            {
                List<DisplayMessageViewModel> messages;
                lock (_queueLock)
                {
                    messages = _messageQueue.ToList();
                    _messageQueue.Clear();
                    QueueSize = 0;
                }

                foreach (var msg in messages)
                {
                    await ShowMessageInternal(msg);
                    VoiceService.PlayImportantSound();
                    await VoiceService.SayAsync(msg.MessageText);
                    await HideMessageInternal(msg);
                    await Task.Delay(300);
                }

                IsAutoDisplay = wasAutoDisplay;
            });
        }

        #endregion

        #region Управление сообщениями

        public void ClearMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                lock (_queueLock)
                {
                    _messageQueue.Clear();
                    QueueSize = 0;
                }
                System.Diagnostics.Debug.WriteLine("[ImportantOverlay] Все сообщения очищены");
            });
        }

        public void ToggleAutoDisplay()
        {
            IsAutoDisplay = !IsAutoDisplay;
            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Автовывод: {(IsAutoDisplay ? "ВКЛ" : "ВЫКЛ")}");
        }

        public void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
            System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Включен: {IsEnabled}");
        }

        #endregion

        #region Обработчики событий

        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var msg in DisplayMessages.Where(m => m.User?.Id == updatedChater.Id))
                {
                    msg.User = updatedChater;
                    msg.UpdateMessageCount();
                }
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _displayTimer?.Dispose();
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        }

        #endregion
        /// <summary>
        /// Воспроизвести следующее сообщение из очереди (для ручного режима)
        /// </summary>
        public async Task PlayNextFromQueueAsync()
        {
            if (_isProcessing)
            {
                System.Diagnostics.Debug.WriteLine("[ImportantOverlay] Уже воспроизводится, подождите");
                return;
            }

            _isProcessing = true;

            try
            {
                DisplayMessageViewModel? nextMessage = null;
                lock (_queueLock)
                {
                    if (_messageQueue.Count > 0)
                    {
                        nextMessage = _messageQueue.Dequeue();
                        QueueSize = _messageQueue.Count;
                        System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Ручное воспроизведение: {nextMessage?.MessageText}, осталось: {QueueSize}");
                    }
                }

                if (nextMessage != null)
                {
                    // Показываем сообщение
                    await ShowMessageInternal(nextMessage);

                    // Ждем для анимации
                    await Task.Delay(500);

                    // Воспроизводим звук и голос
                    await VoiceService.PlayImportantSoundAsync();
                    await VoiceService.SayAsync(nextMessage.MessageText);

                    // Ждем пока сообщение будет видно
                    await Task.Delay(nextMessage.DisplayTimeMs > 0 ? nextMessage.DisplayTimeMs : 3000);

                    // Скрываем сообщение
                    await HideMessageInternal(nextMessage);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ImportantOverlay] Очередь пуста");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImportantOverlay PlayNext Error] {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}