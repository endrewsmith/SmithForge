using SmithForge.Main.Models;
using SmithForge.Main.Services.WebServer;
using SmithForge.Main.Services.WebServer.Handlers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services
{
    public class WebServerService : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly int _port;
        private readonly List<DisplayMessageViewModel> _messages = new();
        private readonly object _lockObject = new();

        private readonly Dictionary<string, string> _infoPageCache = new();
        private readonly SseClientManager _infoManager = new("Info");
        private readonly InfoStreamHandler _infoStreamHandler;
        private readonly object _infoPageCacheLock = new object();
        private readonly string _infoPagesDir;

        private static string HtmlRoot =>
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html");

        private static string OverlaysDir =>
            Path.Combine(HtmlRoot, "Overlays");

        private static string RanksHtmlDir =>
            Path.Combine(HtmlRoot, "Ranks", "html");

        private static string RanksCssDir =>
            Path.Combine(HtmlRoot, "Ranks", "css");

        // === SSE-клиенты основного чата /stream ===
        private readonly SseClientManager _streamManager = new("Stream");
        private readonly ChatStreamHandler _chatStreamHandler;

        // === Alerts Overlay (веб-оверлей алертов) ===
        private readonly SseClientManager _alertsManager = new("Alerts");

        // === Tech events stream (/tech/stream) ===
        private readonly SseClientManager _techManager = new("Tech");

        public static WebServerService? Instance { get; private set; }
        /// <summary>
        /// Событие: техническое событие отправлено (для локального WPF-окна).
        /// </summary>
        public event EventHandler<SmithForge.Features.TechOverlay.TechEvent>? TechnicalEventSent;

        // ✅ КЕШ ИЗОБРАЖЕНИЙ
        private readonly Dictionary<string, byte[]> _imageCache = new();
        private readonly object _imageCacheLock = new object();
        private const int MAX_CACHE_IMAGES = 100;

        public WebServerService(int port = 10881)
        {
            _port = port;
            Instance = this;


            // ============================================================
            // ИНФОРМАЦИОННЫЙ ЧАТ - ПУТИ
            // ============================================================

            _infoPagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Html", "InfoPages");

            Debug.WriteLine($"[WebServer] InfoPages: {_infoPagesDir}");

            // Создаем дефолтные страницы если их нет
            EnsureInfoPagesExist();

            _chatStreamHandler = new ChatStreamHandler(_streamManager);
            _infoStreamHandler = new InfoStreamHandler(_infoManager);
        }

        private void EnsureInfoPagesExist()
        {

            Directory.CreateDirectory(_infoPagesDir);
            Debug.WriteLine($"[WebServer] InfoPages: {_infoPagesDir}");
        }

        /// <summary>
        /// Отправить только обновление аватарки (без создания сообщения)
        /// </summary>
        public void SendAvatarUpdateOnly(Chater chater)
        {
            if (chater == null) return;

            try
            {
                string avatarPath = chater.FullAvatarPath;
                if (string.IsNullOrEmpty(avatarPath) || !File.Exists(avatarPath))
                {
                    Debug.WriteLine($"[WebServer] ⚠️ Аватарка не найдена для {chater.EffectiveName}");
                    return;
                }

                Debug.WriteLine($"[WebServer] Отправка обновления аватарки для {chater.EffectiveName}");

                // Получаем CSS асинхронно или из кэша (если переписали по прошлым советам)
                // Так как метод синхронный, используем .GetAwaiter().GetResult() для Task-метода
                var rankCss = _chatStreamHandler.GetRankCssContent(chater.Rank).GetAwaiter().GetResult();

                var updateData = new
                {
                    type = "avatar_update", // Фронтенд поймет, что это НЕ сообщение
                    userId = chater.Id,
                    displayName = chater.EffectiveName,
                    messageText = "", // Текста нет
                    avatarPath = avatarPath,
                    userRank = chater.Rank,
                    rankDisplay = _chatStreamHandler.GetRankDisplay(chater.Rank),
                    rankClass = _chatStreamHandler.GetRankClass(chater.Rank),
                    rankCss = rankCss,
                    rankTemplate = _chatStreamHandler.GetRankTemplate(chater.Rank),
                    karmaKey = chater.KarmaKeyDisplay,
                    karma = chater.KarmaDisplay,
                    messageCount = chater.MessageCount,
                    timestamp = DateTime.Now.ToString("HH:mm:ss")
                };

                var json = System.Text.Json.JsonSerializer.Serialize(updateData);
                var data = $"data: {json}\n\n";

                // ✅ Вместо MessageAdded вызываем специализированное событие со строкой данных
                BroadcastRawSse(data);

                Debug.WriteLine($"[WebServer] ✅ Отправлено обновление аватарки для {chater.EffectiveName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка отправки обновления аватарки: {ex.Message}");
            }
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _isRunning = true;
                _cts = new CancellationTokenSource();

                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{_port}/");

                // Запускаем обработку запросов
                await Task.Run(() => ProcessRequestsAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка запуска: {ex.Message}");
                Debug.WriteLine($"[WebServer] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            Debug.WriteLine("[WebServer] Остановлен");
        }

        private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
        {
            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    Debug.WriteLine("[WebServer] HttpListener остановлен");
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebServer] Ошибка: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                string path = request.Url?.AbsolutePath ?? "/";

                Debug.WriteLine($"[WebServer] Запрос: {request.HttpMethod} {path}");

                // Добавляем CORS заголовки
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                // ============================================================
                // ОСНОВНОЙ ЧАТ
                // ============================================================

                if (path == "/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /stream запроса!");
                    await HandleStreamRequestAsync(context);
                    return;
                }

                if (path == "/api/messages")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /api/messages запроса!");
                    await HandleApiRequestAsync(context);
                    return;
                }

                // ============================================================
                // ИНФОРМАЦИОННЫЙ ЧАТ (/info)
                // ============================================================

                if (path == "/info" || path == "/info/")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /info запроса!");
                    // ✅ Показываем index.html (сам чат), а не help
                    await ServeInfoPageAsync(context, "index.html");
                    return;
                }

                if (path == "/info/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /info/stream запроса!");
                    await HandleInfoStreamRequestAsync(context);
                    return;
                }

                if (path.StartsWith("/info/page/"))
                {
                    string pageName = path.Substring("/info/page/".Length);
                    Debug.WriteLine($"[WebServer] ✅ Обработка /info/page/{pageName} запроса!");
                    await HandleInfoPageRequestAsync(context, pageName);
                    return;
                }

                if (path == "/info/search")
                {
                    string query = request.QueryString["q"] ?? "";
                    Debug.WriteLine($"[WebServer] ✅ Обработка /info/search?q={query} запроса!");
                    await HandleInfoSearchRequestAsync(context, query);
                    return;
                }

                // ============================================================
                // МЕДИА-ЧАТ
                // ============================================================

                if (path == "/media/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /media/stream запроса!");
                    await HandleMediaStreamRequestAsync(context);
                    return;
                }

                if (path == "/media" || path == "/media/")
                {
                    // Отдаём HTML-страницу для медиа-чата
                    await ServeMediaPageAsync(context);
                    return;
                }

                // ============================================================
                // ALERTS (веб-оверлей алертов)
                // ============================================================

                if (path == "/alerts/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /alerts/stream запроса!");
                    await HandleAlertsStreamRequestAsync(context);
                    return;
                }

                if (path == "/alerts" || path == "/alerts/")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /alerts запроса!");
                    await ServeAlertsPageAsync(context);
                    return;
                }

                // ============================================================
                // TECH EVENTS (технический оверлей)
                // ============================================================

                if (path == "/tech/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /tech/stream запроса!");
                    await HandleTechStreamRequestAsync(context);
                    return;
                }

                if (path == "/tech" || path == "/tech/")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /tech запроса!");
                    await ServeTechPageAsync(context);
                    return;
                }
                // ============================================================
                // ОБЩИЕ РЕСУРСЫ
                // ============================================================

                if (path.StartsWith("/avatar/"))
                {
                    await HandleAvatarRequestAsync(context);
                    return;
                }

                if (path.StartsWith("/emoji/"))
                {
                    await HandleEmojiRequestAsync(context);
                    return;
                }

                if (path.StartsWith("/ranks/"))
                {
                    await HandleRankCssRequestAsync(context);
                    return;
                }

                if (path == "/info/scroll/speed" && request.HttpMethod == "POST")
                {
                    await HandleScrollSpeedAsync(context);
                    return;
                }

                if (path == "/info/appear/speed" && request.HttpMethod == "POST")
                {
                    await HandleAppearSpeedAsync(context);
                    return;
                }

                if (path.StartsWith("/SF_Data/"))
                {
                    await HandleSfDataRequestAsync(context);
                    return;
                }
                // ============================================================
                // СТАТИЧЕСКИЕ ФАЙЛЫ
                // ============================================================

                string filePath = GetFilePath(path);
                Debug.WriteLine($"[WebServer] Запрос файла: {filePath}");
                await ServeFileAsync(context, filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка обработки запроса: {ex.Message}");
                Debug.WriteLine($"[WebServer] StackTrace: {ex.StackTrace}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        // ============================================================
        // ОБРАБОТКА SF_Data С КЕШЕМ
        // ============================================================

        private async Task HandleSfDataRequestAsync(HttpListenerContext context)
        {
            try
            {
                var relativePath = context.Request.Url?.AbsolutePath.TrimStart('/');
                if (string.IsNullOrEmpty(relativePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    relativePath);

                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                // ✅ ПРОВЕРЯЕМ КЕШ
                byte[]? bytes = null;
                lock (_imageCacheLock)
                {
                    if (_imageCache.TryGetValue(filePath, out var cached))
                    {
                        bytes = cached;
                        Debug.WriteLine($"[WebServer] ✅ Из кеша: {Path.GetFileName(filePath)}");
                    }
                }

                // ✅ ЕСЛИ НЕТ В КЕШЕ — ЗАГРУЖАЕМ
                if (bytes == null)
                {
                    bytes = await File.ReadAllBytesAsync(filePath);

                    // Кешируем только изображения (до 5 МБ)
                    if (bytes.Length < 5 * 1024 * 1024)
                    {
                        lock (_imageCacheLock)
                        {
                            // Ограничиваем размер кеша
                            if (_imageCache.Count >= MAX_CACHE_IMAGES)
                            {
                                var toRemove = _imageCache.Take(_imageCache.Count / 2).ToList();
                                foreach (var item in toRemove)
                                {
                                    _imageCache.Remove(item.Key);
                                }
                                Debug.WriteLine($"[WebServer] 🗑 Кеш очищен: удалено {toRemove.Count} изображений");
                            }
                            _imageCache[filePath] = bytes;
                        }
                        Debug.WriteLine($"[WebServer] 💾 Закешировано: {Path.GetFileName(filePath)} ({bytes.Length} байт)");
                    }
                }

                // Content-Type
                var ext = Path.GetExtension(filePath).ToLower();
                context.Response.ContentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".apng" => "image/apng",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                // ✅ ЗАГОЛОВКИ КЕШИРОВАНИЯ ДЛЯ БРАУЗЕРА
                context.Response.Headers.Add("Cache-Control", "public, max-age=86400");
                context.Response.Headers.Add("Expires", DateTime.UtcNow.AddDays(1).ToString("R"));

                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка SF_Data: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }
        private async Task HandleAppearSpeedAsync(HttpListenerContext context)
        {
            var response = context.Response;

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            using var doc = JsonDocument.Parse(body);
            var speed = doc.RootElement.GetProperty("speed").GetDouble(); // может быть дробным (0.3, 0.5, 1.0)

            var json = $"{{\"type\":\"appear_speed\",\"speed\":{speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
            var data = $"data: {json}\n\n";

            await NotifyInfoClientsRaw(data);

            var bytes = Encoding.UTF8.GetBytes($"{{\"status\":\"ok\",\"speed\":{speed}}}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();

            Debug.WriteLine($"[WebServer] ✨ Скорость появления: {speed}s");
        }

        private async Task HandleScrollSpeedAsync(HttpListenerContext context)
        {
            var response = context.Response;

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            // Парсим JSON: { "speed": 800 }
            using var doc = JsonDocument.Parse(body);
            var speed = doc.RootElement.GetProperty("speed").GetInt32();

            // Отправляем SSE всем клиентам /info/stream
            var json = $"{{\"type\":\"scroll_speed\",\"speed\":{speed}}}";
            var data = $"data: {json}\n\n";

            await NotifyInfoClientsRaw(data);

            // Отправляем ответ
            var bytes = Encoding.UTF8.GetBytes($"{{\"status\":\"ok\",\"speed\":{speed}}}");
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();

            Debug.WriteLine($"[WebServer] 🏃 Скорость скролла изменена: {speed}ms");
        }

        private async Task HandleAvatarRequestAsync(HttpListenerContext context)
        {
            try
            {
                var fileName = Path.GetFileName(context.Request.Url?.AbsolutePath);
                if (string.IsNullOrEmpty(fileName))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                // ✅ Ищем в 4 папках: custom, platform, Default, default
                string baseDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Avatars");

                string[] searchFolders = new[]
                {
            Path.Combine(baseDir, "custom"),
            Path.Combine(baseDir, "platform"),
            Path.Combine(baseDir, "Default"),   // ← ДОБАВЛЕНО
            Path.Combine(baseDir, "default"),   // ← ДОБАВЛЕНО (на случай разного регистра)
        };

                string? avatarPath = null;
                foreach (var folder in searchFolders)
                {
                    var candidate = Path.Combine(folder, fileName);
                    if (File.Exists(candidate))
                    {
                        avatarPath = candidate;
                        Debug.WriteLine($"[WebServer] ✅ Аватар найден: {candidate}");
                        break;
                    }
                }

                if (avatarPath == null)
                {
                    Debug.WriteLine($"[WebServer] ❌ Аватар НЕ найден: {fileName}");
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(avatarPath);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка аватара: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        private async Task HandleEmojiRequestAsync(HttpListenerContext context)
        {
            try
            {
                var fileName = Path.GetFileName(context.Request.Url?.AbsolutePath);

                // Проверяем несколько папок
                string[] searchPaths = new[]
                {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Emojis", "YouTube", "Images", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Emojis", "Twitch", "Images", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Emojis", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars", "Emojis", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Emojis", fileName)
        };

                string emojiPath = searchPaths.FirstOrDefault(File.Exists);

                if (emojiPath != null)
                {
                    var bytes = await File.ReadAllBytesAsync(emojiPath);
                    context.Response.ContentType = "image/png";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    Debug.WriteLine($"[WebServer] ✅ Эмодзи отправлен: {fileName}");
                }
                else
                {
                    Debug.WriteLine($"[WebServer] ❌ Эмодзи не найден: {fileName}");
                    context.Response.StatusCode = 404;
                }
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка эмодзи: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task HandleRankCssRequestAsync(HttpListenerContext context)
        {
            try
            {
                // URL вида /ranks/rank_5.css  или  /ranks/css/rank_5.css
                var fileName = Path.GetFileName(context.Request.Url?.AbsolutePath ?? "");
                if (string.IsNullOrEmpty(fileName))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var filePath = Path.Combine(RanksCssDir, fileName);
                Debug.WriteLine($"[WebServer] Запрос CSS ранга: {fileName} -> {filePath}");

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[WebServer] ❌ CSS не найден: {filePath}");
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var css = await File.ReadAllTextAsync(filePath);
                var buffer = Encoding.UTF8.GetBytes(css);

                context.Response.ContentType = "text/css; charset=utf-8";
                context.Response.ContentLength64 = buffer.Length;

                // ✅ no-cache — правки подхватываются без перезагрузки
                context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                context.Response.Headers.Add("Pragma", "no-cache");
                context.Response.Headers.Add("Expires", "0");

                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка CSS ранга: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private string GetFilePath(string path)
        {
            if (path == "/" || string.IsNullOrEmpty(path))
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html", "Overlays", "chat.html");

            var safePath = path.Replace("..", "").TrimStart('/');
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html", "Overlays", safePath);
        }

        private async Task ServeFileAsync(HttpListenerContext context, string filePath)
        {
            var response = context.Response;

            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                var errorHtml = "<html><body><h1>404 - Not Found</h1></body></html>";
                var buffer = Encoding.UTF8.GetBytes(errorHtml);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
                response.Close();
                return;
            }

            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                response.ContentType = extension switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".css" => "text/css",
                    ".js" => "application/javascript",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                var buffer = await File.ReadAllBytesAsync(filePath);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка отправки файла: {ex.Message}");
                response.StatusCode = 500;
            }
            finally
            {
                response.Close();
            }
        }
        private async Task HandleStreamRequestAsync(HttpListenerContext context)
        {
            await _chatStreamHandler.HandleConnectionAsync(
                context,
                _cts?.Token ?? CancellationToken.None);
        }

        private async Task HandleApiRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;

            lock (_lockObject)
            {
                var messages = new List<object>();
                foreach (var msg in _messages)
                {
                    messages.Add(new
                    {
                        displayName = msg.DisplayName,
                        messageText = msg.MessageText,
                        userRank = msg.UserRank,
                        avatarPath = msg.AvatarPath,
                        timestamp = DateTime.Now.ToString("HH:mm:ss")
                    });
                }

                var json = JsonSerializer.Serialize(messages);
                var buffer = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer);
                response.Close();
            }
        }



        public void AddMessage(DisplayMessageViewModel msg)
        {
            if (msg == null) return;
            if (string.IsNullOrEmpty(msg.DisplayName) || msg.DisplayName == "Unknown") return;
            if (string.IsNullOrEmpty(msg.MessageText)) return;

            // Рассылаем напрямую всем stream-клиентам
            BroadcastChatMessage(msg);
        }

        /// <summary>
        /// Разослать сообщение чата всем SSE-клиентам /stream
        /// </summary>
        public void BroadcastChatMessage(DisplayMessageViewModel msg)
        {
            _chatStreamHandler.BroadcastChatMessage(msg);
        }

        private int GetLikesCountForMessage(int messageNumber)
        {
            if (messageNumber <= 0) return 0;
            try
            {
                long id = DatabaseService.GetMessageIdByNumber(messageNumber);
                if (id <= 0) return 0;
                var counts = DatabaseService.GetReactionCounts(id);
                return counts.Likes;
            }
            catch { return 0; }
        }

        private int GetDislikesCountForMessage(int messageNumber)
        {
            if (messageNumber <= 0) return 0;
            try
            {
                long id = DatabaseService.GetMessageIdByNumber(messageNumber);
                if (id <= 0) return 0;
                var counts = DatabaseService.GetReactionCounts(id);
                return counts.Dislikes;
            }
            catch { return 0; }
        }
        /// <summary>
        /// Разослать произвольные SSE-данные всем клиентам /stream
        /// (используется для avatar_update и других спец-сообщений)
        /// </summary>
        public void BroadcastRawSse(string sseData)
        {
            _chatStreamHandler.BroadcastRawSse(sseData);
        }

        private string GetRankTemplate(int rank)
        {
            var templatePath = Path.Combine(RanksHtmlDir, $"rank_{rank}.html");

            // 1. Точный файл ранга
            if (File.Exists(templatePath))
                return File.ReadAllText(templatePath, Encoding.UTF8);

            // 2. Ближайший меньший ранг
            for (int r = rank - 1; r >= 0; r--)
            {
                var fallbackPath = Path.Combine(RanksHtmlDir, $"rank_{r}.html");
                if (File.Exists(fallbackPath))
                {
                    Debug.WriteLine($"[WebServer] Шаблон rank_{rank}.html не найден, используем rank_{r}.html");
                    return File.ReadAllText(fallbackPath, Encoding.UTF8);
                }
            }

            // 3. Fallback
            Debug.WriteLine($"[WebServer] ⚠️ Шаблон для ранга {rank} не найден, отдаём дефолт");
            return GetDefaultTemplate();
        }

        private string GetDefaultTemplate()
        {
            // Это шаблон для РАНГА 0
            return @"<div class='message rank-0'>
        <span class='name'>{displayName}</span>
        <span class='text'>: {formattedMessage}</span>
    </div>";
        }

        // ============================================================
        // ФОРМАТИРОВАНИЕ СООБЩЕНИЙ И РАНГОВ
        // ============================================================

        private string GetFormattedMessageForWeb(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // ✅ 1. YouTube эмодзи :code:
            var emojiRegex = new Regex(@":([a-zA-Z0-9_-]+):");
            text = emojiRegex.Replace(text, match =>
            {
                string emojiCode = match.Groups[1].Value;
                string fullCode = $":{emojiCode}:";

                if (EmojiService.EmojiExists(fullCode))
                {
                    var emojiInfo = EmojiService.GetEmojiInfo(fullCode);
                    if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.ImagePath))
                    {
                        return $"<img src='/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                    }
                }

                string emojiPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Emojis", "YouTube", "Images",
                    $"{emojiCode}.png");

                if (File.Exists(emojiPath))
                {
                    return $"<img src='/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                }

                return match.Value;
            });

            // ✅ 2. Twitch эмодзи [code]
            var twitchRegex = new Regex(@"\[([^\]]+)\]");
            text = twitchRegex.Replace(text, match =>
            {
                string emojiCode = match.Groups[1].Value;
                string fullCode = $"[{emojiCode}]";

                if (EmojiService.EmojiExists(fullCode))
                {
                    var emojiInfo = EmojiService.GetEmojiInfo(fullCode);
                    if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.ImagePath))
                    {
                        return $"<img src='/emoji/{emojiCode}.png' class='emoji twitch-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                    }
                }

                return match.Value;
            });

            // ✅ 3. HTML теги форматирования
            text = Regex.Replace(text, @"<b>(.*?)</b>", "<b>$1</b>");
            text = Regex.Replace(text, @"<i>(.*?)</i>", "<i>$1</i>");
            text = Regex.Replace(text, @"<color=(.*?)>(.*?)</color>", "<span style='color:$1'>$2</span>");
            text = Regex.Replace(text, @"<c=(.*?)>(.*?)</c>", "<span style='color:$1'>$2</span>");

            // ✅ 4. Переносы строк
            text = text.Replace("\n", "<br>");

            return text;
        }

        private string GetRankDisplay(int rank)
        {
            return rank switch
            {
                0 => "",
                1 => "★",
                2 => "★★",
                3 => "★★★",
                4 => "★★★★",
                5 => "★★★★★",
                >= 6 => $"★ {rank}",
                _ => ""
            };
        }

        private string GetRankClass(int rank)
        {
            if (rank == 0)
                return "rank-0";

            var cssPath = Path.Combine(RanksCssDir, $"rank_{rank}.css");

            if (File.Exists(cssPath))
                return $"rank-{rank}";

            for (int r = rank - 1; r >= 0; r--)
            {
                var fallbackPath = Path.Combine(RanksCssDir, $"rank_{r}.css");
                if (File.Exists(fallbackPath))
                    return $"rank-{r}";
            }

            return "rank-0";
        }

        private async Task<string?> GetRankCssContent(int rank)
        {
            if (rank == 0)
                return null;

            try
            {
                var cssPath = Path.Combine(RanksCssDir, $"rank_{rank}.css");

                if (File.Exists(cssPath))
                    return await File.ReadAllTextAsync(cssPath);

                for (int r = rank - 1; r >= 0; r--)
                {
                    var fallbackPath = Path.Combine(RanksCssDir, $"rank_{r}.css");
                    if (File.Exists(fallbackPath))
                    {
                        Debug.WriteLine($"[WebServer] CSS ранга {rank} не найден, используем rank_{r}.css");
                        return await File.ReadAllTextAsync(fallbackPath);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка загрузки CSS ранга {rank}: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // ALERTS OVERLAY (веб-оверлей алертов)
        // ============================================================

        /// <summary>
        /// SSE-поток алертов для /alerts/stream
        /// </summary>
        private async Task HandleAlertsStreamRequestAsync(HttpListenerContext context)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[WebServer] 🔔 Alerts-клиент {client.Id} подключён");

            _alertsManager.Add(client);

            try
            {
                // Приветствие — чтобы браузер сразу понял, что соединение живо
                //await client.SendAsync($"data: {{\"type\":\"hello\",\"ts\":\"{DateTime.Now:HH:mm:ss}\"}}\n\n");
                await client.SendAsync(": ping\n\n");

                // Ждём отключения (или остановки сервера)
                await client.WaitUntilClosedAsync(_cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Alerts-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _alertsManager.Remove(client);
            }
        }

        private async Task HandleTechStreamRequestAsync(HttpListenerContext context)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[WebServer] 🔧 Tech-клиент {client.Id} подключён");

            _techManager.Add(client);

            try
            {
                await client.SendAsync(": ping\n\n");
                await client.WaitUntilClosedAsync(_cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Tech-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _techManager.Remove(client);
            }
        }
        /// <summary>
        /// HTML-страница веб-оверлея алертов для OBS
        /// </summary>
        private async Task ServeAlertsPageAsync(HttpListenerContext context)
        {
            var response = context.Response;

            string alertsHtml = HtmlProvider.GetOverlay("alerts.html");

            var bytes = Encoding.UTF8.GetBytes(alertsHtml);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }


        private async Task ServeTechPageAsync(HttpListenerContext context)
        {
            var response = context.Response;

            string techHtml = HtmlProvider.GetOverlay("tech.html");

            var bytes = Encoding.UTF8.GetBytes(techHtml);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }
        /// <summary>
        /// Отправить алерт в веб-оверлей /alerts (SSE)
        /// </summary>
        public void SendAlertToWeb(
string userName,
string message,
string displayAmount,
string providerType,
string providerName,
int durationSeconds)
        {
            var payload = new
            {
                type = "alert",
                alert = new
                {
                    userName = userName ?? "Аноним",
                    message = message ?? "",
                    displayAmount = displayAmount ?? "",
                    providerType = providerType ?? "donationalerts",
                    providerName = providerName ?? "ALERT",
                    durationSeconds = durationSeconds,
                    timestamp = DateTime.Now.ToString("HH:mm:ss")
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var data = $"data: {json}\n\n";

            if (_alertsManager.Count == 0)
            {
                Debug.WriteLine("[WebServer] 🔔 Алерт сформирован, но нет активных alerts-клиентов");
                return;
            }

            _alertsManager.Broadcast(data);

            Debug.WriteLine($"[WebServer] 🔔 Алерт отправлен ({_alertsManager.Count} клиентов): {userName} - {displayAmount}");
        }


        // ============================================================
        // TECH EVENTS (технический оверлей)
        // ============================================================

        /// <summary>
        /// Отправить техническое событие в /tech/stream.
        /// </summary>
        public void SendTechnicalEvent(SmithForge.Features.TechOverlay.TechEvent evt)
        {
            if (evt == null) return;

            var payload = new
            {
                type = "tech",
                userName = evt.UserName,
                userLogin = evt.UserLogin,
                kind = evt.Kind.ToString().ToLower(),
                text = evt.Text,
                karma = evt.Karma,
                timestamp = evt.Timestamp.ToString("HH:mm:ss")
            };

            // ✅ Уведомляем локальных подписчиков (WPF-окно)
            TechnicalEventSent?.Invoke(this, evt);

            var json = JsonSerializer.Serialize(payload);
            var data = $"data: {json}\n\n";

            _techManager.Broadcast(data);

            Debug.WriteLine($"[WebServer] 🔧 Tech-событие отправлено ({_techManager.Count} клиентов): {evt.UserName} → {evt.Text}");
        }

        /// <summary>
        /// Закрыть все tech-соединения.
        /// </summary>
        public void CloseAllTechConnections()
        {
            _techManager.CloseAll();
        }
        /// <summary>
        /// Закрыть все Alerts SSE-соединения
        /// </summary>
        public void CloseAllAlertsConnections()
        {
            _alertsManager.CloseAll();
        }
        public void Dispose()
        {
            Debug.WriteLine("[WebServer] Начинаем корректное завершение...");

            // 1. Закрываем все SSE-соединения
            CloseAllMediaConnections();
            CloseAllInfoConnections();
            CloseAllAlertsConnections();
            CloseAllStreamConnections();
            CloseAllTechConnections();

            // 2. Останавливаем сервер
            Stop();

            // 3. Очищаем кэши
            ClearImageCache();
            ClearPageCache();

            // 4. Очищаем историю сообщений
            lock (_lockObject)
            {
                _messages.Clear();
            }

            // 5. Закрываем HttpListener
            _listener?.Close();
            _listener = null;

            Debug.WriteLine("[WebServer] Завершение выполнено");
        }

        // ============================================================
        // ИНФОРМАЦИОННЫЙ ЧАТ - МЕТОДЫ
        // ============================================================

        private async Task ServeInfoPageAsync(HttpListenerContext context, string pageName)
        {
            var response = context.Response;

            // ============================================================
            // index.html — каркас /info, из Html/Overlays/
            // ============================================================
            if (pageName == "index.html" || pageName == "index")
            {
                string indexHtml = HtmlProvider.GetOverlay("info.html");
                await SendHtmlResponse(response, indexHtml);
                return;
            }

            // ============================================================
            // Страницы справочника: сначала пользовательская из SF_Data,
            // потом дефолтная из Html/Overlays/pages/
            // ============================================================

            // 1. Пользовательская версия
            string userPagePath = Path.Combine(_infoPagesDir, $"{pageName}.html");
            if (File.Exists(userPagePath))
            {
                string html = await File.ReadAllTextAsync(userPagePath, Encoding.UTF8);
                html = InjectInfoNavigation(html, pageName);
                await SendHtmlResponse(response, html);
                await NotifyInfoClients(pageName);
                return;
            }

            // 2. Дефолтная из проекта
            string defaultHtml = HtmlProvider.GetPage($"{pageName}.html");

            // HtmlProvider возвращает заглушку "<h2>❌ Шаблон не найден: ..." если файла нет
            if (defaultHtml.Contains("Шаблон не найден"))
            {
                response.StatusCode = 404;
                await SendHtmlResponse(response, "<h2>❌ Страница не найдена</h2>");
                return;
            }

            defaultHtml = InjectInfoNavigation(defaultHtml, pageName);
            await SendHtmlResponse(response, defaultHtml);
            await NotifyInfoClients(pageName);
        }

        private async Task HandleInfoPageRequestAsync(HttpListenerContext context, string pageName)
        {
            await ServeInfoPageAsync(context, pageName);
        }

        private async Task HandleInfoSearchRequestAsync(HttpListenerContext context, string query)
        {
            var response = context.Response;

            if (string.IsNullOrEmpty(query))
            {
                await SendHtmlResponse(response, "<p>❌ Введите текст для поиска</p>");
                return;
            }

            var results = new List<(string PageId, string Title, string Snippet)>();
            string lowerQuery = query.ToLower();

            foreach (var page in _infoPageCache)
            {
                if (page.Value.ToLower().Contains(lowerQuery))
                {
                    string title = ExtractTitle(page.Value) ?? page.Key;
                    string snippet = ExtractSnippet(page.Value, query);
                    results.Add((page.Key, title, snippet));
                }
            }

            if (results.Count == 0)
            {
                await SendHtmlResponse(response, $"<p>❌ По запросу '<b>{query}</b>' ничего не найдено</p>");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"<div class='search-results'>");
            sb.AppendLine($"  <h2>🔍 Результаты поиска: '{query}'</h2>");
            sb.AppendLine($"  <p>Найдено: {results.Count}</p>");
            sb.AppendLine($"  <hr/>");

            foreach (var (id, title, snippet) in results.Take(20))
            {
                sb.AppendLine($"  <div class='result-item'>");
                sb.AppendLine($"    <a href='#' onclick='loadPage(\"{id}\")'><b>{title}</b></a>");
                if (!string.IsNullOrEmpty(snippet))
                {
                    sb.AppendLine($"    <p class='snippet'>{snippet}</p>");
                }
                sb.AppendLine($"  </div>");
            }

            if (results.Count > 20)
            {
                sb.AppendLine($"  <p>... и еще {results.Count - 20} результатов</p>");
            }

            sb.AppendLine($"  <hr/>");
            sb.AppendLine($"  <a href='#' onclick='loadPage(\"help\")'>🏠 Главная</a>");
            sb.AppendLine($"</div>");

            await SendHtmlResponse(response, sb.ToString());
        }

        private async Task HandleInfoStreamRequestAsync(HttpListenerContext context)
        {
            await _infoStreamHandler.HandleConnectionAsync(
                context,
                _cts?.Token ?? CancellationToken.None);
        }

        // Оставляем старый метод для обратной совместимости
        private async Task NotifyInfoClients(string pageName)
        {
            if (_infoManager.Count == 0) return;

            var json = $"{{\"type\":\"page_update\",\"page\":\"{pageName}\"}}";
            var data = $"data: {json}\n\n";
            await NotifyInfoClientsRaw(data);
        }

        // ✅ НОВЫЙ МЕТОД: отправка произвольных данных
        private async Task NotifyInfoClientsRaw(string data)
        {
            await _infoManager.BroadcastAsync(data);
        }

        private string InjectInfoNavigation(string html, string pageName)
        {
            if (html.Contains("<!--navigation-->") || html.Contains("{{navigation}}"))
                return html;

            string parent = GetParentPage(pageName);

            var nav = new StringBuilder();
            nav.AppendLine("<div class='info-nav'>");
            nav.AppendLine("  <hr/>");

            if (parent != null && parent != pageName)
            {
                nav.AppendLine($"  <a href='#' onclick='loadPage(\"{parent}\")'>⬅️ Назад</a>");
            }

            nav.AppendLine($"  <a href='#' onclick='loadPage(\"help\")'>🏠 Главная</a>");
            nav.AppendLine("</div>");

            if (html.Contains("</body>"))
                html = html.Replace("</body>", nav.ToString() + "</body>");
            else
                html += nav.ToString();

            return html;
        }

        private string GetParentPage(string pageName)
        {
            var parentMap = new Dictionary<string, string>
            {
                ["formatting"] = "help",
                ["interaction"] = "help",
                ["important"] = "help",
                ["stickers"] = "help",
                ["profile"] = "help",
                ["commands"] = "help",
                ["karma"] = "help",
                ["rules"] = "help"
            };

            return parentMap.TryGetValue(pageName, out string? parent) ? parent : "help";
        }

        private string ExtractTitle(string html)
        {
            var match = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private string ExtractSnippet(string html, string query)
        {
            int index = html.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "";

            int start = Math.Max(0, index - 60);
            int end = Math.Min(html.Length, index + query.Length + 60);
            string snippet = html.Substring(start, end - start);

            snippet = Regex.Replace(snippet, @"<[^>]*>", " ");
            snippet = Regex.Replace(snippet, @"\s+", " ").Trim();

            if (start > 0) snippet = "..." + snippet;
            if (end < html.Length) snippet = snippet + "...";

            return snippet;
        }

        private async Task SendHtmlResponse(HttpListenerResponse response, string html)
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }

        /// <summary>
        /// Отправить сообщение в информационный чат (для OBS)
        /// </summary>
        public void SendInfoMessage(string html, string pageName)
        {
            var json = new
            {
                type = "info_message",
                pageName = pageName,
                html = html,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            var jsonStr = System.Text.Json.JsonSerializer.Serialize(json);
            var data = $"data: {jsonStr}\n\n";

            _ = NotifyInfoClientsRaw(data);

            Debug.WriteLine($"[WebServer] 📡 Отправлено info_message: {pageName}");
        }

        public void ClearImageCache()
        {
            lock (_imageCacheLock)
            {
                var count = _imageCache.Count;
                _imageCache.Clear();
                Debug.WriteLine($"[WebServer] 🗑 Кеш изображений очищен ({count} файлов)");
            }
        }

        public void ClearPageCache()
        {
            lock (_infoPageCacheLock)
            {
                _infoPageCache.Clear();
                Debug.WriteLine("[WebServer] 🗑 Кеш страниц очищен");
            }
        }

        // ============================================================
        // МЕДИА-ЧАТ (стикеры, GIF, видео)
        // ============================================================

        private readonly SseClientManager _mediaManager = new("Media");

        // Обработчик для /media/stream
        private async Task HandleMediaStreamRequestAsync(HttpListenerContext context)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[WebServer] 📺 Медиа-клиент {client.Id} подключён");

            _mediaManager.Add(client);

            try
            {
                // Приветствие — чтобы браузер сразу понял, что соединение живое
                //await client.SendAsync($"data: {{\"type\":\"hello\",\"ts\":\"{DateTime.Now:HH:mm:ss}\"}}\n\n");
                await client.SendAsync(": ping\n\n");

                // Ждём отключения (или остановки сервера)
                await client.WaitUntilClosedAsync(_cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Медиа-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _mediaManager.Remove(client);
            }
        }

        /// <summary>
        /// Отправить медиа-сообщение (стикер или видео)
        /// </summary>
        public void SendMediaMessage(string jsonData)
        {
            var data = $"data: {jsonData}\n\n";
            _mediaManager.Broadcast(data);
        }
        public void SendStickerToMedia(string userName, string stickerPath, string stickerId, bool isAnimated, string text = "")
        {
            try
            {
                // ✅ Преобразуем локальный путь в URL
                string webPath = stickerPath.Replace(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "/")
                    .Replace("\\", "/")
                    .Replace("//", "/");

                // Добавляем префикс /SF_Data/
                if (!webPath.StartsWith("/SF_Data/"))
                {
                    webPath = "/SF_Data/" + webPath.TrimStart('/');
                }

                // ✅ ЛОГИРУЕМ ТЕКСТ ДЛЯ ОТЛАДКИ
                Debug.WriteLine($"[WebServer] 📝 Текст для стикера: '{text}'");

                var json = new
                {
                    type = "sticker",
                    userName = userName ?? "Аноним",
                    stickerId = stickerId ?? "0",
                    stickerPath = webPath,
                    isAnimated = isAnimated,
                    text = text ?? "",  // ✅ ТЕКСТ ПЕРЕДАЁТСЯ
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                var jsonStr = System.Text.Json.JsonSerializer.Serialize(json);

                Debug.WriteLine($"[WebServer] 📺 Отправка JSON: {jsonStr}");

                SendMediaMessage(jsonStr);

                Debug.WriteLine($"[WebServer] 📺 Отправлен стикер в медиа-чат: {stickerId} -> {webPath}, текст: '{text}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] ❌ Ошибка отправки стикера: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправить видео в медиа-чат
        /// </summary>
        public void SendVideoToMedia(string userName, string videoUrl, string text = "")
        {
            var json = new
            {
                type = "video",
                userName = userName,
                videoUrl = videoUrl,
                text = text,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            var jsonStr = System.Text.Json.JsonSerializer.Serialize(json);
            SendMediaMessage(jsonStr);
            Debug.WriteLine($"[WebServer] 📺 Отправлено видео в медиа-чат: {videoUrl}");
        }

        /// <summary>
        /// Отправить текстовое сообщение в медиа-чат
        /// </summary>
        public void SendTextToMedia(string userName, string text)
        {
            var json = new
            {
                type = "text",
                userName = userName,
                text = text,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
            };

            var jsonStr = System.Text.Json.JsonSerializer.Serialize(json);
            SendMediaMessage(jsonStr);
        }

        private async Task ServeMediaPageAsync(HttpListenerContext context)
        {
            var response = context.Response;

            string mediaHtml = HtmlProvider.GetOverlay("media.html");

            var bytes = Encoding.UTF8.GetBytes(mediaHtml);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }

        /// <summary>
        /// Закрыть все SSE-соединения для медиа-чата
        /// </summary>
        public void CloseAllMediaConnections()
        {
            _mediaManager.CloseAll();
        }

        /// <summary>
        /// Закрыть все SSE-соединения для информационного чата
        /// </summary>
        public void CloseAllInfoConnections()
        {
            _infoManager.CloseAll();
        }

        /// <summary>
        /// Закрыть все SSE-соединения для основного чата
        /// </summary>
        public void CloseAllStreamConnections()
        {
            _chatStreamHandler.CloseAll();
        }

        /// <summary>
        /// Обновить существующее сообщение в веб-оверлее (текст, имя, ранк и т.д.)
        /// </summary>
        public void UpdateMessageInWeb(string messageId, object updateData)
        {
            var payload = new
            {
                type = "message_update",
                id = messageId,
                update = updateData,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            var json = JsonSerializer.Serialize(payload);
            BroadcastRawSse($"data: {json}\n\n");
            Debug.WriteLine($"[WebServer] 📝 Обновление отправлено для сообщения {messageId}");
        }

        /// <summary>
        /// Обновить реакции (лайки/дизлайки) на сообщении
        /// </summary>
        public void UpdateReactionInWeb(int messageNumber, string reactionType, int newCount)
        {
            var payload = new
            {
                type = "message_reaction",
                messageNumber = messageNumber,
                reaction = reactionType, // "like" или "dislike"
                count = newCount,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            var json = JsonSerializer.Serialize(payload);
            BroadcastRawSse($"data: {json}\n\n");
            Debug.WriteLine($"[WebServer] 👍 Реакция {reactionType} ({newCount}) для сообщения #{messageNumber}");
        }

        /// <summary>
        /// Запустить анимацию на сообщении (rank_up, pulse, shake и т.д.)
        /// </summary>
        public void TriggerAnimationInWeb(string messageId, string animationName, int durationMs = 1000)
        {
            var payload = new
            {
                type = "message_animation",
                id = messageId,
                animation = animationName,
                duration = durationMs,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            var json = JsonSerializer.Serialize(payload);
            BroadcastRawSse($"data: {json}\n\n");
            Debug.WriteLine($"[WebServer] ✨ Анимация '{animationName}' для сообщения {messageId}");
        }

        /// <summary>
        /// Обновить все сообщения пользователя (например, при смене ника)
        /// </summary>
        public void UpdateAllUserMessagesInWeb(string userId, object updateData)
        {
            var payload = new
            {
                type = "user_messages_update",
                userId = userId,
                update = updateData,
                timestamp = DateTime.Now.ToString("HH:mm:ss")
            };

            var json = JsonSerializer.Serialize(payload);
            BroadcastRawSse($"data: {json}\n\n");
            Debug.WriteLine($"[WebServer] 👤 Обновление всех сообщений пользователя {userId}");
        }


    }
}