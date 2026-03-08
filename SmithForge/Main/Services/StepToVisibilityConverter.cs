using System.Windows;
using System.Windows.Data;

namespace SmithForge.Main.Services
{
    // Обязательно public!
    public class StepToVisibilityConverter : IValueConverter
    {
        public static readonly StepToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;

            long count = 0;
            if (value is long l) count = l;
            else if (value is int i) count = i;

            if (int.TryParse(parameter.ToString(), out int step))
            {
                // Показываем черточку, если текущий счет >= номеру черточки
                return count >= step ? Visibility.Visible : Visibility.Hidden;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
}
