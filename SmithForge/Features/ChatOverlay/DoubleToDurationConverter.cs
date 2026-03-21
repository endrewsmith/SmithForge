using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SmithForge.Features.ChatOverlay
{
    /// <summary>
    /// Конвертирует double (миллисекунды) в Duration для анимации
    /// </summary>
    public class DoubleToDurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Значение по умолчанию - 400 мс
            double milliseconds = 400;

            if (value is double doubleValue && doubleValue > 0)
            {
                milliseconds = doubleValue;
            }
            else if (value is int intValue && intValue > 0)
            {
                milliseconds = intValue;
            }
            else if (value is string strValue && double.TryParse(strValue, out double parsed))
            {
                milliseconds = parsed;
            }

            return new Duration(TimeSpan.FromMilliseconds(milliseconds));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}