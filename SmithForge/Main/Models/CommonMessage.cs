using System;

namespace SmithForge.Main.Models
{
    /// <summary>
    /// Сообщение для обработки в MessageProcessor
    /// </summary>
    public class CommonMessage
    {
        // ========== ИДЕНТИФИКАЦИЯ ==========

        /// <summary>
        /// Тип платформы (youtube, twitch, goodgame)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Уникальный ID пользователя с платформы (НЕ МЕНЯЕТСЯ)
        /// Используется для поиска в БД
        /// </summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>
        /// ID канала/пользователя с платформы (YouTube: UC..., Twitch: ID)
        /// НЕ МЕНЯЕТСЯ, используется для создания аккаунта
        /// </summary>
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Отображаемое имя пользователя (МОЖЕТ МЕНЯТЬСЯ)
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Комбинированный ID для поиска (платформа-логин)
        /// </summary>
        public string TypeLogin => $"{Type}-{Login}";

        // ========== СОДЕРЖАНИЕ ==========

        /// <summary>
        /// Текст сообщения
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Приоритет сообщения
        /// </summary>
        public int Priority { get; set; }

        // ========== ВРЕМЯ ==========

        /// <summary>
        /// Время получения сообщения (Unix timestamp) для БД
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Оригинальное время с платформы (для отображения)
        /// </summary>
        public DateTime OriginalTimestamp { get; set; } = DateTime.UtcNow;

        // ========== ОТОБРАЖЕНИЕ В ОВЕРЛЕЕ ==========

        /// <summary>
        /// Отображение KarmaKey
        /// </summary>
        public string KarmaKeyDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Номер сообщения в стриме
        /// </summary>
        public int MessageNumber { get; set; }

        /// <summary>
        /// Длина сообщения (Short, Medium, Long)
        /// </summary>
        public MessageLength LengthCategory
        {
            get
            {
                int len = Message?.Length ?? 0;
                if (len <= 20) return MessageLength.Short;
                if (len <= 100) return MessageLength.Medium;
                return MessageLength.Long;
            }
        }

        /// <summary>
        /// Базовое время отображения (зависит от длины)
        /// </summary>
        public int BaseDisplayTimeMs => LengthCategory switch
        {
            MessageLength.Short => 7000,
            MessageLength.Medium => 20000,
            MessageLength.Long => 30000,
            _ => 5000
        };

        private int? _customDisplayTimeMs;

        /// <summary>
        /// Итоговое время отображения (кастомное или базовое)
        /// </summary>
        public int DisplayTimeMs
        {
            get => _customDisplayTimeMs ?? BaseDisplayTimeMs;
            set => _customDisplayTimeMs = value;
        }

        // ========== ССЫЛКА НА ПОЛЬЗОВАТЕЛЯ ==========

        /// <summary>
        /// Ссылка на объект Chater (с внутренним UID)
        /// </summary>
        public Chater? User { get; set; }

        /// <summary>
        /// Внутренний UID пользователя (для быстрого доступа)
        /// </summary>
        public string UserId => User?.Id ?? string.Empty;

        // ========== ФЛАГИ СОСТОЯНИЯ ==========

        /// <summary>
        /// Обработано ли сообщение командой
        /// </summary>
        public bool IsProcessedByCommand { get; set; }

        /// <summary>
        /// Списывать ли карму за команду
        /// </summary>
        public bool ShouldChargeForCommand { get; set; } = true;

        // ========== ДЕБАГ ==========

        /// <summary>
        /// Отладочная информация
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[CommonMessage] Type={Type}, Login={Login}, DisplayName={DisplayName}, " +
                   $"Message='{Message}', MsgNum={MessageNumber}, DisplayTime={DisplayTimeMs}ms";
        }
    }
}