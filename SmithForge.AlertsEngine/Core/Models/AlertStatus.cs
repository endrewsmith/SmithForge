using System;

namespace SmithForge.AlertsEngine.Core.Models
{
    /// <summary>
    /// Статус подключения провайдера алертов
    /// </summary>
    public class AlertStatus
    {
        /// <summary>
        /// Подключен ли провайдер в данный момент
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Текст ошибки (если есть)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Время подключения (UTC)
        /// </summary>
        public DateTime ConnectedSince { get; set; }

        /// <summary>
        /// Время последнего полученного алерта (UTC)
        /// </summary>
        public DateTime LastAlertReceived { get; set; }

        /// <summary>
        /// Количество полученных алертов
        /// </summary>
        public int AlertsReceived { get; set; }

        /// <summary>
        /// Строковое представление статуса
        /// </summary>
        public override string ToString()
        {
            if (IsConnected)
                return $"✅ Подключено (алертов: {AlertsReceived})";

            return string.IsNullOrEmpty(ErrorMessage)
                ? "⚪ Отключено"
                : $"❌ Ошибка: {ErrorMessage}";
        }

        /// <summary>
        /// Отметить как подключенный
        /// </summary>
        public void MarkConnected()
        {
            IsConnected = true;
            ErrorMessage = null;
            ConnectedSince = DateTime.UtcNow;
        }

        /// <summary>
        /// Отметить как отключенный
        /// </summary>
        public void MarkDisconnected(string? errorMessage = null)
        {
            IsConnected = false;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Отметить получение алерта
        /// </summary>
        public void MarkAlertReceived()
        {
            AlertsReceived++;
            LastAlertReceived = DateTime.UtcNow;
        }
    }
}