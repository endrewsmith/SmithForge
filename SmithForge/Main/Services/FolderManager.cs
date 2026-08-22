using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public static class FolderManager
    {
        // Переименовали корень в SF_Data (Smith Forge Data)
            private static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data");

            // Пути к подпапкам
            public static string ConfigDir => Path.Combine(Root, "Config");
            public static string DbDir => Path.Combine(Root, "Database");
            public static string CacheDir => Path.Combine(Root, "Cache", "Avatars");
            public static string AssetsDir => Path.Combine(Root, "Assets");
            public static string BadgesDir => Path.Combine(Root, "Assets", "Badges");

            public static void EnsureDirectoriesExist()
            {
                try
                {
                    // Создаем всё дерево (если папки есть, CreateDirectory их не тронет)
                    Directory.CreateDirectory(ConfigDir);
                    Directory.CreateDirectory(DbDir);
                    Directory.CreateDirectory(CacheDir); // Создаст и Cache, и Avatars
                    Directory.CreateDirectory(BadgesDir); // Создаст и Assets, и Badges

                    Debug.WriteLine($"[SYSTEM] Структура {Root} готова.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CRITICAL] Ошибка создания папок: {ex.Message}");
                }
            }

            // Методы для получения путей к конкретным файлам
            public static string GetDbPath() => Path.Combine(DbDir, "smith_forge.db");
            public static string GetConfigPath() => Path.Combine(ConfigDir, "settings.xml");

            // Хелпер для поиска аватарки юзера
            public static string GetAvatarPath(string extId) => Path.Combine(CacheDir, $"{extId}.png");
        }
    }
