using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    public class StickerListRule : IShortcutRule
    {
        public string Name => "StickerList";

        // сс0 — специальный случай, открывает общий список паков
        private static readonly Regex Pattern = new Regex(
            @"\b[сc][сc]0\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Apply(string message)
        {
            return Pattern.Replace(message, "!!info:stickers");
        }
    }
}