using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Serialization;

namespace SmithForge.Main.Models
{
    public partial class AppSettings
    {
        // Константа с значениями по умолчанию - ЕДИНСТВЕННОЕ МЕСТО
        private static readonly List<int> DefaultRankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };

        // Проверь, чтобы буква в букву было так:
        public string ProgramPath { get; set; } = "notepad.exe";

        public int LastStreamNumber { get; set; } = 0;
        public int NetworkPort { get; set; } = 10880;

        // Новые поля для окна
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 500;

        public double OverlayTop { get; set; } = 100;
        public double OverlayLeft { get; set; } = 100;
        public bool IsOverlayLocked { get; set; } = false; // Режим "сквозь клик"

        public double KarmaRateTwitch { get; set; } = 1.0;
        public double KarmaRateYouTube { get; set; } = 1.0;
        public double KarmaRateGoodGame { get; set; } = 1.0;

        public int KarmaPerMessage { get; set; } = 1; // Значение по умолчанию
        public int MinMessageLength { get; set; } = 1;

        private List<int> _rankThresholds = new List<int>(DefaultRankThresholds); // Копируем дефолтные

        [XmlIgnore]
        public List<int> RankThresholds
        {
            get => _rankThresholds;
            set => _rankThresholds = value ?? new List<int>(DefaultRankThresholds);
        }

        // Для сериализации в XML используем только строку
        public string RankThresholdsString
        {
            get => string.Join(",", _rankThresholds);
            set
            {
                try
                {
                    _rankThresholds = value.Split(',').Select(int.Parse).ToList();
                }
                catch
                {
                    _rankThresholds = new List<int>(DefaultRankThresholds); // Используем ту же константу
                }
            }
        }

        public AppSettings() { }
    }
}