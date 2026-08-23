using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace SmithForge.Main.Services
{
    public static class EmojiService
    {
        private static readonly Dictionary<string, EmojiInfo> _emojiMap = new Dictionary<string, EmojiInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private static Regex _globalEmojiRegex;
        private static bool _initialized = false;
        private static string _emojiRoot;
        private static EmojiConfig _config;
        private static readonly object _regexLock = new object();

        public class EmojiInfo
        {
            public string Code { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public string AnimatedPath { get; set; } = "";
            public string SourceName { get; set; } = "";
            public string DisplayText { get; set; } = "";
            public bool IsAnimated => !string.IsNullOrEmpty(AnimatedPath);
        }

        public static void Initialize()
        {
            if (_initialized) return;

            _emojiRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Emojis");

            _config = EmojiConfigService.Load();

            LoadAllEmojis();
            BuildGlobalRegex();

            _initialized = true;
        }

        public static void Reload()
        {
            _initialized = false;
            _emojiMap.Clear();
            _imageCache.Clear();
            _globalEmojiRegex = null;
            Initialize();
        }

        private static void LoadAllEmojis()
        {
            foreach (var source in _config.Sources)
            {
                if (!source.Enabled) continue;

                string sourcePath = Path.Combine(_emojiRoot, source.Folder);
                if (!Directory.Exists(sourcePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[EmojiService] Папка не найдена: {sourcePath}");
                    continue;
                }

                LoadEmojisFromSource(sourcePath, source);
            }
        }

        private static void LoadEmojisFromSource(string sourcePath, EmojiSourceConfig source)
        {
            string imagesPath = Path.Combine(sourcePath, "Images");
            string animatedPath = Path.Combine(sourcePath, "Animated");

            if (Directory.Exists(imagesPath))
            {
                var files = Directory.GetFiles(imagesPath, "*.png");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    AddEmoji(fileName, file, null, source);
                }
            }

            if (Directory.Exists(animatedPath))
            {
                var files = Directory.GetFiles(animatedPath, "*.gif");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    AddEmoji(fileName, null, file, source);
                }
            }
        }

        private static void AddEmoji(string fileName, string imagePath, string animatedPath, EmojiSourceConfig source)
        {
            foreach (var format in source.Formats)
            {
                string emojiCode = format.Replace("{code}", fileName);

                if (!_emojiMap.ContainsKey(emojiCode))
                {
                    _emojiMap[emojiCode] = new EmojiInfo
                    {
                        Code = emojiCode,
                        ImagePath = imagePath,
                        AnimatedPath = animatedPath,
                        SourceName = source.Name,
                        DisplayText = GetDisplayText(fileName, source.Name)
                    };
                }
            }
        }

        private static string GetDisplayText(string fileName, string sourceName)
        {
            var displayMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "hand_pink_waving", "👋" },
                { "face_blue_smiling", "😊" },
                { "Kappa", "Κ" },
                { "PogChamp", "😮" },
                { "smile", "🙂" },
                { "laugh", "😂" },
                { "heart", "❤️" }
            };

            return displayMap.TryGetValue(fileName, out string display) ? display : $"[{fileName}]";
        }

        private static void BuildGlobalRegex()
        {
            lock (_regexLock)
            {
                var patterns = new List<string>();

                if (_config?.Sources != null)
                {
                    foreach (var source in _config.Sources)
                    {
                        if (!source.Enabled) continue;

                        foreach (var format in source.Formats)
                        {
                            string pattern = Regex.Escape(format)
                                .Replace("\\{code\\}", "([a-z0-9\\-_]+(?:-[a-z0-9\\-_]+)*)");
                            patterns.Add(pattern);
                        }
                    }
                }

                var dynamicCodes = _emojiMap.Values
                    .Where(e => e.SourceName == "YouTube")
                    .Select(e => Regex.Escape(e.Code));

                patterns.AddRange(dynamicCodes);

                if (patterns.Count > 0)
                {
                    string combinedPattern = string.Join("|", patterns.Distinct());
                    _globalEmojiRegex = new Regex(combinedPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
            }
        }

        public static FrameworkElement CreateEmojiElement(string emojiCode, double size = 0, bool preferAnimated = true)
        {
            if (_emojiMap.TryGetValue(emojiCode, out var info))
            {
                return CreateEmojiFromInfo(info, size, preferAnimated);
            }

            return null;
        }

        private static FrameworkElement CreateEmojiFromInfo(EmojiInfo info, double size, bool preferAnimated)
        {
            double emojiSize = size > 0 ? size : _config.Settings.DefaultEmojiSize;
            bool useAnimated = preferAnimated && _config.Settings.PreferAnimated;

            string path = useAnimated && info.IsAnimated ? info.AnimatedPath : info.ImagePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var image = new Image
            {
                Width = emojiSize,
                Height = emojiSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, -2),
                ToolTip = info.Code
            };

            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    AnimationBehavior.SetSourceUri(image, new Uri(path, UriKind.Absolute));
                    AnimationBehavior.SetAutoStart(image, true);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    if (_config.Settings.CacheImages && _imageCache.TryGetValue(path, out var cached))
                    {
                        image.Source = cached;
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path, UriKind.Absolute);
                        bitmap.DecodePixelWidth = (int)emojiSize;
                        bitmap.DecodePixelHeight = (int)emojiSize;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        if (_config.Settings.CacheImages)
                            _imageCache[path] = bitmap;

                        image.Source = bitmap;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return image;
        }

        public static List<string> ExtractEmojis(string text)
        {
            var emojis = new List<string>();
            if (string.IsNullOrEmpty(text) || _globalEmojiRegex == null) return emojis;

            var matches = _globalEmojiRegex.Matches(text);
            foreach (Match match in matches)
            {
                emojis.Add(match.Value);
            }

            return emojis;
        }

        public static bool EmojiExists(string emojiCode)
        {
            if (!_initialized) Initialize();
            return _emojiMap.ContainsKey(emojiCode);
        }

        public static EmojiInfo GetEmojiInfo(string emojiCode)
        {
            if (!_initialized) Initialize();
            _emojiMap.TryGetValue(emojiCode, out var info);
            return info;
        }

        public static string DetectSource(string emojiText)
        {
            if (!_initialized) Initialize();

            foreach (var source in _config.Sources)
            {
                if (!source.Enabled) continue;

                foreach (var format in source.Formats)
                {
                    string[] parts = format.Split(new[] { "{code}" }, StringSplitOptions.None);
                    string prefix = parts[0];
                    string suffix = parts.Length > 1 ? parts[1] : "";

                    bool matches = emojiText.StartsWith(prefix, StringComparison.Ordinal) &&
                                   emojiText.EndsWith(suffix, StringComparison.Ordinal);

                    if (matches && !string.IsNullOrEmpty(prefix))
                    {
                        string middle = emojiText.Substring(prefix.Length, emojiText.Length - prefix.Length - suffix.Length);
                        if (Regex.IsMatch(middle, @"^[a-z0-9-_]+$", RegexOptions.IgnoreCase))
                        {
                            return source.Name;
                        }
                    }
                    else if (matches && string.IsNullOrEmpty(prefix))
                    {
                        return source.Name;
                    }
                }
            }

            return "Unknown";
        }

        public static string NormalizeEmojiCode(string emojiText, string sourceName)
        {
            var source = _config.Sources.Find(s => s.Name == sourceName);
            if (source == null) return emojiText;

            foreach (var format in source.Formats)
            {
                string[] parts = format.Split(new[] { "{code}" }, StringSplitOptions.None);
                string prefix = parts[0];
                string suffix = parts.Length > 1 ? parts[1] : "";

                if (emojiText.StartsWith(prefix, StringComparison.Ordinal) &&
                    emojiText.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string code = emojiText.Substring(prefix.Length, emojiText.Length - prefix.Length - suffix.Length);
                    return format.Replace("{code}", code);
                }
            }

            return emojiText;
        }

        public static List<string> GetAllFormats()
        {
            if (!_initialized) Initialize();

            var formats = new List<string>();
            foreach (var source in _config.Sources)
            {
                if (!source.Enabled) continue;
                formats.AddRange(source.Formats);
            }
            return formats;
        }

        public static string NormalizeEmojiCode(string emojiText)
        {
            string source = DetectSource(emojiText);
            return NormalizeEmojiCode(emojiText, source);
        }

        public static Span ParseTextToSpan(string text, double emojiSize = 0)
        {
            var span = new Span();

            if (string.IsNullOrEmpty(text))
                return span;

            if (!_initialized) Initialize();

            double size = emojiSize > 0 ? emojiSize : _config.Settings.DefaultEmojiSize;

            var regex = _globalEmojiRegex ?? new Regex("$^");
            int lastIndex = 0;
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    span.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                string emojiText = match.Value;
                string normalizedCode = NormalizeEmojiCode(emojiText);
                var emojiElement = CreateEmojiElement(normalizedCode, size, true);

                if (emojiElement != null)
                {
                    span.Inlines.Add(new InlineUIContainer(emojiElement));
                }
                else
                {
                    span.Inlines.Add(new Run(emojiText) { Foreground = Brushes.Gray });
                }

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                span.Inlines.Add(new Run(text.Substring(lastIndex)));
            }

            return span;
        }

        public static double GetDefaultEmojiSize()
        {
            if (!_initialized) Initialize();
            return _config?.Settings?.DefaultEmojiSize ?? 14;
        }

        /// <summary>
        /// Добавляет эмодзи в кэш (для ленивой загрузки из YouTube)
        /// </summary>
        public static void AddEmojiToCache(string code, string imagePath)
        {
            if (!_emojiMap.ContainsKey(code))
            {
                var fileName = code.Trim(':');
                _emojiMap[code] = new EmojiInfo
                {
                    Code = code,
                    ImagePath = imagePath,
                    SourceName = "YouTube",
                    DisplayText = GetDisplayText(fileName, "YouTube")
                };

                System.Diagnostics.Debug.WriteLine($"[EmojiService] ✅ Динамически добавлен: {code}");

                // Пересобираем глобальный Regex, чтобы новый эмодзи начал парситься
                BuildGlobalRegex();
            }
        }
    }
}