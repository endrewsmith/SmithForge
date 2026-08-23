namespace SmithForge.ChatEngine.Platforms.GoodGame.Models
{
    public class GgChannel
    {
        public string Name { get; set; } = string.Empty;
        public long Id { get; set; }
        public bool IsPremium { get; set; }
        public string Status { get; set; } = string.Empty;  // "Live" или "offline"
        public string Title { get; set; } = string.Empty;
        public int Viewers { get; set; }

        public bool IsOnline => Status?.ToLower() == "live";
    }
}