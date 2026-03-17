using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Linq;

namespace SmithForge.Features.ChatOverlayShorts
{
    public partial class ChatOverlayShortsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ChatOverlayShortsViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    // Проверяем, не является ли сообщение реакцией или сменой ника
                    bool isReaction = msg.Message.Contains("<like") || msg.Message.Contains("<dislike");
                    bool isNickChange = msg.Message.Contains("<nick");

                    if (isReaction || isNickChange)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Shorts] Скрытое сообщение: {msg.Message}");
                        return;
                    }

                    string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                    if (string.IsNullOrEmpty(cleanText)) return;

                    DisplayMessages.Add(msgVm);

                    System.Diagnostics.Debug.WriteLine($"[ShortsViewModel] Создан msgVm: " +
                        $"User={user.Login}, MessageNumber={msgVm.MessageNumber}, " +
                        $"Длина={cleanText.Length}, Время={msgVm.DisplayTimeMs}мс");

                    if (DisplayMessages.Count > 50)
                    {
                        var oldestMsg = DisplayMessages[0];
                        DisplayMessages.Remove(oldestMsg);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Shorts AddMessage] Ошибка: {ex.Message}");
                }
            });
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
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

        public void ClearMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                System.Diagnostics.Debug.WriteLine("[ShortsViewModel] Сообщения очищены");
            });
        }
    }
}