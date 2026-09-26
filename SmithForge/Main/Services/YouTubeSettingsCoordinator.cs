using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using SmithForge.Main.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор настроек YouTube.
    /// Хранит данные подключения к YouTube-каналу и предоставляет UI-свойства.
    /// </summary>
    public partial class YouTubeSettingsCoordinator : ObservableObject
    {
        private readonly SettingsService _settingsService;

        // ============================================================
        // СВОЙСТВА
        // ============================================================
        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private string _channelId = string.Empty;

        [ObservableProperty]
        private string _videoId = string.Empty;

        [ObservableProperty]
        private string _channelName = string.Empty;

        [ObservableProperty]
        private bool _isConnected = false;

        [ObservableProperty]
        private string _status = "Не подключен";

        [ObservableProperty]
        private int _viewersCount = 0;

        [ObservableProperty]
        private ObservableCollection<YouTubeStreamInfo> _streams = new();

        [ObservableProperty]
        private YouTubeStreamInfo? _selectedStream;

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public YouTubeSettingsCoordinator(SettingsService settingsService, AppSettings settings)
        {
            _settingsService = settingsService;

            // Загружаем начальные значения
            _apiKey = settings.YouTube?.ApiKey ?? string.Empty;
            _channelId = settings.YouTube?.ChannelId ?? string.Empty;
            _videoId = settings.YouTube?.LastVideoId ?? string.Empty;

            Debug.WriteLine($"[YouTubeSettings] Инициализирован: channelId={_channelId}");
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ С НАСТРОЙКАМИ
        // ============================================================
        partial void OnApiKeyChanged(string value)
            => _settingsService.SetYouTubeApiKey(value);

        partial void OnChannelIdChanged(string value)
            => _settingsService.SetYouTubeChannelId(value);

        partial void OnVideoIdChanged(string value)
            => _settingsService.SetYouTubeVideoId(value);

        // ============================================================
        // КОМАНДА
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