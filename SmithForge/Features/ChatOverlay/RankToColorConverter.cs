using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmithForge.Features.ChatOverlay
{
    public class RankToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rank)
            {
                return rank switch
                {
                    1 => new SolidColorBrush(Color.FromRgb(205, 127, 50)),  // Бронза
                    2 => new SolidColorBrush(Color.FromRgb(192, 192, 192)), // Серебро
                    3 => new SolidColorBrush(Color.FromRgb(255, 215, 0)),   // Золото
                    4 => new SolidColorBrush(Color.FromRgb(255, 69, 0)),    // Платина/Красный
                    5 => new SolidColorBrush(Color.FromRgb(0, 255, 255)),   // Бриллиант/Голубой
                    _ => new SolidColorBrush(Color.FromRgb(64, 64, 64))     // Серый для 0
                };
            }
            return new SolidColorBrush(Color.FromRgb(64, 64, 64));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}