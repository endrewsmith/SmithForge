using System;
using System.Diagnostics;
using System.Windows;
using SmithForge.ChatEngine.Models;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services;

/// <summary>
/// Сервис управления настройками приложения
/// </summary>
public class SettingsService
{
    private readonly AppSettings _settings;
    private readonly OverlayManagerService _overlayManager;

    public event Action<ImportantPlaybackMode>? ImportantPlaybackModeChanged;
    public event Action<int>? ImportantQueueCountChanged;

    public SettingsService(AppSettings settings, OverlayManagerService overlayManager)
    {
        _settings = settings;
        _overlayManager = overlayManager;
    }

    // =====================================================
    // YOUTUBE НАСТРОЙКИ
    // =====================================================

    public void SetYouTubeApiKey(string value)
    {
        _settings.YouTube ??= new YouTubeSettings();
        _settings.YouTube.ApiKey = value;
        ConfigService.Save(_settings);
        Debug.WriteLine($"[Settings] YouTube API Key обновлен");
    }

    public void SetYouTubeChannelId(string value)
    {
        _settings.YouTube ??= new YouTubeSettings();
        _settings.YouTube.ChannelId = value;
        ConfigService.Save(_settings);
        Debug.WriteLine($"[Settings] YouTube Channel ID обновлен");
    }

    public void SetYouTubeVideoId(string value)
    {
        _settings.YouTube ??= new YouTubeSettings();
        _settings.YouTube.LastVideoId = value;
        ConfigService.Save(_settings);
        Debug.WriteLine($"[Settings] YouTube Video ID обновлен");
    }

    // =====================================================
    // ВАЖНЫЕ НАСТРОЙКИ
    // =====================================================

    public void SetImportantPlaybackMode(ImportantPlaybackMode value)
    {
        _settings.ImportantPlaybackMode = value;
        ConfigService.Save(_settings);
        Debug.WriteLine($"[Settings] Режим: {(value == ImportantPlaybackMode.Auto ? "АВТО" : "РУЧНОЙ")}");
        ImportantPlaybackModeChanged?.Invoke(value);
    }

    public void SetImportantPlaybackHotkey(string value)
    {
        _settings.ImportantPlaybackHotkey = value;
        ConfigService.Save(_settings);
        Debug.WriteLine($"[Settings] Горячая клавиша: {value}");
    }

    public void SetIsAutoSwitchingEnabled(bool value, int importantQueueCount, ImportantPlaybackMode currentMode)
    {
        Debug.WriteLine($"[Settings] Режим чтения: {(value ? "ВКЛ" : "ВЫКЛ")}");

        _overlayManager.IsAutoSwitchingEnabled = value;

        if (value)
        {
            if (importantQueueCount > 0 && currentMode == ImportantPlaybackMode.Auto)
            {
                SetImportantPlaybackMode(ImportantPlaybackMode.Manual);
                Debug.WriteLine("[Settings] Есть сообщения в очереди, переключено в РУЧНОЙ режим");
            }
            else if (importantQueueCount == 0 && currentMode == ImportantPlaybackMode.Manual)
            {
                SetImportantPlaybackMode(ImportantPlaybackMode.Auto);
                Debug.WriteLine("[Settings] Очередь пуста, переключено в АВТО режим");
            }
        }
    }

    public void UpdateImportantQueueCount(int count, bool isAutoSwitchingEnabled, ImportantPlaybackMode currentMode, Action<int, ImportantPlaybackMode> onStateChanged)
    {
        try
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Debug.WriteLine($"[Settings] UpdateImportantQueueCount: перенаправление в UI поток");
                Application.Current.Dispatcher.BeginInvoke(() => UpdateImportantQueueCount(count, isAutoSwitchingEnabled, currentMode, onStateChanged));
                return;
            }

            ImportantQueueCountChanged?.Invoke(count);

            Debug.WriteLine($"[Settings] ==============================================");
            Debug.WriteLine($"[Settings] UpdateImportantQueueCount ВЫЗВАН!");
            Debug.WriteLine($"[Settings] count = {count}");
            Debug.WriteLine($"[Settings] IsAutoSwitchingEnabled = {isAutoSwitchingEnabled}");
            Debug.WriteLine($"[Settings] ImportantPlaybackMode (до) = {currentMode}");

            if (isAutoSwitchingEnabled)
            {
                if (count > 0 && currentMode == ImportantPlaybackMode.Auto)
                {
                    SetImportantPlaybackMode(ImportantPlaybackMode.Manual);
                    onStateChanged(count, ImportantPlaybackMode.Manual);
                }
                else if (count == 0 && currentMode == ImportantPlaybackMode.Manual)
                {
                    SetImportantPlaybackMode(ImportantPlaybackMode.Auto);
                    onStateChanged(count, ImportantPlaybackMode.Auto);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Settings] Ошибка: {ex.Message}");
        }
    }

    // =====================================================
    // ЗВУКОВЫЕ НАСТРОЙКИ
    // =====================================================

    public void SetStickerDisplayTime(int value)
    {
        _settings.StickerDisplayTimeMs = value;
        _overlayManager.StickerDisplayTime = value;
        ConfigService.Save(_settings);
    }

    public void SetImportantSoundVolume(int value)
    {
        _settings.ImportantSoundVolume = value;
        ConfigService.Save(_settings);
        VoiceService.SetImportantSoundVolume(value);
        Debug.WriteLine($"[Settings] Громкость важных сообщений: {value}%");
    }

    public void SetVoiceVolume(int value)
    {
        _settings.VoiceVolume = value;
        ConfigService.Save(_settings);
        VoiceService.SetVoiceVolume(value);
        Debug.WriteLine($"[Settings] Громкость голоса: {value}%");
    }

    // =====================================================
    // РЕЖИМЫ ОТОБРАЖЕНИЯ
    // =====================================================

    public void SetMainChatMode(ChatDisplayMode value)
    {
        _settings.MainChatMode = value;
        _overlayManager.SetMainMode(value);
        ConfigService.Save(_settings);
    }

    public void SetShortsChatMode(ChatDisplayMode value)
    {
        _settings.ShortsChatMode = value;
        _overlayManager.SetShortsMode(value);
        ConfigService.Save(_settings);
    }

    public void SetImportantChatMode(ChatDisplayMode value)
    {
        _settings.ImportantChatMode = value;
        _overlayManager.SetImportantMode(value);
        ConfigService.Save(_settings);
    }

    public void SetStickersChatMode(ChatDisplayMode value)
    {
        _settings.StickersChatMode = value;
        _overlayManager.SetStickersMode(value);
        ConfigService.Save(_settings);
    }

    // =====================================================
    // НАСТРОЙКИ ОВЕРЛЕЯ
    // =====================================================

    public void SetOverlaySetupMode(bool value, Action onSavePositions)
    {
        _settings.IsOverlaySetupMode = value;
        _overlayManager.SetSetupMode(value);

        if (!value)
        {
            _overlayManager.SaveAllPositions(_settings);
            ConfigService.Save(_settings);
            onSavePositions();
        }
    }

    public void SetOverlayHidden(bool value)
    {
        _settings.IsOverlayHidden = value;
        _overlayManager.SetHidden(value);
        ConfigService.Save(_settings);
    }

    public void SetStickersVisible(bool value)
    {
        _settings.IsStickersVisible = value;
        _overlayManager.SetStickersVisible(value);
        ConfigService.Save(_settings);
    }

    // =====================================================
    // ОБЩИЕ МЕТОДЫ
    // =====================================================

    public void SaveSettings() => ConfigService.Save(_settings);

    public void SetProgramPath(string value)
    {
        _settings.ProgramPath = value;
        ConfigService.Save(_settings);
    }
}