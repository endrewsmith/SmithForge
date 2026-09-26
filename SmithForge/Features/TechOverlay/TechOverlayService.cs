using SmithForge.Main.Services;
using System;
using System.Windows;

namespace SmithForge.Features.TechOverlay
{
    /// <summary>
    /// Фича: технический оверлей.
    ///
    /// Раздаёт события в два места:
    /// 1. WPF-окно (локально у стримера) — для контроля.
    /// 2. HTML-страница /tech (для OBS) — через WebServerService SSE.
    /// </summary>
    public class TechOverlayService
    {
        private readonly WebServerService? _webServer;
        private TechOverlayWindow? _window;

        public TechOverlayService(WebServerService? webServer)
        {
            _webServer = webServer;

            // ✅ Подписываемся на события, которые шлёт WebServerService
            // (команды вызывают SendTechnicalEvent напрямую, а мы хотим их видеть в WPF-окне)
            if (_webServer != null)
                _webServer.TechnicalEventSent += OnTechnicalEventSent;

            CreateWindow();
        }

        private void OnTechnicalEventSent(object? sender, TechEvent evt)
        {
            // Проверка: не наши ли это уже отправленные? Мы не шлём через этот путь, только принимаем.
            _window?.ViewModel.AddEvent(evt);
        }

        private void CreateWindow()
        {
            if (_window != null) return;

            if (Application.Current == null)
            {
                System.Diagnostics.Debug.WriteLine("[TechOverlay] Application.Current == null, окно не создано");
                return;
            }

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(CreateWindow);
                return;
            }

            _window = new TechOverlayWindow
            {
                Visibility = Visibility.Collapsed,
                Top = 100,
                Left = 100
            };

            // Показать окно (сразу скрытым через Closing-хук не сработает, окно нужно сначала показать)
            _window.Show();
            _window.Hide();

            System.Diagnostics.Debug.WriteLine("[TechOverlay] WPF-окно создано");
        }

        /// <summary>
        /// Отправить событие в оверлей (WPF + HTML).
        /// </summary>
        public void Emit(TechEvent evt)
        {
            if (evt == null) return;

            // Отправляем через WebServerService — оттуда событие вернётся
            // в OnTechnicalEventSent и попадёт в WPF-окно.
            _webServer?.SendTechnicalEvent(evt);
        }

        public void Emit(Func<TechEvent> factory)
        {
            if (factory == null) return;
            Emit(factory());
        }

        /// <summary>
        /// Показать / скрыть WPF-окно.
        /// </summary>
        public void Show()
        {
            if (_window == null) CreateWindow();
            if (_window == null) return;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Show);
                return;
            }

            _window.Visibility = Visibility.Visible;
            _window.Topmost = true;
        }

        public void Hide()
        {
            if (_window == null) return;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Hide);
                return;
            }

            _window.Visibility = Visibility.Collapsed;
        }

        public void Toggle()
        {
            if (_window == null) CreateWindow();
            if (_window == null) return;
            if (_window.Visibility == Visibility.Visible)
                Hide();
            else
                Show();
        }

        public bool IsVisible => _window?.Visibility == Visibility.Visible;

        public void Clear()
        {
            _window?.ViewModel.Clear();
        }
    }
}