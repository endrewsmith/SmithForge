using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SmithForge.Main.Models;

namespace SmithForge.Main.Converters
{
    public class YouTubeMethodToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            try
            {
                var currentMethod = (YouTubeConnectionMethod)value;
                var targetMethod = (YouTubeConnectionMethod)Enum.Parse(typeof(YouTubeConnectionMethod), parameter.ToString());

                return currentMethod == targetMethod ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}