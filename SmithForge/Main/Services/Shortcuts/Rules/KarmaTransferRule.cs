using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    public class KarmaTransferRule : IShortcutRule
    {
        public string Name => "KarmaTransfer";

        private static readonly Regex Pattern = new Regex(
            @"\b(?:(?:id)|(?:карма))(\d+):(\d+(?:[.,]\d+)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                string karmaKey = match.Groups[1].Value;
                string amount = match.Groups[2].Value.Replace(',', '.');
                return $"!!karma:{karmaKey}:{amount}";
            });
        }
    }
}