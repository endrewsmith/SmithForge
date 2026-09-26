using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор переключения оверлеев и режимов отображения.
    /// </summary>
    public partial class OverlayTogglesCoordinator : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly OverlayManagerService _overlayManager;
        private readonly DashboardService _dashboardService;
        private readonly SmithForge.Features.MediaDashboard.MediaDashboardService _mediaDashboardService;
        private readonly TechOverlayService? _techOverlay;
        private readonly AppSettings _settings;

        // ============================================================
        // СВОЙСТВА
        // ============================================================
        [ObservableProperty]
        private bool _isOverlaySetupMode = true;

        [ObservableProperty]
        private bool _isOverlayHidden = false;

        [ObservableProperty]
        private bool _isStickersVisible = true;

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
        // СОБЫТИЯ
        // ============================================================
        public event EventHandler? PositionsSaved;

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public OverlayTogglesCoordinator(
            SettingsService settingsService,
            OverlayManagerService overlayManager,
            DashboardService dashboardService,
            SmithForge.Features.MediaDashboard.MediaDashboardService mediaDashboardService,
            TechOverlayService? techOverlay,
            AppSettings settings)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
            _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
            _mediaDashboardService = mediaDashboardService ?? throw new ArgumentNullException(nameof(mediaDashboardService));
            _techOverlay = techOverlay;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Загружаем начальные значения
            _isOverlaySetupMode = settings.IsOverlaySetupMode;
            _isOverlayHidden = settings.IsOverlayHidden;
            _isStickersVisible = settings.IsStickersVisible;
            _mainChatMode = settings.MainChatMode;
            _shortsChatMode = settings.ShortsChatMode;
            _importantChatMode = settings.ImportantChatMode;
            _stickersChatMode = settings.StickersChatMode;
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ С НАСТРОЙКАМИ
        // ============================================================
        partial void OnIsOverlaySetupModeChanged(bool oldValue, bool newValue)
        {
            _settingsService.SetOverlaySetupMode(newValue, () => PositionsSaved?.Invoke(this, EventArgs.Empty));
        }

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
        // КОМАНДЫ ПЕРЕКЛЮЧЕНИЯ
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
            _techOverlay?.Toggle();
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
        public void SaveAllPositions()
        {
            _overlayManager.SaveAllPositions(_settings);
        }
    }
}