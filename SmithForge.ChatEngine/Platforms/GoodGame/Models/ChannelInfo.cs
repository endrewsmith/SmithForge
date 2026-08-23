using System.Text.Json.Serialization;

namespace SmithForge.ChatEngine.Platforms.GoodGame.Models
{
    public class ChannelInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("premium")]
        public bool IsPremium { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public class StreamResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("playerViewers")]
        public int PlayerViewers { get; set; }

        [JsonPropertyName("streamId")]
        public long StreamId { get; set; }

        [JsonPropertyName("channel")]
        public ChannelInfo? Channel { get; set; }
    }
}