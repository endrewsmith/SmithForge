using System;

namespace SmithForge.ChatEngine.Models
{
    /// <summary>
    /// Сырые данные сообщения с платформы (YouTube, Twitch, GoodGame)
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Тип платформы
        /// </summary>
        public ChannelType Platform { get; set; }

        /// <summary>
        /// Уникальный ID пользователя на платформе (НЕ МЕНЯЕТСЯ)
        /// Пример YouTube: "UCsf2sD1gJWus1OUrq2fGwlQ"
        /// </summary>
        public string AuthorId { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя пользователя (МОЖЕТ МЕНЯТЬСЯ)
        /// Пример: "VasyaPupkin"
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Время отправки сообщения
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// ID видео/стрима (для YouTube)
        /// Пример: "BprgTJKqrYw"
        /// </summary>
        public string VideoId { get; set; } = string.Empty;

        /// <summary>
        /// ID канала на платформе
        /// </summary>
        public string ChannelId { get; set; } = string.Empty;
    }
}