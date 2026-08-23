using System;
using System.Collections.Generic;

namespace SmithForge.ChatEngine.Platforms.GoodGame.Models
{
    public class GgMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public long GgId { get; set; }
        public long UserId { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public Dictionary<long, int> SubscriptionDuration { get; set; } = new();
        public string BadgeName { get; set; } = string.Empty;
        public string AuthorColorName { get; set; } = string.Empty;
        public int SponsorLevel { get; set; }
        public int AuthorRights { get; set; }
        public string ColorHex { get; set; } = "#FFFFFF";

    }
}