using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;

namespace SmithForge.Main.Services
{
    public static class VoiceService
    {
        private static MediaPlayer? _mediaPlayer;
        private static TaskCompletionSource<bool>? _soundCompletionSource;

        // Текущий выбранный голос
        private static string? _currentVoiceName;

        public static Task SayAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

            string cleanText = Regex.Replace(text, @"<[^>]*>", "").Trim();
            if (string.IsNullOrEmpty(cleanText)) return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.SetOutputToDefaultAudioDevice();

                        if (!string.IsNullOrEmpty(_currentVoiceName))
                        {
                            try
                            {
                                synth.SelectVoice(_currentVoiceName);
                            }
                            catch
                            {
                                var voice = synth.GetInstalledVoices().FirstOrDefault(v => v.Enabled);
                                if (voice != null)
                                    synth.SelectVoice(voice.VoiceInfo.Name);
                            }
                        }

                        synth.Rate = 1;
                        synth.Volume = 100;
                        synth.Speak(cleanText);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SAPI5 Error] {ex.Message}");
                }
            });
        }

        public static List<string> GetAvailableVoiceNames()
        {
            try
            {
                using (var synth = new SpeechSynthesizer())
                {
                    return synth.GetInstalledVoices()
                        .Where(v => v.Enabled)
                        .Select(v => v.VoiceInfo.Name)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Ошибка: {ex.Message}");
                return new List<string>();
            }
        }

        public static bool SelectVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName)) return false;

            var voices = GetAvailableVoiceNames();
            var match = voices.FirstOrDefault(v =>
                v.Equals(voiceName, StringComparison.OrdinalIgnoreCase) ||
                v.Contains(voiceName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                _currentVoiceName = match;
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Выбран голос: {_currentVoiceName}");
                return true;
            }

            return false;
        }

        public static string? GetCurrentVoiceName() => _currentVoiceName;

        public static void PlaySound(string fileName)
        {
            try
            {
                string soundPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Sounds", fileName);

                if (File.Exists(soundPath))
                {
                    _mediaPlayer?.Close();
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    _mediaPlayer.Play();
                    System.Diagnostics.Debug.WriteLine($"[VoiceService] Воспроизведен звук: {fileName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VoiceService] Файл не найден: {soundPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Ошибка: {ex.Message}");
            }
        }

        public static Task PlaySoundAsync(string fileName)
        {
            _soundCompletionSource = new TaskCompletionSource<bool>();

            try
            {
                string soundPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Sounds", fileName);

                if (File.Exists(soundPath))
                {
                    _mediaPlayer?.Close();
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.MediaEnded += (s, e) => _soundCompletionSource?.TrySetResult(true);
                    _mediaPlayer.MediaFailed += (s, e) => _soundCompletionSource?.TrySetResult(false);
                    _mediaPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    _mediaPlayer.Play();
                    System.Diagnostics.Debug.WriteLine($"[VoiceService] Воспроизведен звук (async): {fileName}");
                }
                else
                {
                    _soundCompletionSource?.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Ошибка: {ex.Message}");
                _soundCompletionSource?.TrySetResult(false);
            }

            return _soundCompletionSource.Task;
        }

        // ========== МЕТОДЫ ДЛЯ СТИКЕРОВ И ВАЖНЫХ СООБЩЕНИЙ ==========

        public static void PlayStickerSound()
        {
            PlaySound("sticker_pop.mp3");
        }

        public static void PlayImportantSound()
        {
            PlaySound("important.mp3");
        }

        public static Task PlayStickerSoundAsync()
        {
            return PlaySoundAsync("sticker_pop.mp3");
        }

        public static Task PlayImportantSoundAsync()
        {
            return PlaySoundAsync("important.mp3");
        }

        public static void PlayBeep()
        {
            Task.Run(() => Console.Beep(1000, 150));
        }
    }
}