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

        /// <summary>
        /// Установить режим отображения чата
        /// </summary>
        public void SetMode(ChatDisplayMode mode)
        {
            _currentMode = mode;

            // Применяем настройки режима к существующим сообщениям
            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }

            System.Diagnostics.Debug.WriteLine($"[ImportantOverlayViewModel] Установлен режим: {mode}");
        }

        /// <summary>
        /// Получить текущий режим
        /// </summary>
        public ChatDisplayMode GetCurrentMode()
        {
            return _currentMode;
        }

        /// <summary>
        /// Применить настройки режима к сообщению
        /// </summary>
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
                case ChatDisplayMode.SmoothScroll:
                case ChatDisplayMode.Instant:
                case ChatDisplayMode.ScrollAndFade:
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
            if (!IsEnabled) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    // Очищаем текст от тегов
                    string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                    if (string.IsNullOrEmpty(cleanText)) return;

                    msgVm.MessageText = cleanText;

                    // Устанавливаем время отображения согласно режиму
                    if (_currentMode == ChatDisplayMode.AppearAndFade ||
                        _currentMode == ChatDisplayMode.ScrollAndFade ||
                        _currentMode == ChatDisplayMode.Compact)
                    {
                        msgVm.DisplayTimeMs = msg.DisplayTimeMs;
                    }
                    else
                    {
                        msgVm.DisplayTimeMs = 0; // Бесконечное отображение
                    }

                    // Применяем настройки режима
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

            Application.Current.Dispatcher.Invoke(() =>
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
                        DisplayMessage(nextMessage);
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
                    DisplayMessage(nextMessage);
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
                    DisplayMessage(msg);
                    await Task.Delay(50);
                }

                IsAutoDisplay = wasAutoDisplay;
            });
        }

        #endregion

        #region Отображение сообщения

        private void DisplayMessage(DisplayMessageViewModel msgVm)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay Display] Выводим: {msgVm.MessageText}");

                    // Для слайд-шоу очищаем все предыдущие сообщения
                    if (_currentMode == ChatDisplayMode.Slideshow)
                    {
                        DisplayMessages.Clear();
                    }

                    DisplayMessages.Add(msgVm);

                    // Таймер удаления для режимов с исчезновением
                    if (_currentMode == ChatDisplayMode.AppearAndFade ||
                        _currentMode == ChatDisplayMode.ScrollAndFade ||
                        _currentMode == ChatDisplayMode.Compact)
                    {
                        if (msgVm.DisplayTimeMs > 0)
                        {
                            Task.Delay(msgVm.DisplayTimeMs).ContinueWith(_ =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (DisplayMessages.Contains(msgVm))
                                    {
                                        DisplayMessages.Remove(msgVm);
                                        System.Diagnostics.Debug.WriteLine($"[ImportantOverlay] Сообщение удалено: {msgVm.MessageText}");
                                    }
                                });
                            });
                        }
                    }

                    // Лимит сообщений для режимов без удаления
                    if (_currentMode != ChatDisplayMode.AppearAndFade &&
                        _currentMode != ChatDisplayMode.ScrollAndFade &&
                        _currentMode != ChatDisplayMode.Compact &&
                        DisplayMessages.Count > 30)
                    {
                        var oldestMsg = DisplayMessages[0];
                        DisplayMessages.Remove(oldestMsg);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImportantOverlay Display Error] {ex.Message}");
                }
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
    }
}