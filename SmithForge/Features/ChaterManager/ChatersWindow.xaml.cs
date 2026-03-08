using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization; // Добавь этот using
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SmithForge.Features.ChaterManager
{
    public partial class ChatersWindow : Window
    {
        public ChatersWindow()
        {
            InitializeComponent();

            Debug.WriteLine("[ChatersWindow] Конструктор");

            // Подписываемся на загрузку окна
            this.Loaded += (s, e) =>
            {
                Debug.WriteLine("[ChatersWindow] Окно загружено");
                Debug.WriteLine($"[ChatersWindow] DataContext: {DataContext?.GetType().Name ?? "null"}");

                // Находим кнопку по имени
                if (this.FindName("TestSaveButton") is Button testButton)
                {
                    testButton.Click += TestButton_Click;
                    Debug.WriteLine("[ChatersWindow] Обработчик тестовой кнопки добавлен");
                }
                else
                {
                    Debug.WriteLine("[ChatersWindow] Тестовая кнопка не найдена!");
                }
            };
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("========== ТЕСТОВАЯ КНОПКА НАЖАТА ==========");

            if (DataContext is ChatersViewModel vm)
            {
                Debug.WriteLine("ViewModel найден, вызываем Save...");
                vm.SaveChaterCommand?.Execute(null);
            }
            else
            {
                Debug.WriteLine("ViewModel не найден!");
                MessageBox.Show("ViewModel не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // ========== КЛАСС КОНВЕРТЕРА ==========
    public class DebugConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "null (отсутствует)";

            return $"{value.GetType().Name} (существует)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}