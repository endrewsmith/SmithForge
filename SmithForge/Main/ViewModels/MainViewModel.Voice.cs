using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Services;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // ПОЛЯ ГОЛОСА И ЗВУКА
        // ============================================================
        [ObservableProperty]
        private int _voiceRate = 3;

        [ObservableProperty]
        private int _scrollSpeed = 1500;

        [ObservableProperty]
        private double _appearSpeed = 0.3;

        [ObservableProperty]
        private int _voiceVolume = 100;

        [ObservableProperty]
        private int _importantSoundVolume = 100;

        [ObservableProperty]
        private int _stickerDisplayTime = 5000;

        // ============================================================
        // КОМАНДА УСТАНОВКИ СКОРОСТИ
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

        partial void OnVoiceRateChanged(int value)
        {
            Settings.VoiceRate = value;
            ConfigService.Save(Settings);
            VoiceService.SetVoiceRate(value);

            Debug.WriteLine($"🎙️ [MainViewModel] Скорость голоса: {value}, сервис={VoiceService.GetVoiceRate()}");
        }

        // ============================================================
        // ОБРАБОТЧИКИ ИЗМЕНЕНИЯ НАСТРОЕК
        // ============================================================
        partial void OnStickerDisplayTimeChanged(int value) => _settingsService.SetStickerDisplayTime(value);
        partial void OnImportantSoundVolumeChanged(int value) => _settingsService.SetImportantSoundVolume(value);
        partial void OnVoiceVolumeChanged(int value) => _settingsService.SetVoiceVolume(value);

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

                var response = await client.PostAsync($"http://localhost:{Settings.NetworkPort}/info/scroll/speed", content);

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

                await client.PostAsync($"http://localhost:{Settings.NetworkPort}/info/appear/speed", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoChat] Ошибка отправки скорости появления: {ex.Message}");
            }
        }
    }
}