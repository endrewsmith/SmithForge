using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SmithForge.Main.Services
{
    public static class HtmlProvider
    {
        private static readonly ConcurrentDictionary<string, string> _cache = new();

        private static string Root =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html");

        public static string GetOverlay(string name)
            => Get(Path.Combine("Overlays", name));

        public static string GetPage(string name)
            => Get(Path.Combine("Overlays", "pages", name));

        public static string Get(string relativePath)
        {
            if (_cache.TryGetValue(relativePath, out var cached))
                return cached;

            string fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"[HtmlProvider] ❌ Не найден: {fullPath}");
                return $"<h2>❌ Шаблон не найден: {relativePath}</h2>";
            }

            string content = File.ReadAllText(fullPath, Encoding.UTF8);
            _cache[relativePath] = content;
            Debug.WriteLine($"[HtmlProvider] ✅ {relativePath} ({content.Length} симв.)");
            return content;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}