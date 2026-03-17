using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.ObjectModel; // Обязательно для ObservableCollection
using System.Windows;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        // ЭТОЙ СТРОКИ НЕ ХВАТАЛО. Она создает "хранилище" для сообщений
        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ImportantOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void ShowImportantMessage(Chater user, CommonMessage msg)
        {
            // Используем Dispatcher, чтобы безопасно менять коллекцию из любого потока
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 1. Очищаем старое важное сообщение
                    DisplayMessages.Clear();

                    // 2. Создаем и добавляем новое
                    var msgVm = new DisplayMessageViewModel(user, msg);
                    DisplayMessages.Add(msgVm);

                    System.Diagnostics.Debug.WriteLine($"[Important VM] Сообщение добавлено в коллекцию.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Important VM Error] {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void ClearMessage()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                DisplayMessages.Clear();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Если в списке кто-то есть, обновляем его данные
                var current = System.Linq.Enumerable.FirstOrDefault(DisplayMessages);
                if (current?.User?.Id == updatedChater.Id)
                {
                    current.User = updatedChater;
                    current.UpdateMessageCount();
                }
            });
        }
    }
}
