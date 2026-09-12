using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.ChatEngine.Platforms.YouTube;

public class YoutubeChatClient
{
    private bool _skipNextActionsResponse = false;
    private readonly HttpClient _httpClient;
    private readonly YoutubeHtmlParser _htmlParser;

    private string _innertubeApiKey = string.Empty;
    private string _clientVersion = "2.20240701.00.00";
    private string _videoId = string.Empty;

    // Токен, которым сейчас опрашиваем
    private string _continuationToken = string.Empty;

    // Флаг: мы уже переключились на All Messages?
    private bool _switchedToAllMessages = false;

    private bool _isRunning = false;
    private CancellationTokenSource? _cancellationTokenSource;
    private int _pollCount = 0;
    private int _totalMessagesReceived = 0;

    private readonly ConcurrentDictionary<string, DateTime> _processedMessageCache = new();

    public static Func<string, bool>? CheckEmojiExists { get; set; }
    public static Action<string, string>? RegisterEmojiInCache { get; set; }

    public event EventHandler<ChatMessage>? OnMessageReceived;
    public event EventHandler<string>? OnLog;
    public event EventHandler<string>? OnStatusChanged;

    public string VideoId => _videoId;

    public YoutubeChatClient(HttpClient httpClient, YoutubeHtmlParser htmlParser)
    {
        _httpClient = httpClient;
        _htmlParser = htmlParser;
    }

    public static void RegisterDelegates(Func<string, bool> checkExists, Action<string, string> register)
    {
        CheckEmojiExists = checkExists;
        RegisterEmojiInCache = register;
    }

    public void SetChatMode(ChatMode mode)
    {
        Log($"📱 Режим чата: {mode}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ПОДКЛЮЧЕНИЕ
    // ═══════════════════════════════════════════════════════════════════════

    public async Task ConnectAsync(string videoId, CancellationToken cancellationToken = default)
    {
        _videoId = videoId;
        Log("═══════════════════════════════════════════════");
        Log($"🔄 ПОДКЛЮЧЕНИЕ к YouTube чату: {videoId}");
        Log("═══════════════════════════════════════════════");

        try
        {
            // ШАГ 1. Загружаем HTML страницы watch
            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            Log($"🌐 Загружаем URL: {videoUrl}");

            var html = await _httpClient.GetStringAsync(videoUrl, cancellationToken);
            Log($"📄 HTML загружен ({html.Length} символов)");

            // ШАГ 2. Извлекаем apiKey, clientVersion и СТАРТОВЫЙ токен
            var (apiKey, clientVersion, startToken) = ParseOptionsFromLivePage(html);

            _innertubeApiKey = apiKey ?? string.Empty;
            _clientVersion = clientVersion ?? "2.20240701.00.00";
            _continuationToken = startToken ?? string.Empty;
            _switchedToAllMessages = false;
            _skipNextActionsResponse = false;

            Log("─── РЕЗУЛЬТАТ ПАРСИНГА ───────────────────────");
            Log($"🔑 API Key: {(_innertubeApiKey.Length > 0 ? $"OK ({_innertubeApiKey.Length})" : "❌ NULL")}");
            Log($"🔑 ClientVersion: {_clientVersion}");
            Log($"📡 Стартовый токен: {(_continuationToken.Length > 0 ? $"OK ({_continuationToken.Length})" : "❌ NULL")}");
            Log($"📡 Preview: {PreviewToken(_continuationToken)}");
            Log("─────────────────────────────────────────────");

            if (string.IsNullOrEmpty(_innertubeApiKey) || string.IsNullOrEmpty(_continuationToken))
            {
                Log("❌ Не удалось получить API key или стартовый токен");
                OnStatusChanged?.Invoke(this, "error");
                return;
            }

            _pollCount = 0;
            _totalMessagesReceived = 0;
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnStatusChanged?.Invoke(this, "connected");
            Log("✅ Подключен к YouTube чату");
            Log("🔄 Запускаем цикл опроса...");

            _ = Task.Run(() => PollChatLoop(_cancellationTokenSource.Token), cancellationToken);
        }
        catch (Exception ex)
        {
            Log($"❌ Ошибка подключения: {ex.Message}");
            Log($"❌ StackTrace: {ex.StackTrace}");
            OnStatusChanged?.Invoke(this, "error");
        }
    }

    /// <summary>
    /// Парсит apiKey, clientVersion и стартовый continuation из HTML.
    /// Стартовый токен — первое вхождение "continuation":"..." в HTML.
    /// </summary>
    private (string? apiKey, string? clientVersion, string? continuation)
        ParseOptionsFromLivePage(string raw)
    {
        Log("─── ПАРСИНГ HTML ────────────────────────────");

        var keyMatch = Regex.Match(raw, "\"INNERTUBE_API_KEY\":\\s*\"([^\"]*)\"");
        string? apiKey = keyMatch.Success ? keyMatch.Groups[1].Value : null;
        Log($"🔑 INNERTUBE_API_KEY: {(apiKey != null ? $"найден ({apiKey.Length})" : "НЕ НАЙДЕН")}");

        var verMatch = Regex.Match(raw, "\"INNERTUBE_CONTEXT_CLIENT_VERSION\":\\s*\"([^\"]*)\"");
        string? clientVersion = verMatch.Success ? verMatch.Groups[1].Value : null;
        Log($"🔑 CLIENT_VERSION: {clientVersion ?? "НЕ НАЙДЕН"}");

        // Стартовый токен — первое вхождение "continuation":"..." в HTML
        var contMatch = Regex.Match(raw, "\"continuation\":\\s*\"([^\"]*)\"");
        string? continuation = contMatch.Success ? contMatch.Groups[1].Value : null;
        Log($"📡 Стартовый continuation: {(continuation != null ? $"{continuation.Length} симв." : "НЕ НАЙДЕН")}");

        return (apiKey, clientVersion, continuation);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ЦИКЛ ОПРОСА
    // ═══════════════════════════════════════════════════════════════════════

    private async Task PollChatLoop(CancellationToken cancellationToken)
    {
        Log("🔄 Цикл опроса ЗАПУЩЕН");

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollChatAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка в цикле: {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }

        Log("⏹ Цикл опроса ОСТАНОВЛЕН");
    }

    private async Task PollChatAsync(CancellationToken cancellationToken)
    {
        _pollCount++;
        int currentPoll = _pollCount;

        var requestUrl = $"https://www.youtube.com/youtubei/v1/live_chat/get_live_chat?key={_innertubeApiKey}&prettyPrint=true";

        var requestBody = new
        {
            context = new
            {
                client = new
                {
                    clientVersion = _clientVersion,
                    clientName = "WEB"
                }
            },
            continuation = _continuationToken
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        //Log($"📤 [Poll #{currentPoll}] Запрос (cont={PreviewToken(_continuationToken)})");

        var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            Log($"⚠️ [Poll #{currentPoll}] Ошибка API: {response.StatusCode} - {err}");

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Log($"🔄 [Poll #{currentPoll}] Токен протух, сбрасываем — перезагрузим HTML");
                _continuationToken = string.Empty;
                _switchedToAllMessages = false;
            }
            return;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        //Log($"📥 [Poll #{currentPoll}] Ответ ({responseJson.Length} символов)");

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            Log($"❌ [Poll #{currentPoll}] YouTube error: {error.GetProperty("message").GetString()}");
            return;
        }

        if (!root.TryGetProperty("continuationContents", out var contContents))
        {
            Log($"⚠️ [Poll #{currentPoll}] Нет continuationContents");
            return;
        }

        var lcc = contContents.GetProperty("liveChatContinuation");

        // ═══════════════════════════════════════════════════════════════════
        // ПЕРВЫЙ ВЫЗОВ — переключаемся на All Messages
        // ═══════════════════════════════════════════════════════════════════
        if (!_switchedToAllMessages)
        {
            Log($"🔄 [Poll #{currentPoll}] Первый вызов — ищем токен All Messages в subMenuItems");

            string? allMessagesToken = ExtractAllMessagesTokenFromSubMenu(lcc);
            if (!string.IsNullOrEmpty(allMessagesToken))
            {
                _continuationToken = allMessagesToken;
                _switchedToAllMessages = true;
                _skipNextActionsResponse = true;
                Log($"✅ [Poll #{currentPoll}] Переключились на All Messages");
                Log($"🔄 [Poll #{currentPoll}] Следующий ответ пропустим (история)");

                // ✅ ВАЖНО: выходим из метода, НЕ обновляя continuation
                // Иначе перезапишем All Messages токен на Top Chat
                await Task.Delay(600, cancellationToken);
                return;
            }
            else
            {
                _switchedToAllMessages = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ОБРАБОТКА СООБЩЕНИЙ
        // ═══════════════════════════════════════════════════════════════════
        if (lcc.TryGetProperty("actions", out var actions))
        {
            var actionCount = actions.GetArrayLength();

            // ✅ ПРОПУСКАЕМ actions первого ответа All Messages — не парсим, не начисляем карму
            if (_skipNextActionsResponse)
            {
                _skipNextActionsResponse = false;
                Log($"📜 [Poll #{currentPoll}] Пропускаем {actionCount} исторических сообщений (первый All Messages)");
                // НЕ обрабатываем actions, идём дальше к обновлению continuation
            }
            else
            {
                // Обычная обработка — новые сообщения
                var messageCount = 0;

                foreach (var action in actions.EnumerateArray())
                {
                    if (action.TryGetProperty("addChatItemAction", out var acia))
                    {
                        var item = acia.GetProperty("item");
                        ChatMessage? message = null;
                        string rendererType = "unknown";

                        if (item.TryGetProperty("liveChatTextMessageRenderer", out var textRenderer))
                        {
                            rendererType = "text";
                            message = ParseMessage(textRenderer);
                        }
                        else if (item.TryGetProperty("liveChatShortsMessageRenderer", out var shortsRenderer))
                        {
                            rendererType = "shorts";
                            message = ParseShortsMessage(shortsRenderer);
                        }
                        else if (item.TryGetProperty("liveChatPaidMessageRenderer", out var paidRenderer))
                        {
                            rendererType = "paid";
                            message = ParsePaidMessage(paidRenderer);
                        }

                        if (message != null)
                        {
                            var key = $"{message.AuthorId}:{message.Text}:{message.Timestamp.Ticks}";
                            if (_processedMessageCache.TryAdd(key, DateTime.UtcNow))
                            {
                                message.VideoId = _videoId;
                                _totalMessagesReceived++;
                                Log($"💬 [{rendererType}] {message.Author}: {Truncate(message.Text, 80)}");
                                OnMessageReceived?.Invoke(this, message);
                                messageCount++;
                            }
                        }
                    }
                }

                if (messageCount > 0)
                    Log($"✅ [Poll #{currentPoll}] Получено {messageCount} новых (всего: {_totalMessagesReceived})");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ОБНОВЛЕНИЕ CONTINUATION
        // ═══════════════════════════════════════════════════════════════════
        if (lcc.TryGetProperty("continuations", out var continuations))
        {
            var arr = continuations.EnumerateArray().ToList();
            if (arr.Count == 0)
            {
                //Log($"⚠️ [Poll #{currentPoll}] Пустой массив continuations");
                return;
            }

            //Log($"🔄 [Poll #{currentPoll}] Continuations: {arr.Count}");
            for (int i = 0; i < arr.Count; i++)
            {
                string type = "unknown";
                if (arr[i].TryGetProperty("invalidationContinuationData", out _)) type = "invalidation";
                else if (arr[i].TryGetProperty("timedContinuationData", out _)) type = "timed";
                else if (arr[i].TryGetProperty("reloadContinuationData", out _)) type = "reload";
                //Log($"   [{i}] {type}");
            }

            // Приоритет: invalidation > timed > reload
            JsonElement? selected = null;

            selected = arr.FirstOrDefault(c => c.TryGetProperty("invalidationContinuationData", out _));
            if (selected == null || selected.Value.ValueKind == JsonValueKind.Undefined)
                selected = arr.FirstOrDefault(c => c.TryGetProperty("timedContinuationData", out _));
            if (selected == null || selected.Value.ValueKind == JsonValueKind.Undefined)
                selected = arr.FirstOrDefault(c => c.TryGetProperty("reloadContinuationData", out _));
            selected ??= arr[0];

            var contElement = selected.Value;
            string oldToken = _continuationToken;
            string newToken = string.Empty;
            int recommendedTimeout = 600;

            if (contElement.TryGetProperty("invalidationContinuationData", out var inval) &&
                inval.TryGetProperty("continuation", out var ic))
            {
                newToken = ic.GetString() ?? _continuationToken;
                recommendedTimeout = inval.TryGetProperty("timeoutMs", out var tm) ? tm.GetInt32() : 600;
            }
            else if (contElement.TryGetProperty("timedContinuationData", out var timed) &&
                     timed.TryGetProperty("continuation", out var tc))
            {
                newToken = tc.GetString() ?? _continuationToken;
                recommendedTimeout = timed.TryGetProperty("timeoutMs", out var tm) ? tm.GetInt32() : 600;
            }
            else if (contElement.TryGetProperty("reloadContinuationData", out var reload) &&
                     reload.TryGetProperty("continuation", out var rc))
            {
                newToken = rc.GetString() ?? _continuationToken;
            }

            // ✅ ОБНОВЛЕНИЕ ТОКЕНА — ОДИН РАЗ
            if (!string.IsNullOrEmpty(newToken))
            {
                _continuationToken = newToken;
            }

            // ✅ ПАУЗА — МИНИМУМ из recommendedTimeout и 600
            int actualTimeout = Math.Min(recommendedTimeout, 600);
            //Log($"⏱ [Poll #{currentPoll}] Пауза {actualTimeout}мс (YouTube рекомендует {recommendedTimeout}мс)");
            await Task.Delay(actualTimeout, cancellationToken);

        }
    }

    /// <summary>
    /// Ищет токен All Messages в subMenuItems первого ответа.
    /// У "Чат" / "Live chat" обычно selected: true.
    /// </summary>
    private string? ExtractAllMessagesTokenFromSubMenu(JsonElement lcc)
    {
        try
        {
            if (!lcc.TryGetProperty("header", out var header)) return null;
            if (!header.TryGetProperty("liveChatHeaderRenderer", out var headerR)) return null;
            if (!headerR.TryGetProperty("viewSelector", out var viewSelector)) return null;
            if (!viewSelector.TryGetProperty("sortFilterSubMenuRenderer", out var sortFilter)) return null;
            if (!sortFilter.TryGetProperty("subMenuItems", out var items)) return null;

            var itemsList = items.EnumerateArray().ToList();
            Log($"   📋 subMenuItems: {itemsList.Count}");

            string? allMessagesToken = null;
            string? topChatToken = null;

            foreach (var item in itemsList)
            {
                string? title = null;
                if (item.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                    title = titleEl.GetString();

                bool isSelected = item.TryGetProperty("selected", out var sel) && sel.GetBoolean();

                // Извлекаем токен
                string? token = null;
                if (item.TryGetProperty("continuation", out var contObj))
                {
                    if (contObj.TryGetProperty("reloadContinuationData", out var rd) &&
                        rd.TryGetProperty("continuation", out var tokenEl))
                        token = tokenEl.GetString();
                    else if (contObj.TryGetProperty("invalidationContinuationData", out var inv) &&
                             inv.TryGetProperty("continuation", out var invEl))
                        token = invEl.GetString();
                }

                Log($"   📋 '{title ?? "(null)"}' selected={isSelected} token={(token?.Length ?? 0)} симв.");

                if (string.IsNullOrEmpty(token)) continue;

                // Классифицируем по названию
                if (!string.IsNullOrEmpty(title) &&
                    (title.Equals("Чат", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("Live chat", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("All messages", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("Все сообщения", StringComparison.OrdinalIgnoreCase)))
                {
                    allMessagesToken = token;
                    Log($"   ✅ All Messages по названию '{title}'");
                }
                else if (!string.IsNullOrEmpty(title) &&
                         (title.Contains("Top", StringComparison.OrdinalIgnoreCase) ||
                          title.Contains("Интересные", StringComparison.OrdinalIgnoreCase)))
                {
                    topChatToken = token;
                    Log($"   📌 Top Chat '{title}'");
                }
                else
                {
                    // Неизвестное название — используем как fallback для All Messages
                    allMessagesToken ??= token;
                    Log($"   ❓ Неизвестное название '{title}' — fallback");
                }
            }

            if (!string.IsNullOrEmpty(allMessagesToken))
            {
                Log($"   ✅ Выбран токен All Messages: {allMessagesToken.Length} симв.");
                return allMessagesToken;
            }

            Log($"   ⚠️ All Messages не найден, используем Top Chat");
            return topChatToken;
        }
        catch (Exception ex)
        {
            Log($"   ⚠️ Ошибка: {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ПАРСИНГ СООБЩЕНИЙ
    // ═══════════════════════════════════════════════════════════════════════

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
                message.AuthorId = authorId.GetString() ?? string.Empty;

            if (renderer.TryGetProperty("authorName", out var authorName) &&
                authorName.TryGetProperty("simpleText", out var nameText))
                message.Author = nameText.GetString() ?? "Unknown";
            else if (!string.IsNullOrEmpty(message.AuthorId))
                message.Author = message.AuthorId;

            if (renderer.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("runs", out var runs))
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
                        try
                        {
                            var thumbnails = emoji.GetProperty("image").GetProperty("thumbnails");
                            var url = thumbnails[0].GetProperty("url").GetString();
                            string? code = null;

                            if (emoji.TryGetProperty("shortcuts", out var shortcuts))
                            {
                                var first = shortcuts.EnumerateArray().FirstOrDefault();
                                if (first.ValueKind != JsonValueKind.Null)
                                {
                                    var sc = first.GetString();
                                    if (!string.IsNullOrEmpty(sc)) code = sc.Trim(':');
                                }
                            }

                            if (string.IsNullOrEmpty(code))
                            {
                                var accessibility = emoji.GetProperty("image").GetProperty("accessibility");
                                var label = accessibility.GetProperty("accessibilityData").GetProperty("label").GetString();
                                if (!string.IsNullOrEmpty(label))
                                    code = label.ToLower().Replace(" ", "_").Replace(":", "");
                            }

                            if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(url))
                            {
                                _ = Task.Run(async () =>
                                {
                                    try { await DownloadEmojiAsync(url!, code!); }
                                    catch (Exception ex) { Log($"❌ Emoji {code}: {ex.Message}"); }
                                });
                                textBuilder.Append($":{code}:");
                            }
                        }
                        catch (Exception ex) { Log($"⚠️ Emoji parse: {ex.Message}"); }
                    }
                }
                message.Text = textBuilder.ToString();
            }

            if (renderer.TryGetProperty("channelId", out var channelId))
                message.ChannelId = channelId.GetString() ?? string.Empty;

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
                message.AuthorId = authorId.GetString() ?? string.Empty;

            if (renderer.TryGetProperty("authorName", out var authorName) &&
                authorName.TryGetProperty("simpleText", out var nameText))
                message.Author = nameText.GetString() ?? "Unknown";
            else if (!string.IsNullOrEmpty(message.AuthorId))
                message.Author = message.AuthorId;

            if (renderer.TryGetProperty("message", out var messageElement))
            {
                if (messageElement.TryGetProperty("simpleText", out var simpleText))
                {
                    message.Text = simpleText.GetString() ?? string.Empty;
                }
                else if (messageElement.TryGetProperty("runs", out var runs))
                {
                    var sb = new StringBuilder();
                    foreach (var run in runs.EnumerateArray())
                    {
                        if (run.TryGetProperty("text", out var text))
                            sb.Append(text.GetString());
                    }
                    message.Text = sb.ToString();
                }
            }

            if (renderer.TryGetProperty("channelId", out var channelId))
                message.ChannelId = channelId.GetString() ?? string.Empty;

            return message;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Ошибка парсинга Shorts: {ex.Message}");
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
                message.AuthorId = authorId.GetString() ?? string.Empty;

            if (renderer.TryGetProperty("authorName", out var authorName) &&
                authorName.TryGetProperty("simpleText", out var nameText))
                message.Author = nameText.GetString() ?? "Unknown";

            if (renderer.TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("runs", out var runs))
            {
                var sb = new StringBuilder();
                foreach (var run in runs.EnumerateArray())
                {
                    if (run.TryGetProperty("text", out var text))
                        sb.Append(text.GetString());
                }
                message.Text = sb.ToString();
            }

            if (renderer.TryGetProperty("purchaseAmountText", out var amount) &&
                amount.TryGetProperty("simpleText", out var amtText))
            {
                var amountText = amtText.GetString() ?? "";
                message.Text = $"💎 {amountText} - {message.Text}";
            }

            return message;
        }
        catch (Exception ex)
        {
            Log($"⚠️ Ошибка парсинга Paid: {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ОТКЛЮЧЕНИЕ И УТИЛИТЫ
    // ═══════════════════════════════════════════════════════════════════════

    public void Disconnect()
    {
        Log("⏹ Отключение...");
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        OnStatusChanged?.Invoke(this, "disconnected");
        Log("⏹ Отключен");
    }

    private void Log(string message)
    {
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    private static string PreviewToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "(null)";
        return token.Length <= 30 ? token : token.Substring(0, 30) + "...";
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        return text.Length <= max ? text : text.Substring(0, max) + "...";
    }

    private async Task<string?> DownloadEmojiAsync(string url, string code)
    {
        try
        {
            var localPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Emojis", "YouTube", "Images",
                $"{code}.png");

            var emojiCode = $":{code}:";

            if (CheckEmojiExists?.Invoke(emojiCode) == true)
                return localPath;

            if (File.Exists(localPath))
            {
                RegisterEmojiInCache?.Invoke(emojiCode, localPath);
                return localPath;
            }

            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var bytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, bytes);
            RegisterEmojiInCache?.Invoke(emojiCode, localPath);
            return localPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Emoji] ❌ {code}: {ex.Message}");
            return null;
        }
    }
}