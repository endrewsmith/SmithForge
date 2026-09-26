// Features/InfoSystem/SoundPageService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmithForge.Features.InfoSystem
{
    public class SoundPageService
    {
        private readonly string _soundsRoot;
        private readonly string _pagesDir;
        private readonly string _soundsPagesDir;
        private readonly Dictionary<string, SoundPackInfo> _packCache = new();
        private readonly Dictionary<string, string> _packNameToId = new();
        private readonly Dictionary<int, string> _packNumberToId = new();
        private readonly Dictionary<string, int> _packIdToNumber = new();
        private int _packCounter = 0;

        public SoundPageService()
        {
            _soundsRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Sounds");

            _pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Html", "InfoPages");

            _soundsPagesDir = Path.Combine(_pagesDir, "sounds");

            Directory.CreateDirectory(_pagesDir);
            Directory.CreateDirectory(_soundsPagesDir);
        }

        // ============================================================
        // МОДЕЛИ
        // ============================================================

        public class SoundPackInfo
        {
            public string PackId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string PreviewSound { get; set; } = string.Empty; // первый звук для превью
            public List<SoundInfo> Sounds { get; set; } = new();
            public int SoundCount => Sounds.Count;
            public string FolderName { get; set; } = string.Empty;
            public int Number { get; set; }
        }

        public class SoundInfo
        {
            public string Id { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
            public string Duration { get; set; } = string.Empty; // можно добавить позже
        }

        // ============================================================
        // СКАНИРОВАНИЕ ПАПОК
        // ============================================================

        public void ScanPacks()
        {
            _packCache.Clear();
            _packNameToId.Clear();
            _packNumberToId.Clear();
            _packIdToNumber.Clear();
            _packCounter = 0;

            if (!Directory.Exists(_soundsRoot))
            {
                Directory.CreateDirectory(_soundsRoot);
                Debug.WriteLine($"[SoundPage] Папка создана: {_soundsRoot}");
                return;
            }

            var allDirs = Directory.GetDirectories(_soundsRoot)
                .OrderBy(d => d)
                .ToList();

            if (allDirs.Count == 0)
            {
                Debug.WriteLine("[SoundPage] ❌ Нет папок со звуками");
                return;
            }

            foreach (var dir in allDirs)
            {
                var folderName = Path.GetFileName(dir);
                if (folderName.StartsWith(".")) continue;
                if (folderName == "Thumbs.db") continue;

                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") ||
                                f.EndsWith(".ogg") || f.EndsWith(".flac") ||
                                f.EndsWith(".m4a") || f.EndsWith(".aac"))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                if (files.Count == 0) continue;

                _packCounter++;

                var baseId = GeneratePackId(folderName);
                var packId = $"{baseId}_{_packCounter}";

                _packNameToId[folderName] = packId;
                _packNumberToId[_packCounter] = packId;
                _packIdToNumber[packId] = _packCounter;

                var pack = new SoundPackInfo
                {
                    PackId = packId,
                    Number = _packCounter,
                    FolderName = folderName,
                    DisplayName = GetDisplayName(folderName),
                    PreviewSound = files.First(),
                    Sounds = files.Select(f => new SoundInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(f),
                        FileName = Path.GetFileName(f),
                        FilePath = f,
                        Extension = Path.GetExtension(f).ToLower()
                    }).ToList()
                };

                _packCache[packId] = pack;
                Debug.WriteLine($"[SoundPage] #{_packCounter} {folderName} → '{packId}' ({pack.Sounds.Count} звуков)");
            }

            Debug.WriteLine($"[SoundPage] Всего паков: {_packCache.Count}");
        }

        // ============================================================
        // ПОИСК ПАКОВ
        // ============================================================

        public string ResolvePackId(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            if (_packCache.ContainsKey(input))
                return input;

            if (_packNameToId.TryGetValue(input, out var packId))
                return packId;

            if (int.TryParse(input, out int number) && _packNumberToId.TryGetValue(number, out packId))
                return packId;

            return null;
        }

        public bool PackExists(string packId) => _packCache.ContainsKey(packId);
        public SoundPackInfo? GetPack(string packId) =>
            _packCache.TryGetValue(packId, out var pack) ? pack : null;

        // ============================================================
        // ГЕНЕРАЦИЯ СТРАНИЦ
        // ============================================================

        public int GenerateAllPages()
        {
            ScanPacks();

            if (_packCache.Count == 0)
            {
                Debug.WriteLine("[SoundPage] ❌ Нет паков для генерации");
                return 0;
            }

            var generatedCount = 0;

            // Главная страница sounds.html
            var soundsHtml = GeneratePacksPageHtml();
            var soundsPath = Path.Combine(_pagesDir, "sounds.html");
            File.WriteAllText(soundsPath, soundsHtml, Encoding.UTF8);
            generatedCount++;
            Debug.WriteLine($"[SoundPage] ✅ Сгенерирована: sounds.html");

            // Страницы паков в папку sounds/
            foreach (var pack in _packCache.Values)
            {
                var packHtml = GeneratePackPageHtml(pack);
                var packPath = Path.Combine(_soundsPagesDir, $"{pack.PackId}.html");
                File.WriteAllText(packPath, packHtml, Encoding.UTF8);
                generatedCount++;
                Debug.WriteLine($"[SoundPage] ✅ Сгенерирована: sounds/{pack.PackId}.html");
            }

            Debug.WriteLine($"[SoundPage] ✅ Всего сгенерировано страниц: {generatedCount}");
            return generatedCount;
        }

        // ============================================================
        // ГЕНЕРАЦИЯ HTML
        // ============================================================

        private string GeneratePacksPageHtml()
        {
            if (_packCache.Count == 0)
            {
                return @"<div class='page-sounds-empty'>
    <h1>🔊 Звуки</h1>
    <p>❌ Нет доступных паков со звуками</p>
    <p style='color:#888;font-size:12px;'>Добавьте папки с аудио в SF_Data/Assets/Sounds/</p>
</div>";
            }

            var sb = new StringBuilder();
            sb.AppendLine("<div class='page-sounds-packs'>");
            sb.AppendLine("  <h1>🔊 Звуки</h1>");
            sb.AppendLine($"  <p>Доступно паков: {_packCache.Count}</p>");
            sb.AppendLine("  <div class='sound-grid'>");

            foreach (var pack in _packCache.Values.OrderBy(p => p.DisplayName))
            {
                // Для превью используем иконку вместо картинки
                var previewIcon = "🎵";

                sb.AppendLine($"  <div class='sound-card'>");
                sb.AppendLine($"    <div class='sound-preview'>");
                sb.AppendLine($"      <span class='sound-icon'>{previewIcon}</span>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"    <div class='sound-info'>");
                sb.AppendLine($"      <span class='sound-number'>#{pack.Number}</span>");
                sb.AppendLine($"      <span class='sound-name'>{pack.DisplayName}</span>");
                sb.AppendLine($"      <span class='sound-count'>🔊 {pack.SoundCount} звуков</span>");
                sb.AppendLine($"      <span class='sound-id'>ID: {pack.PackId}</span>");
                sb.AppendLine($"      <a href='!!info:sounds/{pack.PackId}' class='sound-link'>📂 Открыть</a>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"  </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GeneratePackPageHtml(SoundPackInfo pack)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<div class='page-sounds-pack'>");
            sb.AppendLine($"  <h1>{pack.DisplayName}</h1>");
            sb.AppendLine($"  <p>#{pack.Number} • {pack.SoundCount} звуков</p>");
            sb.AppendLine("  <div class='sound-list'>");

            foreach (var sound in pack.Sounds)
            {
                // Получаем путь для проигрывания
                var soundPath = sound.FilePath.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/").Replace("\\", "/");

                sb.AppendLine($"  <div class='sound-item'>");
                sb.AppendLine($"    <div class='sound-play'>");
                sb.AppendLine($"      <button class='play-btn' data-sound='{soundPath}'>▶</button>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"    <div class='sound-info'>");
                sb.AppendLine($"      <span class='sound-id'>#{sound.Id}</span>");
                sb.AppendLine($"      <span class='sound-command'>!!snd:{pack.PackId}:{sound.Id}</span>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"  </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine($"  <a href='!!info:sounds' class='back-link'>⬅️ Назад к пакам</a>");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ============================================================

        private string GeneratePackId(string folderName)
        {
            var id = folderName
                .ToLower()
                .Replace(" ", "_")
                .Replace("!", "")
                .Replace("?", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("-", "_");

            var translit = new Dictionary<string, string>
            {
                ["а"] = "a",
                ["б"] = "b",
                ["в"] = "v",
                ["г"] = "g",
                ["д"] = "d",
                ["е"] = "e",
                ["ё"] = "e",
                ["ж"] = "zh",
                ["з"] = "z",
                ["и"] = "i",
                ["й"] = "y",
                ["к"] = "k",
                ["л"] = "l",
                ["м"] = "m",
                ["н"] = "n",
                ["о"] = "o",
                ["п"] = "p",
                ["р"] = "r",
                ["с"] = "s",
                ["т"] = "t",
                ["у"] = "u",
                ["ф"] = "f",
                ["х"] = "h",
                ["ц"] = "ts",
                ["ч"] = "ch",
                ["ш"] = "sh",
                ["щ"] = "shch",
                ["ъ"] = "",
                ["ы"] = "y",
                ["ь"] = "",
                ["э"] = "e",
                ["ю"] = "yu",
                ["я"] = "ya"
            };

            foreach (var pair in translit)
            {
                id = id.Replace(pair.Key, pair.Value);
            }

            id = Regex.Replace(id, @"[^a-z0-9_]", "");

            if (string.IsNullOrEmpty(id))
            {
                id = "sound_" + DateTime.Now.Ticks.ToString().Substring(10);
            }

            return id;
        }

        private string GetDisplayName(string folderName)
        {
            if (Regex.IsMatch(folderName, @"\p{Cs}"))
            {
                return folderName;
            }
            return $"📁 {folderName}";
        }

        public int GetPackCount() => _packCache.Count;
        public List<string> GetAllPackIds() => _packCache.Keys.ToList();

        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return CompareNatural(x, y);
            }

            private int CompareNatural(string strA, string strB)
            {
                return Regex.Replace(strA, @"\d+", m => m.Value.PadLeft(10, '0'))
                    .CompareTo(Regex.Replace(strB, @"\d+", m => m.Value.PadLeft(10, '0')));
            }
        }
    }
}