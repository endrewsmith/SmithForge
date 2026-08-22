using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using SmithForge.Main.Models;

namespace SmithForge.Main.Converters
{
    public class ConnectedCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.IEnumerable collection)
            {
                var count = collection.Cast<ChatConnection>().Count(c => c.IsConnected);
                return count.ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}