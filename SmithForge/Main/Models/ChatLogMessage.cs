using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Services;

namespace SmithForge.Main.Models
{
    public partial class ChatLogMessage : ObservableObject
    {
        public long Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string ChaterId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long Timestamp { get; set; }

        // Новые поля
        public int MessageNumber { get; set; } // Номер сообщения в стриме
        public int Likes { get; set; }
        public int Dislikes { get; set; }

        // Вычисляемые поля
        public string Author => ChaterStorage.GetById(ChaterId)?.EffectiveName ?? "Unknown";

        // Для отображения в UI
        public string DisplayNumber => $"#{MessageNumber}";
        public string DisplayLikes => Likes > 0 ? $"👍 {Likes}" : "";
        public string DisplayDislikes => Dislikes > 0 ? $"👎 {Dislikes}" : "";

        // Для поиска
        public string SearchText => $"{Author} {Message}".ToLower();
    }
}