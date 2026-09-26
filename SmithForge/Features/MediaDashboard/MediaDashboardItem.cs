using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SmithForge.Features.MediaDashboard
{
    public class MediaDashboardItem : ObservableObject
    {
        /// <summary>Тип медиа: "sticker", "video", "text"</summary>
        public string MediaType { get; set; } = "sticker";

        /// <summary>Имя отправителя (ник)</summary>
        public string UserName { get; set; } = "Аноним";

        /// <summary>Локальный путь к файлу (стикер/видео)</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>URL для веба (если нужно показать в браузере)</summary>
        public string WebUrl { get; set; } = string.Empty;

        /// <summary>Текст под медиа (может быть пустым)</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Анимированный ли файл (GIF/WebP)</summary>
        public bool IsAnimated { get; set; }

        /// <summary>Время получения</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");
        public bool HasText => !string.IsNullOrWhiteSpace(Text);
        public bool IsVideo => MediaType == "video";
        public bool IsSticker => MediaType == "sticker";
    }
}