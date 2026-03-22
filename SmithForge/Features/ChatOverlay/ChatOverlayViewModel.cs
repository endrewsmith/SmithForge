using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using System.Linq;

namespace SmithForge.Features.ChatOverlay
{
    public partial class ChatOverlayViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private bool _isEnabled = true;
        [ObservableProperty] private ChatDisplayMode _currentDisplayMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        private ChatOverlayWindow? _window;
        private IChatDisplayMode _currentMode;

        public ChatOverlayViewModel()
        {
            SetMode(ChatDisplayMode.AppearAndFade);
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void SetWindow(ChatOverlayWindow window)
        {
            _window = window;
        }

        public void SetMode(ChatDisplayMode mode)
        {
            _currentDisplayMode = mode;
            _currentMode = ChatDisplayModeFactory.GetMode(mode);
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!IsEnabled) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // Пропускаем системные сообщения с тегами
                    if (msg.Message.Contains("<like") ||
                        msg.Message.Contains("<dislike") ||
                        msg.Message.Contains("<nick") ||
                        msg.Message.Contains("<sticker"))
                    {
                        return;
                    }

                    // !!! ВАЖНО: НЕ ОЧИЩАЕМ ТЕКСТ ОТ ТЕГОВ !!!
                    // Сохраняем оригинальный текст с тегами для форматирования
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    // Проверяем что текст не пустой (оригинальный с тегами)
                    if (string.IsNullOrEmpty(msgVm.MessageText)) return;

                    // Применяем настройки режима к сообщению
                    _currentMode.Apply(msgVm);

                    // Проверяем, нужно ли пропустить анимацию (если чат уже полон)
                    bool shouldSkipAnimation = _currentMode.ShouldSkipScaleAnimation(DisplayMessages.Count > 10);

                    // Подготавливаем сообщение к отображению
                    _currentMode.PrepareMessage(msgVm, shouldSkipAnimation);

                    // Добавляем сообщение
                    DisplayMessages.Add(msgVm);

                    // Ограничиваем количество сообщений
                    if (DisplayMessages.Count > 50)
                    {
                        while (DisplayMessages.Count > 40)
                        {
                            DisplayMessages.RemoveAt(0);
                        }
                    }

                    // Обрабатываем автоматический скролл
                    if (_currentMode.ShouldAutoScroll && _window != null)
                    {
                        _window.ScrollToBottom();
                    }

                    // Если сообщение должно исчезнуть через время
                    int displayTime = _currentMode.GetDisplayTimeMs(msg);
                    if (displayTime > 0)
                    {
                        ScheduleMessageRemoval(msgVm, displayTime);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatOverlay AddMessage Error] {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }

        private void ScheduleMessageRemoval(DisplayMessageViewModel msgVm, int delayMs)
        {
            Task.Delay(delayMs).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (DisplayMessages.Contains(msgVm))
                    {
                        DisplayMessages.Remove(msgVm);
                    }
                }));
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

        public void ClearMessages()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DisplayMessages.Clear();
            }));
        }

        public void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
        }
    }
}