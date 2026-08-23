using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Platforms.YouTube;

/// <summary>
/// Коннектор для YouTube чата
/// Объединяет YoutubeChatClient, YouTubeLiveService и YoutubeHtmlParser
/// </summary>
public class YouTubeConnector : IChatConnector
{
    private readonly ILogger<YouTubeConnector>? _logger;
    private readonly HttpClient _httpClient;
    private readonly YoutubeHtmlParser _htmlParser;
    private YoutubeChatClient? _chatClient;
    private YouTubeLiveService? _liveService;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private string _currentVideoId = string.Empty;
    private string _channelId = string.Empty;
    private string _apiKey = string.Empty;
    private ChatMode _chatMode = ChatMode.Normal;
    private readonly string _connectorId;

    public string Id => _connectorId;
    public ChannelType Platform => ChannelType.YouTube;
    public ConnectorStatus Status { get; private set; } = new();

    public event EventHandler<IncomingChatMessage>? MessageReceived;
    public event EventHandler<ConnectorStatus>? StatusChanged;

    public string? GetVideoId()
    {
        return _currentVideoId;
    }

    /// <summary>
    /// Конструктор для подключения к конкретному видео
    /// </summary>
    public YouTubeConnector(
        ILogger<YouTubeConnector>? logger = null,
        string videoId = "",
        string channelId = "",
        string apiKey = "",
        string connectorId = "")
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _htmlParser = new YoutubeHtmlParser();
        _currentVideoId = videoId;
        _channelId = channelId;
        _apiKey = apiKey;
        _connectorId = string.IsNullOrEmpty(connectorId) ? Guid.NewGuid().ToString() : connectorId;

        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Установка режима чата (Normal/Shorts)
    /// </summary>
    public void SetChatMode(ChatMode mode)
    {
        _chatMode = mode;
        _logger?.LogInformation($"📱 Режим чата: {mode}");
    }

    /// <summary>
    /// Подключение к YouTube чату
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Если videoId не указан, пытаемся найти активный стрим
            if (string.IsNullOrEmpty(_currentVideoId) && !string.IsNullOrEmpty(_channelId))
            {
                _logger?.LogInformation($"🔍 Поиск активного стрима для канала: {_channelId}");

                var streams = await GetLiveStreamsAsync();
                if (streams.Count > 0)
                {
                    // Берем первый активный стрим
                    var stream = streams[0];
                    _currentVideoId = stream.VideoId;
                    _logger?.LogInformation($"✅ Найден стрим: {stream.Title} (ID: {_currentVideoId})");
                }
                else
                {
                    throw new Exception("Не найдено активных стримов для указанного канала");
                }
            }

            if (string.IsNullOrEmpty(_currentVideoId))
            {
                throw new Exception("Не указан Video ID или Channel ID");
            }

            _logger?.LogInformation($"▶ Подключение к YouTube чату видео: {_currentVideoId}");

            // Создаем и подключаем чат клиент
            _chatClient = new YoutubeChatClient(_httpClient, _htmlParser);

            // ✅ Передаём режим чата
            _chatClient.SetChatMode(_chatMode);

            _chatClient.OnMessageReceived += OnChatMessageReceived;
            _chatClient.OnLog += OnChatLog;
            _chatClient.OnStatusChanged += OnChatStatusChanged;

            var success = await _chatClient.ConnectAsync(_currentVideoId);

            if (success)
            {
                Status.IsConnected = true;
                Status.ErrorMessage = null;
                Status.LastMessageReceived = DateTime.UtcNow;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogInformation($"✅ Подключен к YouTube чату видео: {_currentVideoId}");
            }
            else
            {
                throw new Exception("Не удалось подключиться к YouTube чату");
            }
        }
        catch (Exception ex)
        {
            Status.IsConnected = false;
            Status.ErrorMessage = ex.Message;
            StatusChanged?.Invoke(this, Status);

            _logger?.LogError(ex, $"❌ Ошибка подключения к YouTube: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Отключение от YouTube чата
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cts?.Cancel();

            if (_chatClient != null)
            {
                _chatClient.OnMessageReceived -= OnChatMessageReceived;
                _chatClient.OnLog -= OnChatLog;
                _chatClient.OnStatusChanged -= OnChatStatusChanged;
                _chatClient.Disconnect();
                _chatClient = null;
            }

            Status.IsConnected = false;
            StatusChanged?.Invoke(this, Status);

            _logger?.LogInformation($"⏹ Отключен от YouTube чата видео: {_currentVideoId}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"❌ Ошибка отключения от YouTube: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Отправка сообщения в чат (не поддерживается YouTube API)
    /// </summary>
    public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        // YouTube не поддерживает отправку сообщений через API для сторонних приложений
        throw new NotSupportedException("Отправка сообщений в YouTube чат не поддерживается через API");
    }

    /// <summary>
    /// Проверка валидности настроек
    /// </summary>
    public Task<bool> ValidateSettingsAsync()
    {
        var isValid = !string.IsNullOrEmpty(_currentVideoId) || !string.IsNullOrEmpty(_channelId);
        return Task.FromResult(isValid);
    }

    /// <summary>
    /// Получение списка активных стримов
    /// </summary>
    public async Task<List<YouTubeStreamInfo>> GetLiveStreamsAsync()
    {
        var streams = new List<YouTubeStreamInfo>();

        try
        {
            if (!string.IsNullOrEmpty(_channelId))
            {
                // Сначала пробуем через API (если есть ключ)
                if (!string.IsNullOrEmpty(_apiKey) && _apiKey != "ВАШ_API_КЛЮЧ")
                {
                    try
                    {
                        _liveService = new YouTubeLiveService(_apiKey);
                        _liveService.OnLog += (s, msg) => _logger?.LogInformation(msg);

                        streams = await _liveService.GetLiveStreamsViaApiAsync(_channelId);
                        if (streams.Count > 0)
                        {
                            return streams;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"⚠️ Ошибка API: {ex.Message}, пробуем парсинг...");
                    }
                }

                // Если API не сработал или нет ключа, пробуем парсинг
                _liveService = new YouTubeLiveService(_apiKey ?? string.Empty);
                _liveService.OnLog += (s, msg) => _logger?.LogInformation(msg);

                streams = await _liveService.GetLiveStreamsViaHtmlAsync(_channelId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Ошибка получения списка стримов");
        }

        return streams;
    }

    /// <summary>
    /// Установка Video ID для подключения
    /// </summary>
    public void SetVideoId(string videoId)
    {
        _currentVideoId = videoId;
        _logger?.LogInformation($"📺 Установлен Video ID: {videoId}");
    }

    /// <summary>
    /// Установка Channel ID для поиска стримов
    /// </summary>
    public void SetChannelId(string channelId)
    {
        _channelId = channelId;
        _logger?.LogInformation($"📺 Установлен Channel ID: {channelId}");
    }

    /// <summary>
    /// Установка API ключа
    /// </summary>
    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        _logger?.LogInformation($"🔑 Установлен API ключ");
    }

    // =====================================================
    // ОБРАБОТЧИКИ СОБЫТИЙ
    // =====================================================

    private void OnChatMessageReceived(object? sender, ChatMessage message)
    {
        if (message == null) return;

        Debug.WriteLine($"[YouTubeConnector {_connectorId}] Получено сообщение: {message.Author}: {message.Text}");

        var incomingMessage = new IncomingChatMessage
        {
            Platform = ChannelType.YouTube,
            ChannelId = message.ChannelId ?? _channelId,
            UserId = message.AuthorId,
            UserName = message.Author,
            Text = message.Text,
            Timestamp = message.Timestamp.ToUniversalTime(),
            VideoId = message.VideoId,
            ConnectorId = Id
        };

        Status.LastMessageReceived = DateTime.UtcNow;
        MessageReceived?.Invoke(this, incomingMessage);
    }

    private void OnChatLog(object? sender, string message)
    {
        _logger?.LogInformation($"[YouTube] {message}");
    }

    private void OnChatStatusChanged(object? sender, string status)
    {
        _logger?.LogInformation($"[YouTube] Статус изменён: {status}");

        if (status == "error")
        {
            Status.IsConnected = false;
            Status.ErrorMessage = "Ошибка чата";
            StatusChanged?.Invoke(this, Status);
        }
        else if (status == "connected")
        {
            Status.IsConnected = true;
            Status.ErrorMessage = null;
            StatusChanged?.Invoke(this, Status);
        }
        else if (status == "disconnected")
        {
            Status.IsConnected = false;
            StatusChanged?.Invoke(this, Status);
        }
    }

    // =====================================================
    // IDisposable
    // =====================================================

    public void Dispose()
    {
        if (_disposed) return;

        _cts?.Cancel();
        _cts?.Dispose();

        if (_chatClient != null)
        {
            _chatClient.OnMessageReceived -= OnChatMessageReceived;
            _chatClient.OnLog -= OnChatLog;
            _chatClient.OnStatusChanged -= OnChatStatusChanged;
            _chatClient.Disconnect();
            _chatClient = null;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}