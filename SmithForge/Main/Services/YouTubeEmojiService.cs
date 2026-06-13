using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XamlAnimatedGif;

namespace SmithForge.Main.Services
{
    public static class YouTubeEmojiService
    {
        private static Dictionary<string, string> _emojiPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _animatedEmojiPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, BitmapImage> _emojiCache = new Dictionary<string, BitmapImage>();
        private static bool _initialized = false;

        public static void Initialize(string emojiFolder = null)
        {
            if (_initialized) return;

            string folder = emojiFolder ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Emojis", "YouTube", "Images");

            string animatedFolder = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Emojis", "YouTube", "Animated");

            if (Directory.Exists(folder))
            {
                var files = Directory.GetFiles(folder, "*.png");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string emojiCode = $":{fileName.Replace('_', '-')}:";
                    _emojiPathMap[emojiCode] = file;
                }
                System.Diagnostics.Debug.WriteLine($"[YouTubeEmoji] Загружено {_emojiPathMap.Count} статичных эмодзи");
            }

            if (Directory.Exists(animatedFolder))
            {
                var files = Directory.GetFiles(animatedFolder, "*.gif");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string emojiCode = $":{fileName.Replace('_', '-')}:";
                    _animatedEmojiPathMap[emojiCode] = file;
                }
                System.Diagnostics.Debug.WriteLine($"[YouTubeEmoji] Загружено {_animatedEmojiPathMap.Count} анимированных эмодзи");
            }

            _initialized = true;
        }

        public static string GetEmojiPath(string emojiCode, bool preferAnimated = true)
        {
            if (!_initialized) Initialize();

            if (preferAnimated && _animatedEmojiPathMap.TryGetValue(emojiCode, out string animatedPath))
                return animatedPath;

            _emojiPathMap.TryGetValue(emojiCode, out string path);
            return path;
        }

        public static BitmapImage LoadEmojiImage(string emojiCode, int size = 24)
        {
            string path = GetEmojiPath(emojiCode, false); // Для PNG берем статичный
            if (string.IsNullOrEmpty(path))
                return null;

            if (_emojiCache.TryGetValue(emojiCode, out var cached))
                return cached;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.DecodePixelWidth = size;
                image.DecodePixelHeight = size;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();

                _emojiCache[emojiCode] = image;
                return image;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Создает UI элемент для эмодзи (поддерживает PNG и GIF анимацию)
        /// </summary>
        public static FrameworkElement CreateEmojiElement(string emojiCode, double size = 24, bool preferAnimated = true)
        {
            string path = GetEmojiPath(emojiCode, preferAnimated);
            if (string.IsNullOrEmpty(path))
                return null;

            var image = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, -2)
            };

            // Проверяем, является ли файл GIF
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                // Используем XamlAnimatedGif для анимации
                try
                {
                    AnimationBehavior.SetSourceUri(image, new Uri(path, UriKind.Absolute));
                    AnimationBehavior.SetAutoStart(image, true);
                    System.Diagnostics.Debug.WriteLine($"[YouTubeEmoji] Анимированный GIF загружен: {emojiCode}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YouTubeEmoji] Ошибка загрузки GIF {emojiCode}: {ex.Message}");
                    return null;
                }
            }
            else
            {
                // Статичное PNG изображение
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.DecodePixelWidth = (int)size;
                    bitmap.DecodePixelHeight = (int)size;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[YouTubeEmoji] Ошибка загрузки PNG {emojiCode}: {ex.Message}");
                    return null;
                }
            }

            return image;
        }

        public static bool EmojiExists(string emojiCode)
        {
            if (!_initialized) Initialize();
            return _emojiPathMap.ContainsKey(emojiCode) || _animatedEmojiPathMap.ContainsKey(emojiCode);
        }

        public static bool HasAnimatedVersion(string emojiCode)
        {
            if (!_initialized) Initialize();
            return _animatedEmojiPathMap.ContainsKey(emojiCode);
        }
    }
}