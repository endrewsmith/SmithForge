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
        private bool _hadScroll = false;
        private System.Windows.Threading.DispatcherTimer? _scrollDebounce;

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            if (DataContext is DashboardViewModel viewModel)
            {
                viewModel.OnScrollChanged(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
            }
        }
        //private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        //{
        //    var scrollViewer = sender as ScrollViewer;
        //    if (scrollViewer == null) return;

        //    // Обновляем состояние по фактическому ScrollableHeight
        //    bool hasScroll = scrollViewer.ScrollableHeight > 0;

        //    if (hasScroll != _hadScroll)
        //    {
        //        // ✅ Дебаунс: ждём 100мс стабильного состояния, потом логируем переход
        //        _scrollDebounce?.Stop();
        //        _scrollDebounce = new System.Windows.Threading.DispatcherTimer
        //        {
        //            Interval = TimeSpan.FromMilliseconds(100)
        //        };
        //        _scrollDebounce.Tick += (s, args) =>
        //        {
        //            _scrollDebounce?.Stop();
        //            _scrollDebounce = null;

        //            // Перепроверяем — состояние не мигнуло за это время
        //            bool stable = scrollViewer.ScrollableHeight > 0;
        //            if (stable == _hadScroll) return;

        //            _hadScroll = stable;

        //            if (stable)
        //            {
        //                System.Diagnostics.Debug.WriteLine("[Dashboard] 📜 СКРОЛЛ ПОЯВИЛСЯ (стабильно)");
        //                // тут твоя логика при появлении
        //                if (DataContext is DashboardViewModel vm)
        //                {
        //                    vm.SmoothScrollToBottom();
        //                }
        //            }
        //            else
        //            {
        //                System.Diagnostics.Debug.WriteLine("[Dashboard] 📜 СКРОЛЛ ИСЧЕЗ (стабильно)");
        //                // тут твоя логика при исчезновении
        //                scrollViewer.UpdateLayout();
        //            }
        //        };
        //        _scrollDebounce.Start();
        //    }

        //    if (DataContext is DashboardViewModel viewModel)
        //    {
        //        viewModel.OnScrollChanged(scrollViewer.VerticalOffset, scrollViewer.ScrollableHeight);
        //    }
        //}
    }
}