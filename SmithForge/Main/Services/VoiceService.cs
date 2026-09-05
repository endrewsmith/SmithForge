using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmithForge.Main.Services
{
    public static class VoiceService
    {
        private static int _importantSoundVolume = 100;
        private static int _voiceVolume = 100;
        private static int _voiceRate = 3;
        private static string? _currentVoiceName;
        private static Dispatcher? _uiDispatcher;

        // ========== ИНИЦИАЛИЗАЦИЯ ==========

        public static void Initialize(Dispatcher dispatcher)
        {
            _uiDispatcher = dispatcher;
            Debug.WriteLine("[VoiceService] Инициализирован с UI Dispatcher");
        }

        // ========== УПРАВЛЕНИЕ СКОРОСТЬЮ ==========

        public static void SetVoiceRate(int rate)
        {
            _voiceRate = Math.Clamp(rate, -10, 10);
            Debug.WriteLine($"[VoiceService] Скорость установлена: {_voiceRate}");
        }

        public static int GetVoiceRate() => _voiceRate;

        // ========== ОСНОВНЫЕ МЕТОДЫ ==========

        public static string CleanSpecialCharacters(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string cleaned = Regex.Replace(text, @"[^\p{L}\p{N}\s]", "");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        public static async Task SayAsync(string text)
        {
            Debug.WriteLine($"[VoiceService] SayAsync НАЧАЛО, поток: {Thread.CurrentThread.ManagedThreadId}");

            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.WriteLine("[VoiceService] Текст пуст");
                    return;
                }

                string cleanText = CleanSpecialCharacters(text);
                if (string.IsNullOrEmpty(cleanText))
                {
                    Debug.WriteLine("[VoiceService] Очищенный текст пуст");
                    return;
                }

                await Task.Run(() =>
                {
                    Debug.WriteLine($"[VoiceService] Task.Run внутри, поток: {Thread.CurrentThread.ManagedThreadId}");
                    try
                    {
                        using (SpeechSynthesizer synth = new SpeechSynthesizer())
                        {
                            synth.SetOutputToDefaultAudioDevice();
                            synth.Volume = _voiceVolume;

                            if (!string.IsNullOrEmpty(_currentVoiceName))
                            {
                                try
                                {
                                    synth.SelectVoice(_currentVoiceName);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"[VoiceService] Ошибка выбора голоса: {ex.Message}");
                                }
                            }

                            // ✅ ИСПОЛЬЗУЕМ _voiceRate
                            synth.Rate = _voiceRate;
                            Debug.WriteLine($"[VoiceService] Скорость: {_voiceRate}, текст: {cleanText}");
                            synth.Speak(cleanText);
                            Debug.WriteLine("[VoiceService] Синтез речи завершен");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[VoiceService] SAPI Error: {ex.Message}");
                        Debug.WriteLine($"[VoiceService] SAPI StackTrace: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VoiceService] SayAsync Error: {ex.Message}");
                Debug.WriteLine($"[VoiceService] SayAsync StackTrace: {ex.StackTrace}");
            }

            Debug.WriteLine("[VoiceService] SayAsync ЗАВЕРШЕН");
        }

        public static List<string> GetAvailableVoiceNames()
        {
            try
            {
                using (var synth = new SpeechSynthesizer())
                {
                    var voices = synth.GetInstalledVoices()
                        .Where(v => v.Enabled)
                        .Select(v => v.VoiceInfo)
                        .ToList();

                    var russianVoices = voices
                        .Where(v => v.Culture.TwoLetterISOLanguageName == "ru")
                        .Select(v => v.Name)
                        .ToList();

                    if (russianVoices.Any())
                    {
                        Debug.WriteLine($"[VoiceService] Найдено русских голосов: {russianVoices.Count}");
                        return russianVoices;
                    }

                    var fallbackVoices = voices
                        .Where(v => v.Name.Contains("Irina", StringComparison.OrdinalIgnoreCase) ||
                                   v.Name.Contains("Pavel", StringComparison.OrdinalIgnoreCase))
                        .Select(v => v.Name)
                        .ToList();

                    if (fallbackVoices.Any())
                    {
                        Debug.WriteLine($"[VoiceService] Найдено русскоязычных голосов: {fallbackVoices.Count}");
                        return fallbackVoices;
                    }

                    Debug.WriteLine("[VoiceService] Русские голоса не найдены");
                    return voices.Select(v => v.Name).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VoiceService] Ошибка: {ex.Message}");
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
                Debug.WriteLine($"[VoiceService] Выбран голос: {_currentVoiceName}");
                return true;
            }

            return false;
        }

        public static string? GetCurrentVoiceName() => _currentVoiceName;

        // ========== МЕТОДЫ ДЛЯ ЗВУКОВ ==========

        public static void PlaySound(string fileName)
        {
            if (_uiDispatcher == null)
            {
                Debug.WriteLine("[VoiceService] UI Dispatcher не инициализирован");
                return;
            }

            _uiDispatcher.Invoke(() =>
            {
                try
                {
                    string soundPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "SF_Data", "Assets", "Sounds", fileName);

                    if (File.Exists(soundPath))
                    {
                        MediaPlayer player = new MediaPlayer();
                        player.Open(new Uri(soundPath, UriKind.Absolute));
                        player.Play();
                        Debug.WriteLine($"[VoiceService] Воспроизведен звук: {fileName}");
                        player.MediaEnded += (s, e) => player.Close();
                    }
                    else
                    {
                        Debug.WriteLine($"[VoiceService] Файл не найден: {soundPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VoiceService] Ошибка: {ex.Message}");
                }
            });
        }

        public static async Task PlayImportantSoundAsync()
        {
            Debug.WriteLine("[VoiceService] PlayImportantSoundAsync: НАЧАЛО");

            if (_uiDispatcher == null)
            {
                Debug.WriteLine("[VoiceService] UI Dispatcher не инициализирован");
                return;
            }

            string soundPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Sounds", "important.mp3");

            if (!File.Exists(soundPath))
            {
                Debug.WriteLine($"[VoiceService] Файл не найден: {soundPath}");
                return;
            }

            var tcs = new TaskCompletionSource<bool>();

            await _uiDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    MediaPlayer player = new MediaPlayer();

                    void OnMediaEnded(object? s, EventArgs e)
                    {
                        Debug.WriteLine("[VoiceService] PlayImportantSoundAsync: MediaEnded");
                        player.MediaEnded -= OnMediaEnded;
                        player.MediaFailed -= OnMediaFailed;
                        player.Close();
                        tcs.TrySetResult(true);
                    }

                    void OnMediaFailed(object? s, ExceptionEventArgs e)
                    {
                        Debug.WriteLine("[VoiceService] PlayImportantSoundAsync: MediaFailed");
                        player.MediaEnded -= OnMediaEnded;
                        player.MediaFailed -= OnMediaFailed;
                        player.Close();
                        tcs.TrySetResult(false);
                    }

                    player.MediaEnded += OnMediaEnded;
                    player.MediaFailed += OnMediaFailed;
                    player.Volume = _importantSoundVolume / 100.0;
                    player.Open(new Uri(soundPath, UriKind.Absolute));
                    player.Play();
                    Debug.WriteLine($"[VoiceService] Воспроизведен важный звук (громкость: {_importantSoundVolume}%)");

                    await Task.Delay(3000);

                    if (!tcs.Task.IsCompleted)
                    {
                        Debug.WriteLine("[VoiceService] PlayImportantSoundAsync: Таймаут");
                        player.MediaEnded -= OnMediaEnded;
                        player.MediaFailed -= OnMediaFailed;
                        player.Close();
                        tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VoiceService] PlayImportantSoundAsync: Ошибка {ex.Message}");
                    tcs.TrySetResult(false);
                }
            });

            await tcs.Task;
            Debug.WriteLine("[VoiceService] PlayImportantSoundAsync: ЗАВЕРШЕНО");
        }

        public static void PlayStickerSound() => PlaySound("sticker_pop.mp3");
        public static void PlayImportantSound() => PlaySound("important.mp3");
        public static Task PlayStickerSoundAsync() => PlayImportantSoundAsync();
        public static void PlayBeep() => Task.Run(() => Console.Beep(1000, 150));

        // ========== УПРАВЛЕНИЕ ГРОМКОСТЬЮ ==========

        public static void SetImportantSoundVolume(int volume)
        {
            _importantSoundVolume = Math.Clamp(volume, 0, 100);
            Debug.WriteLine($"[VoiceService] Громкость звука важных сообщений: {_importantSoundVolume}%");
        }

        public static void SetVoiceVolume(int volume)
        {
            _voiceVolume = Math.Clamp(volume, 0, 100);
            Debug.WriteLine($"[VoiceService] Громкость голоса: {_voiceVolume}%");
        }

        public static int GetImportantSoundVolume() => _importantSoundVolume;
        public static int GetVoiceVolume() => _voiceVolume;
    }
}