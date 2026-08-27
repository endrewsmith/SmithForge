using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    public class WebServerService : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private readonly int _port;
        private readonly string _baseDirectory;
        private readonly List<DisplayMessageViewModel> _messages = new();
        private readonly object _lockObject = new();
        private readonly Dictionary<int, string> _rankTemplates = new();

        public event EventHandler<DisplayMessageViewModel>? MessageAdded;

        public WebServerService(int port = 10881)
        {
            _port = port;
            _baseDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "WebOverlay");
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                Debug.WriteLine($"[WebServer] _baseDirectory = {_baseDirectory}");
                Debug.WriteLine($"[WebServer] Directory exists = {Directory.Exists(_baseDirectory)}");

                // Создаём директорию для веб-файлов
                if (!Directory.Exists(_baseDirectory))
                {
                    Debug.WriteLine("[WebServer] Создаём директорию...");
                    Directory.CreateDirectory(_baseDirectory);
                    CreateDefaultHtmlFiles();
                    Debug.WriteLine("[WebServer] CreateDefaultHtmlFiles() завершён");
                }
                else
                {
                    Debug.WriteLine("[WebServer] Директория уже существует, проверяем index.html");
                    var indexPath = Path.Combine(_baseDirectory, "index.html");
                    if (!File.Exists(indexPath))
                    {
                        Debug.WriteLine("[WebServer] index.html не найден, создаём...");
                        CreateDefaultHtmlFiles();
                    }
                    else
                    {
                        Debug.WriteLine($"[WebServer] index.html существует: {indexPath}");
                    }
                }

                // ✅ СОЗДАЁМ ПАПКИ ДЛЯ РАНГОВ (без templates!)
                var ranksDir = Path.Combine(_baseDirectory, "ranks");
                if (!Directory.Exists(ranksDir))
                {
                    Directory.CreateDirectory(ranksDir);
                    Debug.WriteLine($"[WebServer] Создана папка рангов: {ranksDir}");
                }

                var cssDir = Path.Combine(ranksDir, "css");
                if (!Directory.Exists(cssDir))
                {
                    Directory.CreateDirectory(cssDir);
                    Debug.WriteLine($"[WebServer] Создана папка CSS: {cssDir}");
                }

                var htmlDir = Path.Combine(ranksDir, "html");
                if (!Directory.Exists(htmlDir))
                {
                    Directory.CreateDirectory(htmlDir);
                    Debug.WriteLine($"[WebServer] Создана папка HTML: {htmlDir}");
                }

                // Загружаем шаблоны рангов
                LoadRankTemplates();

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

                Debug.WriteLine($"[WebServer] Запрос: {request.HttpMethod} {request.Url?.AbsolutePath}");

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

                // Обработка SSE (Server-Sent Events)
                if (request.Url?.AbsolutePath == "/stream")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /stream запроса!");
                    await HandleStreamRequestAsync(context);
                    return;
                }

                // Обработка API запросов
                if (request.Url?.AbsolutePath == "/api/messages")
                {
                    Debug.WriteLine("[WebServer] ✅ Обработка /api/messages запроса!");
                    await HandleApiRequestAsync(context);
                    return;
                }

                // Обработка аватаров
                if (request.Url?.AbsolutePath.StartsWith("/avatar/") == true)
                {
                    await HandleAvatarRequestAsync(context);
                    return;
                }

                // Обработка эмодзи
                if (request.Url?.AbsolutePath.StartsWith("/emoji/") == true)
                {
                    await HandleEmojiRequestAsync(context);
                    return;
                }

                // Обработка CSS рангов
                if (request.Url?.AbsolutePath.StartsWith("/ranks/") == true)
                {
                    await HandleRankCssRequestAsync(context);
                    return;
                }

                // Обработка статических файлов
                string filePath = GetFilePath(request.Url?.AbsolutePath ?? "/");
                Debug.WriteLine($"[WebServer] Запрос файла: {filePath}");
                await ServeFileAsync(context, filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка обработки запроса: {ex.Message}");
                Debug.WriteLine($"[WebServer] StackTrace: {ex.StackTrace}");
            }
        }

        private async Task HandleAvatarRequestAsync(HttpListenerContext context)
        {
            try
            {
                var fileName = Path.GetFileName(context.Request.Url?.AbsolutePath);
                var avatarPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Avatars", "custom",
                    fileName);

                if (!File.Exists(avatarPath))
                {
                    // Пробуем в platform
                    avatarPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "SF_Data", "Assets", "Avatars", "platform",
                        fileName);
                }

                if (!File.Exists(avatarPath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(avatarPath);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка аватара: {ex.Message}");
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
        }

        private async Task HandleEmojiRequestAsync(HttpListenerContext context)
        {
            try
            {
                var fileName = Path.GetFileName(context.Request.Url?.AbsolutePath);
                var code = $"[{fileName}]";

                var emojiInfo = EmojiService.GetEmojiInfo(code);
                if (emojiInfo == null || string.IsNullOrEmpty(emojiInfo.ImagePath) || !File.Exists(emojiInfo.ImagePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(emojiInfo.ImagePath);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
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
                var filePath = GetFilePath(context.Request.Url?.AbsolutePath ?? "");
                if (!File.Exists(filePath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }

                var css = await File.ReadAllTextAsync(filePath);
                var buffer = Encoding.UTF8.GetBytes(css);
                context.Response.ContentType = "text/css";
                context.Response.ContentLength64 = buffer.Length;
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
                return Path.Combine(_baseDirectory, "index.html");

            var safePath = path.Replace("..", "").TrimStart('/');
            return Path.Combine(_baseDirectory, safePath);
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
            Debug.WriteLine("[WebServer] HandleStreamRequestAsync НАЧАЛО!");

            var response = context.Response;
            var connectionTaskSource = new TaskCompletionSource<bool>();

            try
            {
                response.Headers.Add("Content-Type", "text/event-stream");
                response.Headers.Add("Cache-Control", "no-cache");
                response.Headers.Add("Connection", "keep-alive");
                response.StatusCode = 200;

                Debug.WriteLine("[WebServer] SSE заголовки отправлены");

                // Отправляем последние 10 сообщений
                List<DisplayMessageViewModel> messagesToSend;
                lock (_lockObject)
                {
                    messagesToSend = _messages.TakeLast(10).ToList();
                }

                Debug.WriteLine($"[WebServer] Отправка {messagesToSend.Count} последних сообщений");

                foreach (var msg in messagesToSend)
                {
                    try
                    {
                        var rankTemplate = GetRankTemplate(msg.UserRank);
                        var rankCss = await GetRankCssContent(msg.UserRank);

                        var json = JsonSerializer.Serialize(new
                        {
                            id = msg.Id,
                            displayName = msg.DisplayName,
                            messageText = msg.MessageText,
                            formattedMessage = GetFormattedMessage(msg),
                            userRank = msg.UserRank,
                            rankDisplay = GetRankDisplay(msg.UserRank),
                            rankClass = GetRankClass(msg.UserRank),
                            rankCss = rankCss,
                            rankTemplate = rankTemplate,
                            avatarPath = msg.AvatarPath,
                            timestamp = DateTime.Now.ToString("HH:mm:ss"),

                            // Данные для кастомных шаблонов (Ранг 1+)
                            karmaKey = msg.User?.KarmaKeyDisplay ?? "",
                            karma = msg.User?.KarmaDisplay ?? "",
                            messageNumber = msg.MessageNumber,
                            messageCount = msg.MessageCount,
                            likes = msg.Likes,
                            dislikes = msg.Dislikes,

                            // Передаем платформу в нижнем регистре для CSS-триггеров (tw, twitch, yt, youtube, gg)
                            platform = msg.Type?.ToLower() ?? "twitch"
                        });

                        var data = $"data: {json}\n\n";
                        var buffer = Encoding.UTF8.GetBytes(data);
                        await response.OutputStream.WriteAsync(buffer);
                        await response.OutputStream.FlushAsync();

                        Debug.WriteLine($"[WebServer] Отправлено сообщение: {msg.DisplayName}: {msg.MessageText}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebServer] Ошибка отправки сообщения из истории: {ex.Message}");
                    }
                }

                // БЕЗОПАСНЫЙ обработчик новых сообщений без async void
                EventHandler<DisplayMessageViewModel> handler = (s, msg) =>
                {
                    // Переносим выполнение в фоновый поток пула, чтобы избежать Deadlock в основном приложении
                    Task.Run(async () =>
                    {
                        try
                        {
                            var rankTemplate = GetRankTemplate(msg.UserRank);
                            var rankCss = await GetRankCssContent(msg.UserRank);

                            var json = JsonSerializer.Serialize(new
                            {
                                id = msg.Id,
                                displayName = msg.DisplayName,
                                messageText = msg.MessageText,
                                formattedMessage = GetFormattedMessage(msg),
                                userRank = msg.UserRank,
                                rankDisplay = GetRankDisplay(msg.UserRank),
                                rankClass = GetRankClass(msg.UserRank),
                                rankCss = rankCss,
                                rankTemplate = rankTemplate,
                                avatarPath = msg.AvatarPath,
                                timestamp = DateTime.Now.ToString("HH:mm:ss"),

                                // Добавляем эти поля и сюда, чтобы новые сообщения отображали карму
                                karmaKey = msg.User?.KarmaKeyDisplay ?? "",
                                karma = msg.User?.KarmaDisplay ?? "",
                                messageNumber = msg.MessageNumber,
                                messageCount = msg.MessageCount,
                                likes = msg.Likes,
                                dislikes = msg.Dislikes,

                                platform = msg.Type?.ToLower() ?? "twitch"
                            });

                            var data = $"data: {json}\n\n";
                            var buffer = Encoding.UTF8.GetBytes(data);

                            // Синхронизируем доступ к потоку записи
                            lock (response.OutputStream)
                            {
                                response.OutputStream.Write(buffer);
                                response.OutputStream.Flush();
                            }

                            Debug.WriteLine($"[WebServer] SSE отправлено новое сообщение: {msg.DisplayName}: {msg.MessageText}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[WebServer] Клиент отключился при попытке отправки: {ex.Message}");
                            connectionTaskSource.TrySetResult(true);
                        }
                    });
                };

                MessageAdded += handler;

                // Подписываемся на отмену через токен сервера
                using var registration = _cts?.Token.Register(() => connectionTaskSource.TrySetResult(true));

                // Безопасно ждём отключения клиента, не блокируя потоки
                await connectionTaskSource.Task;

                MessageAdded -= handler;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка в HandleStreamRequestAsync: {ex.Message}");
            }
            finally
            {
                try { response.Close(); } catch { }
                Debug.WriteLine("[WebServer] HandleStreamRequestAsync ЗАВЕРШЕН");
            }
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
            Debug.WriteLine($"[WebServer] AddMessage: DisplayName='{msg.DisplayName}', MessageText='{msg.MessageText}'");

            if (string.IsNullOrEmpty(msg.DisplayName) || msg.DisplayName == "Unknown")
            {
                Debug.WriteLine($"[WebServer] ⏭ Пропущено Unknown");
                return;
            }

            lock (_lockObject)
            {
                _messages.Add(msg);
                if (_messages.Count > 100)
                {
                    _messages.RemoveAt(0);
                }
            }

            MessageAdded?.Invoke(this, msg);
        }

        private void CreateDefaultHtmlFiles()
        {
            try
            {
                Debug.WriteLine("[WebServer] CreateDefaultHtmlFiles() НАЧАЛО");

                // Базовый путь к папке сборки
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Пути для поиска файла (от корня проекта до папки сборки)
                string[] possiblePaths = new[]
                {
            // Путь от корня проекта (где лежит .csproj)
            Path.Combine(baseDir, "..", "..", "..", "Features", "WebOverlay", "index.html"),
            Path.Combine(baseDir, "..", "..", "Features", "WebOverlay", "index.html"),
            Path.Combine(baseDir, "..", "Features", "WebOverlay", "index.html"),
            // Если файл скопировался в папку сборки
            Path.Combine(baseDir, "Features", "WebOverlay", "index.html"),
            Path.Combine(baseDir, "WebOverlay", "index.html"),
        };

                string sourcePath = null;
                foreach (var path in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        sourcePath = fullPath;
                        Debug.WriteLine($"[WebServer] ✅ Найден index.html: {sourcePath}");
                        break;
                    }
                }

                if (sourcePath != null)
                {
                    string destPath = Path.Combine(_baseDirectory, "index.html");
                    Directory.CreateDirectory(_baseDirectory);
                    File.Copy(sourcePath, destPath, true);
                    Debug.WriteLine($"[WebServer] ✅ index.html скопирован в {destPath}");
                    return;
                }

                Debug.WriteLine("[WebServer] ❌ index.html НЕ НАЙДЕН! Создаю дефолтный.");
                CreateDefaultIndexHtml();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] ❌ Ошибка: {ex.Message}");
                CreateDefaultIndexHtml();
            }
        }

        private void CreateDefaultIndexHtml()
        {
            var path = Path.Combine(_baseDirectory, "index.html");
            var html = @"<!DOCTYPE html>
<html>
<head><meta charset=""UTF-8""><title>SmithForge Chat</title></head>
<body><div id=""chat-container""></div>
<script>
const chatContainer=document.getElementById('chat-container');
const es=new EventSource('/stream');
es.onmessage=e=>{
    try{
        const d=JSON.parse(e.data);
        const div=document.createElement('div');
        div.textContent=d.displayName+': '+d.messageText;
        chatContainer.appendChild(div);
        while(chatContainer.children.length>50)chatContainer.removeChild(chatContainer.firstChild);
    }catch(ex){}
};
</script></body></html>";
            File.WriteAllText(path, html, Encoding.UTF8);
            Debug.WriteLine($"[WebServer] ✅ Создан дефолтный index.html");
        }

        // ============================================================
        // ЗАГРУЗКА ШАБЛОНОВ РАНГОВ
        // ============================================================

        private void LoadRankTemplates()
        {
            var htmlDir = Path.Combine(_baseDirectory, "ranks", "html");
            if (!Directory.Exists(htmlDir))
            {
                Directory.CreateDirectory(htmlDir);
                Debug.WriteLine($"[WebServer] Создана папка HTML шаблонов: {htmlDir}");
                return;
            }

            var templateFiles = Directory.GetFiles(htmlDir, "rank_*.html")
                .OrderBy(f => f);

            foreach (var file in templateFiles)
            {
                var match = Regex.Match(Path.GetFileName(file), @"rank_(\d+)\.html");
                if (!match.Success) continue;

                var rank = int.Parse(match.Groups[1].Value);
                var html = File.ReadAllText(file);
                _rankTemplates[rank] = html;

                Debug.WriteLine($"[WebServer] Загружен шаблон rank_{rank}.html");
            }
        }

        private string GetRankTemplate(int rank)
        {
            var htmlDir = Path.Combine(_baseDirectory, "ranks", "html");
            var templatePath = Path.Combine(htmlDir, $"rank_{rank}.html");

            // 1. Пытаемся прочитать точный файл ранга (включая rank_0.html)
            if (File.Exists(templatePath))
            {
                return File.ReadAllText(templatePath, Encoding.UTF8);
            }

            // 2. Если нет — ищем ближайший меньший ранг на диске
            for (int r = rank - 1; r >= 0; r--)
            {
                var fallbackPath = Path.Combine(htmlDir, $"rank_{r}.html");
                if (File.Exists(fallbackPath))
                {
                    Debug.WriteLine($"[WebServer] Шаблон rank_{rank}.html не найден, используем rank_{r}.html");
                    return File.ReadAllText(fallbackPath, Encoding.UTF8);
                }
            }

            // 3. Если на диске вообще шаром покати — отдаем жестко зашитый дефолт
            Debug.WriteLine($"[WebServer] ⚠️ На диске нет файлов шаблонов. Выдан аварийный GetDefaultTemplate() для ранга {rank}");
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

        private string GetFormattedMessage(DisplayMessageViewModel msg)
        {
            if (string.IsNullOrEmpty(msg.MessageText))
                return string.Empty;

            var text = msg.MessageText;
            var matches = Regex.Matches(text, @"\[([^\]]+)\]");

            if (matches.Count == 0)
                return text;

            var result = text;
            foreach (Match match in matches)
            {
                var code = match.Groups[1].Value;
                var emojiInfo = EmojiService.GetEmojiInfo($"[{code}]");

                if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.ImagePath))
                {
                    var imgTag = $"<img src='/emoji/{code}' class='emoji' alt='{code}' title='{code}'/>";
                    result = result.Replace(match.Value, imgTag);
                }
            }

            return result;
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

            var ranksDir = Path.Combine(_baseDirectory, "ranks", "css");
            var cssPath = Path.Combine(ranksDir, $"rank_{rank}.css");

            if (File.Exists(cssPath))
                return $"rank-{rank}";

            for (int r = rank - 1; r >= 0; r--)
            {
                var fallbackPath = Path.Combine(ranksDir, $"rank_{r}.css");
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
                var ranksDir = Path.Combine(_baseDirectory, "ranks", "css");
                var cssPath = Path.Combine(ranksDir, $"rank_{rank}.css");

                if (File.Exists(cssPath))
                {
                    return await File.ReadAllTextAsync(cssPath);
                }

                for (int r = rank - 1; r >= 0; r--)
                {
                    var fallbackPath = Path.Combine(ranksDir, $"rank_{r}.css");
                    if (File.Exists(fallbackPath))
                    {
                        Debug.WriteLine($"[WebServer] Ранг {rank} не найден, используем rank_{r}.css");
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

        public void Dispose()
        {
            Stop();
            _listener?.Close();
            _cts?.Dispose();
        }
    }
}