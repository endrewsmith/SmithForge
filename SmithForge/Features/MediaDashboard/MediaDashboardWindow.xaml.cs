using System.Windows;
using System.Windows.Controls;

namespace SmithForge.Features.MediaDashboard
{
    public partial class MediaDashboardWindow : Window
    {
        public MediaDashboardWindow()
        {
            InitializeComponent();
            Loaded += MediaDashboardWindow_Loaded;
        }

        private void MediaDashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MediaDashboardViewModel viewModel)
            {
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(MediaDashboardViewModel.AutoScrollEnabled))
                    {
                        ScrollToBottomButton.Visibility = viewModel.AutoScrollEnabled
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    }
                };

                ScrollToBottomButton.Visibility = viewModel.AutoScrollEnabled
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void EventsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer &&
                DataContext is MediaDashboardViewModel viewModel)
            {
                viewModel.OnScrollChanged(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
            }
        }
    }
}