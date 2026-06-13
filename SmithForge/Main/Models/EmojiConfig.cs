using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace SmithForge.Main.Models
{
    [XmlRoot("EmojiConfig")]
    public class EmojiConfig
    {
        [XmlElement("Settings")]
        public EmojiSettings Settings { get; set; } = new EmojiSettings();

        [XmlArray("Sources")]
        [XmlArrayItem("Source")]
        public List<EmojiSourceConfig> Sources { get; set; } = new List<EmojiSourceConfig>();
    }

    public class EmojiSettings
    {
        [XmlElement("DefaultEmojiSize")]
        public int DefaultEmojiSize { get; set; } = 18;

        [XmlElement("PreferAnimated")]
        public bool PreferAnimated { get; set; } = true;

        [XmlElement("CacheImages")]
        public bool CacheImages { get; set; } = true;
    }

    public class EmojiSourceConfig
    {
        [XmlElement("Name")]
        public string Name { get; set; } = "";

        [XmlElement("Folder")]
        public string Folder { get; set; } = "";

        [XmlArray("Formats")]
        [XmlArrayItem("Format")]
        public List<string> Formats { get; set; } = new List<string>();

        [XmlElement("Prefix")]
        public string Prefix { get; set; } = "";

        [XmlElement("Enabled")]
        public bool Enabled { get; set; } = true;
    }

    public static class EmojiConfigService
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data", "Config", "emojis_config.xml");

        private static EmojiConfig _config;

        public static EmojiConfig Load()
        {
            if (_config != null) return _config;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var serializer = new XmlSerializer(typeof(EmojiConfig));
                    using var reader = new StreamReader(ConfigPath);
                    _config = (EmojiConfig?)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiConfig] Ошибка загрузки: {ex.Message}");
            }

            if (_config == null)
            {
                _config = CreateDefaultConfig();
                Save();
            }

            return _config;
        }

        public static void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var serializer = new XmlSerializer(typeof(EmojiConfig));
                using var writer = new StreamWriter(ConfigPath);
                serializer.Serialize(writer, _config);

                System.Diagnostics.Debug.WriteLine($"[EmojiConfig] Сохранен в {ConfigPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiConfig] Ошибка сохранения: {ex.Message}");
            }
        }

        private static EmojiConfig CreateDefaultConfig()
        {
            return new EmojiConfig
            {
                Settings = new EmojiSettings
                {
                    DefaultEmojiSize = 5,
                    PreferAnimated = true,
                    CacheImages = true
                },
                Sources = new List<EmojiSourceConfig>
                {
                    new EmojiSourceConfig
                    {
                        Name = "YouTube",
                        Folder = "YouTube",
                        Formats = new List<string> { ":{code}:" },
                        Prefix = "yt",
                        Enabled = true
                    },
                    new EmojiSourceConfig
                    {
                        Name = "Twitch",
                        Folder = "Twitch",
                        Formats = new List<string> { "[{code}]" },
                        Prefix = "tw",
                        Enabled = true
                    },
                    new EmojiSourceConfig
                    {
                        Name = "GoodGame",
                        Folder = "GoodGame",
                        Formats = new List<string> { "({code})" },
                        Prefix = "gg",
                        Enabled = true
                    }
                }
            };
        }
    }
}