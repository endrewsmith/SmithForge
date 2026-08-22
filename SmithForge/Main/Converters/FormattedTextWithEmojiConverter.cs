using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace SmithForge.Main.Converters
{
    public class FormattedTextWithEmojiConverter : IValueConverter
    {
        // Регулярка для эмодзи
        private static readonly Regex EmojiRegex = new Regex(
            @"(:([a-z0-9_]+(?:-[a-z0-9_]+)*):|\[([^\]]+)\]|;;([^;]+);;|\{([^\}]+)\})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Регулярка для HTML тегов
        private static readonly Regex TagRegex = new Regex(
            @"<(\/?)(b|i|color)(?:=([^>]+))?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        //public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        //{
        //    string text = value as string;
        //    if (string.IsNullOrEmpty(text))
        //        return new Run("");

        //    System.Diagnostics.Debug.WriteLine($"[Converter] Входной текст: '{text}'");

        //    var matches = EmojiRegex.Matches(text);
        //    System.Diagnostics.Debug.WriteLine($"[Converter] Найдено эмодзи: {matches.Count}");

        //    double emojiSize = EmojiService.GetDefaultEmojiSize();
        //    if (parameter != null)
        //        double.TryParse(parameter.ToString(), out emojiSize);

        //    var resultSpan = new Span();
        //    ProcessTextWithTagsAndEmojis(text, resultSpan, emojiSize);

        //    return resultSpan;
        //}
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string;
            if (string.IsNullOrEmpty(text))
                return new Span();

            double emojiSize = EmojiService.GetDefaultEmojiSize();
            if (parameter != null)
                double.TryParse(parameter.ToString(), out emojiSize);

            var resultSpan = new Span();

            // ✅ Проверяем, есть ли эмодзи
            if (!EmojiRegex.IsMatch(text))
            {
                // Обычный текст
                resultSpan.Inlines.Add(new Run(text) { Foreground = Brushes.White });
                return resultSpan;
            }

            // Есть эмодзи — передаём emojiSize
            ProcessTextWithTagsAndEmojis(text, resultSpan, emojiSize);
            return resultSpan;
        }
        private void ProcessTextWithTagsAndEmojis(string text, Span span, double emojiSize)
        {
            if (string.IsNullOrEmpty(text)) return;

            // ✅ ИСПРАВЛЕННАЯ РЕГУЛЯРКА
            var combinedRegex = new Regex(
                @"<(\/?)(b|i|color)(?:=([^>]+))?>|:([^:]+):",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            int lastIndex = 0;
            var currentStyles = new Stack<TextStyle>();
            var matches = combinedRegex.Matches(text);

            foreach (Match match in matches)
            {
                // Добавляем обычный текст до тега/эмодзи
                if (match.Index > lastIndex)
                {
                    string textPart = text.Substring(lastIndex, match.Index - lastIndex);
                    if (!string.IsNullOrEmpty(textPart))
                    {
                        var run = new Run(textPart);
                        ApplyCurrentStyles(run, currentStyles);
                        span.Inlines.Add(run);
                    }
                }

                // Обрабатываем HTML тег
                if (!string.IsNullOrEmpty(match.Groups[1].Value) || !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    bool isClosing = !string.IsNullOrEmpty(match.Groups[1].Value);
                    string tagName = match.Groups[2].Value.ToLower();

                    if (isClosing)
                    {
                        if (currentStyles.Count > 0 && currentStyles.Peek().TagName == tagName)
                        {
                            currentStyles.Pop();
                        }
                    }
                    else
                    {
                        string valueAttr = match.Groups[3].Success ? match.Groups[3].Value.Trim('"', '\'') : null;
                        var style = new TextStyle { TagName = tagName };

                        if (tagName == "color" && !string.IsNullOrEmpty(valueAttr))
                        {
                            style.Color = ParseColor(valueAttr);
                        }
                        currentStyles.Push(style);
                    }
                }
                // Обрабатываем эмодзи
                else
                {
                    string emojiText = match.Value; // например ":hand_pink_waving:"
                    System.Diagnostics.Debug.WriteLine($"[Converter] Обработка эмодзи: {emojiText}");
                    var emojiElement = CreateEmojiElementFromText(emojiText, emojiSize);

                    if (emojiElement != null)
                    {
                        span.Inlines.Add(new InlineUIContainer(emojiElement));
                    }
                    else
                    {
                        var run = new Run(emojiText) { Foreground = Brushes.Gray };
                        ApplyCurrentStyles(run, currentStyles);
                        span.Inlines.Add(run);
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            // Добавляем оставшийся текст
            if (lastIndex < text.Length)
            {
                string remaining = text.Substring(lastIndex);
                if (!string.IsNullOrEmpty(remaining))
                {
                    var run = new Run(remaining);
                    ApplyCurrentStyles(run, currentStyles);
                    span.Inlines.Add(run);
                }
            }
        }

        private void ApplyCurrentStyles(Run run, Stack<TextStyle> styles)
        {
            foreach (var style in styles)
            {
                if (style.TagName == "b")
                {
                    run.FontWeight = FontWeights.Bold;
                }
                else if (style.TagName == "i")
                {
                    run.FontStyle = FontStyles.Italic;
                }
                else if (style.TagName == "color" && style.Color.HasValue)
                {
                    run.Foreground = new SolidColorBrush(style.Color.Value);
                }
            }
        }

        private Color? ParseColor(string colorValue)
        {
            try
            {
                if (colorValue.StartsWith("#"))
                {
                    return (Color)ColorConverter.ConvertFromString(colorValue);
                }

                return colorValue.ToLower() switch
                {
                    "red" => Colors.Red,
                    "green" => Colors.Green,
                    "blue" => Colors.Blue,
                    "yellow" => Colors.Yellow,
                    "white" => Colors.White,
                    "black" => Colors.Black,
                    "cyan" => Colors.Cyan,
                    "magenta" => Colors.Magenta,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private FrameworkElement CreateEmojiElementFromText(string emojiText, double emojiSize)
        {
            try
            {
                //string code = emojiText.Trim(':');
                //string normalizedCode = code.Replace('_', '-');
                string normalizedCode = emojiText.Replace('_', '-');


                var element = EmojiService.CreateEmojiElement(normalizedCode, emojiSize, true);

                if (element == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Converter] ❌ Эмодзи НЕ СОЗДАН: {normalizedCode}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Converter] ✅ Эмодзи создан: {normalizedCode}");
                }

                return element;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Converter] Ошибка эмодзи: {ex.Message}");
                return null;
            }
        }

        private class TextStyle
        {
            public string TagName { get; set; } = "";
            public Color? Color { get; set; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}