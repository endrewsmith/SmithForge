namespace SmithForge.AlertsEngine.Core.Models
{
    /// <summary>
    /// Тип алерта (донат, подписка, фолловер и т.д.)
    /// </summary>
    public enum AlertType
    {
        Unknown = 0,
        Donation = 1,
        Subscription = 2,
        Follow = 3,
        Raid = 4,
        Host = 5,
        Cheer = 6,        // Twitch Bits
        SuperChat = 7,    // YouTube
        Custom = 100
    }
}