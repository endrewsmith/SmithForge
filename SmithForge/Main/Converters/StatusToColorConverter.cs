using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmithForge.Main.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string ?? string.Empty;

            if (status.Contains("✅") || status.Contains("Подключен"))
                return new SolidColorBrush(Colors.LightGreen);
            else if (status.Contains("❌") || status.Contains("Ошибка"))
                return new SolidColorBrush(Colors.OrangeRed);
            else if (status.Contains("🔄") || status.Contains("Подключение"))
                return new SolidColorBrush(Colors.Yellow);
            else
                return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}