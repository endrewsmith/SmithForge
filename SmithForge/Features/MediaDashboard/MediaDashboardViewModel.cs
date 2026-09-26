using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmithForge.Features.MediaDashboard
{
    public partial class MediaDashboardViewModel : ObservableObject
    {
        private const int MaxItems = 200;

        public ObservableCollection<MediaDashboardItem> Items { get; } = new();

        private bool _autoScrollEnabled = true;
        public bool AutoScrollEnabled
        {
            get => _autoScrollEnabled;
            set => SetProperty(ref _autoScrollEnabled, value);
        }

        public ICommand ClearCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ScrollToBottomCommand { get; }

        public MediaDashboardViewModel()
        {
            ClearCommand = new SmithForge.Features.Dashboard.RelayCommand(Clear);
            CloseCommand = new SmithForge.Features.Dashboard.RelayCommand(CloseDashboard);
            ScrollToBottomCommand = new SmithForge.Features.Dashboard.RelayCommand(ForceScrollToBottom);
        }

        // ✅ По аналогии с DashboardViewModel.AddMessage
        public void AddMedia(MediaDashboardItem item)
        {
            if (item == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Add(item);

                while (Items.Count > MaxItems)
                    Items.RemoveAt(0);

                // ✅ Если пользователь внизу — автоматически прокрутить к новому сообщению
                if (AutoScrollEnabled)
                {
                    var window = Application.Current.Windows.OfType<MediaDashboardWindow>().FirstOrDefault();
                    var scrollViewer = window?.FindName("EventsScrollViewer") as ScrollViewer;

                    if (scrollViewer != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                scrollViewer.ScrollToEnd();
                            }),
                            DispatcherPriority.Loaded);
                    }
                }
            });
        }

        public void OnScrollChanged(double verticalOffset, double scrollableHeight)
        {
            bool isAtBottom = Math.Abs(scrollableHeight - verticalOffset) < 50;

            if (!isAtBottom && AutoScrollEnabled)
            {
                AutoScrollEnabled = false;
                System.Diagnostics.Debug.WriteLine("[MediaDashboard] Авто-скролл отключен пользователем");
            }
            else if (isAtBottom && !AutoScrollEnabled)
            {
                AutoScrollEnabled = true;
                System.Diagnostics.Debug.WriteLine("[MediaDashboard] Авто-скролл включен");
            }
        }

        private void ForceScrollToBottom()
        {
            AutoScrollEnabled = true;

            var window = Application.Current.Windows.OfType<MediaDashboardWindow>().FirstOrDefault();
            var scrollViewer = window?.FindName("EventsScrollViewer") as ScrollViewer;
            scrollViewer?.ScrollToEnd();
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Clear();

                // ✅ Обновить layout, чтобы ScrollableHeight = 0
                var window = Application.Current.Windows.OfType<MediaDashboardWindow>().FirstOrDefault();
                var scrollViewer = window?.FindName("EventsScrollViewer") as ScrollViewer;
                scrollViewer?.UpdateLayout();
            });
        }

        private void CloseDashboard()
        {
            var window = Application.Current.Windows.OfType<MediaDashboardWindow>().FirstOrDefault();
            if (window != null)
            {
                window.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine("[MediaDashboard] Окно скрыто через CloseCommand");
            }
        }
    }
}