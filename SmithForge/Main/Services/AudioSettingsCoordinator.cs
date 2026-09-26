using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор настроек голоса, звука и скоростей отображения.
    /// Делегирует сохранение в SettingsService, а HTTP-запросы скоростей — напрямую.
    /// </summary>
    public partial class AudioSettingsCoordinator : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly AppSettings _settings;

        // ============================================================
        // СВОЙСТВА
        // ============================================================
        [ObservableProperty]
        private int _voiceRate = 3;

        [ObservableProperty]
        private int _voiceVolume = 100;

        [ObservableProperty]
        private int _importantSoundVolume = 100;

        [ObservableProperty]
        private int _stickerDisplayTime = 5000;

        [ObservableProperty]
        private int _scrollSpeed = 1500;

        [ObservableProperty]
        private double _appearSpeed = 0.3;

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public AudioSettingsCoordinator(SettingsService settingsService, AppSettings settings)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Загружаем начальные значения из настроек
            _voiceRate = settings.VoiceRate;
            _voiceVolume = settings.VoiceVolume;
            _importantSoundVolume = settings.ImportantSoundVolume;
            _stickerDisplayTime = settings.StickerDisplayTimeMs;

            // Применяем к VoiceService
            VoiceService.SetVoiceRate(_voiceRate);
            VoiceService.SetVoiceVolume(_voiceVolume);
            VoiceService.SetImportantSoundVolume(_importantSoundVolume);

            Debug.WriteLine($"🎙️ [AudioSettings] Инициализирован: voiceRate={_voiceRate}, voiceVolume={_voiceVolume}");
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void SetVoiceRate(object? parameter)
        {
            if (parameter == null) return;

            int value = parameter switch
            {
                int i => i,
                string s when int.TryParse(s, out var p) => p,
                double d => (int)d,
                _ => 3
            };

            VoiceRate = Math.Clamp(value, -10, 10);
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ СО СЕРВИСОМ НАСТРОЕК
        // ============================================================
        partial void OnVoiceRateChanged(int value)
        {
            _settingsService.SetVoiceRate(value);
            Debug.WriteLine($"🎙️ [AudioSettings] Скорость голоса: {value}, сервис={VoiceService.GetVoiceRate()}");
        }

        partial void OnVoiceVolumeChanged(int value)
            => _settingsService.SetVoiceVolume(value);

        partial void OnImportantSoundVolumeChanged(int value)
            => _settingsService.SetImportantSoundVolume(value);

        partial void OnStickerDisplayTimeChanged(int value)
            => _settingsService.SetStickerDisplayTime(value);

        // ============================================================
        // СКОРОСТЬ СКРОЛЛА
        // ============================================================
        partial void OnScrollSpeedChanged(int value)
        {
            _ = SendScrollSpeed(value);
        }

        private async Task SendScrollSpeed(int speed)
        {
            try
            {
                using var client = new HttpClient();
                var json = $"{{\"speed\":{speed}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"http://localhost:{_settings.NetworkPort}/info/scroll/speed", content);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[InfoChat] Скорость скролла отправлена: {speed}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoChat] Ошибка отправки скорости: {ex.Message}");
            }
        }

        // ============================================================
        // СКОРОСТЬ ПОЯВЛЕНИЯ
        // ============================================================
        partial void OnAppearSpeedChanged(double value)
        {
            _ = SendAppearSpeed(value);
        }

        private async Task SendAppearSpeed(double speed)
        {
            try
            {
                using var client = new HttpClient();
                var json = $"{{\"speed\":{speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await client.PostAsync($"http://localhost:{_settings.NetworkPort}/info/appear/speed", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoChat] Ошибка отправки скорости появления: {ex.Message}");
            }
        }
    }
}