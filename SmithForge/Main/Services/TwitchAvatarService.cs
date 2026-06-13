using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

namespace SmithForge.Main.Services
{
    public static class TwitchAvatarService
    {
        private static readonly string AvatarFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Avatars", "platform");

        static TwitchAvatarService()
        {
            if (!Directory.Exists(AvatarFolder))
                Directory.CreateDirectory(AvatarFolder);
        }

        public static async Task<string?> GetAvatarUrlByLogin(string login)
        {
            Debug.WriteLine($"[TwitchAvatar] Поиск для @{login}");

            try
            {
                login = login.TrimStart('@').ToLower();

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                string url = $"https://www.twitch.tv/{login}";
                Debug.WriteLine($"[TwitchAvatar] Запрос: {url}");

                var response = await client.GetAsync(url);
                string html = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[TwitchAvatar] HTML получен, длина: {html.Length}");

                // 1. Ищем аватар в img теге с классом tw-image-avatar
                var imgMatch = Regex.Match(html,
                    @"<img[^>]*class=""[^""]*tw-image-avatar[^""]*""[^>]*src=""([^""]+)""");
                if (imgMatch.Success)
                {
                    string avatarUrl = imgMatch.Groups[1].Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (img): {avatarUrl}");
                    // Меняем размер с 70x70 на 300x300
                    avatarUrl = avatarUrl.Replace("-70x70", "-300x300");
                    return avatarUrl;
                }

                // 2. Ищем в meta-теге og:image
                var metaMatch = Regex.Match(html,
                    @"<meta\s+property=""og:image""\s+content=""([^""]+)""");
                if (metaMatch.Success)
                {
                    string avatarUrl = metaMatch.Groups[1].Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (og:image): {avatarUrl}");
                    avatarUrl = avatarUrl.Replace("-50x50", "-300x300");
                    return avatarUrl;
                }

                // 3. Ищем JSON-данные (альтернативный способ)
                var jsonMatch = Regex.Match(html,
                    @"<script[^>]*type=""application/json""[^>]*>(.*?)</script>",
                    RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    try
                    {
                        string jsonData = jsonMatch.Groups[1].Value;
                        using var doc = JsonDocument.Parse(jsonData);

                        // Поиск URL аватара в JSON
                        var root = doc.RootElement;
                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("user", out var user) &&
                            user.TryGetProperty("profileImageURL", out var avatar))
                        {
                            string avatarUrl = avatar.GetString();
                            Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (JSON): {avatarUrl}");
                            avatarUrl = avatarUrl?.Replace("-50x50", "-300x300") ?? "";
                            return avatarUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TwitchAvatar] Ошибка парсинга JSON: {ex.Message}");
                    }
                }

                // 4. Ищем прямую ссылку на аватар по шаблону
                var avatarMatch = Regex.Match(html,
                    @"https://static-cdn\.jtvnw\.net/jtv_user_pictures/[^""\s]+-profile_image-\d+x\d+\.(?:png|jpg|jpeg)");
                if (avatarMatch.Success)
                {
                    string avatarUrl = avatarMatch.Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (regex): {avatarUrl}");
                    avatarUrl = avatarUrl.Replace("-70x70", "-300x300");
                    return avatarUrl;
                }

                Debug.WriteLine($"[TwitchAvatar] Аватарка не найдена");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка: {ex.Message}");
                Debug.WriteLine($"[TwitchAvatar] Стек: {ex.StackTrace}");
                return null;
            }
        }

        public static async Task<string?> DownloadAvatarAsync(string userId, string avatarUrl)
        {
            try
            {
                string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
                Debug.WriteLine($"[TwitchAvatar] Сохраняем в: {avatarPath}");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                // Добавляем реферер, чтобы избежать 403 ошибки
                client.DefaultRequestHeaders.Add("Referer", "https://www.twitch.tv/");

                var bytes = await client.GetByteArrayAsync(avatarUrl);
                await File.WriteAllBytesAsync(avatarPath, bytes);
                Debug.WriteLine($"[TwitchAvatar] Сохранено, размер: {bytes.Length} байт");

                // ✅ ВОЗВРАЩАЕМ ПУТЬ К СОХРАНЁННОМУ ФАЙЛУ
                return avatarPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка скачивания: {ex.Message}");
                return null;
            }
        }

        public static string? GetAvatarPath(string userId)
        {
            string avatarPath = Path.Combine(AvatarFolder, $"{userId}.png");
            return File.Exists(avatarPath) ? avatarPath : null;
        }
    }
}