namespace SmithForge.AlertsEngine.Core.Models
{
    /// <summary>
    /// Тип провайдера алертов
    /// </summary>
    public enum AlertProviderType
    {
        DonationAlerts = 1,
        DonationPay = 2,
        // В будущем можно добавить:
        // StreamElements = 2,
        // Streamlabs = 3,
        // Twitch = 4,
        // YouTube = 5
    }
}