using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Services;
using System;
using System.Diagnostics;

namespace SmithForge.Features.InfoSystem
{
    /// <summary>
    /// Координатор ротации информационных страниц.
    /// Управляет InfoRotationService и предоставляет UI-свойства для привязки.
    /// </summary>
    public partial class InfoRotationCoordinator : ObservableObject
    {
        private readonly InfoRotationService _rotationService;
        private readonly InfoService _infoService;
        private readonly WebServerService? _webServer;

        // ============================================================
        // СВОЙСТВА ДЛЯ UI
        // ============================================================
        [ObservableProperty]
        private int _totalPages = 0;

        [ObservableProperty]
        private string _status = "⏹ Остановлена";

        [ObservableProperty]
        private int _silentSeconds = 0;

        [ObservableProperty]
        private int _shownPages = 0;

        [ObservableProperty]
        private int _silenceInterval = 30;

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public InfoRotationCoordinator(
            InfoRotationService rotationService,
            InfoService infoService,
            WebServerService? webServer)
        {
            _rotationService = rotationService ?? throw new ArgumentNullException(nameof(rotationService));
            _infoService = infoService ?? throw new ArgumentNullException(nameof(infoService));
            _webServer = webServer;

            // Подписываемся на событие выбора страницы
            _rotationService.PageSelected += OnPageSelected;
        }

        // ============================================================
        // СВОЙСТВО ИЗМЕНИЛОСЬ — ПРИМЕНЯЕМ К СЕРВИСУ
        // ============================================================
        partial void OnSilenceIntervalChanged(int value)
        {
            _rotationService.SetSilenceInterval(value);
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void Start()
        {
            _rotationService.Start(SilenceInterval);
            UpdateStatus();
        }

        [RelayCommand]
        private void Stop()
        {
            _rotationService.Stop();
            UpdateStatus();
        }

        [RelayCommand]
        private void RefreshPages()
        {
            _rotationService.RefreshPagesList();
            UpdateStatus();
        }

        [RelayCommand]
        private void NotifyActivity()
        {
            _rotationService.OnUserActivity();
            UpdateStatus();
        }

        // ============================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ MainViewModel
        // ============================================================
        public void NotifyUserActivity()
        {
            _rotationService.OnUserActivity();
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            var status = _rotationService.GetStatus();
            if (status == null) return;

            TotalPages = status.TotalPages;
            ShownPages = status.ShownPages;
            SilentSeconds = status.SilentSeconds;

            if (!status.IsRunning)
                Status = "⏹ Остановлена";
            else if (status.IsWaitingForSilence)
                Status = "📢 Показ страницы...";
            else
                Status = $"🔇 Тишина: {status.SilentSeconds}с / {status.SilenceIntervalSeconds}с";
        }

        // ============================================================
        // ОБРАБОТЧИК СОБЫТИЯ ВЫБОРА СТРАНИЦЫ
        // ============================================================
        private void OnPageSelected(object? sender, string pageName)
        {
            try
            {
                if (_webServer != null && _infoService != null)
                {
                    var html = _infoService.Render(pageName, "system");
                    _webServer.SendInfoMessage(html, pageName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoRotationCoordinator] ❌ Ошибка: {ex.Message}");
            }
        }
    }
}