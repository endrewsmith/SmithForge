using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // YOUTUBE ПОЛЯ
        // ============================================================
        [ObservableProperty]
        private string _youTubeApiKey = string.Empty;

        [ObservableProperty]
        private string _youTubeChannelId = string.Empty;

        [ObservableProperty]
        private string _youTubeVideoId = string.Empty;

        [ObservableProperty]
        private string _youTubeChannelName = string.Empty;

        [ObservableProperty]
        private bool _isYouTubeConnected = false;

        [ObservableProperty]
        private string _youTubeStatus = "Не подключен";

        [ObservableProperty]
        private int _youTubeViewersCount = 0;

        [ObservableProperty]
        private ObservableCollection<YouTubeStreamInfo> _youTubeStreams = new();

        [ObservableProperty]
        private YouTubeStreamInfo? _selectedYouTubeStream;

        // ============================================================
        // СИНХРОНИЗАЦИЯ С НАСТРОЙКАМИ
        // ============================================================
        partial void OnYouTubeApiKeyChanged(string value) => _settingsService.SetYouTubeApiKey(value);
        partial void OnYouTubeChannelIdChanged(string value) => _settingsService.SetYouTubeChannelId(value);
        partial void OnYouTubeVideoIdChanged(string value) => _settingsService.SetYouTubeVideoId(value);

        // ============================================================
        // КОМАНДА ОТПРАВКИ СООБЩЕНИЯ (не поддерживается YouTube API)
        // ============================================================
        [RelayCommand]
        private async Task SendYouTubeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Debug.WriteLine($"[YouTube] Отправка сообщения (не поддерживается): {message}");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show("Отправка сообщений в YouTube чат не поддерживается через API.",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }
}