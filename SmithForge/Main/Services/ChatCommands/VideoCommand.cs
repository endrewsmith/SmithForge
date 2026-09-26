using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class VideoCommand : BaseCommand
    {
        public override bool IsDashboardVisible => false;
        public override string Name => "video";
        public override IEnumerable<string> Aliases => new[] { "vid", "видео", "клип" };
        public override string Description => "Отправить видео в медиа-чат: !!video:название_видео.mp4";
        public override int Cost => 5;
        public override int MinRank => 0;
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[VideoCommand] НАЧАЛО - Аргументы: {string.Join(", ", info.Arguments)}");

            if (info.Arguments.Count == 0)
            {
                msg.Message = "❌ Укажите название видео: !!video:myvideo.mp4";
                msg.IsProcessedByCommand = true;
                return;
            }

            string videoFileName = info.Arguments[0];
            Debug.WriteLine($"[VideoCommand] Запрошено видео: {videoFileName}");

            // Путь к папке с видео
            string videosRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Videos");

            if (!Directory.Exists(videosRoot))
            {
                Directory.CreateDirectory(videosRoot);
                Debug.WriteLine($"[VideoCommand] Создана папка: {videosRoot}");
            }

            // Ищем видео файл
            var videoPath = FindVideoFile(videosRoot, videoFileName);

            if (string.IsNullOrEmpty(videoPath))
            {
                msg.Message = $"❌ Видео '{videoFileName}' не найдено";
                msg.IsProcessedByCommand = true;
                Debug.WriteLine($"[VideoCommand] Видео не найдено");
                return;
            }

            // Отправляем видео в медиа-чат
            msg.Message = $"<video path='{videoPath}' />";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[VideoCommand] КОНЕЦ - видео: {videoPath}");
        }

        private string FindVideoFile(string rootDir, string searchPattern)
        {
            // Поддерживаемые форматы
            string[] extensions = { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".gif", ".webp" };

            // Проверяем расширения
            foreach (var ext in extensions)
            {
                string fullPath = Path.Combine(rootDir, searchPattern);
                if (!Path.HasExtension(fullPath))
                {
                    fullPath += ext;
                }

                if (File.Exists(fullPath))
                {
                    Debug.WriteLine($"[VideoCommand] Найдено видео: {fullPath}");
                    return fullPath;
                }
            }

            // Ищем по всем файлам в папке
            var files = Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            // Ищем частичное совпадение
            var match = files.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f)
                    .Contains(searchPattern, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                Debug.WriteLine($"[VideoCommand] Найдено видео по частичному совпадению: {match}");
                return match;
            }

            return null;
        }
    }
}