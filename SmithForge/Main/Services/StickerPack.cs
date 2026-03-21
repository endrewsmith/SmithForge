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
        public string FolderName { get; set; } // pack_01, pack_02...
        public string Path { get; set; }
        public int StickerCount { get; set; }
        public List<string> StickerFiles { get; set; }
    }

    public static class StickerManager
    {
        private static Dictionary<int, StickerPack> _packs = new();

        public static void LoadPacks()
        {
            _packs.Clear();

            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Stickers");

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
                return;
            }

            var packDirs = Directory.GetDirectories(basePath)
                .Where(d => Path.GetFileName(d).StartsWith("pack_"))
                .OrderBy(d => d)
                .ToList();

            int packId = 1;

            foreach (var dir in packDirs)
            {
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".gif"))
                    .OrderBy(f => f, new NaturalStringComparer()) // Сортировка 001,002...010
                    .ToList();

                if (files.Any())
                {
                    _packs[packId] = new StickerPack
                    {
                        Id = packId,
                        FolderName = Path.GetFileName(dir),
                        Path = dir,
                        StickerCount = files.Count,
                        StickerFiles = files
                    };
                    packId++;
                }
            }
        }

        public static string GetStickerPath(int packId, int stickerId)
        {
            if (_packs.TryGetValue(packId, out var pack))
            {
                // Проверяем разные форматы имен файлов
                string[] possibleNames = new[]
                {
            $"{stickerId:D3}.png",     // 001.png
            $"{stickerId:D3}.jpg",     // 001.jpg
            $"{stickerId:D3}.gif",     // 001.gif
            $"{stickerId:D2}.png",     // 01.png
            $"{stickerId:D2}.jpg",     // 01.jpg
            $"{stickerId:D2}.gif",     // 01.gif
            $"{stickerId}.png",        // 1.png
            $"{stickerId}.jpg",        // 1.jpg
            $"{stickerId}.gif"         // 1.gif
        };

                foreach (var fileName in possibleNames)
                {
                    string fullPath = System.IO.Path.Combine(pack.Path, fileName);
                    if (System.IO.File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }
            return null;
        }

        // Компаратор для естественной сортировки (001,002...010)
        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return CompareNatural(x, y);
            }

            private static int CompareNatural(string strA, string strB)
            {
                return Regex.Replace(strA, @"\d+", m => m.Value.PadLeft(10, '0')) ==
                       Regex.Replace(strB, @"\d+", m => m.Value.PadLeft(10, '0')) ? 0 :
                       string.Compare(strA, strB, StringComparison.Ordinal);
            }
        }
    }
}