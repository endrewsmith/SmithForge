using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.InfoSystem;
using SmithForge.Main.Services;
using System;
using System.Diagnostics;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // ПОЛЯ РОТАЦИИ
        // ============================================================
        private InfoRotationService? _rotationService;

        [ObservableProperty]
        private int _rotationTotalPages = 0;

        [ObservableProperty]
        private string _rotationStatus = "⏹ Остановлена";

        [ObservableProperty]
        private int _rotationSilentSeconds = 0;

        [ObservableProperty]
        private int _rotationShownPages = 0;

        [ObservableProperty]
        private int _rotationSilenceInterval = 30;

        // ============================================================
        // КОМАНДЫ РОТАЦИИ
        // ============================================================
        [RelayCommand]
        private void RotationStart()
        {
            _rotationService?.Start(RotationSilenceInterval);
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationStop()
        {
            _rotationService?.Stop();
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationRefreshPages()
        {
            _rotationService?.RefreshPagesList();
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationActivity()
        {
            _rotationService?.OnUserActivity();
            UpdateRotationStatus();
        }

        // ============================================================
        // ЛОГИКА РОТАЦИИ
        // ============================================================
        private void OnRotationPageSelected(object? sender, string pageName)
        {
            try
            {
                var webServer = WebServerService.Instance;
                if (webServer != null && _infoService != null)
                {
                    var html = _infoService.Render(pageName, "system");
                    webServer.SendInfoMessage(html, pageName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Rotation] ❌ Ошибка: {ex.Message}");
            }
        }

        private void UpdateRotationStatus()
        {
            var status = _rotationService?.GetStatus();
            if (status == null) return;

            RotationTotalPages = status.TotalPages;
            RotationShownPages = status.ShownPages;
            RotationSilentSeconds = status.SilentSeconds;

            if (!status.IsRunning)
                RotationStatus = "⏹ Остановлена";
            else if (status.IsWaitingForSilence)
                RotationStatus = "📢 Показ страницы...";
            else
                RotationStatus = $"🔇 Тишина: {status.SilentSeconds}с / {status.SilenceIntervalSeconds}с";
        }

        public void NotifyUserActivity()
        {
            _rotationService?.OnUserActivity();
            UpdateRotationStatus();
        }

        partial void OnRotationSilenceIntervalChanged(int value)
        {
            _rotationService?.SetSilenceInterval(value);
        }
    }
}