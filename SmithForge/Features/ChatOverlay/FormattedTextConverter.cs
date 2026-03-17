using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services
{
    public class FormattedTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = value as string ?? string.Empty;
            var span = new Span();
            int index = 0;

            while (index < text.Length)
            {
                // 1. Ищем начало тега
                int openTagIndex = text.IndexOf('<', index);

                // 2. Если '<' больше нет — добавляем остаток и выходим
                if (openTagIndex < 0)
                {
                    span.Inlines.Add(new Run(text.Substring(index)));
                    break;
                }

                // 3. Добавляем текст ДО символа '<'
                if (openTagIndex > index)
                {
                    span.Inlines.Add(new Run(text.Substring(index, openTagIndex - index)));
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

                        // Ищем парный закрывающий тег
                        int endTagIndex = text.IndexOf(closeTagStr, closeTagIndex + 1, StringComparison.OrdinalIgnoreCase);

                        if (endTagIndex > closeTagIndex)
                        {
                            // ТЕГ ВАЛИДНЫЙ - берем содержимое
                            string content = text.Substring(closeTagIndex + 1, endTagIndex - closeTagIndex - 1);
                            var run = new Run(content);

                            // ПРИМЕНЯЕМ СТИЛИ
                            ApplyStyle(run, tagName, fullTag.ToLower());

                            span.Inlines.Add(run);
                            index = endTagIndex + closeTagStr.Length;
                            tagProcessed = true;
                        }
                    }
                }

                // 5. ГАРАНТИЯ ОТ ЗАЦИКЛИВАНИЯ: Если тег битый, одиночный '<' или '</'
                if (!tagProcessed)
                {
                    span.Inlines.Add(new Run("<"));
                    index = openTagIndex + 1; // Всегда сдвигаемся минимум на 1 символ
                }
            }

            return span;
        }

        private void ApplyStyle(Run run, string tagName, string fullTag)
        {
            try
            {
                if (tagName == "b" || tagName == "bold")
                {
                    run.FontWeight = FontWeights.Bold;
                }
                else if (tagName == "i" || tagName == "italic")
                {
                    run.FontStyle = FontStyles.Italic;
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
