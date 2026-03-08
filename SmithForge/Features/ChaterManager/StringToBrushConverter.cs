using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmithForge.Features.ChaterManager
{
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorCode && !string.IsNullOrEmpty(colorCode))
            {
                try
                {
                    // Пробуем преобразовать строку с цветом (например "#9146FF") в кисть
                    var color = (Color)ColorConverter.ConvertFromString(colorCode);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    // Если не получилось, возвращаем серый цвет по умолчанию
                    return new SolidColorBrush(Colors.Gray);
                }
            }

            // Для Twitch, YouTube, GoodGame можно задать цвета по умолчанию
            if (value is string platform)
            {
                return platform.ToLower() switch
                {
                    "tw" or "twitch" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9146FF")),
                    "yt" or "youtube" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF0000")),
                    "gg" or "goodgame" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00A550")),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}