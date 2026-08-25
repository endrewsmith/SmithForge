using SmithForge.Features.Dashboard;
using SmithForge.Main.Models;
using System;
using System.Windows;

namespace SmithForge.Main.Services
{
    public class DashboardService
    {
        private DashboardWindow? _window;
        private DashboardViewModel? _viewModel;
        private bool _isInitialized = false;

        public void Initialize()
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

                // ✅ Подписываемся на закрытие окна
                _window.Closing += (s, e) =>
                {
                    // Отменяем закрытие, просто прячем окно
                    e.Cancel = true;
                    _window.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine("[Dashboard] Окно скрыто через Closing");
                };

                // Показываем окно (оно будет скрыто)
                _window.Show();

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("[Dashboard] Сервис инициализирован");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Ошибка инициализации: {ex.Message}");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null)
            {
                // Если сервис не инициализирован, инициализируем
                Initialize();
                if (_viewModel == null) return;
            }

            _viewModel.AddMessage(user, msg);
        }

        public void Show()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            if (_window == null) return;

            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;

            // Обновляем список сообщений, если они есть
            _window.InvalidateVisual();

            System.Diagnostics.Debug.WriteLine("[Dashboard] Окно показано");
        }

        public void Hide()
        {
            if (_window == null) return;
            _window.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine("[Dashboard] Окно скрыто");
        }

        public void ClearMessages()
        {
            _viewModel?.ClearMessages();
        }

        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;
    }
}