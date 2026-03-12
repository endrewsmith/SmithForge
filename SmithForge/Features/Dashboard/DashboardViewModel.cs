using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SmithForge.Features.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public DashboardViewModel()
        {
            // Подписываемся на обновления чаттеров, если нужно
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msgVm = new DisplayMessageViewModel(user, msg);
                DisplayMessages.Add(msgVm);

                // Ограничиваем количество сообщений (например, 1000)
                if (DisplayMessages.Count > 1000)
                    DisplayMessages.RemoveAt(0);

                // Автоскролл к последнему сообщению
                if (Application.Current.Windows.OfType<DashboardWindow>().FirstOrDefault() is DashboardWindow window)
                {
                    if (window.FindName("MessagesList") is ItemsControl itemsControl)
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(itemsControl);
                        scrollViewer?.ScrollToBottom();
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[Dashboard] Добавлено сообщение от {user.Login}");
            });
        }

        // Вспомогательный метод для поиска ScrollViewer
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    return t;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                System.Diagnostics.Debug.WriteLine("[Dashboard] Сообщения очищены");
            });
        }
    }
}