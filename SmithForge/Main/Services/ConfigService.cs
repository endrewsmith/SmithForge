using SmithForge.Main.Models;
using SmithForge.Main.Services.SmithForge.Main.Services;
using System;
using System.IO;
using System.Xml.Serialization;

namespace SmithForge.Main.Services
{
    public static class ConfigService
    {
        // Теперь путь берется динамически из нашего менеджера папок
        private static string FilePath => FolderManager.GetConfigPath();

        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(AppSettings));

        /// <summary> Записывает настройки в SF_Data/Config/settings.xml </summary>
        public static void Save(AppSettings settings)
        {
            // Используем FilePath вместо FileName
            using var writer = new StreamWriter(FilePath);
            Serializer.Serialize(writer, settings);
        }

        /// <summary> Читает настройки. Если файла нет — создает новые. </summary>
        public static AppSettings Load()
        {
            // Проверяем наличие файла по полному пути
            if (!File.Exists(FilePath)) return new AppSettings();

            using var reader = new StreamReader(FilePath);
            return (AppSettings)Serializer.Deserialize(reader) ?? new AppSettings();
        }
    }
}
