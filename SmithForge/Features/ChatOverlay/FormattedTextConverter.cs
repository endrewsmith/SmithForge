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
            Debug.WriteLine($"[FormattedTextConverter] ========== НАЧАЛО ==========");
            Debug.WriteLine($"[FormattedTextConverter] Входной текст: '{text}'");
            Debug.WriteLine($"[FormattedTextConverter] Длина текста: {text.Length}");

            var span = new Span();
            int inlinesCount = 0;

            int index = 0;
            while (index < text.Length)
            {
                Debug.WriteLine($"[FormattedTextConverter] Обработка с позиции {index}, осталось: '{(index < text.Length ? text.Substring(index) : "")}'");

                if (index < text.Length && text[index] == '<')
                {
                    Debug.WriteLine($"[FormattedTextConverter] Найден открывающий тег на позиции {index}");

                    int closeTag = text.IndexOf('>', index);
                    if (closeTag > index)
                    {
                        string tag = text.Substring(index + 1, closeTag - index - 1);

                        // Извлекаем имя тега (до первого пробела или =)
                        string tagName = tag.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        string closeTagName = $"</{tagName}>";

                        Debug.WriteLine($"[FormattedTextConverter] Тег: '{tag}', имя: '{tagName}', закрывающий: '{closeTagName}'");

                        int endTag = text.IndexOf(closeTagName, closeTag + 1);

                        if (endTag > closeTag)
                        {
                            string content = text.Substring(closeTag + 1, endTag - closeTag - 1);
                            Debug.WriteLine($"[FormattedTextConverter] Содержимое тега: '{content}'");

                            var run = new Run(content);

                            // Обработка различных тегов
                            string tagLower = tag.ToLower();
                            Debug.WriteLine($"[FormattedTextConverter] Обработка тега: '{tagLower}'");

                            // Жирный текст
                            if (tagLower == "bold" || tagLower == "b" || tagName == "bold" || tagName == "b")
                            {
                                run.FontWeight = FontWeights.Bold;
                                Debug.WriteLine($"[FormattedTextConverter] Применен жирный шрифт");
                            }
                            // Курсив
                            else if (tagLower == "italic" || tagLower == "i" || tagName == "italic" || tagName == "i")
                            {
                                run.FontStyle = FontStyles.Italic;
                                Debug.WriteLine($"[FormattedTextConverter] Применен курсив");
                            }
                            // Цвет (color=red или просто c)
                            else if (tagLower.StartsWith("color=") || tagName == "color" || tagName == "c")
                            {
                                string colorValue = "red"; // по умолчанию

                                if (tagLower.StartsWith("color="))
                                {
                                    colorValue = tagLower.Substring(6);
                                }
                                else if (tagName == "c" && tag.Contains("="))
                                {
                                    // Тег вида <c=red>
                                    int equalsPos = tag.IndexOf('=');
                                    if (equalsPos > 0)
                                    {
                                        colorValue = tag.Substring(equalsPos + 1);
                                    }
                                }

                                Debug.WriteLine($"[FormattedTextConverter] Цвет: '{colorValue}'");
                                run.Foreground = GetColorBrush(colorValue);
                            }
                            // Тег продления (не влияет на отображение, просто пропускаем)
                            else if (tagName == "extend")
                            {
                                string extendValue = "0";
                                if (tagLower.StartsWith("extend="))
                                {
                                    extendValue = tagLower.Substring(7);
                                }
                                Debug.WriteLine($"[FormattedTextConverter] Тег продления: {extendValue} мс (не влияет на отображение)");
                                // Просто пропускаем, ничего не меняем в отображении
                            }
                            else
                            {
                                Debug.WriteLine($"[FormattedTextConverter] Неизвестный тег: '{tagLower}'");
                            }

                            span.Inlines.Add(run);
                            inlinesCount++;
                            Debug.WriteLine($"[FormattedTextConverter] Добавлен Run #{inlinesCount}");

                            index = endTag + closeTagName.Length;
                            Debug.WriteLine($"[FormattedTextConverter] Перемещаем индекс на {index}");
                            continue;
                        }
                        else
                        {
                            Debug.WriteLine($"[FormattedTextConverter] Не найден закрывающий тег для '{tag}'");
                        }
                    }
                }

                int nextTag = text.IndexOf('<', index);
                if (nextTag < 0) nextTag = text.Length;

                if (nextTag > index)
                {
                    string content = text.Substring(index, nextTag - index);
                    Debug.WriteLine($"[FormattedTextConverter] Обычный текст: '{content}'");

                    span.Inlines.Add(new Run(content));
                    inlinesCount++;
                    Debug.WriteLine($"[FormattedTextConverter] Добавлен обычный текст #{inlinesCount}");
                }
                index = nextTag;
            }

            Debug.WriteLine($"[FormattedTextConverter] Всего добавлено элементов: {inlinesCount}");
            Debug.WriteLine($"[FormattedTextConverter] ========== КОНЕЦ ==========");
            return span;
        }

        private Brush GetColorBrush(string colorValue)
        {
            Debug.WriteLine($"[FormattedTextConverter] GetColorBrush для '{colorValue}'");

            try
            {
                // Проверяем, является ли строка HEX-цветом
                if (colorValue.StartsWith("#"))
                {
                    Debug.WriteLine($"[FormattedTextConverter] HEX-цвет: {colorValue}");
                    var color = (Color)ColorConverter.ConvertFromString(colorValue);
                    Debug.WriteLine($"[FormattedTextConverter] Преобразован в RGB: {color}");
                    return new SolidColorBrush(color);
                }

                // Иначе используем предопределенные цвета
                Debug.WriteLine($"[FormattedTextConverter] Именованный цвет: {colorValue}");
                Brush result = colorValue.ToLower() switch
                {
                    "red" => new SolidColorBrush(Colors.Red),
                    "green" => new SolidColorBrush(Colors.Green),
                    "blue" => new SolidColorBrush(Colors.Blue),
                    "yellow" => new SolidColorBrush(Colors.Yellow),
                    "purple" => new SolidColorBrush(Colors.Purple),
                    "orange" => new SolidColorBrush(Colors.Orange),
                    "cyan" => new SolidColorBrush(Colors.Cyan),
                    "magenta" => new SolidColorBrush(Colors.Magenta),
                    "pink" => new SolidColorBrush(Colors.Pink),
                    "brown" => new SolidColorBrush(Colors.Brown),
                    "black" => new SolidColorBrush(Colors.Black),
                    "white" => new SolidColorBrush(Colors.White),
                    _ => new SolidColorBrush(Colors.White)
                };

                Debug.WriteLine($"[FormattedTextConverter] Возвращаем кисть: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FormattedTextConverter] ОШИБКА: {ex.Message}");
                return new SolidColorBrush(Colors.White);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}