// EmojiConfigService.cs
using SmithForge.Main.Models;
using System;
using System.IO;
using System.Xml.Serialization;

namespace SmithForge.Main.Services
{
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

                    if (_config == null)
                        _config = CreateDefaultConfig();
                }
                else
                {
                    _config = CreateDefaultConfig();
                    Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiConfig] Ошибка загрузки: {ex.Message}");
                _config = CreateDefaultConfig();
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
                    DefaultEmojiSize = 16,
                    PreferAnimated = true,
                    CacheImages = true
                },
                Sources = new List<EmojiSourceConfig>  // ← EmojiSourceConfig, а не EmojiSource
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
                Formats = new List<string> { ";{code};" },
                Prefix = "gg",
                Enabled = true
            }
        }
            };
        }
    }
}