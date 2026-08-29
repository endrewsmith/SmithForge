using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public static class TwitchAvatarService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly string AvatarFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Avatars", "platform");

        static TwitchAvatarService()
        {
            if (!Directory.Exists(AvatarFolder))
                Directory.CreateDirectory(AvatarFolder);

            // ✅ Правильные заголовки как у реального браузера
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
        }

        /// <summary>
        /// Получить URL аватарки с повторными попытками
        /// </summary>
        public static async Task<string?> GetAvatarUrlByLogin(string login, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(login))
                return null;

            login = login.TrimStart('@').ToLower();
            Debug.WriteLine($"[TwitchAvatar] Поиск аватарки для @{login}");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                Debug.WriteLine($"[TwitchAvatar] Попытка {attempt} из {maxRetries}");

                try
                {
                    string avatarUrl = await GetAvatarFromHtml(login);
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        Debug.WriteLine($"[TwitchAvatar] ✅ Найдена аватарка: {avatarUrl}");
                        return avatarUrl;
                    }

                    // ✅ Ждём между попытками (увеличиваем с каждой попыткой)
                    int delayMs = attempt * 1000;
                    Debug.WriteLine($"[TwitchAvatar] Аватарка не найдена, ждём {delayMs}мс перед повторной попыткой...");
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TwitchAvatar] Ошибка попытки {attempt}: {ex.Message}");
                    if (attempt == maxRetries)
                        throw;
                }
            }

            Debug.WriteLine($"[TwitchAvatar] ❌ Аватарка не найдена после {maxRetries} попыток");
            return null;
        }

        /// <summary>
        /// Получить URL аватарки через парсинг HTML
        /// </summary>
        private static async Task<string?> GetAvatarFromHtml(string login)
        {
            try
            {
                string url = $"https://www.twitch.tv/{login}";
                Debug.WriteLine($"[TwitchAvatar] Загрузка: {url}");

                var response = await _httpClient.GetAsync(url);

                // ✅ Проверяем, не пришла ли капча
                string html = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(html))
                {
                    Debug.WriteLine("[TwitchAvatar] HTML пуст");
                    return null;
                }

                // ✅ Проверяем на капчу
                if (html.Contains("captcha") || html.Contains("cf-challenge") || html.Contains("Checking your browser"))
                {
                    Debug.WriteLine("[TwitchAvatar] ⚠️ Обнаружена капча или проверка браузера!");
                    return null;
                }

                Debug.WriteLine($"[TwitchAvatar] HTML загружен, длина: {html.Length}");

                // Способ 1: og:image (самый надёжный)
                var metaMatch = Regex.Match(html,
                    @"<meta\s+property=""og:image""\s+content=""([^""]+)""",
                    RegexOptions.IgnoreCase);

                if (metaMatch.Success)
                {
                    string avatarUrl = metaMatch.Groups[1].Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (og:image): {avatarUrl}");
                    avatarUrl = NormalizeAvatarUrl(avatarUrl);
                    return avatarUrl;
                }

                // Способ 2: img с классом tw-image-avatar
                var imgMatch = Regex.Match(html,
                    @"<img[^>]*class=""[^""]*tw-image-avatar[^""]*""[^>]*src=""([^""]+)""",
                    RegexOptions.IgnoreCase);

                if (imgMatch.Success)
                {
                    string avatarUrl = imgMatch.Groups[1].Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (img): {avatarUrl}");
                    avatarUrl = NormalizeAvatarUrl(avatarUrl);
                    return avatarUrl;
                }

                // Способ 3: JSON данные
                var jsonMatch = Regex.Match(html,
                    @"<script[^>]*type=""application/json""[^>]*>(.*?)</script>",
                    RegexOptions.Singleline);

                if (jsonMatch.Success)
                {
                    try
                    {
                        string jsonData = jsonMatch.Groups[1].Value;
                        using var doc = JsonDocument.Parse(jsonData);

                        var root = doc.RootElement;

                        // Пробуем разные пути
                        if (root.TryGetProperty("data", out var data) &&
                            data.TryGetProperty("user", out var user) &&
                            user.TryGetProperty("profileImageURL", out var avatar))
                        {
                            string avatarUrl = avatar.GetString();
                            Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (JSON): {avatarUrl}");
                            avatarUrl = NormalizeAvatarUrl(avatarUrl);
                            return avatarUrl;
                        }

                        if (root.TryGetProperty("user", out var user2) &&
                            user2.TryGetProperty("profileImageURL", out var avatar2))
                        {
                            string avatarUrl = avatar2.GetString();
                            Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (JSON alt): {avatarUrl}");
                            avatarUrl = NormalizeAvatarUrl(avatarUrl);
                            return avatarUrl;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"[TwitchAvatar] Ошибка JSON: {ex.Message}");
                    }
                }

                // Способ 4: Прямая ссылка по шаблону
                var avatarMatch = Regex.Match(html,
                    @"https://static-cdn\.jtvnw\.net/jtv_user_pictures/[^""\s]+-profile_image-\d+x\d+\.(?:png|jpg|jpeg)",
                    RegexOptions.IgnoreCase);

                if (avatarMatch.Success)
                {
                    string avatarUrl = avatarMatch.Value;
                    Debug.WriteLine($"[TwitchAvatar] Найдена аватарка (regex): {avatarUrl}");
                    avatarUrl = NormalizeAvatarUrl(avatarUrl);
                    return avatarUrl;
                }

                Debug.WriteLine("[TwitchAvatar] Аватарка не найдена в HTML");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка парсинга HTML: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Нормализовать URL аватарки (увеличить размер)
        /// </summary>
        private static string NormalizeAvatarUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Заменяем маленькие размеры на 300x300
            url = Regex.Replace(url, @"-\d+x\d+", "-300x300");

            // Убеждаемся, что протокол HTTPS
            if (url.StartsWith("http://"))
                url = "https://" + url.Substring(7);

            return url;
        }

        /// <summary>
        /// Скачать аватарку
        /// </summary>
        public static async Task<string?> DownloadAvatarAsync(string userId, string avatarUrl, bool forceUpdate = false)
        {
            try
            {
                if (string.IsNullOrEmpty(avatarUrl))
                    return null;

                string extension = Path.GetExtension(avatarUrl);
                if (string.IsNullOrEmpty(extension))
                    extension = ".png";

                string avatarPath = Path.Combine(AvatarFolder, $"{userId}{extension}");

                // ✅ Снимаем ReadOnly перед удалением
                RemoveReadOnlyAttribute(avatarPath);

                // ✅ Если forceUpdate = true и файл существует - удаляем его
                if (forceUpdate && File.Exists(avatarPath))
                {
                    Debug.WriteLine($"[TwitchAvatar] Принудительное обновление, удаляем старый файл: {avatarPath}");

                    bool deleted = false;
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            File.Delete(avatarPath);
                            deleted = true;
                            Debug.WriteLine($"[TwitchAvatar] Файл удалён после {attempt + 1} попытки");
                            break;
                        }
                        catch (IOException)
                        {
                            Debug.WriteLine($"[TwitchAvatar] Файл заблокирован, попытка {attempt + 1} из 5...");
                            await Task.Delay(300);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[TwitchAvatar] Ошибка удаления: {ex.Message}");
                            break;
                        }
                    }

                    if (!deleted)
                    {
                        string timestamp = DateTime.Now.Ticks.ToString();
                        string newPath = Path.Combine(AvatarFolder, $"{userId}_{timestamp}{extension}");
                        avatarPath = newPath;
                    }
                }

                // ✅ Если файл уже существует и не нужно принудительно обновлять - возвращаем путь
                if (!forceUpdate && File.Exists(avatarPath))
                {
                    Debug.WriteLine($"[TwitchAvatar] Аватар уже существует: {avatarPath}");
                    return avatarPath;
                }

                Debug.WriteLine($"[TwitchAvatar] Скачиваем в: {avatarPath}");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                client.DefaultRequestHeaders.Add("Referer", "https://www.twitch.tv/");

                var bytes = await client.GetByteArrayAsync(avatarUrl);

                string? directory = Path.GetDirectoryName(avatarPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                await File.WriteAllBytesAsync(avatarPath, bytes);

                // ✅ СНИМАЕМ READONLY СРАЗУ ПОСЛЕ СОХРАНЕНИЯ
                RemoveReadOnlyAttribute(avatarPath);

                Debug.WriteLine($"[TwitchAvatar] Скачан, размер: {bytes.Length} байт");
                return avatarPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка скачивания: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Снять атрибут "Только для чтения" с файла
        /// </summary>
        private static void RemoveReadOnlyAttribute(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                    Debug.WriteLine($"[TwitchAvatar] Снят атрибут ReadOnly: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка снятия ReadOnly: {ex.Message}");
            }
        }

        /// <summary>
        /// Получить локальный путь к аватарке
        /// </summary>
        public static string? GetAvatarPath(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            string[] extensions = { ".png", ".jpg", ".jpeg", ".gif" };
            foreach (var ext in extensions)
            {
                string path = Path.Combine(AvatarFolder, $"{userId}{ext}");
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>
        /// Удалить аватарку
        /// </summary>
        public static bool DeleteAvatar(string userId)
        {
            try
            {
                var path = GetAvatarPath(userId);
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                    Debug.WriteLine($"[TwitchAvatar] Удалена: {path}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TwitchAvatar] Ошибка удаления: {ex.Message}");
                return false;
            }
        }
    }
}