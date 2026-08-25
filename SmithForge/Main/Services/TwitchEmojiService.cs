// SmithForge.Main\Services\TwitchEmojiService.cs
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public static class TwitchEmojiService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private static readonly string ImagesFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Emojis", "Twitch", "Images");

        private static readonly ConcurrentDictionary<string, string> _emojiPathMap = new();

        // ⭐ ДЕЛЕГАТЫ (КАК В YOUTUBE!)
        public static Func<string, bool>? CheckEmojiExists { get; set; }
        public static Action<string, string>? RegisterEmojiInCache { get; set; }

        static TwitchEmojiService()
        {
            Directory.CreateDirectory(ImagesFolder);
        }

        // ⭐ РЕГИСТРАЦИЯ ДЕЛЕГАТОВ (КАК В YOUTUBE!)
        public static void RegisterDelegates(Func<string, bool> checkExists, Action<string, string> register)
        {
            CheckEmojiExists = checkExists;
            RegisterEmojiInCache = register;
        }

        public static async Task<string?> GetOrDownloadEmojiAsync(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return null;

            // ✅ ОТЛАДКА: выводим ID
            System.Diagnostics.Debug.WriteLine($"[TwitchEmoji] ID получен: '{emoteId}' (длина: {emoteId.Length})");

            var emojiCode = emoteId;

            // Проверяем через делегат (глобальный кэш)
            if (CheckEmojiExists?.Invoke(emojiCode) == true)
            {
                return emojiCode;
            }

            // Проверяем локальный кэш
            if (_emojiPathMap.TryGetValue(emoteId, out var cachedPath))
            {
                return cachedPath;
            }

            // Проверяем файл на диске
            var localPath = Path.Combine(ImagesFolder, $"{emoteId}.png");
            if (File.Exists(localPath))
            {
                _emojiPathMap[emoteId] = localPath;
                RegisterEmojiInCache?.Invoke(emojiCode, localPath);
                return localPath;
            }

            // ✅ ОТЛАДКА: выводим URL ДО скачивания
            var url = $"https://static-cdn.jtvnw.net/emoticons/v2/{emoteId}/default/light/1.0";
            System.Diagnostics.Debug.WriteLine($"[TwitchEmoji] URL: '{url}'");

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);

                _emojiPathMap[emoteId] = localPath;
                RegisterEmojiInCache?.Invoke(emojiCode, localPath);

                System.Diagnostics.Debug.WriteLine($"[TwitchEmoji] ✅ Скачан: {emoteId}");
                return localPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TwitchEmoji] ❌ Ошибка {emoteId}: {ex.Message}");
                return null;
            }
        }
    }
}