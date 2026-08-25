using System;
using System.Diagnostics;
using System.Windows;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.Features.ChatOverlay;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Features.ImportantOverlay;
using SmithForge.Features.StickersOverlay;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services;

/// <summary>
/// Сервис управления оверлеями чата
/// </summary>
public class OverlayManagerService
{
    private ChatOverlayService? _overlay;
    private ChatOverlayShortsService? _shorts;
    private ImportantOverlayService? _important;
    private StickersOverlayService? _stickers;
    private readonly AppSettings _settings;

    public OverlayManagerService(AppSettings settings)
    {
        _settings = settings;
    }

    public void Initialize(
        bool isSetupMode,
        bool isOverlayHidden,
        bool isStickersVisible,
        ChatDisplayMode mainMode,
        ChatDisplayMode shortsMode,
        ChatDisplayMode importantMode,
        ChatDisplayMode stickersMode,
        ImportantPlaybackMode importantModePlayback,
        int importantSoundVolume,
        int voiceVolume,
        int stickerDisplayTime)
    {
        // Главный оверлей
        _overlay = new ChatOverlayService();
        _overlay.Initialize(_settings.OverlayTop, _settings.OverlayLeft);
        _overlay.SetSetupMode(isSetupMode);
        _overlay.SetDisplayMode(mainMode);
        _overlay.LoadPosition(_settings);

        // Shorts оверлей
        _shorts = new ChatOverlayShortsService();
        _shorts.Initialize(
            _settings.ShortsWindowTop,
            _settings.ShortsWindowLeft,
            _settings.ShortsWindowWidth,
            _settings.ShortsWindowHeight,
            isSetupMode);
        _shorts.SetSetupMode(isSetupMode);
        _shorts.SetDisplayMode(shortsMode);
        _shorts.LoadPosition(_settings);

        // Important оверлей
        _important = new ImportantOverlayService(_settings);
        _important.IsAutoSwitchingEnabled = true;
        _important.Initialize(
            _settings.ImportantOverlayTop,
            _settings.ImportantOverlayLeft,
            _settings.ImportantOverlayWidth,
            _settings.ImportantOverlayHeight,
            isSetupMode);
        _important.SetSetupMode(isSetupMode);
        _important.SetDisplayMode(importantMode);
        _important.LoadPosition(_settings);
        _important.QueueCountChanged += (s, count) =>
        {
            ImportantQueueCount = count;
            ImportantQueueChanged?.Invoke(this, count);
        };

        // Stickers оверлей
        _stickers = new StickersOverlayService();
        _stickers.Initialize(
            _settings.StickersWindowTop,
            _settings.StickersWindowLeft,
            _settings.StickersWindowWidth,
            _settings.StickersWindowHeight,
            isSetupMode);
        _stickers.SetSetupMode(isSetupMode);
        _stickers.SetDisplayMode(stickersMode);
        _stickers.LoadPosition(_settings);

        // Применение скрытия
        if (isOverlayHidden)
        {
            SetHidden(true);
        }

        // Применение видимости стикеров
        if (isStickersVisible)
        {
            _stickers.Show();
        }

        ImportantSoundVolume = importantSoundVolume;
        VoiceVolume = voiceVolume;
        VoiceService.SetImportantSoundVolume(importantSoundVolume);
        VoiceService.SetVoiceVolume(voiceVolume);
        StickerDisplayTime = stickerDisplayTime;
    }

    public int ImportantQueueCount { get; private set; }
    public int ImportantSoundVolume { get; private set; }
    public int VoiceVolume { get; private set; }
    public int StickerDisplayTime { get; set; }
    public bool IsAutoSwitchingEnabled { get; set; }
    public bool IsPlaying => _important?.IsPlaying == true;
    public int QueueSize => _important?.QueueSize ?? 0;

    public event EventHandler<int>? ImportantQueueChanged;

    // =====================================================
    // УПРАВЛЕНИЕ ОЧЕРЕДЬЮ
    // =====================================================

    public async Task PlayNextFromQueueAsync()
    {
        if (_important != null)
            await _important.PlayNextFromQueueAsync();
        else
            await Task.CompletedTask;
    }

    // =====================================================
    // УПРАВЛЕНИЕ РЕЖИМАМИ
    // =====================================================

    public void SetMainMode(ChatDisplayMode mode) => _overlay?.SetDisplayMode(mode);
    public void SetShortsMode(ChatDisplayMode mode) => _shorts?.SetDisplayMode(mode);
    public void SetImportantMode(ChatDisplayMode mode) => _important?.SetDisplayMode(mode);
    public void SetStickersMode(ChatDisplayMode mode) => _stickers?.SetDisplayMode(mode);

    public void SetSetupMode(bool isSetupMode)
    {
        _overlay?.SetSetupMode(isSetupMode);
        _shorts?.SetSetupMode(isSetupMode);
        _important?.SetSetupMode(isSetupMode);
        _stickers?.SetSetupMode(isSetupMode);
    }

    public void SetHidden(bool isHidden)
    {
        _overlay?.SetHidden(isHidden);
        _shorts?.SetHidden(isHidden);
        _important?.SetHidden(isHidden);
        _stickers?.SetHidden(isHidden);
    }

    public void SetStickersVisible(bool isVisible)
    {
        if (isVisible)
            _stickers?.Show();
        else
            _stickers?.Hide();
    }

    // =====================================================
    // ДОБАВЛЕНИЕ СООБЩЕНИЙ
    // =====================================================

    public void AddMessage(Chater user, CommonMessage msg)
    {
        // Отправляем параллельно
        Parallel.Invoke(
            () => _overlay?.AddMessage(user, msg),
            () => _shorts?.AddMessage(user, msg)
        );
    }

    public void AddImportantMessage(Chater user, CommonMessage msg)
    {
        _important?.ShowImportantMessage(user, msg);
    }

    public void AddStickerMessage(Chater user, CommonMessage msg)
    {
        _stickers?.ShowSticker(user, msg);
    }

    // =====================================================
    // СОХРАНЕНИЕ ПОЗИЦИЙ
    // =====================================================

    public void SaveAllPositions(AppSettings settings)
    {
        _overlay?.SavePosition(settings);
        _shorts?.SavePosition(settings);
        _important?.SavePosition(settings);
        _stickers?.SavePosition(settings);
    }

    // =====================================================
    // TOGGLE
    // =====================================================

    public void ToggleShorts() => _shorts?.Toggle();
    public void ToggleImportant()
    {
        if (_important?.IsVisible == true)
            _important?.Hide();
        else
            _important?.Show();
    }
}