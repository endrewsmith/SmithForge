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
            //// === Главный оверлей ===
            //_overlay = new ChatOverlayService();
            //_overlay.Initialize(_settings.OverlayTop, _settings.OverlayLeft);
            //_overlay.SetSetupMode(isSetupMode);
            //_overlay.SetDisplayMode(mainMode);
            //_overlay.LoadPosition(_settings);

            //// === Shorts оверлей ===
            //_shorts = new ChatOverlayShortsService();
            //_shorts.Initialize(
            //    _settings.ShortsWindowTop,
            //    _settings.ShortsWindowLeft,
            //    _settings.ShortsWindowWidth,
            //    _settings.ShortsWindowHeight,
            //    isSetupMode);
            //_shorts.SetSetupMode(isSetupMode);
            //_shorts.SetDisplayMode(shortsMode);
            //_shorts.LoadPosition(_settings);

            //// === Important оверлей ===
            //_important = new ImportantOverlayService(_settings);
            //_important.IsAutoSwitchingEnabled = true;
            //_important.Initialize(
            //    _settings.ImportantOverlayTop,
            //    _settings.ImportantOverlayLeft,
            //    _settings.ImportantOverlayWidth,
            //    _settings.ImportantOverlayHeight,
            //    isSetupMode);
            //_important.SetSetupMode(isSetupMode);
            //_important.SetDisplayMode(importantMode);
            //_important.LoadPosition(_settings);
            //_important.QueueCountChanged += (s, count) =>
            //{
            //    ImportantQueueCount = count;
            //    ImportantQueueChanged?.Invoke(this, count);
            //};

            //// === Stickers оверлей ===
            //_stickers = new StickersOverlayService();
            //_stickers.Initialize(
            //    _settings.StickersWindowTop,
            //    _settings.StickersWindowLeft,
            //    _settings.StickersWindowWidth,
            //    _settings.StickersWindowHeight,
            //    isSetupMode);
            //_stickers.SetSetupMode(isSetupMode);
            //_stickers.SetDisplayMode(stickersMode);
            //_stickers.LoadPosition(_settings);
            //_stickers.SetDisplayTime(stickerDisplayTime);

            //// === ⭐ Alerts оверлей (НОВЫЙ) ===
            //_alerts = new AlertsOverlayService();
            //_alerts.Initialize(
            //    _settings.AlertsOverlayTop,
            //    _settings.AlertsOverlayLeft,
            //    _settings.AlertsOverlayWidth,
            //    _settings.AlertsOverlayHeight,
            //    isSetupMode);
            //_alerts.SetSetupMode(isSetupMode);
            //_alerts.SetAlertDuration(_settings.AlertsAlertDuration);
            //_alerts.LoadPosition(_settings);

            //// Применяем скрытие
            //if (isOverlayHidden)
            //{
            //    SetHidden(true);
            //}

            //// Применяем видимость стикеров
            //if (isStickersVisible)
            //{
            //    _stickers.Show();
            //}

            //// Применяем видимость алертов
            //_alerts.SetVisible(_settings.AlertsOverlayVisible);

            // Сохраняем настройки звука и стикеров
            ImportantSoundVolume = importantSoundVolume;
            VoiceVolume = voiceVolume;
            VoiceService.SetImportantSoundVolume(importantSoundVolume);
            VoiceService.SetVoiceVolume(voiceVolume);
            StickerDisplayTime = stickerDisplayTime;

            Debug.WriteLine("[OverlayManager] Все оверлеи инициализированы");
        }

        // =====================================================
        // СВОЙСТВА / СОБЫТИЯ
        // =====================================================

        public int ImportantQueueCount { get; private set; }
        public int ImportantSoundVolume { get; private set; }
        public int VoiceVolume { get; private set; }
        public int StickerDisplayTime { get; set; }
        public bool IsAutoSwitchingEnabled { get; set; }
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