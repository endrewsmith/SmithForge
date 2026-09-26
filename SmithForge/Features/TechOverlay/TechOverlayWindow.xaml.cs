using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SmithForge.Features.TechOverlay
{
    public partial class TechOverlayWindow : Window
    {
        public TechOverlayViewModel ViewModel { get; }

        private bool _isScrolling = false;

        public TechOverlayWindow()
        {
            InitializeComponent();

            ViewModel = new TechOverlayViewModel();
            DataContext = ViewModel;

            // ✅ Автоскролл вниз при новом событии
            ViewModel.Events.CollectionChanged += OnEventsChanged;

            // ✅ Не закрывать окно крестиком, только скрывать
            Closing += (s, e) =>
            {
                e.Cancel = true;
                Visibility = Visibility.Collapsed;
            };
        }

        private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add) return;

            // ✅ Точно как в дашборде: через BeginInvoke с приоритетом Loaded
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (EventsScrollViewer != null)
                    {
                        _isScrolling = true;
                        EventsScrollViewer.ScrollToEnd();
                        _isScrolling = false;
                    }
                }),
                DispatcherPriority.Loaded);
        }

        private void EventsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (EventsScrollViewer == null) return;
            if (_isScrolling) return;

            bool isAtBottom = Math.Abs(EventsScrollViewer.ScrollableHeight - EventsScrollViewer.VerticalOffset) < 50;

            if (!isAtBottom && ScrollToBottomButton.Visibility != Visibility.Visible)
            {
                ScrollToBottomButton.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine("[TechOverlay] Авто-скролл отключён пользователем");
            }
            else if (isAtBottom && ScrollToBottomButton.Visibility == Visibility.Visible)
            {
                ScrollToBottomButton.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine("[TechOverlay] Авто-скролл включён");
            }
        }

        private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToBottomButton.Visibility = Visibility.Collapsed;

            if (EventsScrollViewer != null)
            {
                _isScrolling = true;
                EventsScrollViewer.ScrollToEnd();
                _isScrolling = false;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.Clear();

            // ✅ Сбросить layout, чтобы ScrollableHeight = 0 (как в дашборде)
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EventsScrollViewer?.UpdateLayout();
            }), DispatcherPriority.Background);
        }
    }
}