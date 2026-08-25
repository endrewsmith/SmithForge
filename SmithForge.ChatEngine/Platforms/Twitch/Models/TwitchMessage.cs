// SmithForge.ChatEngine\Platforms\Twitch\Models\TwitchMessage.cs
using System;
using System.Collections.Generic;

namespace SmithForge.ChatEngine.Platforms.Twitch.Models
{
    /// <summary>
    /// Данные об эмодзи из сообщения Twitch
    /// </summary>
    public class TwitchEmoteData
    {
        public string Id { get; set; } = string.Empty;           // "25"
        public List<int> Positions { get; set; } = new();        // [0, 5]
        public string Code { get; set; } = string.Empty;         // "Kappa"
    }

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

        /// <summary>
        /// EmoteSet из Twitch (формат: "25:0-4,5-9/26:10-14")
        /// </summary>
        public string EmoteSet { get; set; } = string.Empty;

        /// <summary>
        /// Распарсенные эмодзи
        /// </summary>
        public List<TwitchEmoteData> Emotes { get; set; } = new();

        /// <summary>
        /// Есть ли эмодзи в сообщении
        /// </summary>
        public bool HasEmotes => Emotes.Count > 0 || !string.IsNullOrEmpty(EmoteSet);
    }
}