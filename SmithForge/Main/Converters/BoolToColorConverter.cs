using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmithForge.Main.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Если передан параметр, используем его как цвета через разделитель '|'
                if (parameter is string colors)
                {
                    var parts = colors.Split('|');
                    if (parts.Length >= 2)
                    {
                        try
                        {
                            return boolValue
                                ? (SolidColorBrush)new BrushConverter().ConvertFrom(parts[0])
                                : (SolidColorBrush)new BrushConverter().ConvertFrom(parts[1]);
                        }
                        catch
                        {
                            // Если не удалось распарсить, используем стандартные
                        }
                    }
                }

                // Стандартные цвета
                return boolValue
                    ? new SolidColorBrush(Colors.Green)   // для true (например, Shorts)
                    : new SolidColorBrush(Colors.Blue);   // для false (например, обычный стрим)
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // ✅ УДАЛИТЕ ЭТОТ КЛАСС ОТСЮДА
    // public class BoolToTextConverter : IValueConverter
    // {
    //     ...
    // }
}