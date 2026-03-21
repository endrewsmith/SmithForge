using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System;

namespace SmithForge.Features.StickersOverlay
{
    public partial class StickersOverlayViewModel : ObservableObject
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

        public StickersOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
            StartDisplayTimer();
        }

        #region Управление режимом

        /// <summary>
        /// Установить режим отображения
        /// </summary>
        public void SetMode(ChatDisplayMode mode)
        {
            _currentMode = mode;

            // Применяем настройки режима к существующим сообщениям
            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }

            System.Diagnostics.Debug.WriteLine($"[StickersOverlayViewModel] Установлен режим: {mode}");
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
                    break;
                case ChatDisplayMode.AppearAndFade:
                case ChatDisplayMode.AppearOnly:
                case ChatDisplayMode.Slideshow:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    break;
                default:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    break;
            }
        }

        #endregion

        public void StartDisplayTimer()
        {
            _displayTimer?.Dispose();
            _displayTimer = new Timer(ProcessQueue, null, 0, (int)MessageDisplayDelay);
        }

        public void StopDisplayTimer()
        {
            _displayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void ShowSticker(Chater user, CommonMessage msg)
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

                    // Применяем настройки режима
                    ApplyModeSettings(msgVm);

                    lock (_queueLock)
                    {
                        if (_messageQueue.Count >= MaxQueueSize)
                        {
                            _messageQueue.Dequeue();
                        }
                        _messageQueue.Enqueue(msgVm);
                        QueueSize = _messageQueue.Count;
                        System.Diagnostics.Debug.WriteLine($"[StickersOverlay] Добавлен стикер в очередь: {cleanText}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StickersOverlay AddSticker Error] {ex.Message}");
                }
            });
        }

        private void ProcessQueue(object? state)
        {
            if (_isProcessing || !IsAutoDisplay || !IsEnabled) return;
            _isProcessing = true;

            // Проверяем, существует ли приложение
            if (Application.Current == null || Application.Current.Dispatcher == null)
            {
                _isProcessing = false;
                return;
            }

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
                        }
                    }
                    if (nextMessage != null)
                    {
                        DisplaySticker(nextMessage);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StickersOverlay ProcessQueue Error] {ex.Message}");
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }
        private void DisplaySticker(DisplayMessageViewModel msgVm)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Add(msgVm);

                // Таймер удаления для режимов с исчезновением
                if (_currentMode == ChatDisplayMode.AppearAndFade ||
                    _currentMode == ChatDisplayMode.ScrollAndFade ||
                    _currentMode == ChatDisplayMode.Compact)
                {
                    Task.Delay(msgVm.DisplayTimeMs).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
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
                    // Для слайд-шоу показываем только последний стикер
                    var tempList = DisplayMessages.ToList();
                    foreach (var oldMsg in tempList.Where(m => m != msgVm))
                    {
                        DisplayMessages.Remove(oldMsg);
                    }
                }
            });
        }

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

        public void ClearStickers()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                lock (_queueLock) _messageQueue.Clear();
                QueueSize = 0;
            });
        }

        public void Dispose()
        {
            _displayTimer?.Dispose();
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        }
    }
}