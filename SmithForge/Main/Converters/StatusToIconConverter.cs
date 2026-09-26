using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmithForge.Main.Converters
{
    /// <summary>
    /// Конвертер статуса чата в цвет индикатора-кружочка.
    /// ⚪ серый — не подключён
    /// 🟢 зелёный — подключён
    /// 🟡 жёлтый — подключается
    /// 🔴 красный — ошибка
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string ?? "";

            // 🟡 СНАЧАЛА проверяем "подключается" — потому что "Подключение" содержит "Подключен"
            if (status.Contains("🔄") || status.Contains("Подключение"))
                return new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)); // 🟡

            // 🟢 Потом "подключён"
            if (status.Contains("✅") || status.Contains("Подключен"))
                return new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // 🟢

            // 🔴 Ошибка
            if (status.Contains("❌") || status.Contains("Ошибка"))
                return new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)); // 🔴

            // ⚪ Не подключён
            return new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}