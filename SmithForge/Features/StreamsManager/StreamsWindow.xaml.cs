using System;
using System.Collections.Generic;
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

namespace SmithForge.Features.StreamsManager
{
    /// <summary>
    /// Логика взаимодействия для StreamsWindow.xaml
    /// </summary>
    public partial class StreamsWindow : Window
    {
        public StreamsWindow()
        {
            InitializeComponent();
            this.DataContext = new StreamsViewModel();
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Проверяем, что контекст окна — это наша вью-модель
            if (DataContext is StreamsViewModel vm)
            {
                // Вызываем команду открытия логов
                if (vm.OpenLogsCommand.CanExecute(null))
                {
                    vm.OpenLogsCommand.Execute(null);
                }
            }
        }
    }
}
