using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    public class StickerRandomRule : IShortcutRule
    {
        public string Name => "StickerRandom";

        private static readonly Regex Pattern = new Regex(
            @"\b[сc][сc][сc]\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Apply(string message)
        {
            return Pattern.Replace(message, "!!st:random:random");
        }
    }
}