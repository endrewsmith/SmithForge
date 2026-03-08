using CommunityToolkit.Mvvm.ComponentModel;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SmithForge.Main.Models
{
    public partial class StreamSession : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private int _number;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private long _startTime;

        [ObservableProperty]
        private long _endTime;

        // Для отображения статуса
        public string Status => EndTime == 0 ? "🔴 В эфире" : "✅ Завершен";

        // Для поиска
        public string SearchString => $"{Number} {Title}";
    }
}