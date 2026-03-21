using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services
{
    public static class ConfigService
    {
        private static readonly string _configPath;
        private static readonly JsonSerializerOptions _jsonOptions;

        static ConfigService()
        {
            try
            {
                Debug.WriteLine("[ConfigService] Инициализация...");

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appData, "SmithForge");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                    Debug.WriteLine($"[ConfigService] Создана папка: {appFolder}");
                }

                _configPath = Path.Combine(appFolder, "settings.json");
                Debug.WriteLine($"[ConfigService] Путь к настройкам: {_configPath}");

                // ПРАВИЛЬНЫЕ НАСТРОЙКИ ДЛЯ СЕРИАЛИЗАЦИИ
                _jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true,
                    IncludeFields = true,  // Добавлено
                    IgnoreReadOnlyProperties = false
                };

                Debug.WriteLine("[ConfigService] Инициализация завершена");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] ОШИБКА инициализации: {ex.Message}");
                throw;
            }
        }

        public static AppSettings Load()
        {
            try
            {
                Debug.WriteLine($"[ConfigService] Загрузка настроек из {_configPath}");

                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    Debug.WriteLine($"[ConfigService] Файл найден, размер: {json.Length} байт");

                    var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                    if (settings != null)
                    {
                        settings.CommandShortcuts ??= new Dictionary<string, string>();
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

        private static AppSettings CreateDefaultSettings()
        {
            Debug.WriteLine("[ConfigService] Создание настроек по умолчанию");

            var settings = new AppSettings
            {
                CommandShortcuts = new Dictionary<string, string>
                {
                    { "ввв", "!!важно" },
                    { "вж", "!!важно" },
                    { "ст", "!!st" },
                    { "ж", "!!жирный" },
                    { "к", "!!курсив" },
                    { "ц", "!!цвет" }
                }
            };

            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Debug.WriteLine($"[ConfigService] Сохранение настроек в {_configPath}");

                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(_configPath, json);

                Debug.WriteLine($"[ConfigService] Настройки сохранены, размер: {json.Length} байт");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConfigService] Ошибка сохранения: {ex.Message}");
            }
        }
    }
}