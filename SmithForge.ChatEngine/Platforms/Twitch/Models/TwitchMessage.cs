using System;

namespace SmithForge.ChatEngine.Platforms.Twitch.Models
{
    public class TwitchMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Author { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ColorHex { get; set; } = "#9146FF";
        public string Channel { get; set; } = string.Empty;
        public bool IsAction { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool IsBroadcaster { get; set; }
        public bool IsModerator { get; set; }
        public bool IsSubscriber { get; set; }
        public bool IsVip { get; set; }
    }
}