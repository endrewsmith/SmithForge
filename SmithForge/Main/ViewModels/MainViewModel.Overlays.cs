using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Main.Services;
using System.Collections.Generic;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // ПОЛЯ ОВЕРЛЕЕВ
        // ============================================================
        [ObservableProperty]
        private bool _isOverlaySetupMode = true;

        [ObservableProperty]
        private bool _isOverlayHidden = false;

        [ObservableProperty]
        private bool _isStickersVisible = true;

        // ============================================================
        // РЕЖИМЫ ОТОБРАЖЕНИЯ ЧАТОВ
        // ============================================================
        [ObservableProperty]
        private ChatDisplayMode _mainChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _shortsChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _importantChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _stickersChatMode = ChatDisplayMode.AppearAndFade;

        public List<SmithForge.Main.Models.ChatDisplayModeInfo> AvailableModes { get; } = ChatDisplayModeFactory.GetAvailableModes();

        // ============================================================
        // СИНХРОНИЗАЦИЯ С НАСТРОЙКАМИ
        // ============================================================
        partial void OnIsOverlaySetupModeChanged(bool oldValue, bool newValue)
            => _settingsService.SetOverlaySetupMode(newValue, () => LastMessageText = "✅ Позиции окон сохранены");

        partial void OnIsOverlayHiddenChanged(bool oldValue, bool newValue)
            => _settingsService.SetOverlayHidden(newValue);

        partial void OnIsStickersVisibleChanged(bool oldValue, bool newValue)
            => _settingsService.SetStickersVisible(newValue);

        partial void OnMainChatModeChanged(ChatDisplayMode value)
            => _settingsService.SetMainChatMode(value);

        partial void OnShortsChatModeChanged(ChatDisplayMode value)
            => _settingsService.SetShortsChatMode(value);

        partial void OnImportantChatModeChanged(ChatDisplayMode value)
            => _settingsService.SetImportantChatMode(value);

        partial void OnStickersChatModeChanged(ChatDisplayMode value)
            => _settingsService.SetStickersChatMode(value);

        // ============================================================
        // КОМАНДЫ ПЕРЕКЛЮЧЕНИЯ ОВЕРЛЕЕВ
        // ============================================================
        [RelayCommand]
        private void ToggleDashboard()
        {
            _dashboardService.Initialize();

            if (_dashboardService.IsVisible)
                _dashboardService.Hide();
            else
                _dashboardService.Show();
        }

        [RelayCommand]
        private void ToggleMediaDashboard()
        {
            _mediaDashboardService.Initialize();

            if (_mediaDashboardService.IsVisible)
                _mediaDashboardService.Hide();
            else
                _mediaDashboardService.Show();
        }

        [RelayCommand]
        private void ToggleTechOverlay()
        {
            TechOverlay?.Toggle();
        }

        [RelayCommand]
        private void ToggleShortsOverlay()
        {
            _overlayManager.ToggleShorts();
        }

        [RelayCommand]
        private void ToggleImportantOverlay()
        {
            _overlayManager.ToggleImportant();
        }

        [RelayCommand]
        private void ToggleStickersOverlay()
        {
            IsStickersVisible = !IsStickersVisible;
        }

        // ============================================================
        // СОХРАНЕНИЕ ПОЗИЦИЙ
        // ============================================================
        public void SaveOverlayPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveShortsPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveImportantPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveStickersPosition() => _overlayManager.SaveAllPositions(Settings);
    }
}