using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmithForge.Main.Services;

/// <summary>
/// Сервис для отображения диалоговых окон
/// </summary>
public class DialogService
{
    /// <summary>
    /// Показывает диалог для ввода Video ID
    /// </summary>
    public async Task<string?> ShowVideoIdDialogAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new Window
            {
                Title = "Введите Video ID",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Введите ID видео (11 символов):",
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);

            var textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(5)
            };
            Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "✅ Подключить",
                Padding = new Thickness(15, 5, 15, 5),
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "❌ Отмена",
                Padding = new Thickness(15, 5, 15, 5)
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            okButton.Click += (s, e) =>
            {
                var id = textBox.Text.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    MessageBox.Show("Введите Video ID!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (id.Length != 11)
                {
                    MessageBox.Show("Video ID должен содержать 11 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                tcs.SetResult(id);
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                tcs.SetResult(null);
                dialog.Close();
            };

            dialog.Closed += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(null);
                }
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            dialog.ShowDialog();
        });

        return await tcs.Task;
    }
}