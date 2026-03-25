using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public static class ImportantQueueService
    {
        private static readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private static bool _isPlaying = false;

        public static int QueueCount => _messageQueue.Count;

        /// <summary>
        /// Добавить сообщение в очередь
        /// </summary>
        public static void Enqueue(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            _messageQueue.Enqueue(message);
            Debug.WriteLine($"[ImportantQueue] Добавлено в очередь. Всего: {_messageQueue.Count}");
        }

        /// <summary>
        /// Воспроизвести следующее сообщение
        /// </summary>
        public static async Task PlayNext()
        {
            if (_isPlaying)
            {
                Debug.WriteLine("[ImportantQueue] Уже воспроизводится, подождите");
                return;
            }

            if (!_messageQueue.TryDequeue(out string message))
            {
                Debug.WriteLine("[ImportantQueue] Очередь пуста");
                return;
            }

            try
            {
                _isPlaying = true;

                // Очищаем текст от тегов
                string cleanMessage = message
                    .Replace("<important>", "")
                    .Replace("</important>", "")
                    .Trim();

                Debug.WriteLine($"[ImportantQueue] Воспроизведение: {cleanMessage}");

                // Получаем настройки громкости
                var settings = ConfigService.Load();

                // Устанавливаем громкость перед воспроизведением
                VoiceService.SetImportantSoundVolume(settings.ImportantSoundVolume);
                VoiceService.SetVoiceVolume(settings.VoiceVolume);

                // Воспроизводим звук и голос
                await VoiceService.PlayImportantSoundAsync();
                await VoiceService.SayAsync(cleanMessage);

                Debug.WriteLine($"[ImportantQueue] Воспроизведение завершено. Осталось: {_messageQueue.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportantQueue] Ошибка: {ex.Message}");
            }
            finally
            {
                _isPlaying = false;
            }
        }

        /// <summary>
        /// Очистить очередь
        /// </summary>
        public static void ClearQueue()
        {
            while (_messageQueue.TryDequeue(out _)) { }
            Debug.WriteLine("[ImportantQueue] Очередь очищена");
        }
    }
}