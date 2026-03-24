using SmithForge.Main.Models;
using System;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace SmithForge.Main.Services
{
    public static class ConfigService
    {
        private static readonly string _configPath;
        private static readonly XmlSerializer _serializer;

        static ConfigService()
        {
            try
            {
                Debug.WriteLine("[ConfigService] Инициализация...");

                // Путь относительно папки с программой
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configDir = Path.Combine(baseDir, "SF_Data", "Config");

                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    Debug.WriteLine($"[ConfigService] Создана папка: {configDir}");
                }

                _configPath = Path.Combine(configDir, "settings.xml");
                _serializer = new XmlSerializer(typeof(AppSettings));

                Debug.WriteLine($"[ConfigService] Путь к настройкам: {_configPath}");
                Debug.WriteLine("[ConfigService] Инициализация завершена");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] ОШИБКА инициализации: {ex.Message}");
                throw;
            }
        }

        /// <summary> Загружает настройки. Если файла нет — создает настройки по умолчанию </summary>
        public static AppSettings Load()
        {
            try
            {
                Debug.WriteLine($"[ConfigService] Загрузка настроек из {_configPath}");

                if (File.Exists(_configPath))
                {
                    using var reader = new StreamReader(_configPath);
                    var settings = (AppSettings?)_serializer.Deserialize(reader);

                    if (settings != null)
                    {
                        // ПОЛНАЯ ИНИЦИАЛИЗАЦИЯ всех свойств, которые могут быть null
                        if (settings.CommandShortcuts == null)
                        {
                            settings.CommandShortcuts = new List<ShortcutItem>();
                            Debug.WriteLine("[ConfigService] CommandShortcuts был null, инициализирован пустым списком");
                        }

                        if (settings.CommandPrefixes == null)
                        {
                            settings.CommandPrefixes = new List<string>();
                            Debug.WriteLine("[ConfigService] CommandPrefixes был null, инициализирован пустым списком");
                        }

                        if (settings.RankThresholds == null)
                        {
                            settings.RankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };
                            Debug.WriteLine("[ConfigService] RankThresholds был null, инициализирован значениями по умолчанию");
                        }

                        Debug.WriteLine($"[ConfigService] Настройки загружены, сокращений: {settings.CommandShortcuts.Count}");
                        return settings;
                    }
                }

                Debug.WriteLine("[ConfigService] Файл не найден, создаем настройки по умолчанию");
                return CreateDefaultSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Ошибка загрузки: {ex.Message}");
                return CreateDefaultSettings();
            }
        }

        /// <summary> Создает настройки по умолчанию с предустановленными командами </summary>
        private static AppSettings CreateDefaultSettings()
        {
            Debug.WriteLine("[ConfigService] Создание настроек по умолчанию");

            var settings = new AppSettings
            {
                CommandShortcuts = new List<ShortcutItem>
                {
                    new ShortcutItem { Key = "ввв", Value = "!!важно" },
                    new ShortcutItem { Key = "вж", Value = "!!важно" },
                    new ShortcutItem { Key = "ввм", Value = "!!важно:м" },
                    new ShortcutItem { Key = "ввж", Value = "!!важно:ж" },
                    new ShortcutItem { Key = "вв0", Value = "!!важно:0" },
                    new ShortcutItem { Key = "вв1", Value = "!!важно:1" },
                    new ShortcutItem { Key = "вв2", Value = "!!важно:2" },
                    new ShortcutItem { Key = "вв3", Value = "!!важно:3" },
                    new ShortcutItem { Key = "вв4", Value = "!!важно:4" },
                    new ShortcutItem { Key = "вв5", Value = "!!важно:5" },
                    new ShortcutItem { Key = "вв6", Value = "!!важно:6" },
                    new ShortcutItem { Key = "вв7", Value = "!!важно:7" },
                },
                CommandPrefixes = new List<string> { "!", "/" },
                RankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 },
                MainChatMode = ChatDisplayMode.AppearAndFade,
                ShortsChatMode = ChatDisplayMode.AppearAndFade,
                ImportantChatMode = ChatDisplayMode.AppearAndFade,
                StickersChatMode = ChatDisplayMode.AppearAndFade
            };

            return settings;
        }

        /// <summary> Сохраняет настройки в XML-файл </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                Debug.WriteLine($"[ConfigService] Сохранение настроек в {_configPath}");

                // Убеждаемся, что все свойства инициализированы перед сохранением
                if (settings.CommandShortcuts == null)
                    settings.CommandShortcuts = new List<ShortcutItem>();

                if (settings.CommandPrefixes == null)
                    settings.CommandPrefixes = new List<string>();

                if (settings.RankThresholds == null)
                    settings.RankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };

                // Убираем дубликаты перед сохранением
                settings.CommandPrefixes = settings.CommandPrefixes.Distinct().ToList();

                // Создаем директорию если её нет
                string? directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(_configPath);
                _serializer.Serialize(writer, settings);

                Debug.WriteLine($"[ConfigService] Настройки сохранены");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Ошибка сохранения: {ex.Message}");
            }
        }
    }
}