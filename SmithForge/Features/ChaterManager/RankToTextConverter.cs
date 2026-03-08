using System;
using System.Globalization;
using System.Windows.Data;

namespace SmithForge.Features.ChaterManager
{
    public class RankToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rank)
            {
                return rank switch
                {
                    0 => "☆",
                    1 => "★",
                    2 => "★★",
                    3 => "★★★",
                    4 => "★★★★",
                    _ => rank.ToString()
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}