using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SmithForge.Main.Services
{
    public static class TextBlockExtensions
    {
        // Регистрируем Attached Property
        public static readonly DependencyProperty FormattedInlinesProperty =
            DependencyProperty.RegisterAttached(
                "FormattedInlines",
                typeof(Inline),
                typeof(TextBlockExtensions),
                new PropertyMetadata(null, OnFormattedInlinesChanged));

        // Геттер
        public static Inline GetFormattedInlines(DependencyObject obj)
        {
            return (Inline)obj.GetValue(FormattedInlinesProperty);
        }

        // Сеттер
        public static void SetFormattedInlines(DependencyObject obj, Inline value)
        {
            obj.SetValue(FormattedInlinesProperty, value);
        }

        // Логика обновления: когда конвертер возвращает Span, мы вставляем его в TextBlock
        private static void OnFormattedInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                if (e.NewValue is Inline inline)
                {
                    textBlock.Inlines.Add(inline);
                }
            }
        }
    }
}
