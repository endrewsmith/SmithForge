using SmithForge.Main.Models;

namespace SmithForge.Features.ChatManager
{
    public class YouTubeMethodItem
    {
        public YouTubeConnectionMethod Method { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
}