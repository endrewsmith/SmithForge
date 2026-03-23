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

        public static Task SayAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

            // Чистим текст от тегов <important> и прочих
            string cleanText = Regex.Replace(text, @"<[^>]*>", "").Trim();
            if (string.IsNullOrEmpty(cleanText)) return Task.CompletedTask;

            return Task.Run(() =>
            {
                try
                {
                    using (SpeechSynthesizer synth = new SpeechSynthesizer())
                    {
                        synth.SetOutputToDefaultAudioDevice();

                        // Ищем голос Александра
                        var voice = synth.GetInstalledVoices()
                            .FirstOrDefault(v => v.Enabled && v.VoiceInfo.Name.Contains("Aleksandr"));

                        if (voice != null)
                        {
                            synth.SelectVoice(voice.VoiceInfo.Name);
                        }

                        // Настройки (можно подкрутить)
                        synth.Rate = 1;   // Скорость (-10 до 10)
                        synth.Volume = 100; // Громкость

                        // Speak блокирует поток до конца речи, что нам и нужно для очереди
                        synth.Speak(cleanText);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SAPI5 Error] {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Воспроизвести звуковой файл из папки SF_Data/Assets/Sounds/ (без ожидания)
        /// </summary>
        /// <param name="fileName">Имя файла (например, "sticker_pop.mp3")</param>
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
                System.Diagnostics.Debug.WriteLine($"[VoiceService] Ошибка воспроизведения: {ex.Message}");
            }
        }

        /// <summary>
        /// Воспроизвести звуковой файл с ожиданием окончания
        /// </summary>
        /// <param name="fileName">Имя файла (например, "important.mp3")</param>
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
                    _mediaPlayer.MediaEnded += (s, e) =>
                    {
                        _soundCompletionSource?.TrySetResult(true);
                    };
                    _mediaPlayer.MediaFailed += (s, e) =>
                    {
                        _soundCompletionSource?.TrySetResult(false);
                    };
                    _mediaPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    _mediaPlayer.Play();

                    System.Diagnostics.Debug.WriteLine($"[VoiceService] Воспроизведен звук (async): {fileName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[VoiceService] Файл не найден: {soundPath}");
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

        /// <summary>
        /// Звук для стикера (без ожидания)
        /// </summary>
        public static void PlayStickerSound()
        {
            PlaySound("sticker_pop.mp3");
        }

        /// <summary>
        /// Звук для важного сообщения (без ожидания)
        /// </summary>
        public static void PlayImportantSound()
        {
            PlaySound("important.mp3");
        }

        /// <summary>
        /// Звук для важного сообщения с ожиданием окончания
        /// </summary>
        public static Task PlayImportantSoundAsync()
        {
            return PlaySoundAsync("important.mp3");
        }

        /// <summary>
        /// Звук для стикера с ожиданием окончания
        /// </summary>
        public static Task PlayStickerSoundAsync()
        {
            return PlaySoundAsync("sticker_pop.mp3");
        }

        /// <summary>
        /// Короткий системный бип (для теста, не требует файлов)
        /// </summary>
        public static void PlayBeep()
        {
            Task.Run(() => Console.Beep(1000, 150));
        }
    }
}