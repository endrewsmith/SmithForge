using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Linq;

namespace SmithForge.Features.ChatOverlayShorts
{
    public partial class ChatOverlayShortsViewModel : ObservableObject
    {
        #region Поля и свойства

        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private bool _isAutoDisplay = true;

        [ObservableProperty] private double _messageDisplayDelay = 800;
        [ObservableProperty] private int _maxQueueSize = 50;
        [ObservableProperty] private int _queueSize;

        [ObservableProperty] private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        private readonly Queue<DisplayMessageViewModel> _messageQueue = new();
        private Timer? _displayTimer;
        private bool _isProcessing = false;
        private readonly object _queueLock = new object();
        private bool _isDisposed = false;

        #endregion

        #region Команды

        public IRelayCommand ClearQueueCommand { get; }
        public IRelayCommand DisplayAllMessagesCommand { get; }
        public IRelayCommand DisplayNextMessageCommand { get; }
        public IRelayCommand ClearAllMessagesCommand { get; }
        public IRelayCommand ToggleAutoDisplayCommand { get; }
        public IRelayCommand ToggleSetupModeCommand { get; }
        public IRelayCommand ToggleEnabledCommand { get; }

        #endregion

        #region Конструктор

        public ChatOverlayShortsViewModel()
        {
            ClearQueueCommand = new RelayCommand(ClearQueue);
            DisplayAllMessagesCommand = new RelayCommand(DisplayAllMessages);
            DisplayNextMessageCommand = new RelayCommand(DisplayNextMessage);
            ClearAllMessagesCommand = new RelayCommand(ClearAllMessages);
            ToggleAutoDisplayCommand = new RelayCommand(ToggleAutoDisplay);
            ToggleSetupModeCommand = new RelayCommand(ToggleSetupMode);
            ToggleEnabledCommand = new RelayCommand(ToggleEnabled);

            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
            StartDisplayTimer();
        }

        #endregion

        #region Управление режимом

        public void SetMode(ChatDisplayMode mode)
        {
            if (_isDisposed) return;

            _currentMode = mode;

            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }

            System.Diagnostics.Debug.WriteLine($"[ShortsViewModel] Установлен режим: {mode}");
        }

        private void ApplyModeSettings(DisplayMessageViewModel msgVm)
        {
            switch (_currentMode)
            {
                case ChatDisplayMode.Compact:
                    msgVm.ShowAvatar = false;
                    msgVm.ShowRank = false;
                    break;
                default:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    break;
            }
        }

        #endregion

        #region Управление

        public void StartDisplayTimer()
        {
            if (_isDisposed) return;

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

        public void ClearQueue()
        {
            lock (_queueLock)
            {
                _messageQueue.Clear();
                QueueSize = 0;
                System.Diagnostics.Debug.WriteLine("[Shorts Queue] Очередь очищена");
            }
        }

        public void ClearAllMessages()
        {
            if (_isDisposed) return;

            SafeDispatcherInvoke(() =>
            {
                DisplayMessages.Clear();
                System.Diagnostics.Debug.WriteLine("[Shorts] Все сообщения удалены");
            });
        }

        public void ToggleAutoDisplay()
        {
            IsAutoDisplay = !IsAutoDisplay;
            System.Diagnostics.Debug.WriteLine($"[Shorts AutoDisplay] {(IsAutoDisplay ? "ВКЛ" : "ВЫКЛ")}");
        }

        public void ToggleSetupMode()
        {
            IsSetupMode = !IsSetupMode;
        }

        public void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
            System.Diagnostics.Debug.WriteLine($"[Shorts Enabled] {(IsEnabled ? "ВКЛ" : "ВЫКЛ")}");
        }

        #endregion

        #region Безопасный вызов Dispatcher

        private void SafeDispatcherInvoke(Action action)
        {
            try
            {
                if (_isDisposed) return;

                if (Application.Current == null || Application.Current.Dispatcher == null)
                    return;

                Application.Current.Dispatcher.Invoke(action);
            }
            catch (TaskCanceledException)
            {
                // Приложение закрывается - игнорируем
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeDispatcherInvoke] {ex.Message}");
            }
        }

        #endregion

        #region Добавление сообщений

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!IsEnabled || _isDisposed) return;

            try
            {
                var msgVm = new DisplayMessageViewModel(user, msg);

                if (msg.Message.Contains("<like") || msg.Message.Contains("<dislike") || msg.Message.Contains("<nick"))
                {
                    if (msg.Message.Contains("<like") || msg.Message.Contains("<dislike"))
                        ProcessReactionTags(msgVm);
                    else
                        ProcessNickTag(msgVm);
                    return;
                }

                string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                if (string.IsNullOrEmpty(cleanText)) return;

                msgVm.MessageText = cleanText;
                ApplyModeSettings(msgVm);

                lock (_queueLock)
                {
                    if (_messageQueue.Count >= MaxQueueSize)
                    {
                        _messageQueue.Dequeue();
                    }
                    _messageQueue.Enqueue(msgVm);
                    QueueSize = _messageQueue.Count;
                    System.Diagnostics.Debug.WriteLine($"[Shorts Queue] Добавлено: {cleanText}, размер: {QueueSize}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts AddMessage Error] {ex.Message}");
            }
        }

        #endregion

        #region Обработка очереди

        private void ProcessQueue(object? state)
        {
            if (_isProcessing || !IsAutoDisplay || !IsEnabled || _isDisposed) return;

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
                    }
                }

                if (nextMessage != null)
                {
                    DisplayMessage(nextMessage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts ProcessQueue Error] {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        public void DisplayNextMessage()
        {
            if (_isProcessing || _isDisposed) return;

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
        }

        public void DisplayAllMessages()
        {
            if (_isDisposed) return;

            bool wasAutoDisplay = IsAutoDisplay;
            IsAutoDisplay = false;

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
                Thread.Sleep(50);
            }

            IsAutoDisplay = wasAutoDisplay;
        }

        #endregion

        #region Отображение сообщения

        private void DisplayMessage(DisplayMessageViewModel msgVm)
        {
            if (_isDisposed) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts Display] Выводим: {msgVm.MessageText ?? "Sticker"}");

                SafeDispatcherInvoke(() =>
                {
                    DisplayMessages.Add(msgVm);
                });

                // Таймер удаления сообщения
                if (_currentMode == ChatDisplayMode.AppearAndFade ||
                    _currentMode == ChatDisplayMode.ScrollAndFade ||
                    _currentMode == ChatDisplayMode.Compact)
                {
                    Task.Delay(msgVm.DisplayTimeMs).ContinueWith(t =>
                    {
                        if (_isDisposed) return;

                        SafeDispatcherInvoke(() =>
                        {
                            if (DisplayMessages.Contains(msgVm))
                            {
                                DisplayMessages.Remove(msgVm);
                            }
                        });
                    });
                }
                else if (_currentMode == ChatDisplayMode.Slideshow)
                {
                    var tempList = DisplayMessages.ToList();
                    foreach (var oldMsg in tempList.Where(m => m != msgVm))
                    {
                        SafeDispatcherInvoke(() => DisplayMessages.Remove(oldMsg));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts Display Error] {ex.Message}");
            }
        }

        #endregion

        #region Обработчики событий

        private void ProcessReactionTags(DisplayMessageViewModel m)
        {
            System.Diagnostics.Debug.WriteLine($"[Shorts Reaction] {m.MessageText}");
        }

        private void ProcessNickTag(DisplayMessageViewModel m)
        {
            System.Diagnostics.Debug.WriteLine($"[Shorts Nick] {m.MessageText}");
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            if (_isDisposed) return;

            SafeDispatcherInvoke(() =>
            {
                try
                {
                    foreach (var msg in DisplayMessages.Where(m => m.User?.Id == updatedChater.Id))
                    {
                        msg.User = updatedChater;
                        msg.UpdateMessageCount();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Shorts OnChaterUpdated] Ошибка: {ex.Message}");
                }
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _displayTimer?.Dispose();
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;

            lock (_queueLock)
            {
                _messageQueue.Clear();
            }

            SafeDispatcherInvoke(() =>
            {
                DisplayMessages.Clear();
            });
        }

        #endregion
    }
}