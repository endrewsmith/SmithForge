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
                "Html", "InfoPages");

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
                .OrderBy(d => d, new NaturalStringComparer())
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

                // ✅ Парсим номер из имени папки (gaechka_1 → 1)
                int packNumber = ExtractNumberFromName(folderName, _packCounter);

                // ✅ packId = имя папки (gaechka_1 → gaechka_1)
                var packId = GeneratePackId(folderName);

                // ✅ СОХРАНЯЕМ СВЯЗИ
                _packNameToId[folderName] = packId;
                _packNumberToId[packNumber] = packId;
                _packIdToNumber[packId] = packNumber;

                var pack = new StickerPackInfo
                {
                    PackId = packId,
                    Number = packNumber,
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
                Debug.WriteLine($"[StickerPage] #{packNumber} {folderName} → '{packId}' ({pack.Stickers.Count} стикеров)");
            }

            Debug.WriteLine($"[StickerPage] Всего паков: {_packCache.Count}");
        }

        // ✅ Извлечение номера из НАЧАЛА имени папки (001_папка → 1)
        private int ExtractNumberFromName(string folderName, int fallback)
        {
            var match = Regex.Match(folderName, @"^(\d+)_");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            {
                return number;
            }
            return fallback;
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

            // ✅ ГАРАНТИРОВАННО СОЗДАЁМ ПАПКИ ПЕРЕД ГЕНЕРАЦИЕЙ
            Directory.CreateDirectory(_pagesDir);
            Directory.CreateDirectory(_stickersPagesDir);

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
                return @"<div class='packs-page'>
    <div class='packs-header'>
        <div class='packs-header-name'>Стикеры</div>
        <div class='packs-header-count'>0 паков</div>
    </div>
    <p style='color:#888;font-size:12px;'>Добавьте папки с картинками в SF_Data/Assets/Stickers/</p>
</div>";
            }

            var sb = new StringBuilder();
            sb.AppendLine("<div class='packs-page'>");

            // ============================================================
            // ПЛАШКА-ЗАГОЛОВОК
            // ============================================================
            sb.AppendLine("  <div class='packs-header'>");
            sb.AppendLine("    <div class='packs-header-name'>Паки стикеров</div>");
            sb.AppendLine("    <div class='packs-header-cmd-row'>");
            sb.AppendLine("      <span class='packs-cmd packs-cmd-main'>ссс</span>");
            sb.AppendLine("      <span class='packs-cmd packs-cmd-main'>!!info:stickers</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");

            // ============================================================
            // СЕТКА ПАКОВ
            // ============================================================
            sb.AppendLine("  <div class='packs-grid'>");

            foreach (var pack in _packCache.Values.OrderBy(p => p.DisplayName))
            {
                // Убираем числовой префикс в начале: 001_папка → папка
                string cleanName = System.Text.RegularExpressions.Regex.Replace(
                    pack.FolderName, @"^\d+_", "");

                var previewPath = pack.PreviewImage.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/").Replace("\\", "/");

                sb.AppendLine("    <div class='pack-card'>");

                // Превью
                sb.AppendLine("      <div class='pack-preview'>");
                sb.AppendLine($"        <img src='{previewPath}' alt='{cleanName}' />");
                sb.AppendLine("      </div>");

                // Плашка команды
                // Название пака + плашка команды
                sb.AppendLine("      <div class='pack-info'>");
                sb.AppendLine($"        <span class='pack-name'>{cleanName}</span>");
                sb.AppendLine($"        <span class='pack-cmd'>сс{pack.Number}</span>");
                sb.AppendLine("      </div>");

                sb.AppendLine("    </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GeneratePackPageHtml(StickerPackInfo pack)
        {
            // Убираем числовой префикс в начале: 001_папка → папка
            string cleanName = System.Text.RegularExpressions.Regex.Replace(
                pack.FolderName, @"^\d+_", "");

            var sb = new StringBuilder();
            sb.AppendLine("<div class='page-stickers-pack'>");

            // ============================================================
            // ПЛАШКА-ЗАГОЛОВОК: название пака + команда рандома
            // ============================================================
            sb.AppendLine("  <div class='stk-header'>");
            sb.AppendLine($"    <div class='stk-header-name'>{cleanName}</div>");
            sb.AppendLine("    <div class='stk-header-cmd-row'>");
            sb.AppendLine($"      <span class='stk-cmd stk-cmd-main'>с{pack.Number}с</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");

            // ============================================================
            // СЕТКА СТИКЕРОВ — старая разметка, ничего не меняется
            // ============================================================
            sb.AppendLine("  <div class='sticker-grid'>");

            foreach (var sticker in pack.Stickers)
            {
                var stickerPath = sticker.FilePath.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/").Replace("\\", "/");

                string shortCommand = $"с{pack.Number}с{sticker.Id.TrimStart('0')}";

                sb.AppendLine($"  <div class='sticker-card'>");
                sb.AppendLine($"    <div class='sticker-preview'>");
                sb.AppendLine($"      <img src='{stickerPath}' alt='{sticker.Id}' />");
                if (sticker.IsAnimated)
                {
                    sb.AppendLine($"      <span class='sticker-badge'>🎬 GIF</span>");
                }
                sb.AppendLine($"    </div>");
                sb.AppendLine($"    <div class='sticker-info'>");
                sb.AppendLine($"      <span class='sticker-command'>{shortCommand}</span>");
                sb.AppendLine($"    </div>");
                sb.AppendLine($"  </div>");
            }

            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ============================================================

        private string GeneratePackId(string folderName)
        {
            return folderName;
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