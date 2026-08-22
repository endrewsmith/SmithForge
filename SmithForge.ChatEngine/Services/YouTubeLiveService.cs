using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Services;

public class YouTubeLiveService
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly YoutubeHtmlParser _htmlParser;
    private readonly Microsoft.Extensions.Logging.ILogger<YouTubeLiveService>? _logger;

    public event EventHandler<string>? OnLog;

    public YouTubeLiveService(
        string apiKey,
        Microsoft.Extensions.Logging.ILogger<YouTubeLiveService>? logger = null)
    {
        _apiKey = apiKey;
        _logger = logger;
        _httpClient = new HttpClient();
        _htmlParser = new YoutubeHtmlParser();
    }

    /// <summary>
    /// Получение LIVE стримов через Google API
    /// </summary>
    public async Task<List<YouTubeStreamInfo>> GetLiveStreamsViaApiAsync(string channelId)
    {
        try
        {
            Log($"🔍 Поиск LIVE стримов через API для канала: {channelId}");

            var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                ApiKey = _apiKey,
                ApplicationName = "SmithForge"
            });

            // Получаем имя канала
            var channelRequest = youtubeService.Channels.List("snippet");
            channelRequest.Id = channelId;
            var channelResponse = await channelRequest.ExecuteAsync();
            var channelName = channelResponse.Items?.FirstOrDefault()?.Snippet?.Title ?? "Неизвестный канал";
            Log($"📺 Канал: {channelName}");

            // Ищем LIVE стримы
            var searchRequest = youtubeService.Search.List("snippet");
            searchRequest.ChannelId = channelId;
            searchRequest.Type = "video";
            searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
            searchRequest.MaxResults = 10;

            var response = await searchRequest.ExecuteAsync();

            if (response.Items == null || response.Items.Count == 0)
            {
                Log("❌ Активных LIVE стримов не найдено (API)");
                return new List<YouTubeStreamInfo>();
            }

            Log($"✅ Найдено {response.Items.Count} LIVE стримов (через API)");

            var streams = response.Items
                .Where(item => item.Id?.VideoId != null)
                .Select(item => new YouTubeStreamInfo
                {
                    VideoId = item.Id.VideoId!,
                    Title = item.Snippet?.Title ?? "Без названия",
                    IsShorts = item.Snippet?.Title?.Contains("#shorts", StringComparison.OrdinalIgnoreCase) ?? false,
                    ChannelName = channelName,
                    IsLive = true,
                    StartTime = DateTime.UtcNow
                })
                .ToList();

            foreach (var stream in streams)
            {
                Log($"  - {stream.DisplayText} (ID: {stream.VideoId})");
            }

            return streams;
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка API: {ex.Message}");
            _logger?.LogError(ex, "Ошибка получения LIVE стримов через API");
            throw;
        }
    }

    /// <summary>
    /// Получение LIVE стримов через парсинг HTML (бесплатно, без использования API ключа)
    /// </summary>
    public async Task<List<YouTubeStreamInfo>> GetLiveStreamsViaHtmlAsync(string channelId)
    {
        try
        {
            Log($"🔍 Поиск LIVE стримов через парсинг HTML для канала: {channelId}");

            var url = $"https://www.youtube.com/channel/{channelId}";
            Log($"📡 Загружаем: {url}");

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Log($"❌ Не удалось загрузить страницу: {response.StatusCode}");
                return new List<YouTubeStreamInfo>();
            }

            var html = await response.Content.ReadAsStringAsync();
            Log($"📄 HTML загружен ({html.Length} символов)");

            var streams = _htmlParser.ParseLiveStreamsFromHtml(html, Log);

            if (streams.Count == 0)
            {
                Log("❌ Активных LIVE стримов не найдено");
                return new List<YouTubeStreamInfo>();
            }

            // Добавляем имя канала
            foreach (var stream in streams)
            {
                stream.ChannelName = await GetChannelNameFromHtmlAsync(channelId);
                stream.IsLive = true;
                stream.StartTime = DateTime.UtcNow;
            }

            Log($"✅ Найдено {streams.Count} LIVE стримов через HTML парсинг");

            foreach (var stream in streams)
            {
                Log($"  - {stream.DisplayText} (ID: {stream.VideoId})");
            }

            return streams;
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка HTML парсинга: {ex.Message}");
            _logger?.LogError(ex, "Ошибка получения LIVE стримов через HTML");
            throw;
        }
    }

    /// <summary>
    /// Получение имени канала из HTML
    /// </summary>
    private async Task<string> GetChannelNameFromHtmlAsync(string channelId)
    {
        try
        {
            var url = $"https://www.youtube.com/channel/{channelId}";
            var html = await _httpClient.GetStringAsync(url);
            return _htmlParser.ExtractChannelNameDirectly(html);
        }
        catch
        {
            return "Неизвестный канал";
        }
    }

    /// <summary>
    /// Получение списка стримов (автоматический выбор метода)
    /// </summary>
    public async Task<List<YouTubeStreamInfo>> GetLiveStreamsAsync(string channelId, bool preferApi = true)
    {
        if (preferApi && !string.IsNullOrEmpty(_apiKey) && _apiKey != "ВАШ_API_КЛЮЧ")
        {
            try
            {
                return await GetLiveStreamsViaApiAsync(channelId);
            }
            catch
            {
                Log("⚠️ API метод не сработал, пробуем HTML парсинг...");
                return await GetLiveStreamsViaHtmlAsync(channelId);
            }
        }

        return await GetLiveStreamsViaHtmlAsync(channelId);
    }

    private void Log(string message)
    {
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
        _logger?.LogInformation(message);
    }
}