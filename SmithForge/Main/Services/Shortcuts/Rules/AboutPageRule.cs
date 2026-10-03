using System.IO;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    /// <summary>
    /// ab4   → !!info:about:04_xxx
    /// про4  → !!info:about:04_xxx
    /// Открывает страницу About с префиксом NN_ из папки about.
    /// </summary>
    public class AboutPageRule : IShortcutRule
    {
        public string Name => "AboutPage";

        // Одна регулярка ловит оба варианта: abN и проN
        private static readonly Regex Pattern = new Regex(
            @"\b(?:(?:ab)|(?:[пp][рr][оo]))(\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _aboutDir;

        public AboutPageRule(string pagesDir)
        {
            // pagesDir — это корень Html/InfoPages. About лежит в подпапке.
            _aboutDir = Path.Combine(pagesDir, "about");
        }

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                string numStr = match.Groups[1].Value;
                string prefix = numStr.PadLeft(2, '0');

                if (!Directory.Exists(_aboutDir))
                    return "!!info:about";

                var files = Directory.GetFiles(_aboutDir, $"{prefix}_*.html");
                if (files.Length == 0)
                    return "!!info:about";

                string fileName = Path.GetFileNameWithoutExtension(files[0]);
                return $"!!info:about:{fileName}";
            });
        }
    }
}