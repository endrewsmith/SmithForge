using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    public class InfoByNumberRule : IShortcutRule
    {
        public string Name => "InfoByNumber";

        private static readonly Regex Pattern = new Regex(
            @"\b[іiи]{2}(\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly string _pagesDir;

        public InfoByNumberRule(string pagesDir)
        {
            _pagesDir = pagesDir;
        }

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                if (!int.TryParse(match.Groups[1].Value, out int pageNumber) || pageNumber <= 0)
                    return match.Value;

                string prefix = pageNumber.ToString("D2");
                if (!Directory.Exists(_pagesDir)) return match.Value;

                var candidates = Directory.GetFiles(_pagesDir, $"{prefix}_*.html")
                    .OrderBy(f => f).ToArray();

                if (candidates.Length == 0) return match.Value;

                string fileName = Path.GetFileNameWithoutExtension(candidates[0]);
                return $"!!info:{fileName}";
            });
        }
    }
}