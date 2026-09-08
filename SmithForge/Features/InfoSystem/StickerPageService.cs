// Features/InfoSystem/StickerPageService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SmithForge.Features.InfoSystem
{
    public class StickerPageService
    {
        private readonly string _stickersRoot;
        private readonly string _pagesDir;
        private readonly string _stickersPagesDir; // ← НОВАЯ ПАПКА
        private readonly Dictionary<string, StickerPackInfo> _packCache = new();
        private readonly Dictionary<string, string> _packNameToId = new();
        private readonly Dictionary<int, string> _packNumberToId = new();
        private readonly Dictionary<string, int> _packIdToNumber = new();
        private int _packCounter = 0;

        public StickerPageService()
        {
            _stickersRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Stickers");

            _pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "InfoWeb", "Pages");

            // ✅ НОВАЯ ПАПКА ДЛЯ СТРАНИЦ СТИКЕРОВ
            _stickersPagesDir = Path.Combine(_pagesDir, "stickers");

            Directory.CreateDirectory(_pagesDir);
            Directory.CreateDirectory(_stickersPagesDir);
        }

        // ============================================================
        // МОДЕЛИ
        // ============================================================

        public class StickerPackInfo
        {
            public string PackId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string PreviewImage { get; set; } = string.Empty;
            public List<StickerInfo> Stickers { get; set; } = new();
            public int StickerCount => Stickers.Count;
            public string FolderName { get; set; } = string.Empty;
            public int Number { get; set; }
        }

        public class StickerInfo
        {
            public string Id { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string FilePath { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
            public bool IsAnimated => Extension == ".gif" || Extension == ".webp" || Extension == ".apng";
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

            if (!Directory.Exists(_stickersRoot))
            {
                Directory.CreateDirectory(_stickersRoot);
                Debug.WriteLine($"[StickerPage] Папка создана: {_stickersRoot}");
                return;
            }

            var allDirs = Directory.GetDirectories(_stickersRoot)
                .OrderBy(d => d)
                .ToList();

            if (allDirs.Count == 0)
            {
                Debug.WriteLine("[StickerPage] ❌ Нет папок со стикерами");
                return;
            }

            foreach (var dir in allDirs)
            {
                var folderName = Path.GetFileName(dir);
                if (folderName.StartsWith(".")) continue;
                if (folderName == "Thumbs.db") continue;

                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") ||
                                f.EndsWith(".gif") || f.EndsWith(".webp") || f.EndsWith(".apng"))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                if (files.Count == 0) continue;

                _packCounter++;

                var baseId = GeneratePackId(folderName);
                var packId = $"{baseId}_{_packCounter}";

                // ✅ СОХРАНЯЕМ СВЯЗИ
                _packNameToId[folderName] = packId;
                _packNumberToId[_packCounter] = packId;
                _packIdToNumber[packId] = _packCounter;

                var pack = new StickerPackInfo
                {
                    PackId = packId,
                    Number = _packCounter,
                    FolderName = folderName,
                    DisplayName = GetDisplayName(folderName),
                    PreviewImage = files.First(),
                    Stickers = files.Select(f => new StickerInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(f),
                        FileName = Path.GetFileName(f),
                        FilePath = f,
                        Extension = Path.GetExtension(f).ToLower()
                    }).ToList()
                };

                _packCache[packId] = pack;
                Debug.WriteLine($"[StickerPage] #{_packCounter} {folderName} → '{packId}' ({pack.Stickers.Count} стикеров)");
            }

            Debug.WriteLine($"[StickerPage] Всего паков: {_packCache.Count}");
        }

        // ============================================================
        // ПОИСК ПАКОВ
        // ============================================================

        public string ResolvePackId(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            // 1️⃣ Если это ID пака с номером (spotty_1)
            if (_packCache.ContainsKey(input))
                return input;

            // 2️⃣ Если это имя папки (spotty)
            if (_packNameToId.TryGetValue(input, out var packId))
                return packId;

            // 3️⃣ Если это число — номер пака (!!st:2:2)
            if (int.TryParse(input, out int number) && _packNumberToId.TryGetValue(number, out packId))
                return packId;

            return null;
        }

        public string GetPackIdByName(string name) =>
            _packNameToId.TryGetValue(name, out var id) ? id : null;

        public string GetPackIdByNumber(int number) =>
            _packNumberToId.TryGetValue(number, out var id) ? id : null;

        public int GetPackNumber(string packId) =>
            _packIdToNumber.TryGetValue(packId, out var number) ? number : 0;

        public bool PackExists(string packId) => _packCache.ContainsKey(packId);
        public StickerPackInfo? GetPack(string packId) =>
            _packCache.TryGetValue(packId, out var pack) ? pack : null;

        // ============================================================
        // ГЕНЕРАЦИЯ СТРАНИЦ
        // ============================================================

        public int GenerateAllPages()
        {
            ScanPacks();

            if (_packCache.Count == 0)
            {
                Debug.WriteLine("[StickerPage] ❌ Нет паков для генерации");
                return 0;
            }

            var generatedCount = 0;

            // ✅ 1. Генерируем главную страницу stickers.html (в корне Pages)
            var stickersHtml = GeneratePacksPageHtml();
            var stickersPath = Path.Combine(_pagesDir, "stickers.html");
            File.WriteAllText(stickersPath, stickersHtml, Encoding.UTF8);
            generatedCount++;
            Debug.WriteLine($"[StickerPage] ✅ Сгенерирована: stickers.html");

            // ✅ 2. Генерируем страницы паков в папку stickers/
            foreach (var pack in _packCache.Values)
            {
                var packHtml = GeneratePackPageHtml(pack);
                var packPath = Path.Combine(_stickersPagesDir, $"{pack.PackId}.html");
                File.WriteAllText(packPath, packHtml, Encoding.UTF8);
                generatedCount++;
                Debug.WriteLine($"[StickerPage] ✅ Сгенерирована: stickers/{pack.PackId}.html");
            }

            Debug.WriteLine($"[StickerPage] ✅ Всего сгенерировано страниц: {generatedCount}");
            return generatedCount;
        }

        // ============================================================
        // ГЕНЕРАЦИЯ HTML
        // ============================================================

        private string GeneratePacksPageHtml()
        {
            if (_packCache.Count == 0)
            {
                return @"<div class='page-stickers-empty'>
    <h1>🎨 Стикеры</h1>
    <p>❌ Нет доступных паков со стикерами</p>
    <p style='color:#888;font-size:12px;'>Добавьте папки с картинками в SF_Data/Assets/Stickers/</p>
</div>";
            }

            var sb = new StringBuilder();
            sb.AppendLine("<div class='page-stickers-packs'>");
            sb.AppendLine("  <h1>🎨 Стикеры</h1>");
            sb.AppendLine($"  <p>Доступно паков: {_packCache.Count}</p>");
            sb.AppendLine("  <div class='pack-grid'>");

            foreach (var pack in _packCache.Values.OrderBy(p => p.DisplayName))
            {
                var previewPath = pack.PreviewImage.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/").Replace("\\", "/");

                sb.AppendLine($"  <div class='pack-card'>");
                sb.AppendLine($"    <div class='pack-preview'>");
                sb.AppendLine($"      <img src='{previewPath}' alt='{pack.DisplayName}' />");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"    <div class='pack-info'>");
                sb.AppendLine($"      <span class='pack-number'>#{pack.Number}</span>");
                sb.AppendLine($"      <span class='pack-name'>{pack.DisplayName}</span>");
                sb.AppendLine($"      <span class='pack-count'>🎨 {pack.StickerCount} стикеров</span>");
                sb.AppendLine($"      <span class='pack-id'>ID: {pack.PackId}</span>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"  </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GeneratePackPageHtml(StickerPackInfo pack)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<div class='page-stickers-pack'>");
            sb.AppendLine($"  <h1>{pack.DisplayName}</h1>");
            sb.AppendLine($"  <p>#{pack.Number} • {pack.StickerCount} стикеров</p>");
            sb.AppendLine("  <div class='sticker-grid'>");

            foreach (var sticker in pack.Stickers)
            {
                var stickerPath = sticker.FilePath.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/").Replace("\\", "/");

                sb.AppendLine($"  <div class='sticker-card'>");
                sb.AppendLine($"    <div class='sticker-preview'>");
                sb.AppendLine($"      <img src='{stickerPath}' alt='{sticker.Id}' />");
                if (sticker.IsAnimated)
                {
                    sb.AppendLine($"      <span class='sticker-badge'>🎬 GIF</span>");
                }
                sb.AppendLine($"    </div>");
                sb.AppendLine($"    <div class='sticker-info'>");
                sb.AppendLine($"      <span class='sticker-id'>#{sticker.Id}</span>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"  </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine($"  <a href='!!info:stickers' class='back-link'>⬅️ Назад к пакам</a>");
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
                id = "pack_" + DateTime.Now.Ticks.ToString().Substring(10);
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