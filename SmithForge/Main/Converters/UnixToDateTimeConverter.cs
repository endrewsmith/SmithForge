using System;
using System.Globalization;
using System.Windows.Data;

namespace SmithForge.Main.Converters  // ← ДОЛЖНО БЫТЬ ТОЧНО ТАК
{
    public class UnixToDateTimeConverter : IValueConverter  // ← ДОЛЖЕН БЫТЬ public
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long unixTime && unixTime > 0)
            {
                var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
                return dateTime.ToString("dd.MM.yyyy HH:mm");
            }
            return value is long l && l == 0 ? "Идет эфир..." : "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}