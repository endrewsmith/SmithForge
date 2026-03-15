using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SmithForge.Main.Services
{
    public static class TextBlockExtensions
    {
        public static readonly DependencyProperty FormattedInlinesProperty =
            DependencyProperty.RegisterAttached(
                "FormattedInlines",
                typeof(Inline),
                typeof(TextBlockExtensions),
                new PropertyMetadata(null, OnFormattedInlinesChanged));

        public static Inline GetFormattedInlines(DependencyObject obj)
        {
            return (Inline)obj.GetValue(FormattedInlinesProperty);
        }

        public static void SetFormattedInlines(DependencyObject obj, Inline value)
        {
            obj.SetValue(FormattedInlinesProperty, value);
        }

        private static void OnFormattedInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // Проверяем, что свойство висит на Inline (Run или Span)
            if (d is Inline currentInline)
            {
                // Пытаемся найти родительский TextBlock
                if (currentInline.Parent is TextBlock textBlock)
                {
                    // Находим индекс текущего элемента вручную
                    int index = -1;
                    int count = 0;
                    foreach (var inline in textBlock.Inlines)
                    {
                        if (inline == currentInline)
                        {
                            index = count;
                            break;
                        }
                        count++;
                    }

                    if (index != -1)
                    {
                        // Удаляем всё, что идет ПОСЛЕ нашего элемента (старый текст сообщения)
                        while (textBlock.Inlines.Count > index + 1)
                        {
                            textBlock.Inlines.Remove(textBlock.Inlines.LastInline);
                        }

                        // Добавляем новый текст из конвертера в конец коллекции
                        if (e.NewValue is Inline newInline)
                        {
                            textBlock.Inlines.Add(newInline);
                        }
                    }
                }
            }
        }



        private static void UpdateInlines(TextBlock textBlock, Inline? newInline)
        {
            // Если XAML еще не отрисовал ник и двоеточие, выходим. 
            // WPF вызовет это снова, когда отработает привязка ника.
            if (textBlock.Inlines.Count < 2) return;

            // Удаляем ВСЁ, что идет после индекса 1 (после ника и двоеточия)
            while (textBlock.Inlines.Count > 2)
            {
                textBlock.Inlines.Remove(textBlock.Inlines.LastInline);
            }

            // Добавляем текст сообщения
            if (newInline != null)
            {
                // Чтобы избежать ошибки "Inline уже принадлежит другому объекту"
                // (если сообщение обновляется), можно сделать проверку на родителя, 
                // но обычно в конвертере создается новый объект, так что Add достаточно.
                textBlock.Inlines.Add(newInline);
            }
        }

    }
}