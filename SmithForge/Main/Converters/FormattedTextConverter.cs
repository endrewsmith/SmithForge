using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Converters
{
    public class FormattedTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string ?? string.Empty;

            // ОТЛАДОЧНЫЙ ВЫВОД
            Debug.WriteLine($"[FormattedTextConverter] ===== CONVERT CALLED =====");
            Debug.WriteLine($"[FormattedTextConverter] Input text: '{text}'");
            Debug.WriteLine($"[FormattedTextConverter] Text length: {text.Length}");

            var span = new Span();
            int index = 0;

            while (index < text.Length)
            {
                // 1. Ищем начало тега
                int openTagIndex = text.IndexOf('<', index);

                // 2. Если '<' больше нет — добавляем остаток и выходим
                if (openTagIndex < 0)
                {
                    string remaining = text.Substring(index);
                    Debug.WriteLine($"[FormattedTextConverter] Adding remaining text: '{remaining}'");
                    span.Inlines.Add(new Run(remaining));
                    break;
                }

                // 3. Добавляем текст ДО символа '<'
                if (openTagIndex > index)
                {
                    string beforeTag = text.Substring(index, openTagIndex - index);
                    Debug.WriteLine($"[FormattedTextConverter] Text before tag: '{beforeTag}'");
                    span.Inlines.Add(new Run(beforeTag));
                }

                bool tagProcessed = false;

                // 4. Проверяем, не является ли это закрывающим тегом '</' сразу
                if (openTagIndex + 1 < text.Length && text[openTagIndex + 1] != '/')
                {
                    int closeTagIndex = text.IndexOf('>', openTagIndex);

                    // Если есть закрывающая '>'
                    if (closeTagIndex > openTagIndex)
                    {
                        string fullTag = text.Substring(openTagIndex + 1, closeTagIndex - openTagIndex - 1);
                        string tagName = fullTag.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLower() ?? "";
                        string closeTagStr = $"</{tagName}>";

                        Debug.WriteLine($"[FormattedTextConverter] Found tag: '{tagName}', full: '{fullTag}'");

                        // Ищем парный закрывающий тег
                        int endTagIndex = text.IndexOf(closeTagStr, closeTagIndex + 1, StringComparison.OrdinalIgnoreCase);

                        if (endTagIndex > closeTagIndex)
                        {
                            // ТЕГ ВАЛИДНЫЙ - берем содержимое
                            string content = text.Substring(closeTagIndex + 1, endTagIndex - closeTagIndex - 1);
                            Debug.WriteLine($"[FormattedTextConverter] Tag content: '{content}'");

                            var run = new Run(content);

                            // ПРИМЕНЯЕМ СТИЛИ
                            ApplyStyle(run, tagName, fullTag.ToLower());

                            Debug.WriteLine($"[FormattedTextConverter] Applied style for tag '{tagName}'");

                            span.Inlines.Add(run);
                            index = endTagIndex + closeTagStr.Length;
                            tagProcessed = true;
                        }
                    }
                }

                // 5. ГАРАНТИЯ ОТ ЗАЦИКЛИВАНИЯ: Если тег битый, одиночный '<' или '</'
                if (!tagProcessed)
                {
                    Debug.WriteLine($"[FormattedTextConverter] Unprocessed char at index {openTagIndex}, adding '<' as text");
                    span.Inlines.Add(new Run("<"));
                    index = openTagIndex + 1; // Всегда сдвигаемся минимум на 1 символ
                }
            }

            Debug.WriteLine($"[FormattedTextConverter] Final span has {span.Inlines.Count} inlines");
            Debug.WriteLine($"[FormattedTextConverter] ===== CONVERT END =====");

            return span;
        }

        private void ApplyStyle(Run run, string tagName, string fullTag)
        {
            try
            {
                Debug.WriteLine($"[FormattedTextConverter] ApplyStyle for tag: '{tagName}', full: '{fullTag}'");

                if (tagName == "b" || tagName == "bold")
                {
                    run.FontWeight = FontWeights.Bold;
                    Debug.WriteLine($"[FormattedTextConverter] Applied Bold");
                }
                else if (tagName == "i" || tagName == "italic")
                {
                    run.FontStyle = FontStyles.Italic;
                    Debug.WriteLine($"[FormattedTextConverter] Applied Italic");
                }
                else if (tagName == "c" || tagName == "color")
                {
                    string colorValue = "white";
                    if (fullTag.Contains("="))
                    {
                        int eqPos = fullTag.IndexOf('=');
                        colorValue = fullTag.Substring(eqPos + 1).Trim();
                    }
                    run.Foreground = GetColorBrush(colorValue);
                    Debug.WriteLine($"[FormattedTextConverter] Applied Color: {colorValue}");
                }
                // Тег important и extend просто игнорируем (они для логики, не для стиля)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Converter Style Error] {ex.Message}");
            }
        }

        private Brush GetColorBrush(string colorValue)
        {
            try
            {
                if (colorValue.StartsWith("#"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorValue));

                return colorValue switch
                {
                    "red" => Brushes.Red,
                    "green" => Brushes.Green,
                    "blue" => Brushes.Blue,
                    "yellow" => Brushes.Yellow,
                    _ => Brushes.White
                };
            }
            catch { return Brushes.White; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}