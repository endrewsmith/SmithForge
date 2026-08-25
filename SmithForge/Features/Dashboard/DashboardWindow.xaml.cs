using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmithForge.Features.Dashboard
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow()
        {
            InitializeComponent();
            Loaded += DashboardWindow_Loaded;
        }

        private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(DashboardViewModel.AutoScrollEnabled))
                    {
                        ScrollToBottomButton.Visibility = viewModel.AutoScrollEnabled ? Visibility.Collapsed : Visibility.Visible;
                    }
                };

                ScrollToBottomButton.Visibility = viewModel.AutoScrollEnabled ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ✅ Просто скрываем окно
            this.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[Dashboard] Окно скрыто через CloseButton");
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                var scrollViewer = sender as ScrollViewer;
                if (scrollViewer != null)
                {
                    viewModel.OnScrollChanged(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
                }
            }
        }
    }
}