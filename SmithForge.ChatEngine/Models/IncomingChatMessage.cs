using System;
using System.Collections.Generic;

namespace SmithForge.ChatEngine.Models
{
    /// <summary>
    /// Универсальное сообщение для всех платформ
    /// </summary>
    public class IncomingChatMessage
    {
        /// <summary>
        /// Тип платформы
        /// </summary>
        public ChannelType Platform { get; set; }

        /// <summary>
        /// Уникальный ID пользователя на платформе (НЕ МЕНЯЕТСЯ)
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя пользователя (МОЖЕТ МЕНЯТЬСЯ)
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Время получения сообщения (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ID видео/стрима
        /// </summary>
        public string? VideoId { get; set; }

        /// <summary>
        /// ID канала на платформе
        /// </summary>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Уникальный ID коннектора (стрима), из которого пришло сообщение.
        /// Нужно для дедупликации, когда один канал подключён через несколько стримов (Shorts + Normal).
        /// </summary>
        public string ConnectorId { get; set; } = string.Empty;

        /// <summary>
        /// Полный внешний ID для поиска в БД
        /// Формат: "youtube:UCsf2sD1gJWus1OUrq2fGwlQ"
        /// </summary>
        public string ExternalId => $"{Platform}:{UserId}".ToLower();

        /// <summary>
        /// Создать копию сообщения
        /// </summary>
        public IncomingChatMessage Clone()
        {
            return new IncomingChatMessage
            {
                Platform = Platform,
                UserId = UserId,
                UserName = UserName,
                Text = Text,
                Timestamp = Timestamp,
                VideoId = VideoId,
                ChannelId = ChannelId,
                ConnectorId = ConnectorId
            };
        }

        /// <summary>
        /// Строковое представление
        /// </summary>
        public override string ToString()
        {
            return $"[{Platform}] {UserName}: {Text}";
        }
    }
}