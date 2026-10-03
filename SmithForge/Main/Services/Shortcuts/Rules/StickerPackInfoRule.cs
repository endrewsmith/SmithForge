using System.Text.RegularExpressions;
using SmithForge.Features.InfoSystem;

namespace SmithForge.Main.Services.Shortcuts.Rules
{
    public class StickerPackInfoRule : IShortcutRule
    {
        public string Name => "StickerPackInfo";

        private readonly StickerPageService _stickerPageService;

        private static readonly Regex Pattern = new Regex(
            @"\b[сc][сc](\d+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public StickerPackInfoRule(StickerPageService stickerPageService)
        {
            _stickerPageService = stickerPageService;
        }

        public string Apply(string message)
        {
            return Pattern.Replace(message, match =>
            {
                if (!int.TryParse(match.Groups[1].Value, out int packNumber))
                    return match.Value;

                string packId = _stickerPageService?.GetPackIdByNumber(packNumber);
                return !string.IsNullOrEmpty(packId)
                    ? $"!!info:stickers:{packNumber}"
                    : "!!info:stickers";
            });
        }
    }
}