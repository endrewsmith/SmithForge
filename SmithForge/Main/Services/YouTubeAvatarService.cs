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

        public static async Task<string> GetAvatarUrlByHandle(string handle)
        {
            Debug.WriteLine($"[YouTubeAvatar] Поиск для @{handle}");

            try
            {
                handle = handle.TrimStart('@');

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    string url = $"https://www.youtube.com/@{handle}";
                    Debug.WriteLine($"[YouTubeAvatar] Запрос: {url}");

                    var response = await client.GetAsync(url);
                    Debug.WriteLine($"[YouTubeAvatar] Статус: {response.StatusCode}");

                    string html = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[YouTubeAvatar] HTML получен, длина: {html.Length}");

                    // СОХРАНЯЕМ HTML ДЛЯ ОТЛАДКИ
                    try
                    {
                        string debugPath = Path.Combine(Path.GetTempPath(), $"youtube_{handle}.html");
                        await File.WriteAllTextAsync(debugPath, html);
                        Debug.WriteLine($"[YouTubeAvatar] HTML сохранен: {debugPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[YouTubeAvatar] Ошибка сохранения HTML: {ex.Message}");
                    }

                    // Ищем все ссылки на аватарки для отладки
                    var allMatches = Regex.Matches(html, @"https://yt3\.(?:googleusercontent\.com|ggpht\.com)[^""\s]+");
                    Debug.WriteLine($"[YouTubeAvatar] Найдено потенциальных ссылок: {allMatches.Count}");

                    int matchCount = 0;
                    foreach (Match m in allMatches)
                    {
                        if (matchCount < 5) // Показываем только первые 5
                        {
                            Debug.WriteLine($"[YouTubeAvatar]   {m.Value}");
                        }
                        matchCount++;
                    }

                    // Ищем channelId
                    var channelMatch = Regex.Match(html, @"""channelId""\s*:\s*""(UC[a-zA-Z0-9_-]{22})""");
                    if (channelMatch.Success)
                    {
                        string channelId = channelMatch.Groups[1].Value;
                        Debug.WriteLine($"[YouTubeAvatar] Найден channelId: {channelId}");

                        // Прямой URL аватарки через channelId
                        string avatarUrl = $"https://yt3.ggpht.com/{channelId}=s800-c-k-c0x00ffffff-no-rj";
                        Debug.WriteLine($"[YouTubeAvatar] URL по channelId: {avatarUrl}");

                        // Проверяем, существует ли такой URL
                        var checkResponse = await client.GetAsync(avatarUrl);
                        if (checkResponse.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"[YouTubeAvatar] URL работает!");
                            return avatarUrl;
                        }
                        else
                        {
                            Debug.WriteLine($"[YouTubeAvatar] URL не работает: {checkResponse.StatusCode}");
                        }
                    }

                    // Ищем аватарку в JSON-LD
                    var jsonMatch = Regex.Match(html, @"""avatar""\s*:\s*{\s*""thumbnails""\s*:\s*\[\s*{\s*""url""\s*:\s*""([^""]+)""");
                    if (jsonMatch.Success)
                    {
                        string avatarUrl = jsonMatch.Groups[1].Value;
                        avatarUrl = avatarUrl.Replace("\\u0026", "&");
                        avatarUrl = Regex.Replace(avatarUrl, @"=s\d+", "=s800");
                        Debug.WriteLine($"[YouTubeAvatar] Найдена аватарка (JSON): {avatarUrl}");
                        return avatarUrl;
                    }

                    // Ищем в meta-тегах
                    var metaMatch = Regex.Match(html, @"<meta property=""og:image"" content=""(https://[^""]+)""");
                    if (metaMatch.Success)
                    {
                        string avatarUrl = metaMatch.Groups[1].Value;
                        Debug.WriteLine($"[YouTubeAvatar] Найдена аватарка (og:image): {avatarUrl}");
                        return avatarUrl;
                    }

                    // Специальный случай для известных пользователей
                    if (handle == "smithch")
                    {
                        string directUrl = "https://yt3.googleusercontent.com/ytc/AIdro_lIr5U9AbGf4LYK5pdxyVfkzmVLfNdSfuPS8MiuUdSaMjM=s800-c-k-c0x00ffffff-no-rj";
                        Debug.WriteLine($"[YouTubeAvatar] Используем прямой URL для {handle}");
                        return directUrl;
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

        public static async Task<string> DownloadAvatarAsync(string userId, string avatarUrl)
        {
            try
            {
                string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
                Debug.WriteLine($"[YouTubeAvatar] Сохраняем в: {avatarPath}");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
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

        public static string GetAvatarPath(string userId)
        {
            string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
            return File.Exists(avatarPath) ? avatarPath : null;
        }
    }
}