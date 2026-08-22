using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmithForge.Main.Converters
{
    public class PlatformToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string platform = value.ToString()?.ToLower() ?? string.Empty;
            string targetPlatform = parameter.ToString()?.ToLower() ?? string.Empty;

            return platform == targetPlatform ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}