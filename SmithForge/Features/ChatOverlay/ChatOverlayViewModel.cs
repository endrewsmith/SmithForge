using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace SmithForge.Features.ChatOverlay
{
    public partial class ChatOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ChatOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msgVm = new DisplayMessageViewModel(user, msg.Message, msg.MessageNumber, msg.Type);
                // ОТЛАДКА
                System.Diagnostics.Debug.WriteLine($"[ChatOverlayViewModel] Создан msgVm: " +
                    $"User={user.Login}, MessageNumber={msgVm.MessageNumber}");
                DisplayMessages.Add(msgVm);

                if (DisplayMessages.Count > 8)
                    DisplayMessages.RemoveAt(0);

                Task.Delay(15000).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (DisplayMessages.Contains(msgVm))
                        {
                            DisplayMessages.Remove(msgVm);
                        }
                    });
                });
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
    }
}