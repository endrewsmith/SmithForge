namespace SmithForge.ChatEngine.Platforms.YouTube.Models;

public class YouTubeStreamInfo
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsShorts { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string LiveChatId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ContinuationToken { get; set; } = string.Empty;
    public int ViewerCount { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsLive { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;

    public string DisplayText => $"{Title} {(IsShorts ? "🎬 [SHORTS]" : "📺")}";
}