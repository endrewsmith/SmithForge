using System;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts.Models
{
    /// <summary>
    /// Токены, полученные после успешной OAuth-авторизации
    /// </summary>
    public class DonationAlertsTokens
    {
        /// <summary>
        /// Access Token — используется для всех API-запросов
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Refresh Token — используется для обновления access token
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Когда истекает Access Token
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// ID пользователя на DonationAlerts
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// Получен ли access token
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(AccessToken);

        public override string ToString()
        {
            return $"AccessToken: {AccessToken?.Substring(0, Math.Min(20, AccessToken?.Length ?? 0))}..., " +
                   $"Expires: {ExpiresAt:yyyy-MM-dd HH:mm}, UserId: {UserId}";
        }
    }
}