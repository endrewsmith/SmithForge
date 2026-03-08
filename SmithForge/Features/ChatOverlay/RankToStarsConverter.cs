using System;
using System.Globalization;
using System.Windows.Data;

namespace SmithForge.Features.ChatOverlay
{
    public class RankToStarsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rank)
            {
                return rank switch
                {
                    0 => "☆☆☆☆☆",
                    1 => "★☆☆☆☆",
                    2 => "★★☆☆☆",
                    3 => "★★★☆☆",
                    4 => "★★★★☆",
                    5 => "★★★★★",
                    _ => new string('★', Math.Min(rank, 5)) + new string('☆', Math.Max(0, 5 - Math.Min(rank, 5)))
                };
            }
            return "☆☆☆☆☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}