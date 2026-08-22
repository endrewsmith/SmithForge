using System.Windows;
using System.Windows.Controls;
using SmithForge.Features.YouTubeManager.ViewModels;

namespace SmithForge.Features.YouTubeManager.Views;

public partial class YouTubeManagerView : UserControl
{
    private YouTubeManagerViewModel? _viewModel;

    public YouTubeManagerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as YouTubeManagerViewModel;

        if (_viewModel != null)
        {
            // Подписываемся на логи
            _viewModel.OnLog += OnLogMessage;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.OnLog -= OnLogMessage;
            _viewModel.Dispose();
        }
    }

    private void OnLogMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (LogBox == null) return;

            LogBox.Text += $"{message}\n";
            LogBox.ScrollToEnd();

            // Ограничиваем количество строк
            var lines = LogBox.Text.Split('\n');
            if (lines.Length > 1000)
            {
                LogBox.Text = string.Join("\n", lines.Skip(lines.Length - 1000));
            }
        });
    }
}