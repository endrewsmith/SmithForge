using System;

namespace SmithForge.AlertsEngine.Core.Models
{
    /// <summary>
    /// Модель входящего алерта (универсальная для всех провайдеров)
    /// </summary>
    public class IncomingAlert
    {
        /// <summary>
        /// Уникальный ID алерта у провайдера
        /// </summary>
        public string ProviderId { get; set; } = string.Empty;

        /// <summary>
        /// Тип провайдера (DonationAlerts, StreamElements, ...)
        /// </summary>
        public AlertProviderType ProviderType { get; set; }

        /// <summary>
        /// Тип алерта (донат, подписка, ...)
        /// </summary>
        public AlertType Type { get; set; }

        /// <summary>
        /// Имя пользователя (кто отправил)
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Сообщение от пользователя
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Сумма (для донатов)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Валюта (RUB, USD, EUR, ...)
        /// </summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// Время получения алерта
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Готовая строка для отображения в оверлее
        /// (например: "Vasya задонатил 100 RUB")
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>
        /// Покрыл ли зритель комиссию сервиса (только для донатов)
        /// </summary>
        public bool IsCommissionCovered { get; set; }
    }
}