using CommunityToolkit.Mvvm.ComponentModel;
using System.Xml.Serialization;

namespace SmithForge.Main.Models
{
    public partial class AppSettings : ObservableObject
    {

        [ObservableProperty] private bool _isOverlayHidden = false;

        private bool _isOverlaySetupMode = true;
        private List<string> _commandPrefixes = new() { "!!", "..", ",," };
        private static readonly List<int> DefaultRankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };
        private List<int> _rankThresholds = new List<int>(DefaultRankThresholds);

        public bool IsOverlaySetupMode
        {
            get => _isOverlaySetupMode;
            set => _isOverlaySetupMode = value;
        }

        public List<string> CommandPrefixes
        {
            get => _commandPrefixes;
            set => _commandPrefixes = value ?? new() { "!!", "..", ",," };
        }

        public string ProgramPath { get; set; } = "notepad.exe";
        public int LastStreamNumber { get; set; } = 0;
        public int NetworkPort { get; set; } = 10880;

        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 500;

        public double OverlayTop { get; set; } = 100;
        public double OverlayLeft { get; set; } = 100;
        public double OverlayWidth { get; set; } = 450;
        public double OverlayHeight { get; set; } = 600;
        public bool OverlayVisible { get; set; } = true;
        public bool IsOverlayLocked { get; set; } = false;

        public double ShortsWindowTop { get; set; } = 150;
        public double ShortsWindowLeft { get; set; } = 150;
        public double ShortsWindowWidth { get; set; } = 450;
        public double ShortsWindowHeight { get; set; } = 600;
        public bool ShortsWindowVisible { get; set; } = false;
        public bool IsShortsLocked { get; set; } = false; // ← ДОБАВЛЕНО

        public double KarmaRateTwitch { get; set; } = 1.0;
        public double KarmaRateYouTube { get; set; } = 1.0;
        public double KarmaRateGoodGame { get; set; } = 1.0;

        public int KarmaPerMessage { get; set; } = 1;
        public int MinMessageLength { get; set; } = 1;

        [XmlIgnore]
        public List<int> RankThresholds
        {
            get => _rankThresholds;
            set => _rankThresholds = value ?? new List<int>(DefaultRankThresholds);
        }

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
                    _rankThresholds = new List<int>(DefaultRankThresholds);
                }
            }
        }

        public AppSettings() { }
    }
}