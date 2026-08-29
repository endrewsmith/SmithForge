using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Сервис для загрузки аватарок с GoodGame
    /// </summary>
    public static class GoodGameAvatarService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly string AvatarFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Avatars", "platform");

        static GoodGameAvatarService()
        {
            if (!Directory.Exists(AvatarFolder))
                Directory.CreateDirectory(AvatarFolder);

            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        }

        /// <summary>
        /// Получить URL аватарки по ID или имени канала
        /// </summary>
        public static async Task<string?> GetAvatarUrlByChannelId(string channelId, string channelName = null, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(channelId) && string.IsNullOrWhiteSpace(channelName))
                return null;

            Debug.WriteLine($"[GoodGameAvatar] Поиск аватарки для ID: {channelId}, имя: {channelName ?? "неизвестно"}");

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                Debug.WriteLine($"[GoodGameAvatar] Попытка {attempt} из {maxRetries}");

                try
                {
                    string avatarUrl = null;

                    // ✅ 1. Если есть имя канала - используем его (ГЛАВНЫЙ СПОСОБ)
                    if (!string.IsNullOrEmpty(channelName))
                    {
                        avatarUrl = await GetAvatarFromPage(channelName);
                        if (!string.IsNullOrEmpty(avatarUrl))
                        {
                            Debug.WriteLine($"[GoodGameAvatar] ✅ Найдена аватарка по имени: {avatarUrl}");
                            return avatarUrl;
                        }
                    }

                    // ✅ 2. Пробуем по ID (если это не похоже на имя)
                    if (!string.IsNullOrEmpty(channelId) && long.TryParse(channelId, out _))
                    {
                        // GoodGame не использует ID в URL, но пробуем на всякий случай
                        avatarUrl = await GetAvatarFromPage(channelId);
                        if (!string.IsNullOrEmpty(avatarUrl))
                        {
                            Debug.WriteLine($"[GoodGameAvatar] ✅ Найдена аватарка по ID: {avatarUrl}");
                            return avatarUrl;
                        }
                    }

                    // ✅ 3. Если channelId похож на имя (содержит буквы) - пробуем его
                    if (!string.IsNullOrEmpty(channelId) && !long.TryParse(channelId, out _) && string.IsNullOrEmpty(channelName))
                    {
                        avatarUrl = await GetAvatarFromPage(channelId);
                        if (!string.IsNullOrEmpty(avatarUrl))
                        {
                            Debug.WriteLine($"[GoodGameAvatar] ✅ Найдена аватарка по channelId как имени: {avatarUrl}");
                            return avatarUrl;
                        }
                    }

                    int delayMs = attempt * 800;
                    Debug.WriteLine($"[GoodGameAvatar] Аватарка не найдена, ждём {delayMs}мс...");
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GoodGameAvatar] Ошибка попытки {attempt}: {ex.Message}");
                    if (attempt == maxRetries)
                        throw;
                }
            }

            Debug.WriteLine($"[GoodGameAvatar] ❌ Аватарка не найдена после {maxRetries} попыток");
            return null;
        }

        /// <summary>
        /// Получить URL аватарки со страницы канала
        /// </summary>
        /// <summary>
        /// Получить URL аватарки со страницы канала
        /// </summary>
        private static async Task<string?> GetAvatarFromPage(string pageName)
        {
            try
            {
                // ✅ Правильный URL: https://goodgame.ru/{имя}
                string pageUrl = $"https://goodgame.ru/{pageName}";
                Debug.WriteLine($"[GoodGameAvatar] Загрузка: {pageUrl}");

                var response = await _httpClient.GetAsync(pageUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[GoodGameAvatar] Страница не найдена: {response.StatusCode}");
                    return null;
                }

                string html = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[GoodGameAvatar] HTML загружен, длина: {html.Length}");

                if (string.IsNullOrEmpty(html) || html.Length < 1000)
                    return null;

                // Проверяем на капчу
                if (html.Contains("captcha") || html.Contains("cf-challenge") || html.Contains("Checking your browser"))
                {
                    Debug.WriteLine("[GoodGameAvatar] ⚠️ Обнаружена капча или проверка браузера!");
                    return null;
                }

                // ============ СПОСОБ 1: Ищем avatar в JSON ============
                var jsonMatch = Regex.Match(html,
                    @"""avatar""\s*:\s*""([^""]+\.(?:jpg|jpeg|png|gif))""",
                    RegexOptions.IgnoreCase);
                if (jsonMatch.Success)
                {
                    string avatarFileName = jsonMatch.Groups[1].Value;

                    // ✅ ОЧИЩАЕМ URL от лишних символов
                    avatarFileName = avatarFileName.Replace("\\/", "/");

                    string avatarUrl;
                    if (avatarFileName.StartsWith("http"))
                    {
                        avatarUrl = avatarFileName;
                    }
                    else
                    {
                        // ✅ Если путь начинается с /files/avatars/ или /images/avatars/
                        if (avatarFileName.StartsWith("/files/avatars/") || avatarFileName.StartsWith("/images/avatars/"))
                        {
                            avatarUrl = $"https://static.goodgame.ru{avatarFileName}";
                        }
                        else
                        {
                            avatarUrl = $"https://static.goodgame.ru/images/avatars/{avatarFileName}";
                        }
                    }

                    Debug.WriteLine($"[GoodGameAvatar] Найдена аватарка (JSON): {avatarUrl}");
                    return NormalizeAvatarUrl(avatarUrl);
                }

                // ============ СПОСОБ 2: Ищем streamer.avatar ============
                var streamerMatch = Regex.Match(html,
                    @"""streamer""\s*:\s*\{[^}]*""avatar""\s*:\s*""([^""]+\.(?:jpg|jpeg|png|gif))""",
                    RegexOptions.IgnoreCase);
                if (streamerMatch.Success)
                {
                    string avatarFileName = streamerMatch.Groups[1].Value;
                    avatarFileName = avatarFileName.Replace("\\/", "/");

                    string avatarUrl;
                    if (avatarFileName.StartsWith("http"))
                    {
                        avatarUrl = avatarFileName;
                    }
                    else
                    {
                        if (avatarFileName.StartsWith("/files/avatars/") || avatarFileName.StartsWith("/images/avatars/"))
                        {
                            avatarUrl = $"https://static.goodgame.ru{avatarFileName}";
                        }
                        else
                        {
                            avatarUrl = $"https://static.goodgame.ru/images/avatars/{avatarFileName}";
                        }
                    }
                    Debug.WriteLine($"[GoodGameAvatar] Найдена аватарка (streamer): {avatarUrl}");
                    return NormalizeAvatarUrl(avatarUrl);
                }

                // ============ СПОСОБ 3: Ищем avatar в HTML ============
                var avatarMatch = Regex.Match(html,
                    @"avatar[""\s:]+([^""\s,]+\.(?:jpg|jpeg|png|gif))",
                    RegexOptions.IgnoreCase);
                if (avatarMatch.Success)
                {
                    string avatarFileName = avatarMatch.Groups[1].Value;
                    avatarFileName = avatarFileName.Replace("\\/", "/");

                    string avatarUrl;
                    if (avatarFileName.StartsWith("http"))
                    {
                        avatarUrl = avatarFileName;
                    }
                    else
                    {
                        if (avatarFileName.StartsWith("/files/avatars/") || avatarFileName.StartsWith("/images/avatars/"))
                        {
                            avatarUrl = $"https://static.goodgame.ru{avatarFileName}";
                        }
                        else
                        {
                            avatarUrl = $"https://static.goodgame.ru/images/avatars/{avatarFileName}";
                        }
                    }
                    Debug.WriteLine($"[GoodGameAvatar] Найдена аватарка (HTML): {avatarUrl}");
                    return NormalizeAvatarUrl(avatarUrl);
                }

                // ============ СПОСОБ 4: Прямая ссылка по шаблону ============
                var directMatch = Regex.Match(html,
                    @"https://static\.goodgame\.ru/(?:images|files)/avatars/[^""\s]+\.(?:jpg|jpeg|png|gif)",
                    RegexOptions.IgnoreCase);
                if (directMatch.Success)
                {
                    string avatarUrl = directMatch.Value;
                    avatarUrl = avatarUrl.Replace("\\/", "/");
                    Debug.WriteLine($"[GoodGameAvatar] Найдена аватарка (direct): {avatarUrl}");
                    return NormalizeAvatarUrl(avatarUrl);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoodGameAvatar] Ошибка: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Нормализовать URL аватарки
        /// </summary>
        private static string NormalizeAvatarUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;

            // Убеждаемся, что протокол HTTPS
            if (url.StartsWith("http://"))
                url = "https://" + url.Substring(7);

            // Если URL неполный, добавляем домен
            if (url.StartsWith("/"))
                url = $"https://static.goodgame.ru{url}";

            return url;
        }

        /// <summary>
        /// Скачать аватарку
        /// </summary>
        /// <summary>
        /// Скачать аватарку
        /// </summary>
        public static async Task<string?> DownloadAvatarAsync(string userId, string avatarUrl, bool forceUpdate = false)
        {
            try
            {
                if (string.IsNullOrEmpty(avatarUrl))
                    return null;

                // 🌟 Фиксируем расширение. Все файлы аватарок будут строго .png (или .jpg)
                // Это гарантирует, что имя файла всегда будет ровно одно: userId.png
                string extension = ".png";
                string avatarPath = Path.Combine(AvatarFolder, $"{userId}{extension}");

                // Если принудительное обновление не требуется и файл уже есть — сразу отдаем его
                if (!forceUpdate && File.Exists(avatarPath))
                {
                    return avatarPath;
                }

                Debug.WriteLine($"[GoodGameAvatar] Загрузка аватарки. Назначенный путь: {avatarPath}");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                client.DefaultRequestHeaders.Add("Referer", "https://goodgame.ru");

                // Скачиваем байты
                var bytes = await client.GetByteArrayAsync(avatarUrl);

                // Гарантируем, что папка существует
                Directory.CreateDirectory(AvatarFolder);

                // Перед перезаписью жестко удаляем старый файл, если он там был
                if (File.Exists(avatarPath))
                {
                    try
                    {
                        RemoveReadOnlyAttribute(avatarPath);
                        File.Delete(avatarPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[GoodGameAvatar] Не удалось удалить старый файл перед перезаписью: {ex.Message}");
                    }
                }

                // Сохраняем поверх
                await File.WriteAllBytesAsync(avatarPath, bytes);
                RemoveReadOnlyAttribute(avatarPath);

                Debug.WriteLine($"[GoodGameAvatar] Аватар успешно сохранен в единственный файл: {avatarPath}");
                return avatarPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoodGameAvatar] Ошибка скачивания: {ex.Message}");
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
                    Debug.WriteLine($"[GoodGameAvatar] Снят атрибут ReadOnly: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoodGameAvatar] Ошибка снятия ReadOnly: {ex.Message}");
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
                    Debug.WriteLine($"[GoodGameAvatar] Удалена: {path}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GoodGameAvatar] Ошибка удаления: {ex.Message}");
                return false;
            }
        }
    }
}