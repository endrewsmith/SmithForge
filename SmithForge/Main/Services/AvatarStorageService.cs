using System;
using System.IO;

namespace SmithForge.Main.Services
{
    public static class AvatarStorageService
    {
        private static readonly string BaseAvatarFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Assets", "Avatars");

        static AvatarStorageService()
        {
            // Создаем все необходимые папки
            Directory.CreateDirectory(Path.Combine(BaseAvatarFolder, "default"));
            Directory.CreateDirectory(Path.Combine(BaseAvatarFolder, "platform", "youtube"));
            Directory.CreateDirectory(Path.Combine(BaseAvatarFolder, "platform", "twitch"));
            Directory.CreateDirectory(Path.Combine(BaseAvatarFolder, "platform", "goodgame"));
            Directory.CreateDirectory(Path.Combine(BaseAvatarFolder, "custom"));
        }

        /// <summary>
        /// Получить путь к аватарке с учетом категории
        /// </summary>
        public static string GetAvatarPath(string userId, string category = "custom")
        {
            string avatarPath = Path.Combine(BaseAvatarFolder, category, $"{userId}.png");
            return File.Exists(avatarPath) ? avatarPath : null;
        }

        /// <summary>
        /// Сохранить аватарку в указанную категорию
        /// </summary>
        public static string SaveAvatar(string userId, byte[] imageData, string category = "custom")
        {
            try
            {
                string categoryFolder = Path.Combine(BaseAvatarFolder, category);
                Directory.CreateDirectory(categoryFolder);

                string avatarPath = Path.Combine(categoryFolder, $"{userId}.png");
                File.WriteAllBytes(avatarPath, imageData);

                System.Diagnostics.Debug.WriteLine($"[AvatarStorage] Сохранена в {category}: {avatarPath}");
                return avatarPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AvatarStorage] Ошибка: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получить аватарку по умолчанию
        /// </summary>
        public static string GetDefaultAvatar()
        {
            string defaultPath = Path.Combine(BaseAvatarFolder, "default", "default.png");
            return File.Exists(defaultPath) ? defaultPath : null;
        }

        /// <summary>
        /// Приоритетный поиск аватарки (сначала custom, потом platform, потом default)
        /// </summary>
        public static string FindAvatar(string userId, string platform = null)
        {
            // 1. Сначала ищем в custom (установленные командой)
            string customPath = GetAvatarPath(userId, "custom");
            if (customPath != null) return customPath;

            // 2. Потом в platform (автоматические из соцсетей)
            if (!string.IsNullOrEmpty(platform))
            {
                string platformPath = GetAvatarPath(userId, $"platform/{platform}");
                if (platformPath != null) return platformPath;
            }

            // 3. Возвращаем default
            return GetDefaultAvatar();
        }
    }
}