using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml.Serialization;

namespace SmithForge.Main.Models
{
    public partial class AppSettings : ObservableObject
    {

        private static readonly List<int> DefaultRankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "settings.json");

        // Окно приложения
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 600;

        // Сетевые настройки
        public double NetworkPort { get; set; } = 8080;
        public string ProgramPath { get; set; } = string.Empty;
        public int LastStreamNumber { get; set; } = 0;
        public int MinMessageLength { get; set; } = 1;
        public bool IsOverlaySetupMode { get; set; } = true;

        // Настройки для главного оверлея
        public double OverlayTop { get; set; } = 100;
        public double OverlayLeft { get; set; } = 100;
        public double OverlayWidth { get; set; } = 450;
        public double OverlayHeight { get; set; } = 600;
        public bool OverlayVisible { get; set; } = true;

        // Настройки для шортов
        public double ShortsWindowTop { get; set; } = 200;
        public double ShortsWindowLeft { get; set; } = 200;
        public double ShortsWindowWidth { get; set; } = 450;
        public double ShortsWindowHeight { get; set; } = 600;
        public bool ShortsWindowVisible { get; set; } = true;

        // Настройки для важных сообщений
        public double ImportantOverlayTop { get; set; } = 300;
        public double ImportantOverlayLeft { get; set; } = 300;
        public double ImportantOverlayWidth { get; set; } = 400;
        public double ImportantOverlayHeight { get; set; } = 200;

        // Настройки для стикеров
        public double StickersWindowTop { get; set; } = 400;
        public double StickersWindowLeft { get; set; } = 400;
        public double StickersWindowWidth { get; set; } = 300;
        public double StickersWindowHeight { get; set; } = 300;
        public bool StickersWindowVisible { get; set; } = true;

        // Дополнительные настройки видимости
        public bool IsStickersVisible { get; set; } = true;
        public bool IsOverlayHidden { get; set; } = false;

        // Режимы для чатов
        public ChatDisplayMode MainChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode ShortsChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode ImportantChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode StickersChatMode { get; set; } = ChatDisplayMode.AppearAndFade;

        // Множители для платформ
        public double KarmaRateTwitch { get; set; } = 1.0;
        public double KarmaRateYouTube { get; set; } = 1.0;
        public double KarmaRateGoodGame { get; set; } = 1.0;

        // Команды и префиксы
        public Dictionary<string, string> CommandShortcuts { get; set; } = new Dictionary<string, string>();
        public List<string> CommandPrefixes { get; set; } = new List<string> { "!", "/" };

        // Пороги рангов
        private List<int> _rankThresholds = new List<int>(DefaultRankThresholds);
        [XmlIgnore]
        public List<int> RankThresholds
        {
            get => _rankThresholds;
            set => _rankThresholds = value ?? new List<int>(DefaultRankThresholds);
        }
        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Ошибка сохранения: {ex.Message}");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Ошибка загрузки: {ex.Message}");
            }
            return new AppSettings();
        }
    }

    public class RankThreshold
    {
        public int Rank { get; set; }
        public int MinMessages { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}