using System;
using System.Globalization;
using System.Windows.Data;

namespace SmithForge.Main.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Если передан параметр, используем его как формат
                if (parameter is string format)
                {
                    return boolValue ? format : string.Empty;
                }

                // Для YouTube стримов
                return boolValue ? "📱 Shorts" : "📺 Обычный";
            }
            return "❓ Неизвестно";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}