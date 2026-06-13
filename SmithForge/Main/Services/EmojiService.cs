// EmojiService.cs
using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.IO;
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
        private static Dictionary<string, EmojiInfo> _emojiMap = new Dictionary<string, EmojiInfo>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private static Regex _globalEmojiRegex;
        private static bool _initialized = false;
        private static string _emojiRoot;
        private static EmojiConfig _config;

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

            // Отладка: выводим загруженные источники
            //System.Diagnostics.Debug.WriteLine($"[EmojiService] Загружено источников: {_config.Sources.Count}");
            //foreach (var source in _config.Sources)
            //{
            //    System.Diagnostics.Debug.WriteLine($"[EmojiService] Источник: {source.Name}, Enabled: {source.Enabled}, Форматы: {string.Join(", ", source.Formats)}");
            //}

            LoadAllEmojis();
            BuildGlobalRegex();

            //System.Diagnostics.Debug.WriteLine($"[EmojiService] Загружено {_emojiMap.Count} эмодзи из {_config.Sources.Count} источников");
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

            // Выводим первые 20 ключей для проверки
            //System.Diagnostics.Debug.WriteLine($"[EmojiService] Всего ключей: {_emojiMap.Count}");
            //System.Diagnostics.Debug.WriteLine($"[EmojiService] Примеры ключей:");
            foreach (var key in _emojiMap.Keys.Take(20))
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiService]   - {key}");
            }
        }
        private static void LoadEmojisFromSource(string sourcePath, EmojiSourceConfig source)
        {
            string imagesPath = Path.Combine(sourcePath, "Images");
            string animatedPath = Path.Combine(sourcePath, "Animated");

            //System.Diagnostics.Debug.WriteLine($"[EmojiService] Загрузка из {source.Name}, папка: {sourcePath}");
            //System.Diagnostics.Debug.WriteLine($"[EmojiService]   Images: {imagesPath}, существует: {Directory.Exists(imagesPath)}");
            //System.Diagnostics.Debug.WriteLine($"[EmojiService]   Animated: {animatedPath}, существует: {Directory.Exists(animatedPath)}");

            if (Directory.Exists(imagesPath))
            {
                var files = Directory.GetFiles(imagesPath, "*.png");
                System.Diagnostics.Debug.WriteLine($"[EmojiService]   Найдено PNG: {files.Length}");

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    System.Diagnostics.Debug.WriteLine($"[EmojiService]     Добавляем: {fileName}");
                    AddEmoji(fileName, file, null, source);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiService]   Папка Images не существует: {imagesPath}");
            }

            if (Directory.Exists(animatedPath))
            {
                var files = Directory.GetFiles(animatedPath, "*.gif");
                System.Diagnostics.Debug.WriteLine($"[EmojiService]   Найдено GIF: {files.Length}");

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    System.Diagnostics.Debug.WriteLine($"[EmojiService]     Добавляем GIF: {fileName}");
                    AddEmoji(fileName, null, file, source);
                }
            }
        }

        private static void AddEmoji(string fileName, string imagePath, string animatedPath, EmojiSourceConfig source)
        {
            System.Diagnostics.Debug.WriteLine($"[EmojiService] AddEmoji: fileName={fileName}, source={source.Name}");

            foreach (var format in source.Formats)
            {
                string emojiCode = format.Replace("{code}", fileName);
                System.Diagnostics.Debug.WriteLine($"[EmojiService]   Формат: {format} -> код: {emojiCode}");

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
                    System.Diagnostics.Debug.WriteLine($"[EmojiService]   ✅ Добавлен: {emojiCode}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[EmojiService]   ⚠️ Уже существует: {emojiCode}");
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
            var patterns = new List<string>();

            foreach (var source in _config.Sources)
            {
                if (!source.Enabled) continue;

                foreach (var format in source.Formats)
                {
                    string pattern = Regex.Escape(format)
                        .Replace("\\{code\\}", "([a-z0-9]+(?:-[a-z0-9]+)*)");
                    patterns.Add(pattern);
                }
            }

            if (patterns.Count > 0)
            {
                string combinedPattern = string.Join("|", patterns);
                _globalEmojiRegex = new Regex(combinedPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
        }

        public static FrameworkElement CreateEmojiElement(string emojiCode, double size = 0, bool preferAnimated = true)
        {
            System.Diagnostics.Debug.WriteLine($"[EmojiService] CreateEmojiElement: {emojiCode}");

            if (!_emojiMap.TryGetValue(emojiCode, out var info))
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiService] Эмодзи не найден в словаре: {emojiCode}");
                return null;
            }

            double emojiSize = size > 0 ? size : _config.Settings.DefaultEmojiSize;
            bool useAnimated = preferAnimated && _config.Settings.PreferAnimated;

            string path = useAnimated && info.IsAnimated ? info.AnimatedPath : info.ImagePath;
            if (string.IsNullOrEmpty(path))
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiService] Путь к файлу пуст для {emojiCode}");
                return null;
            }

            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[EmojiService] Файл не существует: {path}");
                return null;
            }

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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EmojiService] GIF error {emojiCode}: {ex.Message}");
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EmojiService] PNG error {emojiCode}: {ex.Message}");
                    return null;
                }
            }

            return image;  // ← ВАЖНО: возвращаем изображение в конце
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
        /// <summary>
        /// Определяет источник эмодзи по тексту (по формату из конфига)
        /// </summary>
        public static string DetectSource(string emojiText)
        {
            if (!_initialized) Initialize();

            foreach (var source in _config.Sources)
            {
                if (!source.Enabled) continue;

                foreach (var format in source.Formats)
                {
                    // Получаем начало и конец формата
                    string[] parts = format.Split(new[] { "{code}" }, StringSplitOptions.None);
                    string prefix = parts[0];
                    string suffix = parts.Length > 1 ? parts[1] : "";

                    // Проверяем, начинается и заканчивается ли текст соответствующими символами
                    bool matches = emojiText.StartsWith(prefix, StringComparison.Ordinal) &&
                                   emojiText.EndsWith(suffix, StringComparison.Ordinal);

                    // Дополнительная проверка: после префикса не должно быть лишних символов до суффикса
                    if (matches && !string.IsNullOrEmpty(prefix))
                    {
                        string middle = emojiText.Substring(prefix.Length, emojiText.Length - prefix.Length - suffix.Length);
                        // middle может содержать только буквы, цифры и дефисы
                        if (System.Text.RegularExpressions.Regex.IsMatch(middle, @"^[a-z0-9\-]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            return source.Name;
                        }
                    }
                    else if (matches && string.IsNullOrEmpty(prefix))
                    {
                        // Если нет префикса (формат {code})
                        return source.Name;
                    }
                }
            }

            return "Unknown";
        }
        /// <summary>
        /// Нормализует код эмодзи для поиска в словаре
        /// </summary>
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
                    // Извлекаем код из середины
                    string code = emojiText.Substring(prefix.Length, emojiText.Length - prefix.Length - suffix.Length);
                    // Возвращаем в формате, который используется в _emojiMap
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


        /// <summary>
        /// Преобразует текст с эмодзи в Span (для использования в TextBlock.Inlines)
        /// </summary>
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
    }
}