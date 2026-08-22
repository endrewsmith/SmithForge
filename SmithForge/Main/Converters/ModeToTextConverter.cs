using System;
using System.Globalization;
using System.Windows.Data;
using SmithForge.Main.Models;

namespace SmithForge.Main.Converters
{
    public class ModeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMode mode)
            {
                return mode == ChatMode.Normal ? "📺 Обычный" : "📱 Shorts";
            }
            return "❓";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}