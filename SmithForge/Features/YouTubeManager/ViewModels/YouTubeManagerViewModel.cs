using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Models;
using SmithForge.ChatEngine.Services;
using SmithForge.Features.YouTubeManager.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Channels;
using System.Windows.Media;

namespace SmithForge.Features.YouTubeManager.ViewModels;

public partial class YouTubeManagerViewModel : ObservableObject
{
    private readonly ILogger<YouTubeManagerViewModel>? _logger;
    private readonly HttpClient _httpClient;
    private YouTubeLiveService? _liveService;
    private string _currentFilter = "all";
    private bool _isFiltering = false;
    private bool _isConnected = false;

    // Коллекции
    public ObservableCollection<YouTubeStreamModel> AllStreams { get; } = new();
    public ObservableCollection<YouTubeStreamModel> FilteredStreams { get; } = new();
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<YoutubeChatClient> ActiveClients { get; } = new();

    // Свойства для привязки
    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _channelId = "UCsf2sD1gJWus1OUrq2fGwlQ";

    [ObservableProperty]
    private string _manualVideoId = "BprgTJKqrYw";

    [ObservableProperty]
    private string _statusText = "⚪ Не подключен";

    [ObservableProperty]
    private Brush _statusColor = Brushes.Gray;

    [ObservableProperty]
    private string _activeStreamsText = "📡 Активных стримов: 0";

    [ObservableProperty]
    private string _messagesCountText = "💬 Сообщений: 0";

    [ObservableProperty]
    private string _activeClientsText = "🔗 Подключено: 0";

    [ObservableProperty]
    private string _filterCountText = "Показано: 0";

    [ObservableProperty]
    private bool _isConnectedToAny;

    [ObservableProperty]
    private bool _isLoading;

    // События для логирования в UI
    public event EventHandler<string>? OnLog;

    // ✅ Новое событие для передачи сообщений в MainViewModel
    public event EventHandler<ChatMessage>? MessageReceived;

    public YouTubeManagerViewModel(ILogger<YouTubeManagerViewModel>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();

        LogMessage("🚀 YouTube Manager запущен");
        LogMessage("💡 Введите API ключ и ID канала");
        LogMessage("💡 Используйте фильтр для отбора стримов по типу");
        LogMessage("💡 Или введите Video ID вручную и нажмите 'Добавить вручную'");
    }

    // =====================================================
    // ФИЛЬТРАЦИЯ
    // =====================================================

    [RelayCommand]
    private void ApplyFilter(string filterType)
    {
        if (_isFiltering) return;
        _isFiltering = true;

        try
        {
            _currentFilter = filterType;
            FilteredStreams.Clear();

            IEnumerable<YouTubeStreamModel> filtered;

            switch (_currentFilter)
            {
                case "regular":
                    filtered = AllStreams.Where(s => !s.IsShorts);
                    break;
                case "shorts":
                    filtered = AllStreams.Where(s => s.IsShorts);
                    break;
                default:
                    filtered = AllStreams;
                    break;
            }

            foreach (var stream in filtered)
            {
                FilteredStreams.Add(stream);
            }

            FilterCountText = $"Показано: {FilteredStreams.Count}";
            LogMessage($"🔍 Фильтр: {_currentFilter} - показано {FilteredStreams.Count} стримов");
        }
        finally
        {
            _isFiltering = false;
        }
    }

    // =====================================================
    // ПОИСК СТРИМОВ
    // =====================================================

    [RelayCommand]
    private async Task FindStreamsViaApiAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "ВАШ_API_КЛЮЧ")
            {
                LogMessage("❌ Введите корректный Google API ключ!");
                return;
            }

            if (string.IsNullOrEmpty(ChannelId))
            {
                LogMessage("❌ Введите ID канала!");
                return;
            }

            _liveService = new YouTubeLiveService(ApiKey);
            _liveService.OnLog += (s, msg) => LogMessage(msg);

            LogMessage($"🔍 Поиск LIVE стримов через API (тратит квоту)...");
            SetStatus("Поиск через API...", Brushes.Orange);

            var streams = await _liveService.GetLiveStreamsViaApiAsync(ChannelId);
            AddStreamsToList(streams, "API");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Ошибка: {ex.Message}");
            SetStatus($"Ошибка", Brushes.Red);
            _logger?.LogError(ex, "Ошибка поиска через API");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task FindStreamsViaHtmlAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            if (string.IsNullOrEmpty(ChannelId))
            {
                LogMessage("❌ Введите ID канала!");
                return;
            }

            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "ВАШ_API_КЛЮЧ")
            {
                LogMessage("⚠️ API ключ не указан, имя канала может не определиться");
            }

            _liveService = new YouTubeLiveService(ApiKey);
            _liveService.OnLog += (s, msg) => LogMessage(msg);

            LogMessage($"🔍 Поиск LIVE стримов через парсинг (бесплатно, 0 квоты)...");
            SetStatus("Поиск через парсинг...", Brushes.Orange);

            var streams = await _liveService.GetLiveStreamsViaHtmlAsync(ChannelId);
            AddStreamsToList(streams, "парсинг (бесплатно)");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Ошибка: {ex.Message}");
            SetStatus($"Ошибка", Brushes.Red);
            _logger?.LogError(ex, "Ошибка поиска через HTML");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddStreamsToList(List<YouTubeStreamInfo> streams, string method)
    {
        if (streams.Count == 0)
        {
            LogMessage($"❌ Стримы не найдены (метод: {method})");
            SetStatus("Стримы не найдены", Brushes.Red);
            return;
        }

        var addedCount = 0;
        foreach (var stream in streams)
        {
            if (!AllStreams.Any(s => s.VideoId == stream.VideoId))
            {
                var model = new YouTubeStreamModel
                {
                    VideoId = stream.VideoId,
                    Title = stream.Title,
                    IsShorts = stream.IsShorts,
                    ChannelName = stream.ChannelName,
                    IsSelected = false
                };
                AllStreams.Add(model);
                addedCount++;
            }
        }

        LogMessage($"✅ Добавлено {addedCount} новых стримов (метод: {method})");
        SetStatus($"Добавлено {addedCount} стримов", Brushes.Green);

        ApplyFilter("all");
        ActiveStreamsText = $"📡 Активных стримов: {AllStreams.Count}";
        LogMessage($"📊 Всего стримов в списке: {AllStreams.Count}");
    }

    // =====================================================
    // РУЧНОЕ ДОБАВЛЕНИЕ СТРИМА
    // =====================================================

    [RelayCommand]
    private async Task AddManualStreamAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var videoId = ManualVideoId.Trim();

            if (string.IsNullOrEmpty(videoId) || videoId.Length != 11)
            {
                LogMessage($"❌ Неверный формат Video ID: '{videoId}'");
                return;
            }

            if (AllStreams.Any(s => s.VideoId == videoId))
            {
                LogMessage($"⚠️ Стрим с ID {videoId} уже есть в списке");
                return;
            }

            LogMessage($"➕ Добавление стрима вручную: {videoId}");

            var title = await GetVideoTitleAsync(videoId);
            var isShorts = title.Contains("#shorts", StringComparison.OrdinalIgnoreCase);

            var stream = new YouTubeStreamModel
            {
                VideoId = videoId,
                Title = title,
                IsShorts = isShorts,
                IsSelected = false
            };

            AllStreams.Add(stream);
            ApplyFilter("all");

            LogMessage($"✅ Добавлен стрим: {stream.DisplayText}");
            ActiveStreamsText = $"📡 Активных стримов: {AllStreams.Count}";
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Ошибка добавления: {ex.Message}");
            _logger?.LogError(ex, "Ошибка добавления стрима вручную");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<string> GetVideoTitleAsync(string videoId)
    {
        try
        {
            if (string.IsNullOrEmpty(ApiKey) || ApiKey == "ВАШ_API_КЛЮЧ")
            {
                return "Ручной стрим (неизвестно)";
            }

            var url = $"https://www.googleapis.com/youtube/v3/videos?id={videoId}&part=snippet&key={ApiKey}";
            var response = await _httpClient.GetStringAsync(url);

            using var doc = System.Text.Json.JsonDocument.Parse(response);
            var items = doc.RootElement.GetProperty("items");

            if (items.GetArrayLength() > 0)
            {
                var video = items[0];
                var title = video.GetProperty("snippet").GetProperty("title").GetString();
                return title ?? "Без названия";
            }

            return "Видео не найдено";
        }
        catch
        {
            return "Ручной стрим";
        }
    }

    [RelayCommand]
    private void ClearAllStreams()
    {
        if (IsConnectedToAny)
        {
            DisconnectAllClients();
        }

        AllStreams.Clear();
        ApplyFilter("all");
        ActiveStreamsText = "📡 Активных стримов: 0";
        LogMessage("🗑 Все стримы удалены из списка");
    }

    // =====================================================
    // ПОДКЛЮЧЕНИЕ К СТРИМАМ
    // =====================================================

    [RelayCommand]
    public async Task ConnectSelectedAsync()
    {
        var selected = FilteredStreams.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            LogMessage("❌ Выберите хотя бы один стрим!");
            return;
        }

        await ConnectToStreams(selected);
    }

    [RelayCommand]
    private async Task ConnectAllAsync()
    {
        if (FilteredStreams.Count == 0)
        {
            LogMessage("❌ Нет доступных стримов!");
            return;
        }

        foreach (var stream in FilteredStreams)
        {
            stream.IsSelected = true;
        }

        await ConnectToStreams(FilteredStreams.ToList());
    }

    private async Task ConnectToStreams(List<YouTubeStreamModel> streams)
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            if (streams.Count == 0)
            {
                LogMessage("❌ Нет стримов для подключения!");
                return;
            }

            DisconnectAllClients();

            LogMessage($"▶ Подключение к {streams.Count} стримам...");
            SetStatus($"Подключение к {streams.Count} стримам...", Brushes.Orange);

            var tasks = streams.Select(s => ConnectToSingleStream(s));
            await Task.WhenAll(tasks);

            IsConnectedToAny = true;
            ActiveClientsText = $"🔗 Подключено: {ActiveClients.Count}";

            SetStatus($"Подключен к {ActiveClients.Count} стримам ✅", Brushes.Green);
            LogMessage($"✅ Подключен к {ActiveClients.Count} стримам");
        }
        catch (Exception ex)
        {
            LogMessage($"❌ Ошибка подключения: {ex.Message}");
            SetStatus("Ошибка", Brushes.Red);
            _logger?.LogError(ex, "Ошибка подключения к стримам");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ConnectToSingleStream(YouTubeStreamModel stream)
    {
        try
        {
            LogMessage($"  - Подключение к: {stream.Title} (ID: {stream.VideoId})");

            var parser = new YoutubeHtmlParser();
            var client = new YoutubeChatClient(_httpClient, parser);

            client.OnMessageReceived += OnChatMessageReceived;
            client.OnLog += (s, msg) => LogMessage($"[{stream.Title}] {msg}");
            client.OnStatusChanged += OnChatStatusChanged;

            var success = await client.ConnectAsync(stream.VideoId);

            if (success)
            {
                ActiveClients.Add(client);
                LogMessage($"  ✅ Подключен к: {stream.Title}");
                AddSystemMessage($"Подключен к чату: {stream.Title}", stream.VideoId);
            }
            else
            {
                LogMessage($"  ❌ Не удалось подключиться к: {stream.Title}");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"  ❌ Ошибка подключения к {stream.Title}: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DisconnectAll()
    {
        DisconnectAllClients();
    }

    private void DisconnectAllClients()
    {
        foreach (var client in ActiveClients)
        {
            client.Disconnect();
            client.OnMessageReceived -= OnChatMessageReceived;
            client.OnLog -= (s, msg) => { };
            client.OnStatusChanged -= OnChatStatusChanged;
        }

        ActiveClients.Clear();
        IsConnectedToAny = false;
        ActiveClientsText = "🔗 Подключено: 0";

        LogMessage("⏹ Все клиенты отключены");
        SetStatus("Отключен", Brushes.Gray);
        AddSystemMessage("Отключен от всех чатов", "");
    }

    // =====================================================
    // ОБРАБОТЧИКИ СОБЫТИЙ
    // =====================================================

    private void OnChatMessageReceived(object? sender, ChatMessage message)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            Messages.Add(message);
            MessagesCountText = $"💬 Сообщений: {Messages.Count}";
        });
        
        // ✅ Вызываем событие, чтобы MainViewModel тоже получил сообщение
        MessageReceived?.Invoke(this, message);
    }

    private void OnChatStatusChanged(object? sender, string status)
    {
        if (status == "error")
        {
            SetStatus("Ошибка чата", Brushes.Red);
        }
    }

    // =====================================================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // =====================================================

    private void LogMessage(string message)
    {
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
        _logger?.LogInformation(message);
    }

    private void SetStatus(string text, Brush color)
    {
        StatusText = text;
        StatusColor = color;
    }

    private void AddSystemMessage(string text, string videoId)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessage
            {
                Platform = ChannelType.YouTube,  // ✅ добавляем платформу
                Author = "Система",
                AuthorId = "system",              // ✅ системный ID
                Text = text,
                Timestamp = DateTime.Now,
                VideoId = videoId,
                ChannelId = string.Empty
            });
        });
    }

    public void Dispose()
    {
        DisconnectAllClients();
        _httpClient.Dispose();
    }
}