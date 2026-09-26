using System;
using System.Diagnostics;
using System.Windows;

namespace SmithForge.Features.MediaDashboard
{
    public class MediaDashboardService
    {
        private MediaDashboardWindow? _window;
        private MediaDashboardViewModel? _viewModel;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();

        public void Initialize()
        {
            if (_isInitialized) return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                if (Application.Current == null) return;

                if (!Application.Current.Dispatcher.CheckAccess())
                    Application.Current.Dispatcher.Invoke(InitializeOnUi);
                else
                    InitializeOnUi();
            }
        }

        private void InitializeOnUi()
        {
            if (_isInitialized) return;

            try
            {
                _viewModel = new MediaDashboardViewModel();
                _window = new MediaDashboardWindow
                {
                    DataContext = _viewModel,
                    Visibility = Visibility.Collapsed
                };

                _window.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    _window!.Visibility = Visibility.Collapsed;
                };

                _window.Show();

                _isInitialized = true;
                Debug.WriteLine("[MediaDashboard] Сервис инициализирован");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaDashboard] Ошибка инициализации: {ex.Message}");
            }
        }

        public void AddMedia(MediaDashboardItem item)
        {
            if (!_isInitialized) Initialize();
            if (_viewModel == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
                _viewModel.AddMedia(item);
            else
                Application.Current.Dispatcher.BeginInvoke(() => _viewModel.AddMedia(item));
        }

        public void Show()
        {
            if (!_isInitialized) Initialize();
            if (_window == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _window.Visibility = Visibility.Visible;
                _window.Topmost = true;
            });
        }

        public void Hide()
        {
            if (_window == null) return;
            Application.Current.Dispatcher.Invoke(() => _window.Visibility = Visibility.Collapsed);
        }

        public void Toggle()
        {
            if (_window?.Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }

        public void Clear()
        {
            if (_viewModel == null) return;
            if (Application.Current.Dispatcher.CheckAccess())
                _viewModel.Clear();
            else
                Application.Current.Dispatcher.BeginInvoke(() => _viewModel.Clear());
        }

        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;
    }
}