using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    /// <summary>
    /// с2с → !!st:2:random
    /// Случайный стикер из конкретного пака.
    /// </summary>
    public class StickerRandomFromPackRule : IShortcutRule
    {
        public string Name => "StickerRandomFromPack";

        private static readonly Regex Pattern = new Regex(
            @"\b[сc](\d+)[сc]\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                string packNumber = match.Groups[1].Value;
                return $"!!st:{packNumber}:random";
            });
        }
    }
}