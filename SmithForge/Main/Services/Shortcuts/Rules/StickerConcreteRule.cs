using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    /// <summary>
    /// с2с3 → !!st:2:3
    /// Конкретный стикер из конкретного пака.
    /// </summary>
    public class StickerConcreteRule : IShortcutRule
    {
        public string Name => "StickerConcrete";

        private static readonly Regex Pattern = new Regex(
            @"\b[сc](\d+)[сc](\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                string packNumber = match.Groups[1].Value;
                string stickerNumber = match.Groups[2].Value;
                return $"!!st:{packNumber}:{stickerNumber}";
            });
        }
    }
}