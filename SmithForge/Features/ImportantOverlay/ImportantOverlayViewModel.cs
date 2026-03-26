using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private bool _isAutoDisplay = true;
        [ObservableProperty] private int _queueSize;
        [ObservableProperty] private int _maxQueueSize = 50;
        [ObservableProperty] private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        private readonly Queue<DisplayMessageViewModel> _messageQueue = new();
        private readonly object _queueLock = new object();
        private bool _isProcessing = false;

        public ImportantOverlayViewModel()
        {
            Debug.WriteLine("[ImportantOverlayViewModel] Конструктор вызван");
            BindingOperations.EnableCollectionSynchronization(DisplayMessages, new object());
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
            Debug.WriteLine("[ImportantOverlayViewModel] Инициализация завершена");
        }

        #region Управление режимом

        public void SetMode(ChatDisplayMode mode)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => SetMode(mode));
                return;
            }

            _currentMode = mode;
            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }
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

        #region Добавление сообщений

        /// <summary>
        /// Показывает сообщение и возвращает ссылку на него (для Service)
        /// </summary>
        public DisplayMessageViewModel? ShowMessageAndReturn(Chater user, CommonMessage msg)
        {
            Debug.WriteLine($"[ViewModel] ShowMessageAndReturn НАЧАЛО, поток: {Thread.CurrentThread.ManagedThreadId}");
            Debug.WriteLine($"[ViewModel] Dispatcher.CheckAccess: {Application.Current.Dispatcher.CheckAccess()}");

            // Убеждаемся, что мы в UI потоке
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Debug.WriteLine("[ViewModel] ShowMessageAndReturn: НЕ в UI потоке! Это ошибка!");
                return Application.Current.Dispatcher.Invoke(() => ShowMessageAndReturn(user, msg));
            }

            try
            {
                var msgVm = new DisplayMessageViewModel(user, msg);
                string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                if (string.IsNullOrEmpty(cleanText)) return null;

                msgVm.MessageText = cleanText;
                msgVm.DisplayTimeMs = 5000;
                ApplyModeSettings(msgVm);

                // Добавляем в коллекцию (теперь точно в UI потоке)
                DisplayMessages.Add(msgVm);

                Debug.WriteLine($"[ViewModel] Сообщение показано: {cleanText}");

                return msgVm;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ViewModel] ShowMessageAndReturn Error: {ex.Message}");
                Debug.WriteLine($"[ViewModel] ShowMessageAndReturn StackTrace: {ex.StackTrace}");
                return null;
            }
        }
        public void ShowMessage(Chater user, CommonMessage msg)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowMessage(user, msg));
                return;
            }

            try
            {
                var msgVm = new DisplayMessageViewModel(user, msg);
                string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                if (string.IsNullOrEmpty(cleanText)) return;

                msgVm.MessageText = cleanText;
                msgVm.DisplayTimeMs = 5000;
                ApplyModeSettings(msgVm);

                DisplayMessages.Add(msgVm);

                Debug.WriteLine($"[ImportantOverlay] Сообщение показано: {cleanText}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportantOverlay ShowMessage Error] {ex.Message}");
            }
        }
        /// <summary>
        /// Удаляет конкретное сообщение из дисплея
        /// </summary>
        public void RemoveMessage(DisplayMessageViewModel msgVm)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => RemoveMessage(msgVm));
                return;
            }

            try
            {
                if (DisplayMessages.Contains(msgVm))
                {
                    DisplayMessages.Remove(msgVm);
                    Debug.WriteLine($"[ImportantOverlay] Сообщение удалено: {msgVm.MessageText}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportantOverlay RemoveMessage Error] {ex.Message}");
            }
        }

        /// <summary>
        /// Очищает все сообщения с дисплея
        /// </summary>
        public void ClearMessages()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ClearMessages());
                return;
            }

            DisplayMessages.Clear();
            Debug.WriteLine("[ImportantOverlay] Все сообщения очищены");
        }

        #endregion

        #region Вспомогательные методы

        private void UpdateMainQueueCount()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    var mainVm = Application.Current.MainWindow?.DataContext as MainViewModel;
                    mainVm?.UpdateImportantQueueCount(QueueSize);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportantOverlayViewModel] UpdateMainQueueCount Error: {ex.Message}");
            }
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnChaterUpdated(updatedChater));
                return;
            }

            foreach (var msg in DisplayMessages.Where(m => m.User?.Id == updatedChater.Id))
            {
                msg.User = updatedChater;
                msg.UpdateMessageCount();
            }
        }

        public void Dispose()
        {
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        }

        #endregion
    }
}