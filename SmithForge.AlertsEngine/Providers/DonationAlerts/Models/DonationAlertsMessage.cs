using System.Text.Json.Serialization;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts.Models
{
    /// <summary>
    /// Сообщение о донате от DonationAlerts (приходит через Centrifugo).
    /// </summary>
    public class DonationAlertsMessage
    {
        /// <summary>
        /// Уникальный ID доната (число!)
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Имя пользователя, отправившего донат
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        /// <summary>
        /// Сумма доната
        /// </summary>
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Валюта (RUB, USD, EUR, ...)
        /// </summary>
        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        /// <summary>
        /// Сообщение от пользователя
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Дата оплаты (строка, формат "yyyy-MM-dd HH:mm:ss")
        /// </summary>
        [JsonPropertyName("date_paid")]
        public string? DatePaid { get; set; }

        /// <summary>
        /// Тип алерта (1 = донат)
        /// </summary>
        [JsonPropertyName("alert_type")]
        public int AlertType { get; set; }

        /// <summary>
        /// Дополнительные данные в виде JSON-строки.
        /// Внутри — is_commission_covered, randomness и т.д.
        /// </summary>
        [JsonPropertyName("additional_data")]
        public string? AdditionalData { get; set; }
    }
}