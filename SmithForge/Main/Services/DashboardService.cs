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
        private bool _isClosed = false;  // ← Добавлен флаг

        public void Initialize()
        {
            if (_isInitialized && !_isClosed) return;

            try
            {
                _viewModel = new DashboardViewModel();
                _window = new DashboardWindow
                {
                    DataContext = _viewModel,
                    Visibility = Visibility.Collapsed // Окно скрыто по умолчанию
                };

                // ✅ Подписываемся на закрытие окна
                _window.Closed += (s, e) =>
                {
                    _isClosed = true;
                    _window = null;
                };

                _isInitialized = true;
                _isClosed = false;
                System.Diagnostics.Debug.WriteLine("[Dashboard] Сервис инициализирован");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Ошибка инициализации: {ex.Message}");
            }
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            if (!_isInitialized || _viewModel == null) return;
            _viewModel.AddMessage(user, msg);
        }

        public void Show()
        {
            // ✅ Если окно было закрыто — пересоздаём
            if (_isClosed || _window == null)
            {
                Initialize();
            }

            if (_window == null) return;

            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
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

        public bool IsVisible => _window?.Visibility == Visibility.Visible;
    }
}