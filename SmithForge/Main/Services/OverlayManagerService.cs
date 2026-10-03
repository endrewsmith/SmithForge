using SmithForge.AlertsEngine.Core.Models;
using SmithForge.Features.AlertsOverlay;
using SmithForge.Features.ChatOverlay;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Features.ImportantOverlay;
using SmithForge.Features.StickersOverlay;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Main.Services
{
    public class OverlayManagerService
    {
        private ChatOverlayService? _overlay;
        private ChatOverlayShortsService? _shorts;
        private ImportantOverlayService? _important;
        private StickersOverlayService? _stickers;
        private AlertsOverlayService? _alerts;                     // ← НОВОЕ
        private readonly AppSettings _settings;

        public OverlayManagerService(AppSettings settings)
        {
            _settings = settings;
        }

        // =====================================================
        // ИНИЦИАЛИЗАЦИЯ
        // =====================================================

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
            // === Important оверлей ===
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

            // Сохраняем настройки звука и стикеров
            ImportantSoundVolume = importantSoundVolume;
            VoiceVolume = voiceVolume;
            VoiceService.SetImportantSoundVolume(importantSoundVolume);
            VoiceService.SetVoiceVolume(voiceVolume);
            StickerDisplayTime = stickerDisplayTime;

            Debug.WriteLine("[OverlayManager] Important оверлей инициализирован");
        }

        // =====================================================
        // СВОЙСТВА / СОБЫТИЯ
        // =====================================================

        public int ImportantQueueCount { get; private set; }
        public int ImportantSoundVolume { get; private set; }
        public int VoiceVolume { get; private set; }
        public int StickerDisplayTime { get; set; }
        private bool _isAutoSwitchingEnabled = true;
        public bool IsAutoSwitchingEnabled
        {
            get => _isAutoSwitchingEnabled;
            set
            {
                _isAutoSwitchingEnabled = value;
                if (_important != null)
                {
                    _important.IsAutoSwitchingEnabled = value;
                }
                Debug.WriteLine($"[OverlayManager] IsAutoSwitchingEnabled = {value}");
            }
        }
        public bool IsPlaying => _important?.IsPlaying == true;
        public int QueueSize => _important?.QueueSize ?? 0;

        public event EventHandler<int>? ImportantQueueChanged;

        // =====================================================
        // УПРАВЛЕНИЕ ОЧЕРЕДЬЮ ВАЖНЫХ
        // =====================================================

        public async Task PlayNextFromQueueAsync()
        {
            if (_important != null)
                await _important.PlayNextFromQueueAsync();
            else
                await Task.CompletedTask;
        }
        public void TryResumeAutoPlayback()
        {
            _important?.TryResumeAutoPlayback();
        }
        // =====================================================
        // УПРАВЛЕНИЕ РЕЖИМАМИ ОТОБРАЖЕНИЯ
        // =====================================================

        public void SetMainMode(ChatDisplayMode mode) => _overlay?.SetDisplayMode(mode);
        public void SetShortsMode(ChatDisplayMode mode) => _shorts?.SetDisplayMode(mode);
        public void SetImportantMode(ChatDisplayMode mode) => _important?.SetDisplayMode(mode);
        public void SetStickersMode(ChatDisplayMode mode) => _stickers?.SetDisplayMode(mode);

        // =====================================================
        // SETUP MODE (перетаскивание всех оверлеев)
        // =====================================================

        public void SetSetupMode(bool isSetupMode)
        {
            _overlay?.SetSetupMode(isSetupMode);
            _shorts?.SetSetupMode(isSetupMode);
            _important?.SetSetupMode(isSetupMode);
            _stickers?.SetSetupMode(isSetupMode);
            _alerts?.SetSetupMode(isSetupMode);
        }

        // =====================================================
        // HIDDEN (спрятать все окна за экран)
        // =====================================================

        public void SetHidden(bool isHidden)
        {
            _overlay?.SetHidden(isHidden);
            _shorts?.SetHidden(isHidden);
            _important?.SetHidden(isHidden);
            _stickers?.SetHidden(isHidden);
            _alerts?.SetHidden(isHidden);
        }

        // =====================================================
        // ВИДИМОСТЬ СТИКЕРОВ
        // =====================================================

        public void SetStickersVisible(bool isVisible)
        {
            if (isVisible)
                _stickers?.Show();
            else
                _stickers?.Hide();
        }

        // =====================================================
        // ⭐ ВИДИМОСТЬ АЛЕРТОВ (НОВОЕ)
        // =====================================================

        public void SetAlertsVisible(bool isVisible)
        {
            _alerts?.SetVisible(isVisible);
            Debug.WriteLine($"[OverlayManager] Alerts Visible: {isVisible}");
        }

        /// <summary>
        /// Установить длительность показа алертов (в секундах)
        /// </summary>
        public void SetAlertsDuration(int seconds)
        {
            _alerts?.SetAlertDuration(seconds);
            Debug.WriteLine($"[OverlayManager] Alerts Duration: {seconds} сек.");
        }

        /// <summary>
        /// ⭐ ГЛАВНЫЙ МЕТОД — показать алерт от любого провайдера
        /// </summary>
        public void ShowAlert(IncomingAlert alert)
        {
            if (alert == null)
            {
                Debug.WriteLine("[OverlayManager] ShowAlert: alert = null");
                return;
            }

            Debug.WriteLine($"[OverlayManager] ShowAlert: [{alert.ProviderType}] {alert.DisplayText}");
            _alerts?.ShowAlert(alert);
        }

        /// <summary>
        /// Очистить очередь алертов
        /// </summary>
        public void ClearAlerts()
        {
            _alerts?.ClearAll();
        }

        // =====================================================
        // ДОБАВЛЕНИЕ СООБЩЕНИЙ (чат)
        // =====================================================

        public void AddMessage(Chater user, CommonMessage msg)
        {
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
            _alerts?.SavePosition(settings);
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
}