using System;
using System.Globalization;
using System.Windows.Data;

namespace SmithForge.Main.Utils
{
    public class UnixToDateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long unixTime && unixTime > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            }
            return value is long l && l == 0 ? "Идет эфир..." : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

