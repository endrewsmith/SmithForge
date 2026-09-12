using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services
{
    public class StickerPack
    {
        public int Id { get; set; }           // 1,2,3...
        public string FolderName { get; set; } // gaechka_1, persik_2...
        public string Path { get; set; }
        public int StickerCount { get; set; }
        public List<string> StickerFiles { get; set; }
    }

    public static class StickerManager
    {
        private static Dictionary<int, StickerPack> _packs = new();
        private static Dictionary<string, int> _folderToPackId = new(); // имя папки -> ID пака

        public static void LoadPacks()
        {
            _packs.Clear();
            _folderToPackId.Clear();

            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Stickers");

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
                System.Diagnostics.Debug.WriteLine($"[StickerManager] Создана папка: {basePath}");
                return;
            }

            // ✅ БЕРЁМ ВСЕ ПАПКИ (не только pack_*)
            var packDirs = Directory.GetDirectories(basePath)
                .Where(d => !Path.GetFileName(d).StartsWith(".") &&
                            Path.GetFileName(d) != "Thumbs.db")
                .OrderBy(d => d, new NaturalStringComparer())
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[StickerManager] Найдено папок: {packDirs.Count}");

            int packId = 1;

            foreach (var dir in packDirs)
            {
                string folderName = Path.GetFileName(dir);

                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") ||
                                f.EndsWith(".jpeg") || f.EndsWith(".gif") ||
                                f.EndsWith(".webp") || f.EndsWith(".apng"))
                    .OrderBy(f => f, new NaturalStringComparer())
                    .ToList();

                if (files.Any())
                {
                    var pack = new StickerPack
                    {
                        Id = packId,
                        FolderName = folderName,
                        Path = dir,
                        StickerCount = files.Count,
                        StickerFiles = files
                    };

                    _packs[packId] = pack;
                    _folderToPackId[folderName] = packId;

                    System.Diagnostics.Debug.WriteLine($"[StickerManager] Пак #{packId}: {folderName} ({files.Count} стикеров)");
                    packId++;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[StickerManager] ⚠️ Папка {folderName} пуста (нет изображений)");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[StickerManager] Всего загружено паков: {_packs.Count}");
        }

        public static string GetStickerPath(int packId, int stickerId)
        {
            System.Diagnostics.Debug.WriteLine($"[StickerManager] Поиск: пак {packId}, стикер {stickerId}");

            // 1. Прямой поиск по ID
            if (_packs.TryGetValue(packId, out var pack))
            {
                System.Diagnostics.Debug.WriteLine($"[StickerManager] Найден пак: {pack.FolderName}");
                return GetStickerFileFromPack(pack, stickerId);
            }

            // 2. Если не нашли, пробуем найти по имени папки с суффиксом _номер
            string targetSuffix = $"_{packId}";
            var folderMatch = _folderToPackId.FirstOrDefault(kvp => kvp.Key.EndsWith(targetSuffix));

            if (folderMatch.Key != null && _packs.TryGetValue(folderMatch.Value, out var packByFolder))
            {
                System.Diagnostics.Debug.WriteLine($"[StickerManager] Найден пак по суффиксу: {packByFolder.FolderName}");
                return GetStickerFileFromPack(packByFolder, stickerId);
            }

            // 3. Пробуем найти по индексу в списке (если папки отсортированы)
            var sortedPacks = _packs.Values.OrderBy(p => p.Id).ToList();
            if (packId >= 1 && packId <= sortedPacks.Count)
            {
                var packByIndex = sortedPacks[packId - 1];
                System.Diagnostics.Debug.WriteLine($"[StickerManager] Найден пак по индексу: {packByIndex.FolderName}");
                return GetStickerFileFromPack(packByIndex, stickerId);
            }

            System.Diagnostics.Debug.WriteLine($"[StickerManager] ❌ Пак {packId} не найден");
            return null;
        }

        private static string GetStickerFileFromPack(StickerPack pack, int stickerId)
        {
            if (pack == null || pack.StickerFiles == null || pack.StickerFiles.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[StickerManager] ❌ Пак пуст");
                return null;
            }

            // Проверяем разные форматы имен файлов
            string[] possibleNames = new[]
            {
                $"{stickerId:D3}.png",     // 001.png
                $"{stickerId:D3}.jpg",     // 001.jpg
                $"{stickerId:D3}.jpeg",    // 001.jpeg
                $"{stickerId:D3}.gif",     // 001.gif
                $"{stickerId:D3}.webp",    // 001.webp
                $"{stickerId:D2}.png",     // 01.png
                $"{stickerId:D2}.jpg",     // 01.jpg
                $"{stickerId:D2}.gif",     // 01.gif
                $"{stickerId}.png",        // 1.png
                $"{stickerId}.jpg",        // 1.jpg
                $"{stickerId}.gif",        // 1.gif
                $"sticker_{stickerId}.png", // sticker_1.png
                $"sticker_{stickerId}.gif", // sticker_1.gif
            };

            foreach (var fileName in possibleNames)
            {
                string fullPath = System.IO.Path.Combine(pack.Path, fileName);
                if (System.IO.File.Exists(fullPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[StickerManager] ✅ Найден: {fileName}");
                    return fullPath;
                }
            }

            // Если не нашли по имени, пробуем по индексу
            if (stickerId >= 1 && stickerId <= pack.StickerFiles.Count)
            {
                string result = pack.StickerFiles[stickerId - 1];
                System.Diagnostics.Debug.WriteLine($"[StickerManager] ✅ Найден по индексу: {Path.GetFileName(result)}");
                return result;
            }

            System.Diagnostics.Debug.WriteLine($"[StickerManager] ❌ Стикер {stickerId} не найден в паке {pack.FolderName}");
            return null;
        }

        public static List<StickerPack> GetAllPacks()
        {
            return _packs.Values.OrderBy(p => p.Id).ToList();
        }

        public static StickerPack GetPack(int packId)
        {
            _packs.TryGetValue(packId, out var pack);
            return pack;
        }

        public static int GetPackCount() => _packs.Count;

        // Компаратор для естественной сортировки (001,002...010)
        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return CompareNatural(x, y);
            }

            private static int CompareNatural(string strA, string strB)
            {
                if (strA == null && strB == null) return 0;
                if (strA == null) return -1;
                if (strB == null) return 1;

                return Regex.Replace(strA, @"\d+", m => m.Value.PadLeft(10, '0'))
                    .CompareTo(Regex.Replace(strB, @"\d+", m => m.Value.PadLeft(10, '0')));
            }
        }
    }
}