using SmithForge.Features.Dashboard;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Windows;

namespace SmithForge.Main.Services
{
    public class DashboardService
    {
        private DashboardWindow? _window;
        private DashboardViewModel? _viewModel;
        private bool _isInitialized = false;
        private readonly object _initLock = new object();

        public void Initialize()
        {
            if (_isInitialized) return;

            // Защита от параллельной инициализации из нескольких потоков
            lock (_initLock)
            {
                if (_isInitialized) return;

                // Всегда инициализируем в UI-потоке
                if (Application.Current == null) return;

                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(InitializeOnUi);
                }
                else
                {
                    InitializeOnUi();
                }
            }
        }

        private void InitializeOnUi()
        {
            if (_isInitialized) return;

            try
            {
                _viewModel = new DashboardViewModel();
                _window = new DashboardWindow
                {
                    DataContext = _viewModel,
                    Visibility = Visibility.Collapsed
                };

                // Подписываемся на закрытие окна: не закрывать, а скрывать
                _window.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    if (_window != null)
                        _window.Visibility = Visibility.Collapsed;
                    Debug.WriteLine("[Dashboard] Окно скрыто через Closing");
                };

                // Показываем окно (оно будет скрыто)
                _window.Show();

                _isInitialized = true;
                Debug.WriteLine("[Dashboard] Сервис инициализирован");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard] Ошибка инициализации: {ex.Message}");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null)
            {
                Initialize();
                if (_viewModel == null) return;
            }

            if (Application.Current == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                try { _viewModel.AddMessage(user, msg); }
                catch (Exception ex) { Debug.WriteLine($"[Dashboard] Ошибка AddMessage: {ex.Message}"); }
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try { _viewModel?.AddMessage(user, msg); }
                    catch (Exception ex) { Debug.WriteLine($"[Dashboard] Ошибка AddMessage: {ex.Message}"); }
                });
            }
        }

        public void Show()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (_window == null) return;

            if (Application.Current == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                ShowOnUi();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(ShowOnUi);
            }
        }

        private void ShowOnUi()
        {
            if (_window == null) return;

            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
            _window.InvalidateVisual();

            Debug.WriteLine("[Dashboard] Окно показано");
        }

        public void Hide()
        {
            if (_window == null) return;

            if (Application.Current == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                HideOnUi();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(HideOnUi);
            }
        }

        private void HideOnUi()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Collapsed;
            Debug.WriteLine("[Dashboard] Окно скрыто");
        }

        public void ClearMessages()
        {
            if (_viewModel == null) return;

            if (Application.Current == null) return;

            if (Application.Current.Dispatcher.CheckAccess())
            {
                try { _viewModel.ClearMessages(); }
                catch (Exception ex) { Debug.WriteLine($"[Dashboard] Ошибка ClearMessages: {ex.Message}"); }
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try { _viewModel?.ClearMessages(); }
                    catch (Exception ex) { Debug.WriteLine($"[Dashboard] Ошибка ClearMessages: {ex.Message}"); }
                });
            }
        }

        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;
    }
}