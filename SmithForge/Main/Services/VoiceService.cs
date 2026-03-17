using System;
using System.Linq;
using System.Speech.Synthesis; // Наш новый пакет
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public static class VoiceService
    {
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
    }
}
