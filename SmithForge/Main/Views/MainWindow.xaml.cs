using SmithForge.Features.ChaterManager;
using SmithForge.Main.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new SmithForge.ViewModels.MainViewModel();
            DataContext = vm;
            WindowStateService.Bind(this, vm.Settings);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Ничего не делаем здесь - всё сохраняется в OnClosed
        }

        protected override void OnClosed(EventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                // Останавливаем чат если он запущен
                if (vm.IsProcessRunning)
                {
                    vm.StopCommand.Execute(null);
                }

                // Сохраняем позицию главного окна
                vm.Settings.WindowTop = this.Top;
                vm.Settings.WindowLeft = this.Left;
                vm.Settings.WindowHeight = this.Height;
                vm.Settings.WindowWidth = this.Width;
                vm.Settings.IsOverlaySetupMode = vm.IsOverlaySetupMode;

                // Сохраняем позицию оверлеев
                vm.SaveOverlayPosition();
                vm.SaveShortsPosition(); // ← добавить этот метод в MainViewModel

                ConfigService.Save(vm.Settings);
            }
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
        private void OpenChaters_Click(object sender, RoutedEventArgs e)
        {
            var win = new ChatersWindow();
            win.DataContext = new ChatersViewModel();
            win.Owner = this;
            win.ShowDialog();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            string newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            var regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(newText);
        }

        private void TextBox_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == ApplicationCommands.Paste)
                e.Handled = true;
        }

        private void OpenStreams_Click(object sender, RoutedEventArgs e)
        {
            var win = new SmithForge.Features.StreamsManager.StreamsWindow();
            win.DataContext = new SmithForge.Features.StreamsManager.StreamsViewModel();
            win.Owner = this;
            win.ShowDialog();
        }

        private void ToggleDashboard_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleDashboardCommand?.Execute(null);
            }
        }

        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void DecimalValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            string content = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            Regex regex = new Regex(@"^[0-9]*\.?[0-9]*$");
            e.Handled = !regex.IsMatch(content);
        }

        private void ToggleShortsOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleShortsOverlayCommand.Execute(null);
            }
        }

        private void ToggleImportantOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleImportantOverlayCommand?.Execute(null);
            }
        }

        private void ToggleStickersOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SmithForge.ViewModels.MainViewModel vm)
            {
                vm.ToggleStickersOverlayCommand.Execute(null);
            }
        }


    }
}