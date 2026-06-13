using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Linq;

namespace SmithForge.Main.Models
{
    public partial class AppSettings : ObservableObject
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "settings.xml");
        private static readonly List<int> DefaultRankThresholds = new List<int> { 10, 50, 100, 200, 500, 1000 };

        // Поле для хранения порогов
        private List<int> _rankThresholds;

        public AppSettings()
        {
            CommandShortcuts = new List<ShortcutItem>();
            CommandPrefixes = new List<string> { "!", "/" };
            _rankThresholds = new List<int>(DefaultRankThresholds);
        }

        public int StickerDisplayTimeMs { get; set; } = 5000;

        // Окно приложения
        public double WindowTop { get; set; } = 100;
        public double WindowLeft { get; set; } = 100;
        public double WindowWidth { get; set; } = 800;
        public double WindowHeight { get; set; } = 600;

        // Сетевые настройки
        public double NetworkPort { get; set; } = 10880;
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
        public bool ImportantOverlayVisible { get; set; } = true;

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
        [XmlArray("CommandShortcuts")]
        [XmlArrayItem("Shortcut")]
        public List<ShortcutItem> CommandShortcuts { get; set; }

        public List<string> CommandPrefixes { get; set; }

        // Настройки голоса
        public string SelectedVoice { get; set; } = string.Empty;
        public string DefaultMaleVoice { get; set; } = string.Empty;
        public string DefaultFemaleVoice { get; set; } = string.Empty;

        public int ImportantSoundVolume { get; set; } = 100;
        public int VoiceVolume { get; set; } = 100;

        // Режим воспроизведения важных сообщений (Auto/Manual)
        public ImportantPlaybackMode ImportantPlaybackMode { get; set; } = ImportantPlaybackMode.Auto;

        // Горячая клавиша для воспроизведения (по умолчанию F8)
        public string ImportantPlaybackHotkey { get; set; } = "F8";

        // Пороги рангов - с автоматической очисткой при get и set
        [XmlIgnore] // Игнорируем для XML, так как используем отдельное поле
        public List<int> RankThresholds
        {
            get
            {
                // При получении всегда возвращаем уникальные отсортированные значения
                if (_rankThresholds == null)
                {
                    _rankThresholds = new List<int>(DefaultRankThresholds);
                }
                return _rankThresholds.Distinct().OrderBy(x => x).ToList();
            }
            set
            {
                if (value != null)
                {
                    // Очищаем дубликаты и сортируем
                    _rankThresholds = value.Distinct().OrderBy(x => x).ToList();
                }
                else
                {
                    _rankThresholds = new List<int>(DefaultRankThresholds);
                }
                OnPropertyChanged();
            }
        }

        // Специальный метод для XML сериализации
        [XmlArray("RankThresholds")]
        [XmlArrayItem("int")]
        public List<int> RankThresholdsForXml
        {
            get
            {
                // При сериализации возвращаем очищенный список
                return RankThresholds;
            }
            set
            {
                // При десериализации очищаем дубликаты
                if (value != null)
                {
                    RankThresholds = value;
                }
            }
        }

        /// <summary> Создает настройки по умолчанию с предустановленными командами </summary>
        public static AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings
            {
                CommandShortcuts = new List<ShortcutItem>
                {
                    new ShortcutItem { Key = "ввв", Value = "!!важно" },
                    new ShortcutItem { Key = "вж", Value = "!!важно" },
                    new ShortcutItem { Key = "ввм", Value = "!!важно:м" },
                    new ShortcutItem { Key = "ввж", Value = "!!важно:ж" },
                    new ShortcutItem { Key = "вв0", Value = "!!важно:0" },
                    new ShortcutItem { Key = "вв1", Value = "!!важно:1" },
                    new ShortcutItem { Key = "вв2", Value = "!!важно:2" },
                    new ShortcutItem { Key = "вв3", Value = "!!важно:3" },
                    new ShortcutItem { Key = "вв4", Value = "!!важно:4" },
                    new ShortcutItem { Key = "вв5", Value = "!!важно:5" },
                    new ShortcutItem { Key = "вв6", Value = "!!важно:6" },
                    new ShortcutItem { Key = "вв7", Value = "!!важно:7" },
                },
                CommandPrefixes = new List<string> { "!", "/" },
                MainChatMode = ChatDisplayMode.AppearAndFade,
                ShortsChatMode = ChatDisplayMode.AppearAndFade,
                ImportantChatMode = ChatDisplayMode.AppearAndFade,
                StickersChatMode = ChatDisplayMode.AppearAndFade
            };

            // Устанавливаем пороги
            settings._rankThresholds = new List<int>(DefaultRankThresholds);

            return settings;
        }

        /// <summary> Сохраняет настройки в XML файл </summary>
        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Очищаем дубликаты перед сохранением
                if (CommandPrefixes != null)
                {
                    CommandPrefixes = CommandPrefixes.Distinct().ToList();
                }

                // Очищаем дубликаты в сокращениях
                if (CommandShortcuts != null)
                {
                    CommandShortcuts = CommandShortcuts
                        .GroupBy(x => x.Key)
                        .Select(g => g.First())
                        .ToList();
                }

                // Очищаем пороги через setter
                if (_rankThresholds != null)
                {
                    RankThresholds = _rankThresholds; // Триггерит очистку
                }

                var serializer = new XmlSerializer(typeof(AppSettings));
                using var writer = new StreamWriter(ConfigPath);
                serializer.Serialize(writer, this);

                System.Diagnostics.Debug.WriteLine($"[AppSettings] Настройки сохранены в {ConfigPath}");
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Сохранено сокращений: {CommandShortcuts?.Count ?? 0}");
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Сохранено порогов: {_rankThresholds?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Ошибка сохранения: {ex.Message}");
            }
        }

        /// <summary> Загружает настройки из XML файла </summary>
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
                        // Инициализация сокращений
                        if (settings.CommandShortcuts == null || settings.CommandShortcuts.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine("[AppSettings] CommandShortcuts пуст, заполняем значениями по умолчанию");
                            settings.CommandShortcuts = CreateDefaultSettings().CommandShortcuts;
                        }

                        // Инициализация префиксов
                        if (settings.CommandPrefixes == null || settings.CommandPrefixes.Count == 0)
                        {
                            settings.CommandPrefixes = new List<string> { "!", "/" };
                        }

                        // Принудительная очистка порогов (через setter)
                        if (settings._rankThresholds != null)
                        {
                            settings.RankThresholds = settings._rankThresholds;
                        }
                        else
                        {
                            settings.RankThresholds = new List<int>(DefaultRankThresholds);
                        }

                        System.Diagnostics.Debug.WriteLine($"[AppSettings] Настройки загружены из {ConfigPath}");
                        System.Diagnostics.Debug.WriteLine($"[AppSettings] Загружено сокращений: {settings.CommandShortcuts.Count}");
                        System.Diagnostics.Debug.WriteLine($"[AppSettings] Загружено порогов: {settings._rankThresholds?.Count ?? 0}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppSettings] Ошибка загрузки: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("[AppSettings] Созданы настройки по умолчанию");
            return CreateDefaultSettings();
        }

        /// <summary> Вспомогательный метод для конвертации List в Dictionary </summary>
        public Dictionary<string, string> GetCommandShortcutsAsDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (CommandShortcuts != null)
            {
                foreach (var item in CommandShortcuts)
                {
                    if (!dict.ContainsKey(item.Key))
                    {
                        dict[item.Key] = item.Value;
                    }
                }
            }
            return dict;
        }

        /// <summary> Вспомогательный метод для установки Dictionary в List </summary>
        public void SetCommandShortcutsFromDictionary(Dictionary<string, string> dict)
        {
            if (dict != null)
            {
                CommandShortcuts = dict.Select(kvp => new ShortcutItem { Key = kvp.Key, Value = kvp.Value }).ToList();
            }
        }
    }

    /// <summary> Вспомогательный класс для хранения пары ключ-значение в XML </summary>
    public class ShortcutItem
    {
        [XmlAttribute("key")]
        public string Key { get; set; } = string.Empty;

        [XmlAttribute("value")]
        public string Value { get; set; } = string.Empty;
    }
}