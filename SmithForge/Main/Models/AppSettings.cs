using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;

namespace SmithForge.Main.Models
{
    // === ОСНОВНОЙ КЛАСС НАСТРОЕК ===
    public partial class AppSettings : ObservableObject
    {

        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data",
            "settings.xml"
        );

        private static readonly List<int> DefaultRankThresholds = new List<int> { 11, 50, 100, 200, 500, 1000 };

        private List<int> _rankThresholds;

        private int _voiceRate = 3;

        public AppSettings()
        {
            CommandShortcuts = new List<ShortcutItem>();
            CommandPrefixes = new List<string> { "!", "/" };
            _rankThresholds = new List<int>(DefaultRankThresholds);

            YouTube = new YouTubeSettings();
            Twitch = new TwitchSettings();
            GoodGame = new GoodGameSettings();
        }

        // === Окно приложения ===
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 600;

        // === Сетевые настройки ===
        public double NetworkPort { get; set; } = 10881;
        public string ProgramPath { get; set; } = string.Empty;
        public int LastStreamNumber { get; set; } = 0;
        public int MinMessageLength { get; set; } = 1;
        public bool IsOverlaySetupMode { get; set; } = true;

        // === Настройки для главного оверлея ===
        public double OverlayTop { get; set; } = 100;
        public double OverlayLeft { get; set; } = 100;
        public double OverlayWidth { get; set; } = 450;
        public double OverlayHeight { get; set; } = 600;
        public bool OverlayVisible { get; set; } = true;

        // === Настройки для шортов ===
        public double ShortsWindowTop { get; set; } = 200;
        public double ShortsWindowLeft { get; set; } = 200;
        public double ShortsWindowWidth { get; set; } = 450;
        public double ShortsWindowHeight { get; set; } = 600;
        public bool ShortsWindowVisible { get; set; } = true;

        // === Настройки для важных сообщений ===
        public double ImportantOverlayTop { get; set; } = 300;
        public double ImportantOverlayLeft { get; set; } = 300;
        public double ImportantOverlayWidth { get; set; } = 400;
        public double ImportantOverlayHeight { get; set; } = 200;
        public bool ImportantOverlayVisible { get; set; } = true;

        // === Настройки для стикеров ===
        public double StickersWindowTop { get; set; } = 400;
        public double StickersWindowLeft { get; set; } = 400;
        public double StickersWindowWidth { get; set; } = 300;
        public double StickersWindowHeight { get; set; } = 300;
        public bool StickersWindowVisible { get; set; } = true;

        // === Дополнительные настройки видимости ===
        public bool IsStickersVisible { get; set; } = true;
        public bool IsOverlayHidden { get; set; } = false;

        // === Режимы для чатов ===
        public ChatDisplayMode MainChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode ShortsChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode ImportantChatMode { get; set; } = ChatDisplayMode.AppearAndFade;
        public ChatDisplayMode StickersChatMode { get; set; } = ChatDisplayMode.AppearAndFade;

        public bool AutoOpenMediaChat { get; set; } = true;

        // === Множители для платформ ===
        public double KarmaRateTwitch { get; set; } = 1.0;
        public double KarmaRateYouTube { get; set; } = 1.0;
        public double KarmaRateGoodGame { get; set; } = 1.0;

        // === Команды и префиксы ===
        [XmlArray("CommandShortcuts")]
        [XmlArrayItem("Shortcut")]
        public List<ShortcutItem> CommandShortcuts { get; set; }

        public List<string> CommandPrefixes { get; set; }

        // === Настройки голоса ===
        public string SelectedVoice { get; set; } = string.Empty;
        public string DefaultMaleVoice { get; set; } = string.Empty;
        public string DefaultFemaleVoice { get; set; } = string.Empty;

        public int ImportantSoundVolume { get; set; } = 100;
        public int VoiceVolume { get; set; } = 100;

        public int VoiceRate
        {
            get => _voiceRate;
            set
            {
                if (_voiceRate != value)
                {
                    _voiceRate = Math.Clamp(value, -10, 10);
                    OnPropertyChanged();
                }
            }
        }

        public ImportantPlaybackMode ImportantPlaybackMode { get; set; } = ImportantPlaybackMode.Auto;

        public string ImportantPlaybackHotkey { get; set; } = "F8";

        public int StickerDisplayTimeMs { get; set; } = 5000;

        // === Платформы ===
        [XmlElement("YouTube")]
        public YouTubeSettings YouTube { get; set; }

        [XmlElement("Twitch")]
        public TwitchSettings Twitch { get; set; }

        [XmlElement("GoodGame")]
        public GoodGameSettings GoodGame { get; set; }

        // === Ранги ===
        [XmlIgnore]
        public List<int> RankThresholds
        {
            get
            {
                if (_rankThresholds == null)
                    _rankThresholds = new List<int>(DefaultRankThresholds);
                return _rankThresholds.Distinct().OrderBy(x => x).ToList();
            }
            set
            {
                _rankThresholds = value?.Distinct().OrderBy(x => x).ToList()
                    ?? new List<int>(DefaultRankThresholds);
                OnPropertyChanged();
            }
        }

        [XmlArray("RankThresholds")]
        [XmlArrayItem("int")]
        public List<int> RankThresholdsForXml
        {
            get => RankThresholds;
            set => RankThresholds = value;
        }

        // ============================================================
        // === МЕТОДЫ ===
        // ============================================================

        public static AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings
            {
                CommandShortcuts = new List<ShortcutItem>
                {
                    new ShortcutItem { Key = "ввв", Value = "!!voice" },
                    new ShortcutItem { Key = "вж", Value = "!!voice" },
                    new ShortcutItem { Key = "ввм", Value = "!!voice:м" },
                    new ShortcutItem { Key = "ввж", Value = "!!voice:ж" },
                    new ShortcutItem { Key = "вв0", Value = "!!voice:0" },
                    new ShortcutItem { Key = "вв1", Value = "!!voice:1" },
                    new ShortcutItem { Key = "вв2", Value = "!!voice:2" },
                    new ShortcutItem { Key = "вв3", Value = "!!voice:3" },
                    new ShortcutItem { Key = "вв4", Value = "!!voice:4" },
                    new ShortcutItem { Key = "вв5", Value = "!!voice:5" },
                    new ShortcutItem { Key = "вв6", Value = "!!voice:6" },
                    new ShortcutItem { Key = "вв7", Value = "!!voice:7" },
                },
                CommandPrefixes = new List<string> { "!", "/" },
                MainChatMode = ChatDisplayMode.AppearAndFade,
                ShortsChatMode = ChatDisplayMode.AppearAndFade,
                ImportantChatMode = ChatDisplayMode.AppearAndFade,
                StickersChatMode = ChatDisplayMode.AppearAndFade,
                ImportantPlaybackMode = ImportantPlaybackMode.Auto,

                YouTube = new YouTubeSettings
                {
                    ApiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY") ?? string.Empty,
                    ChannelId = string.Empty,
                    AutoConnect = true,
                    ShowSubscriberAlerts = true,
                    PollingInterval = 600,
                    Colors = new YouTubeColorSettings()
                },
                Twitch = new TwitchSettings
                {
                    ClientId = string.Empty,
                    AccessToken = string.Empty,
                    ChannelName = string.Empty,
                    AutoConnect = false
                },
                GoodGame = new GoodGameSettings
                {
                    ChannelId = string.Empty,
                    AutoConnect = false
                },

                // === Оверлей алертов ===
                AlertsOverlayTop = 200,
                AlertsOverlayLeft = 200,
                AlertsOverlayWidth = 500,
                AlertsOverlayHeight = 150,
                AlertsOverlayVisible = true,
                AlertsAlertDuration = 10,

                // === Провайдеры алертов ===
                DonationAlertsEnabled = false,
                DonationAlertsClientId = string.Empty,
                DonationAlertsClientSecret = string.Empty,
                DonationAlertsAccessToken = string.Empty,
                DonationAlertsRefreshToken = string.Empty,
                DonationAlertsTokenExpiresAt = DateTime.MinValue,
                DonationAlertsUserId = 0,

                DonationPayEnabled = false,
                DonationPayApiKey = string.Empty,
                DonationPayUserId = 0,
                DonationPaySocketToken = string.Empty
            };

            settings._rankThresholds = new List<int>(DefaultRankThresholds);
            return settings;
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

                CommandPrefixes = CommandPrefixes?.Distinct().ToList() ?? new List<string>();

                CommandShortcuts = CommandShortcuts?
                    .GroupBy(x => x.Key)
                    .Select(g => g.First())
                    .ToList() ?? new List<ShortcutItem>();

                if (_rankThresholds != null)
                    RankThresholds = _rankThresholds;

                YouTube ??= new YouTubeSettings();
                Twitch ??= new TwitchSettings();
                GoodGame ??= new GoodGameSettings();

                var serializer = new XmlSerializer(typeof(AppSettings));
                using var writer = new StreamWriter(ConfigPath);
                serializer.Serialize(writer, this);

                System.Diagnostics.Debug.WriteLine($"[AppSettings] Настройки сохранены в {ConfigPath}");
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
                    var serializer = new XmlSerializer(typeof(AppSettings));
                    using var reader = new StreamReader(ConfigPath);
                    var settings = (AppSettings?)serializer.Deserialize(reader);

                    if (settings != null)
                    {
                        settings.CommandShortcuts ??= CreateDefaultSettings().CommandShortcuts;
                        settings.CommandPrefixes ??= new List<string> { "!", "/" };
                        settings._rankThresholds ??= new List<int>(DefaultRankThresholds);

                        settings.YouTube ??= new YouTubeSettings();
                        settings.YouTube.Colors ??= new YouTubeColorSettings();
                        settings.Twitch ??= new TwitchSettings();
                        settings.GoodGame ??= new GoodGameSettings();

                        if (string.IsNullOrEmpty(settings.YouTube.ApiKey))
                        {
                            var envApiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
                            if (!string.IsNullOrEmpty(envApiKey))
                                settings.YouTube.ApiKey = envApiKey;
                        }

                        System.Diagnostics.Debug.WriteLine($"[AppSettings] Настройки загружены из {ConfigPath}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Ошибка загрузки: {ex.Message}");
            }

            return CreateDefaultSettings();
        }

        public Dictionary<string, string> GetCommandShortcutsAsDictionary()
        {
            var dict = new Dictionary<string, string>();
            foreach (var item in CommandShortcuts ?? new List<ShortcutItem>())
            {
                if (!dict.ContainsKey(item.Key))
                    dict[item.Key] = item.Value;
            }
            return dict;
        }

        public void SetCommandShortcutsFromDictionary(Dictionary<string, string> dict)
        {
            CommandShortcuts = dict?.Select(kvp => new ShortcutItem { Key = kvp.Key, Value = kvp.Value }).ToList()
                ?? new List<ShortcutItem>();
        }

        // === Вспомогательные методы для YouTube ===
        public string GetYouTubeApiKey() => YouTube?.ApiKey ?? string.Empty;
        public string GetYouTubeChannelId() => YouTube?.ChannelId ?? string.Empty;

        public void UpdateYouTubeSettings(string apiKey, string channelId, bool autoConnect = true)
        {
            YouTube ??= new YouTubeSettings();
            YouTube.ApiKey = apiKey;
            YouTube.ChannelId = channelId;
            YouTube.AutoConnect = autoConnect;
            OnPropertyChanged(nameof(YouTube));
        }

        public bool IsYouTubeConfigured() =>
            YouTube != null &&
            !string.IsNullOrEmpty(YouTube.ApiKey) &&
            !string.IsNullOrEmpty(YouTube.ChannelId);


        // ============================================================
        // === НАСТРОЙКИ ОВЕРЛЕЯ АЛЕРТОВ (общий для всех провайдеров) ===
        // ============================================================
        public double AlertsOverlayTop { get; set; } = 200;
        public double AlertsOverlayLeft { get; set; } = 200;
        public double AlertsOverlayWidth { get; set; } = 500;
        public double AlertsOverlayHeight { get; set; } = 150;
        public bool AlertsOverlayVisible { get; set; } = true;

        /// <summary>
        /// Длительность показа одного алерта (в секундах)
        /// </summary>
        public int AlertsAlertDuration { get; set; } = 10;

        // ============================================================
        // === ПРОВАЙДЕР: DonationAlerts (OAuth) ===
        // ============================================================
        public bool DonationAlertsEnabled { get; set; } = false;

        /// <summary>
        /// App ID (Client ID) — пользователь копирует с сайта DonationAlerts
        /// </summary>
        public string DonationAlertsClientId { get; set; } = string.Empty;

        /// <summary>
        /// API Key (Client Secret) — пользователь копирует с сайта DonationAlerts
        /// </summary>
        public string DonationAlertsClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// OAuth Access Token — получается автоматически после авторизации
        /// </summary>
        public string DonationAlertsAccessToken { get; set; } = string.Empty;

        /// <summary>
        /// OAuth Refresh Token — для обновления access_token
        /// </summary>
        public string DonationAlertsRefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Дата истечения access_token
        /// </summary>
        public DateTime DonationAlertsTokenExpiresAt { get; set; } = DateTime.MinValue;

        /// <summary>
        /// User ID на DonationAlerts — получается автоматически после OAuth
        /// </summary>
        public long DonationAlertsUserId { get; set; } = 0;

        // ============================================================
        // === ПРОВАЙДЕР: DonatePay ===
        // ============================================================
        public bool DonationPayEnabled { get; set; } = false;

        /// <summary>
        /// API-ключ DonatePay (из личного кабинета)
        /// </summary>
        public string DonationPayApiKey { get; set; } = string.Empty;

        /// <summary>
        /// User ID на DonatePay (получается автоматически при подключении)
        /// </summary>
        public long DonationPayUserId { get; set; } = 0;

        /// <summary>
        /// Socket-токен для Centrifugo (получается автоматически)
        /// </summary>
        public string DonationPaySocketToken { get; set; } = string.Empty;


    }

    // ============================================================
    // === КЛАССЫ НАСТРОЕК ПЛАТФОРМ ===
    // ============================================================

    public class YouTubeSettings
    {
        [XmlElement("ApiKey")]
        public string ApiKey { get; set; } = string.Empty;

        [XmlElement("ChannelId")]
        public string ChannelId { get; set; } = string.Empty;

        [XmlElement("AutoConnect")]
        public bool AutoConnect { get; set; } = true;

        [XmlElement("ShowSubscriberAlerts")]
        public bool ShowSubscriberAlerts { get; set; } = true;

        [XmlElement("PollingInterval")]
        public int PollingInterval { get; set; } = 600;

        [XmlElement("Colors")]
        public YouTubeColorSettings Colors { get; set; } = new();

        [XmlElement("LastVideoId")]
        public string LastVideoId { get; set; } = string.Empty;

        [XmlElement("MaxHistorySize")]
        public int MaxHistorySize { get; set; } = 1000;
    }

    public class YouTubeColorSettings
    {
        [XmlElement("Streamer")]
        public string Streamer { get; set; } = "#FFD600";

        [XmlElement("Moderator")]
        public string Moderator { get; set; } = "#5E84F1";

        [XmlElement("Member")]
        public string Member { get; set; } = "#107516";

        [XmlElement("Verified")]
        public string Verified { get; set; } = "#808080";
    }

    public class TwitchSettings
    {
        [XmlElement("ClientId")]
        public string ClientId { get; set; } = string.Empty;

        [XmlElement("AccessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [XmlElement("ChannelName")]
        public string ChannelName { get; set; } = string.Empty;

        [XmlElement("AutoConnect")]
        public bool AutoConnect { get; set; } = false;
    }

    public class GoodGameSettings
    {
        [XmlElement("ChannelId")]
        public string ChannelId { get; set; } = string.Empty;

        [XmlElement("AutoConnect")]
        public bool AutoConnect { get; set; } = false;
    }

    public class ShortcutItem
    {
        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlAttribute("value")]
        public string Value { get; set; } = string.Empty;
    }
}