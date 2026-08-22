using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.ChatEngine.Models;

namespace SmithForge.ChatEngine.Services;

public class YoutubeChatClient
{
    private readonly HttpClient _httpClient;
    private readonly YoutubeHtmlParser _htmlParser;
    private string _innertubeApiKey = string.Empty;
    private string _continuationToken = string.Empty;
    private string _channelName = string.Empty;
    private string _videoId = string.Empty;
    private bool _isRunning = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _connectionTime;
    private ChatMode _chatMode = ChatMode.Normal;
    private bool _isFirstResponse = true;

    // Кэш для дедупликации сообщений
    private readonly ConcurrentDictionary<string, DateTime> _processedMessageCache = new();
    private const int CacheTtlSeconds = 60;

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<string>? OnLog;
    public event EventHandler<string>? OnStatusChanged;

    public string VideoId => _videoId;

    public YoutubeChatClient(HttpClient httpClient, YoutubeHtmlParser htmlParser)
    {
        _httpClient = httpClient;
        _htmlParser = htmlParser;
    }

    public void SetChatMode(ChatMode mode)
    {
        _chatMode = mode;
        Log($"📱 Режим чата: {mode}");
    }

    public async Task<bool> ConnectAsync(string videoId)
    {
        try
        {
            _videoId = videoId;
            Log($"🔄 Подключение к чату видео: {videoId}");

            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            var html = await _httpClient.GetStringAsync(videoUrl);
            Log("📄 HTML загружен");

            _innertubeApiKey = _htmlParser.ExtractApiKeyDirectly(html);
            Log($"🔑 API ключ получен");

            _continuationToken = _htmlParser.ExtractContinuationSmart(html, Log);
            Log($"✅ Continuation получен");

            _channelName = _htmlParser.ExtractChannelNameDirectly(html);
            Log($"📺 Канал: {_channelName}");

            _isFirstResponse = true;
            _connectionTime = DateTime.UtcNow;
            Log($"⏱ Время подключения: {_connectionTime:HH:mm:ss} UTC");

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => PollChatLoop(_cancellationTokenSource.Token));

            OnStatusChanged?.Invoke(this, "connected");
            Log("✅ Подключен к чату YouTube");
            return true;
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка подключения: {ex.Message}");
            OnStatusChanged?.Invoke(this, "error");
            return false;
        }
    }

    private async Task PollChatLoop(CancellationToken cancellationToken)
    {
        Log("🔄 Запущен цикл опроса чата");

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollChatAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка в цикле опроса: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }

        Log("⏹ Цикл опроса остановлен");
    }

    private async Task PollChatAsync(CancellationToken cancellationToken)
    {
        var requestUrl = $"https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key={_innertubeApiKey}";

        var requestBody = new
        {
            context = new
            {
                client = new
                {
                    hl = "en-GB",
                    gl = "RU",
                    clientName = "WEB",
                    clientVersion = "2.20200814.00.00",
                    osName = "Windows",
                    osVersion = "10.0"
                }
            },
            continuation = _continuationToken
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"⚠️ Ошибка API: {response.StatusCode} - {errorContent}");
            return;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var message = error.GetProperty("message").GetString();
            Log($"❌ Ошибка от YouTube: {message}");
            return;
        }

        if (root.TryGetProperty("continuationContents", out var continuationContents))
        {
            var liveChatContinuation = continuationContents.GetProperty("liveChatContinuation");

            if (liveChatContinuation.TryGetProperty("actions", out var actions))
            {
                var messageCount = 0;

                if (_isFirstResponse)
                {
                    _isFirstResponse = false;

                    var historyCount = 0;
                    foreach (var action in actions.EnumerateArray())
                    {
                        if (action.TryGetProperty("addChatItemAction", out _))
                        {
                            historyCount++;
                        }
                    }

                    Log($"📜 ПЕРВЫЙ ОТВЕТ: пропускаем {historyCount} исторических сообщений");
                }
                else
                {
                    foreach (var action in actions.EnumerateArray())
                    {
                        if (action.TryGetProperty("addChatItemAction", out var addChatItemAction))
                        {
                            var item = addChatItemAction.GetProperty("item");
                            ChatMessage? message = null;

                            if (item.TryGetProperty("liveChatTextMessageRenderer", out var textRenderer))
                            {
                                message = ParseMessage(textRenderer);
                            }
                            else if (item.TryGetProperty("liveChatShortsMessageRenderer", out var shortsRenderer))
                            {
                                message = ParseShortsMessage(shortsRenderer);
                            }
                            else if (item.TryGetProperty("liveChatPaidMessageRenderer", out var paidRenderer))
                            {
                                message = ParsePaidMessage(paidRenderer);
                            }
                            else if (item.TryGetProperty("liveChatViewerEngagementMessageRenderer", out _))
                            {
                                continue;
                            }

                            if (message != null)
                            {
                                var dedupKey = $"{message.AuthorId}:{message.Text}:{message.Timestamp.Ticks}";
                                if (_processedMessageCache.TryAdd(dedupKey, DateTime.UtcNow))
                                {
                                    message.VideoId = _videoId;
                                    OnMessageReceived?.Invoke(this, message);
                                    messageCount++;
                                }
                            }
                        }
                    }

                    if (messageCount > 0)
                        Log($"💬 Получено {messageCount} новых сообщений");
                }
            }

            if (liveChatContinuation.TryGetProperty("continuations", out var continuations))
            {
                var firstContinuation = continuations.EnumerateArray().First();

                if (firstContinuation.TryGetProperty("reloadContinuationData", out var reloadData))
                {
                    _continuationToken = reloadData.GetProperty("continuation").GetString() ?? _continuationToken;
                    Log($"🔄 Обновлён continuation (reload)");
                }
                else if (firstContinuation.TryGetProperty("timedContinuationData", out var timedData))
                {
                    _continuationToken = timedData.GetProperty("continuation").GetString() ?? _continuationToken;
                    var timeoutMs = timedData.GetProperty("timeoutMs").GetInt32();
                    var delayMs = Math.Min(timeoutMs, 2000);
                    Log($"⏱ Задержка {delayMs}мс (из {timeoutMs}мс)");
                    await Task.Delay(delayMs, cancellationToken);
                }
                else if (firstContinuation.TryGetProperty("invalidationContinuationData", out var invalidationData))
                {
                    _continuationToken = invalidationData.GetProperty("continuation").GetString() ?? _continuationToken;
                    var timeoutMs = invalidationData.GetProperty("timeoutMs").GetInt32();
                    var delayMs = Math.Min(timeoutMs, 2000);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }
    }

    private ChatMessage? ParseMessage(JsonElement renderer)
    {
        try
        {
            var message = new ChatMessage
            {
                Platform = ChannelType.YouTube,
                Timestamp = DateTime.Now,
                VideoId = _videoId
            };

            if (renderer.TryGetProperty("authorExternalChannelId", out var authorId))
            {
                message.AuthorId = authorId.GetString() ?? string.Empty;
            }

            if (renderer.TryGetProperty("authorName", out var authorName))
            {
                message.Author = authorName.GetProperty("simpleText").GetString() ?? "Unknown";
            }
            else if (!string.IsNullOrEmpty(message.AuthorId))
            {
                message.Author = message.AuthorId;
            }

            if (renderer.TryGetProperty("message", out var messageElement))
            {
                var runs = messageElement.GetProperty("runs");
                var textBuilder = new StringBuilder();

                foreach (var run in runs.EnumerateArray())
                {
                    if (run.TryGetProperty("text", out var text))
                    {
                        textBuilder.Append(text.GetString());
                    }
                    else if (run.TryGetProperty("emoji", out var emoji))
                    {
                        var accessibility = emoji.GetProperty("image").GetProperty("accessibility");
                        var label = accessibility.GetProperty("accessibilityData").GetProperty("label").GetString();

                        // ✅ Преобразуем в формат :code: для EmojiService
                        if (!string.IsNullOrEmpty(label))
                        {
                            var code = label.ToLower()
                                .Replace(" ", "_")
                                .Replace("-", "_")
                                .Replace(":", "");
                            textBuilder.Append($":{code}:");
                        }
                    }
                }

                message.Text = textBuilder.ToString();
            }

            if (renderer.TryGetProperty("channelId", out var channelId))
            {
                message.ChannelId = channelId.GetString() ?? string.Empty;
            }

            return message;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Ошибка парсинга сообщения: {ex.Message}");
            return null;
        }
    }

    private ChatMessage? ParseShortsMessage(JsonElement renderer)
    {
        try
        {
            var message = new ChatMessage
            {
                Platform = ChannelType.YouTube,
                Timestamp = DateTime.Now,
                VideoId = _videoId
            };

            if (renderer.TryGetProperty("authorExternalChannelId", out var authorId))
            {
                message.AuthorId = authorId.GetString() ?? string.Empty;
            }

            if (renderer.TryGetProperty("authorName", out var authorName))
            {
                message.Author = authorName.GetProperty("simpleText").GetString() ?? "Unknown";
            }
            else if (!string.IsNullOrEmpty(message.AuthorId))
            {
                message.Author = message.AuthorId;
            }

            if (renderer.TryGetProperty("message", out var messageElement))
            {
                if (messageElement.TryGetProperty("simpleText", out var simpleText))
                {
                    message.Text = simpleText.GetString() ?? string.Empty;
                }
                else if (messageElement.TryGetProperty("runs", out var runs))
                {
                    var textBuilder = new StringBuilder();
                    foreach (var run in runs.EnumerateArray())
                    {
                        if (run.TryGetProperty("text", out var text))
                        {
                            textBuilder.Append(text.GetString());
                        }
                        else if (run.TryGetProperty("emoji", out var emoji))
                        {
                            var accessibility = emoji.GetProperty("image").GetProperty("accessibility");
                            var label = accessibility.GetProperty("accessibilityData").GetProperty("label").GetString();

                            if (!string.IsNullOrEmpty(label))
                            {
                                var code = label.ToLower()
                                    .Replace(" ", "_")
                                    .Replace("-", "_")
                                    .Replace(":", "");
                                textBuilder.Append($":{code}:");
                            }
                        }
                    }
                    message.Text = textBuilder.ToString();
                }
            }

            if (renderer.TryGetProperty("channelId", out var channelId))
            {
                message.ChannelId = channelId.GetString() ?? string.Empty;
            }

            return message;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Ошибка парсинга Shorts сообщения: {ex.Message}");
            return null;
        }
    }

    private ChatMessage? ParsePaidMessage(JsonElement renderer)
    {
        try
        {
            var message = new ChatMessage
            {
                Platform = ChannelType.YouTube,
                Timestamp = DateTime.Now,
                VideoId = _videoId
            };

            if (renderer.TryGetProperty("authorExternalChannelId", out var authorId))
            {
                message.AuthorId = authorId.GetString() ?? string.Empty;
            }

            if (renderer.TryGetProperty("authorName", out var authorName))
            {
                message.Author = authorName.GetProperty("simpleText").GetString() ?? "Unknown";
            }

            if (renderer.TryGetProperty("message", out var messageElement))
            {
                if (messageElement.TryGetProperty("runs", out var runs))
                {
                    var textBuilder = new StringBuilder();
                    foreach (var run in runs.EnumerateArray())
                    {
                        if (run.TryGetProperty("text", out var text))
                        {
                            textBuilder.Append(text.GetString());
                        }
                    }
                    message.Text = textBuilder.ToString();
                }
            }

            if (renderer.TryGetProperty("purchaseAmountText", out var amount))
            {
                var amountText = amount.GetProperty("simpleText").GetString() ?? "";
                message.Text = $"💎 {amountText} - {message.Text}";
            }

            return message;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Ошибка парсинга Paid сообщения: {ex.Message}");
            return null;
        }
    }

    public void Disconnect()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        OnStatusChanged?.Invoke(this, "disconnected");
        Log("⏹ Отключен от чата");
    }

    private void Log(string message)
    {
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}