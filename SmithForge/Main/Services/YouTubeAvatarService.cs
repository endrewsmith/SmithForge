using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    public static class YouTubeAvatarService
    {
        private static readonly string AvatarFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Avatars", "platform");

        static YouTubeAvatarService()
        {
            if (!Directory.Exists(AvatarFolder))
                Directory.CreateDirectory(AvatarFolder);
        }

        /// <summary>
        /// Получить URL аватарки по ID канала (UC...)
        /// </summary>
        public static async Task<string?> GetAvatarUrlByChannelId(string channelId)
        {
            Debug.WriteLine($"[YouTubeAvatar] Поиск для канала: {channelId}");

            try
            {
                channelId = channelId.Trim();

                // Проверяем формат Channel ID
                if (!channelId.StartsWith("UC") || channelId.Length < 20)
                {
                    Debug.WriteLine($"[YouTubeAvatar] Неверный формат Channel ID: {channelId}");
                    return null;
                }

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    // Используем URL с channel ID
                    string pageUrl = $"https://www.youtube.com/channel/{channelId}";
                    Debug.WriteLine($"[YouTubeAvatar] Запрос: {pageUrl}");

                    var response = await client.GetAsync(pageUrl);
                    Debug.WriteLine($"[YouTubeAvatar] Статус: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Страница не найдена: {response.StatusCode}");
                        return null;
                    }

                    string html = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[YouTubeAvatar] HTML получен, длина: {html.Length}");

                    // СОХРАНЯЕМ HTML ДЛЯ ОТЛАДКИ
                    try
                    {
                        string debugPath = Path.Combine(Path.GetTempPath(), $"youtube_{channelId}.html");
                        await File.WriteAllTextAsync(debugPath, html);
                        Debug.WriteLine($"[YouTubeAvatar] HTML сохранен: {debugPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Ошибка сохранения HTML: {ex.Message}");
                    }

                    // ============ СПОСОБ 1: Прямой URL по Channel ID ============
                    // YouTube имеет стандартный формат аватарки по ID
                    string directAvatarUrl = $"https://yt3.ggpht.com/{channelId}=s800-c-k-c0x00ffffff-no-rj";
                    Debug.WriteLine($"[YouTubeAvatar] Прямой URL: {directAvatarUrl}");

                    var checkResponse = await client.GetAsync(directAvatarUrl);
                    if (checkResponse.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Прямой URL работает!");
                        return directAvatarUrl;
                    }
                    else
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Прямой URL не работает: {checkResponse.StatusCode}");
                    }

                    // ============ СПОСОБ 2: Поиск в JSON ============
                    // Ищем avatar в JSON-данных
                    var jsonPatterns = new[]
                    {
                        @"""avatar""\s*:\s*{\s*""thumbnails""\s*:\s*\[\s*{\s*""url""\s*:\s*""([^""]+)""",
                        @"""avatarThumbnailUrl""\s*:\s*""([^""]+)""",
                        @"avatarUrl""\s*:\s*""([^""]+)""",
                    };

                    foreach (var pattern in jsonPatterns)
                    {
                        var jsonMatch = Regex.Match(html, pattern);
                        if (jsonMatch.Success)
                        {
                            string avatarUrl = jsonMatch.Groups[1].Value;
                            avatarUrl = avatarUrl.Replace("\\u0026", "&");
                            avatarUrl = Regex.Replace(avatarUrl, @"=s\d+", "=s800");
                            Debug.WriteLine($"[YouTubeAvatar] Найдена аватарка (JSON): {avatarUrl}");
                            return avatarUrl;
                        }
                    }

                    // ============ СПОСОБ 3: og:image meta-тег ============
                    var metaMatch = Regex.Match(html, @"<meta property=""og:image"" content=""(https://[^""]+)""");
                    if (metaMatch.Success)
                    {
                        string avatarUrl = metaMatch.Groups[1].Value;
                        Debug.WriteLine($"[YouTubeAvatar] Найдена аватарка (og:image): {avatarUrl}");
                        return avatarUrl;
                    }

                    // ============ СПОСОБ 4: Поиск всех ссылок ============
                    var allMatches = Regex.Matches(html, @"https://yt3\.(?:googleusercontent\.com|ggpht\.com)[^""\s]+");
                    Debug.WriteLine($"[YouTubeAvatar] Найдено потенциальных ссылок: {allMatches.Count}");

                    // Ищем самую большую аватарку (с большим размером)
                    string bestAvatar = null;
                    int bestSize = 0;

                    foreach (Match m in allMatches)
                    {
                        string avatarUrl = m.Value;
                        // Ищем размер в URL
                        var sizeMatch = Regex.Match(avatarUrl, @"=s(\d+)");
                        if (sizeMatch.Success)
                        {
                            int size = int.Parse(sizeMatch.Groups[1].Value);
                            if (size > bestSize)
                            {
                                bestSize = size;
                                bestAvatar = avatarUrl;
                            }
                        }
                        else if (string.IsNullOrEmpty(bestAvatar))
                        {
                            bestAvatar = avatarUrl;
                        }
                    }

                    if (!string.IsNullOrEmpty(bestAvatar))
                    {
                        // Увеличиваем размер до максимума
                        bestAvatar = Regex.Replace(bestAvatar, @"=s\d+", "=s800");
                        Debug.WriteLine($"[YouTubeAvatar] Найдена лучшая аватарка: {bestAvatar} (размер: {bestSize})");
                        return bestAvatar;
                    }

                    // ============ СПОСОБ 5: Специальные случаи ============
                    // Для известных каналов можно добавить прямые ссылки
                    var knownChannels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "UCsf2sD1gJWus1OUrq2fGwlQ", "https://yt3.googleusercontent.com/ytc/AIdro_lIr5U9AbGf4LYK5pdxyVfkzmVLfNdSfuPS8MiuUdSaMjM=s800-c-k-c0x00ffffff-no-rj" },
                        // Добавь другие известные каналы по необходимости
                    };

                    if (knownChannels.TryGetValue(channelId, out string knownUrl))
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Используем известный URL для {channelId}");
                        return knownUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeAvatar] Ошибка: {ex.Message}");
                Debug.WriteLine($"[YouTubeAvatar] Стек: {ex.StackTrace}");
            }

            Debug.WriteLine($"[YouTubeAvatar] Аватарка не найдена");
            return null;
        }

        /// <summary>
        /// Скачать аватарку и сохранить в папку platform
        /// </summary>
        public static async Task<string?> DownloadAvatarAsync(string userId, string avatarUrl)
        {
            try
            {
                string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
                Debug.WriteLine($"[YouTubeAvatar] Сохраняем в: {avatarPath}");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                    client.DefaultRequestHeaders.Add("Referer", "https://www.youtube.com/");

                    var bytes = await client.GetByteArrayAsync(avatarUrl);
                    await File.WriteAllBytesAsync(avatarPath, bytes);
                    Debug.WriteLine($"[YouTubeAvatar] Сохранено, размер: {bytes.Length} байт");
                    return avatarPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeAvatar] Ошибка скачивания: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получить путь к сохраненной аватарке
        /// </summary>
        public static string? GetAvatarPath(string userId)
        {
            string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
            return File.Exists(avatarPath) ? avatarPath : null;
        }

        // ============ СТАРЫЙ МЕТОД ДЛЯ ОБРАТНОЙ СОВМЕСТИМОСТИ ============
        // Можно удалить, если он не используется
        [Obsolete("Используйте GetAvatarUrlByChannelId вместо этого метода")]
        public static async Task<string?> GetAvatarUrlByHandle(string handle)
        {
            Debug.WriteLine($"[YouTubeAvatar] GetAvatarUrlByHandle устарел, используйте GetAvatarUrlByChannelId");

            // Пытаемся найти Channel ID по handle
            try
            {
                handle = handle.TrimStart('@');
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    string handleUrl = $"https://www.youtube.com/@{handle}";
                    var response = await client.GetAsync(handleUrl);
                    string html = await response.Content.ReadAsStringAsync();

                    // Ищем channelId в HTML
                    var channelMatch = Regex.Match(html, @"""channelId""\s*:\s*""(UC[a-zA-Z0-9_-]{22})""");
                    if (channelMatch.Success)
                    {
                        string foundChannelId = channelMatch.Groups[1].Value;
                        Debug.WriteLine($"[YouTubeAvatar] Найден channelId: {foundChannelId} для handle @{handle}");
                        return await GetAvatarUrlByChannelId(foundChannelId);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeAvatar] Ошибка в GetAvatarUrlByHandle: {ex.Message}");
            }

            return null;
        }
    }
}