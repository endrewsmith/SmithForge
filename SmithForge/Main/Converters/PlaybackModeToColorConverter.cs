using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmithForge.Main.Models;

namespace SmithForge.Main.Converters
{
    public class PlaybackModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImportantPlaybackMode mode && parameter is string param)
            {
                if (param == "Auto" && mode == ImportantPlaybackMode.Auto)
                    return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Зеленый
                if (param == "Manual" && mode == ImportantPlaybackMode.Manual)
                    return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Оранжевый
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}