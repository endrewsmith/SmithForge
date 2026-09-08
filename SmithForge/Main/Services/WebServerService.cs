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
        public event EventHandler<string> RawSsePayloadReady; // Событие для отправки готового JSON в стрим

        private readonly Dictionary<string, string> _infoPageCache = new();
        private readonly List<HttpListenerContext> _infoStreamClients = new();
        private readonly object _infoLock = new object();
        private readonly string _infoPagesDir;
        private readonly string _infoWebDir;

        public static WebServerService? Instance { get; private set; }

        // ✅ КЕШ ИЗОБРАЖЕНИЙ
        private readonly Dictionary<string, byte[]> _imageCache = new();
        private readonly object _imageCacheLock = new object();
        private const int MAX_CACHE_IMAGES = 100;

        public WebServerService(int port = 10881)
        {
            _port = port;
            Instance = this;
            _baseDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "WebOverlay");

            // ============================================================
            // ИНФОРМАЦИОННЫЙ ЧАТ - ПУТИ
            // ============================================================

            _infoWebDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "InfoWeb");

            _infoPagesDir = Path.Combine(
                _infoWebDir, "Pages");

            // Создаем папки если их нет
            Directory.CreateDirectory(_infoWebDir);
            Directory.CreateDirectory(_infoPagesDir);

            // Создаем дефолтные страницы если их нет
            EnsureInfoPagesExist();
        }

        private void EnsureInfoPagesExist()
        {
            // Главная страница
            string helpPath = Path.Combine(_infoPagesDir, "help.html");
            if (!File.Exists(helpPath))
            {
                File.WriteAllText(helpPath, DefaultHelpPage, Encoding.UTF8);
                Debug.WriteLine($"[WebServer] ✅ Создана help.html");
            }

            // Страница форматирования
            string formattingPath = Path.Combine(_infoPagesDir, "formatting.html");
            if (!File.Exists(formattingPath))
            {
                File.WriteAllText(formattingPath, DefaultFormattingPage, Encoding.UTF8);
                Debug.WriteLine($"[WebServer] ✅ Создана formatting.html");
            }

            // index.html для информационного чата
            string indexPath = Path.Combine(_infoWebDir, "index.html");
            if (!File.Exists(indexPath))
            {
                File.WriteAllText(indexPath, DefaultInfoIndexHtml, Encoding.UTF8);
                Debug.WriteLine($"[WebServer] ✅ Создан info/index.html");
            }
        }
        /// <summary>
        /// Отправить обновление чаттера в веб-оверлей
        /// </summary>
        public void SendChaterUpdate(Chater chater, DisplayMessageViewModel msgVm)
        {
            try
            {
                if (chater == null) return;

                // ✅ Проверяем, что аватарка существует
                if (string.IsNullOrEmpty(msgVm.AvatarPath) || !File.Exists(msgVm.AvatarPath))
                {
                    Debug.WriteLine($"[WebServer] ⚠️ Аватарка не найдена для {chater.EffectiveName}");
                    return;
                }

                Debug.WriteLine($"[WebServer] SendChaterUpdate вызван для {chater.EffectiveName}");
                Debug.WriteLine($"[WebServer] AvatarPath: {msgVm.AvatarPath}");

                // ✅ Отправляем через событие ТОЛЬКО если есть текст сообщения
                // Если это обновление аватарки (пустое сообщение) - пропускаем
                if (!string.IsNullOrEmpty(msgVm.MessageText))
                {
                    MessageAdded?.Invoke(this, msgVm);
                    Debug.WriteLine($"[WebServer] ✅ Отправлено обновление для {chater.EffectiveName}");
                }
                else
                {
                    // ✅ Для обновления аватарки отправляем специальное SSE-событие
                    SendAvatarUpdateOnly(chater);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка в SendChaterUpdate: {ex.Message}");
            }
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
                var rankCss = GetRankCssContent(chater.Rank).GetAwaiter().GetResult();

                var updateData = new
                {
                    type = "avatar_update", // Фронтенд поймет, что это НЕ сообщение
                    userId = chater.Id,
                    displayName = chater.EffectiveName,
                    messageText = "", // Текста нет
                    avatarPath = avatarPath,
                    userRank = chater.Rank,
                    rankDisplay = GetRankDisplay(chater.Rank),
                    rankClass = GetRankClass(chater.Rank),
                    rankCss = rankCss,
                    rankTemplate = GetRankTemplate(chater.Rank),
                    karmaKey = chater.KarmaKeyDisplay,
                    karma = chater.KarmaDisplay,
                    messageCount = chater.MessageCount,
                    timestamp = DateTime.Now.ToString("HH:mm:ss")
                };

                var json = System.Text.Json.JsonSerializer.Serialize(updateData);
                var data = $"data: {json}\n\n";

                // ✅ Вместо MessageAdded вызываем специализированное событие со строкой данных
                RawSsePayloadReady?.Invoke(this, data);

                Debug.WriteLine($"[WebServer] ✅ Отправлено обновление аватарки для {chater.EffectiveName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка отправки обновления аватарки: {ex.Message}");
            }
        }


        /// <summary>
        /// Отправить обновление аватарки в веб-оверлей
        /// </summary>
        /// <summary>
        /// Отправить обновление аватарки в веб-оверлей
        /// </summary>
        public void SendAvatarUpdate(Chater chater, DisplayMessageViewModel msgVm)
        {
            if (chater == null) return;

            try
            {
                Debug.WriteLine($"[WebServer] Отправка обновления аватарки для {chater.EffectiveName}");

                // ✅ Проверяем, что аватарка существует
                if (string.IsNullOrEmpty(msgVm.AvatarPath) || !File.Exists(msgVm.AvatarPath))
                {
                    Debug.WriteLine($"[WebServer] ⚠️ Аватарка не найдена: {msgVm.AvatarPath}");
                    return;
                }

                // ✅ НЕ вызываем MessageAdded для пустых сообщений
                // Вместо этого отправляем SSE событие напрямую через MessageAdded с типом avatar_update
                // Но для этого нужно, чтобы в JavaScript обрабатывался этот тип

                // ✅ Отправляем через SSE напрямую, используя существующий механизм
                // Создаём сообщение с текстом-маркером, который JavaScript обработает как обновление
                var tempMsg = new DisplayMessageViewModel(chater, new CommonMessage
                {
                    Message = "🔄",  // ← Специальный маркер для обновления
                    Type = "avatar_update",
                    MessageNumber = 0
                });
                tempMsg.RefreshAvatar();

                // ✅ Теперь добавляем в историю и отправляем
                // Добавляем с маркером, но потом JavaScript скроет его
                lock (_lockObject)
                {
                    _messages.Add(tempMsg);
                    if (_messages.Count > 100)
                    {
                        _messages.RemoveAt(0);
                    }
                }

                MessageAdded?.Invoke(this, tempMsg);

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

                if (path.StartsWith("/info/css/") || path.StartsWith("/info/js/") || path.StartsWith("/info/images/"))
                {
                    await ServeInfoStaticFileAsync(context, path);
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

                // Проверяем через EmojiService
                if (EmojiService.EmojiExists(fullCode))
                {
                    var emojiInfo = EmojiService.GetEmojiInfo(fullCode);
                    if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.ImagePath))
                    {
                        return $"<img src='/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                    }
                }

                // Проверяем файл напрямую
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

            // ✅ 3. HTML теги
            text = Regex.Replace(text, @"\[b\](.*?)\[/b\]", "<b>$1</b>");
            text = Regex.Replace(text, @"\[i\](.*?)\[/i\]", "<i>$1</i>");
            text = Regex.Replace(text, @"\[color=(.*?)\](.*?)\[/color\]", "<span style='color:$1'>$2</span>");

            // ✅ 4. Заменяем переносы строк
            text = text.Replace("\n", "<br>");

            return text;
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
                        // ✅ Конвертируем эмодзи в HTML
                        string formattedText = GetFormattedMessageForWeb(msg.MessageText);

                        var rankTemplate = GetRankTemplate(msg.UserRank);
                        var rankCss = await GetRankCssContent(msg.UserRank);

                        var json = JsonSerializer.Serialize(new
                        {
                            type = "chat_message",
                            id = msg.Id,
                            displayName = msg.DisplayName,
                            messageText = formattedText,
                            formattedMessage = formattedText,
                            userRank = msg.UserRank,
                            rankDisplay = GetRankDisplay(msg.UserRank),
                            rankClass = GetRankClass(msg.UserRank),
                            rankCss = rankCss,
                            rankTemplate = rankTemplate,
                            avatarPath = msg.AvatarPath,
                            timestamp = DateTime.Now.ToString("HH:mm:ss"),
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
                        await response.OutputStream.WriteAsync(buffer);
                        await response.OutputStream.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebServer] Ошибка отправки сообщения из истории: {ex.Message}");
                    }
                }

                // ✅ Обработчик для обычных сообщений
                EventHandler<DisplayMessageViewModel> messageHandler = (s, msg) =>
                {
                    if (string.IsNullOrEmpty(msg.MessageText))
                        return;

                    Task.Run(async () =>
                    {
                        try
                        {
                            // ✅ Конвертируем эмодзи в HTML
                            string formattedText = GetFormattedMessageForWeb(msg.MessageText);

                            var rankTemplate = GetRankTemplate(msg.UserRank);
                            var rankCss = await GetRankCssContent(msg.UserRank);

                            var json = JsonSerializer.Serialize(new
                            {
                                type = "chat_message",
                                id = msg.Id,
                                displayName = msg.DisplayName,
                                messageText = formattedText,
                                formattedMessage = formattedText,
                                userRank = msg.UserRank,
                                rankDisplay = GetRankDisplay(msg.UserRank),
                                rankClass = GetRankClass(msg.UserRank),
                                rankCss = rankCss,
                                rankTemplate = rankTemplate,
                                avatarPath = msg.AvatarPath,
                                timestamp = DateTime.Now.ToString("HH:mm:ss"),
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

                            lock (response.OutputStream)
                            {
                                response.OutputStream.Write(buffer);
                                response.OutputStream.Flush();
                            }

                            Debug.WriteLine($"[WebServer] SSE отправлено сообщение: {msg.DisplayName}: {msg.MessageText}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[WebServer] Ошибка отправки: {ex.Message}");
                            connectionTaskSource.TrySetResult(true);
                        }
                    });
                };

                // ✅ Обработчик для обновлений аватарки
                EventHandler<string> avatarUpdateHandler = (s, rawData) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var buffer = Encoding.UTF8.GetBytes(rawData);

                            lock (response.OutputStream)
                            {
                                response.OutputStream.Write(buffer);
                                response.OutputStream.Flush();
                            }

                            Debug.WriteLine($"[WebServer] SSE отправлено обновление аватарки");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[WebServer] Ошибка отправки обновления аватарки: {ex.Message}");
                            connectionTaskSource.TrySetResult(true);
                        }
                    });
                };

                MessageAdded += messageHandler;
                RawSsePayloadReady += avatarUpdateHandler;

                using var registration = _cts?.Token.Register(() => connectionTaskSource.TrySetResult(true));

                await connectionTaskSource.Task;

                MessageAdded -= messageHandler;
                RawSsePayloadReady -= avatarUpdateHandler;
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

            // ✅ ЕСЛИ ТЕКСТ ПУСТОЙ - ПРОСТО ВЫХОДИМ, НЕ ВЫЗЫВАЕМ MessageAdded
            if (string.IsNullOrEmpty(msg.MessageText))
            {
                Debug.WriteLine($"[WebServer] ⏭ Пропущено сообщение с пустым текстом");
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

            // ✅ 1. Сначала конвертируем YouTube эмодзи :code:
            var emojiRegex = new Regex(@":([a-zA-Z0-9_-]+):");
            text = emojiRegex.Replace(text, match =>
            {
                string emojiCode = match.Groups[1].Value;
                string fullCode = $":{emojiCode}:";

                // Проверяем существование эмодзи через EmojiService
                if (EmojiService.EmojiExists(fullCode))
                {
                    var emojiInfo = EmojiService.GetEmojiInfo(fullCode);
                    if (emojiInfo != null && !string.IsNullOrEmpty(emojiInfo.ImagePath))
                    {
                        // Возвращаем HTML для веба
                        return $"<img src='/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                    }
                }

                // Проверяем в папке YouTube эмодзи напрямую
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

            // ✅ 2. Конвертируем Twitch эмодзи [code]
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

            // ✅ 3. Обрабатываем HTML теги
            text = Regex.Replace(text, @"\[b\](.*?)\[/b\]", "<b>$1</b>");
            text = Regex.Replace(text, @"\[i\](.*?)\[/i\]", "<i>$1</i>");
            text = Regex.Replace(text, @"\[color=(.*?)\](.*?)\[/color\]", "<span style='color:$1'>$2</span>");

            // ✅ 4. Заменяем переносы строк
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

        // ============================================================
        // ИНФОРМАЦИОННЫЙ ЧАТ - МЕТОДЫ
        // ============================================================

        private async Task ServeInfoPageAsync(HttpListenerContext context, string pageName)
        {
            var response = context.Response;

            // ============================================================
            // ✅ ДЛЯ index.html — читаем с диска, НЕ кешируем
            // ============================================================
            if (pageName == "index.html" || pageName == "index")
            {
                string indexPath = Path.Combine(_infoWebDir, "index.html");

                if (!File.Exists(indexPath))
                {
                    EnsureInfoPagesExist();
                }

                if (File.Exists(indexPath))
                {
                    string indexHtml = await File.ReadAllTextAsync(indexPath, Encoding.UTF8);
                    await SendHtmlResponse(response, indexHtml);
                    return;
                }
                else
                {
                    response.StatusCode = 404;
                    await SendHtmlResponse(response, "<h2>❌ index.html не найден</h2>");
                    return;
                }
            }

            // ============================================================
            // ✅ ДЛЯ СТРАНИЦ-СООБЩЕНИЙ (help, formatting, karma...)
            // ============================================================

            // 1️⃣ СНАЧАЛА ПРОВЕРЯЕМ КЕШ
            if (_infoPageCache.TryGetValue(pageName, out string? cachedHtml))
            {
                await SendHtmlResponse(response, cachedHtml);
                return;
            }

            // 2️⃣ ЕСЛИ НЕТ В КЕШЕ — ИЩЕМ НА ДИСКЕ
            string filePath = Path.Combine(_infoPagesDir, $"{pageName}.html");

            if (!File.Exists(filePath))
            {
                // 3️⃣ ЕСЛИ НЕТ НА ДИСКЕ — НИЧЕГО НЕ ВЫВОДИМ (404)
                response.StatusCode = 404;
                await SendHtmlResponse(response, "<h2>❌ Страница не найдена</h2>");
                return;
            }

            // 4️⃣ ЗАГРУЖАЕМ С ДИСКА
            string html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

            // Добавляем навигацию (только для страниц-сообщений)
            html = InjectInfoNavigation(html, pageName);

            // Кешируем
            _infoPageCache[pageName] = html;

            // Отправляем
            await SendHtmlResponse(response, html);

            // Уведомляем SSE клиентов об обновлении страницы
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
            var response = context.Response;

            response.Headers.Add("Content-Type", "text/event-stream");
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.StatusCode = 200;

            lock (_infoLock)
            {
                _infoStreamClients.Add(context);
            }

            Debug.WriteLine($"[WebServer] 📡 Инфо-клиент подключен. Всего: {_infoStreamClients.Count}");

            try
            {
                while (context.Request.InputStream.CanRead)
                {
                    await Task.Delay(1000);
                    if (!context.Request.InputStream.CanRead)
                        break;
                }
            }
            finally
            {
                lock (_infoLock)
                {
                    _infoStreamClients.Remove(context);
                }
                Debug.WriteLine($"[WebServer] 📡 Инфо-клиент отключен. Осталось: {_infoStreamClients.Count}");
                try { response.Close(); } catch { }
            }
        }

        private async Task ServeInfoStaticFileAsync(HttpListenerContext context, string path)
        {
            var response = context.Response;

            string relativePath = path.Substring("/info/".Length);
            string filePath = Path.Combine(_infoWebDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            var extension = Path.GetExtension(filePath).ToLower();
            response.ContentType = extension switch
            {
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };

            var bytes = await File.ReadAllBytesAsync(filePath);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }

        // Оставляем старый метод для обратной совместимости
        private async Task NotifyInfoClients(string pageName)
        {
            if (_infoStreamClients.Count == 0) return;

            var json = $"{{\"type\":\"page_update\",\"page\":\"{pageName}\"}}";
            var data = $"data: {json}\n\n";
            await NotifyInfoClientsRaw(data);
        }

        // ✅ НОВЫЙ МЕТОД: отправка произвольных данных
        private async Task NotifyInfoClientsRaw(string data)
        {
            if (_infoStreamClients.Count == 0) return;

            var bytes = Encoding.UTF8.GetBytes(data);
            var disconnected = new List<HttpListenerContext>();

            lock (_infoLock)
            {
                foreach (var client in _infoStreamClients)
                {
                    try
                    {
                        client.Response.OutputStream.Write(bytes);
                        client.Response.OutputStream.Flush();
                    }
                    catch
                    {
                        disconnected.Add(client);
                    }
                }

                foreach (var client in disconnected)
                {
                    _infoStreamClients.Remove(client);
                }
            }

            await Task.CompletedTask;
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
            lock (_infoLock)
            {
                _infoPageCache.Clear();
                Debug.WriteLine("[WebServer] 🗑 Кеш страниц очищен");
            }
        }

        // ============================================================
        // ДЕФОЛТНЫЕ СТРАНИЦЫ (в самом конце)
        // ============================================================

        private const string DefaultHelpPage = @"
<div class='page-root'>
    <h1>📚 Справочник команд SmithForge</h1>
    <p>Выберите раздел для просмотра:</p>
    
    <div class='page-grid'>
        <div class='page-card'>
            <span class='icon'>🎨</span>
            <a href='#' onclick='loadPage(""formatting"")'>Форматирование текста</a>
            <span class='desc'>Жирный, курсив, цвет</span>
        </div>
        <div class='page-card'>
            <span class='icon'>💰</span>
            <a href='#' onclick='loadPage(""karma"")'>Карма и ранги</a>
            <span class='desc'>Как заработать и тратить</span>
        </div>
        <div class='page-card'>
            <span class='icon'>📢</span>
            <a href='#' onclick='loadPage(""important"")'>Важные сообщения</a>
            <span class='desc'>Озвучивание в эфире</span>
        </div>
        <div class='page-card'>
            <span class='icon'>🎨</span>
            <a href='#' onclick='loadPage(""stickers"")'>Стикеры</a>
            <span class='desc'>Отправка стикеров</span>
        </div>
        <div class='page-card'>
            <span class='icon'>👤</span>
            <a href='#' onclick='loadPage(""profile"")'>Профиль</a>
            <span class='desc'>Аватарка и настройки</span>
        </div>
        <div class='page-card'>
            <span class='icon'>📋</span>
            <a href='#' onclick='loadPage(""commands"")'>Все команды</a>
            <span class='desc'>Полный список</span>
        </div>
    </div>
</div>";

        private const string DefaultFormattingPage = @"
<div class='page-formatting'>
    <h1>🎨 Форматирование текста</h1>
    <p>Украшайте свои сообщения!</p>
    
    <div class='command-list'>
        <div class='command'>
            <span class='icon'>𝐁</span>
            <div class='command-info'>
                <span class='name'>!!bold</span>
                <span class='aliases'>!!b, !!ж, !!жирный</span>
                <span class='desc'>Сделать текст жирным</span>
                <span class='cost'>💰 2 кармы</span>
            </div>
        </div>
        <div class='command'>
            <span class='icon'>𝘐</span>
            <div class='command-info'>
                <span class='name'>!!italic</span>
                <span class='aliases'>!!i, !!к, !!курсив</span>
                <span class='desc'>Сделать текст курсивом</span>
                <span class='cost'>💰 2 кармы</span>
            </div>
        </div>
        <div class='command'>
            <span class='icon'>🎨</span>
            <div class='command-info'>
                <span class='name'>!!color</span>
                <span class='aliases'>!!c, !!цвет</span>
                <span class='desc'>Покрасить текст</span>
                <span class='cost'>💰 3 кармы</span>
            </div>
        </div>
        <div class='command'>
            <span class='icon'>⏱️</span>
            <div class='command-info'>
                <span class='name'>!!extend</span>
                <span class='aliases'>!!e, !!продлить</span>
                <span class='desc'>Увеличить время показа</span>
                <span class='cost'>💰 1-10 кармы</span>
            </div>
        </div>
    </div>
</div>";

        private const string DefaultInfoIndexHtml = """
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>📚 Инфо-чат SmithForge</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', sans-serif;
            background: transparent;
            color: #eee;
            height: 100vh;
            display: flex;
            flex-direction: column;
            overflow: hidden;
        }

        #chat-container {
            flex: 1;
            overflow-y: auto;
            padding: 10px 15px;
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        .chat-spacer {
            height: 100%;
            flex-shrink: 0;
            background: transparent;
            pointer-events: none;
        }

        /* ✅ Базовый класс без анимации (анимация будет добавляться через JS) */
        .message {
            background: rgba(26, 26, 46, 0.92);
            border: 1px solid rgba(45, 45, 68, 0.6);
            border-radius: 12px;
            padding: 14px 18px;
            max-width: 98%;
            flex-shrink: 0;
            backdrop-filter: blur(6px);
            box-shadow: 0 4px 20px rgba(0,0,0,0.4);
            opacity: 0;
            transform: translateY(20px);
            /* animation задается через JS */
        }

        .message .msg-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 8px;
            font-size: 11px;
            color: #888;
        }
        .message .msg-header .page-name {
            color: #4caf50;
            font-size: 10px;
            background: rgba(76, 175, 80, 0.15);
            padding: 2px 10px;
            border-radius: 12px;
        }
        .message .msg-header .timestamp {
            color: #555;
            font-size: 10px;
        }
        .message .msg-body {
            font-size: 13px;
            line-height: 1.5;
            color: #ddd;
        }

        .message .msg-body h1 { font-size: 16px; color: #ffd700; margin-bottom: 6px; }
        .message .msg-body h2 { font-size: 14px; color: #ffd700; margin-bottom: 4px; }
        .message .msg-body p { margin-bottom: 4px; color: #bbb; }
        .message .msg-body .page-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; }
        .message .msg-body .page-card {
            background: rgba(45, 45, 68, 0.5);
            padding: 10px;
            border-radius: 6px;
            text-align: center;
        }
        .message .msg-body .page-card .icon { font-size: 20px; display: block; }
        .message .msg-body .page-card .name { color: #ffd700; font-weight: bold; font-size: 12px; }
        .message .msg-body .page-card .desc { color: #aaa; font-size: 10px; }
        .message .msg-body .command-list .command {
            display: flex;
            align-items: center;
            background: rgba(45, 45, 68, 0.4);
            padding: 5px 10px;
            margin: 3px 0;
            border-radius: 4px;
            gap: 8px;
            flex-wrap: wrap;
        }
        .message .msg-body .command-list .command .icon { font-size: 16px; }
        .message .msg-body .command-list .command .name { color: #ffd700; font-weight: bold; font-size: 12px; }
        .message .msg-body .command-list .command .aliases { color: #888; font-size: 10px; }
        .message .msg-body .command-list .command .desc { color: #ccc; font-size: 12px; flex: 1; }
        .message .msg-body .command-list .command .cost { color: #4caf50; font-size: 10px; }
        .message .msg-body .info-nav { display: none; }
        .message .msg-body .error { color: #ff6b6b; text-align: center; padding: 10px; }

        @keyframes slideUp {
            0% {
                opacity: 0;
                transform: translateY(20px);
            }
            100% {
                opacity: 1;
                transform: translateY(0);
            }
        }

        #chat-container::-webkit-scrollbar { width: 3px; }
        #chat-container::-webkit-scrollbar-track { background: transparent; }
        #chat-container::-webkit-scrollbar-thumb { background: rgba(45, 45, 68, 0.6); border-radius: 2px; }
    /* ============================================================
   СТИКЕРЫ
   ============================================================ */

/* Список паков */
.pack-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 12px;
    margin: 10px 0;
}

.pack-card {
    background: rgba(45, 45, 68, 0.5);
    border-radius: 10px;
    padding: 12px;
    text-align: center;
    transition: transform 0.2s;
}
.pack-card:hover {
    transform: scale(1.02);
}

.pack-preview img {
    width: 80px;
    height: 80px;
    object-fit: contain;
    border-radius: 8px;
    background: rgba(0,0,0,0.3);
}

.pack-info {
    margin-top: 8px;
}
.pack-name {
    display: block;
    font-weight: bold;
    color: #ffd700;
}
.pack-count {
    display: block;
    font-size: 11px;
    color: #888;
}
.pack-link {
    display: inline-block;
    margin-top: 6px;
    padding: 4px 12px;
    background: #4caf50;
    color: #fff;
    border-radius: 4px;
    text-decoration: none;
    font-size: 12px;
}
.pack-link:hover {
    background: #66bb6a;
}

/* Список стикеров в паке */
.sticker-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
    gap: 10px;
    margin: 10px 0;
}

.sticker-card {
    background: rgba(45, 45, 68, 0.5);
    border-radius: 8px;
    padding: 10px;
    text-align: center;
}

.sticker-preview {
    position: relative;
}
.sticker-preview img {
    width: 80px;
    height: 80px;
    object-fit: contain;
}

.sticker-badge {
    position: absolute;
    top: -4px;
    right: -4px;
    font-size: 10px;
    background: #ff6b6b;
    padding: 1px 6px;
    border-radius: 8px;
}

.sticker-info {
    margin-top: 6px;
}
.sticker-id {
    display: block;
    font-size: 11px;
    color: #888;
}
.sticker-command {
    display: block;
    font-size: 10px;
    color: #4caf50;
    font-family: monospace;
    background: rgba(0,0,0,0.3);
    padding: 2px 6px;
    border-radius: 4px;
    margin-top: 4px;
}

/* Кнопка назад */
.back-link {
    display: inline-block;
    margin-top: 12px;
    color: #ffd700;
    text-decoration: none;
}
.back-link:hover {
    text-decoration: underline;
}

	</style>
</head>
<body>

    <div id="chat-container"></div>

    <script>
        const chatContainer = document.getElementById('chat-container');

        // ============================================================
        // НАСТРОЙКИ
        // ============================================================
        const MAX_MESSAGES = 2;

        // ============================================================
        // ТЕКУЩАЯ СКОРОСТЬ СКРОЛЛА (по умолчанию 1500ms)
        // ============================================================
        let currentScrollSpeed = 1500;

        // ============================================================
        // ТЕКУЩАЯ СКОРОСТЬ ПОЯВЛЕНИЯ (по умолчанию 0.3s)
        // ============================================================
        let currentAppearSpeed = 0.3;

        // ============================================================
        // РАСПОРКА (spacer)
        // ============================================================
        function initSpacer() {
            const spacer = document.createElement('div');
            spacer.className = 'chat-spacer';
            chatContainer.appendChild(spacer);
        }
        initSpacer();

        // ============================================================
        // КАСТОМНЫЙ СКРОЛЛ С ИСПОЛЬЗОВАНИЕМ currentScrollSpeed
        // ============================================================
        function smoothScrollToBottom(element, duration) {
            const start = element.scrollTop;
            const target = element.scrollHeight - element.clientHeight;
            const change = target - start;

            if (change <= 0) return;

            let startTime = null;

            function animateScroll(currentTime) {
                if (!startTime) startTime = currentTime;
                const timeElapsed = currentTime - startTime;
                const progress = Math.min(timeElapsed / duration, 1);
                const ease = progress * (2 - progress);
                element.scrollTop = start + change * ease;

                if (timeElapsed < duration) {
                    requestAnimationFrame(animateScroll);
                }
            }
            requestAnimationFrame(animateScroll);
        }

        // ============================================================
        // ОБНОВЛЕНИЕ СКОРОСТИ СКРОЛЛА ИЗ SSE
        // ============================================================
        function updateScrollSpeed(speed) {
            currentScrollSpeed = speed;
            console.log(`🏃 Скорость скролла обновлена: ${speed}ms`);
        }

        // ============================================================
        // ОБНОВЛЕНИЕ СКОРОСТИ ПОЯВЛЕНИЯ ИЗ SSE
        // ============================================================
        function updateAppearSpeed(speed) {
            currentAppearSpeed = speed;
            console.log(`✨ Скорость появления обновлена: ${speed}s`);
        }

        // ============================================================
        // ДОБАВЛЕНИЕ СООБЩЕНИЯ (использует currentScrollSpeed и currentAppearSpeed)
        // ============================================================
        function addMessage(pageName, html) {
    const msgDiv = document.createElement('div');
    msgDiv.className = 'message';

    const timestamp = new Date().toLocaleTimeString();

    msgDiv.innerHTML = `
        <div class="msg-header">
            <span class="page-name">📄 ${pageName}</span>
            <span class="timestamp">${timestamp}</span>
        </div>
        <div class="msg-body">${html}</div>
    `;

    // ✅ Применяем анимацию с текущей скоростью появления
    msgDiv.style.animation = `slideUp ${currentAppearSpeed}s ease-out forwards`;

    chatContainer.appendChild(msgDiv);

    // ✅ ИСПРАВЛЕННАЯ ОЧИСТКА: выбираем только элементы с классом .message
    // Распорка (.chat-spacer) в этот список не попадет и останется нетронутой
    const messages = chatContainer.getElementsByClassName('message');

    while (messages.length > MAX_MESSAGES) {
        messages[0].remove(); // Удаляем самое старое текстовое сообщение
    }

    // ✅ Используем текущую скорость скролла
    requestAnimationFrame(() => {
        smoothScrollToBottom(chatContainer, currentScrollSpeed);
    });
}


        let es = null; 
        // ============================================================
        // SSE ПОДКЛЮЧЕНИЕ (с обработкой скорости)
        // ============================================================
        function connectInfoStream() {
            const es = new EventSource('/info/stream');

            es.onmessage = function(e) {
                try {
                    const data = JSON.parse(e.data);

                    if (data.type === 'info_message') {
                        addMessage(data.pageName, data.html);
                    }

                    // ✅ Обработка скорости скролла
                    if (data.type === 'scroll_speed') {
                        updateScrollSpeed(data.speed);
                    }

                    // ✅ Обработка скорости появления
                    if (data.type === 'appear_speed') {
                        updateAppearSpeed(data.speed);
                    }
                } catch (ex) {
                    console.error('SSE error:', ex);
                }
            };

            es.onerror = function() {
                setTimeout(connectInfoStream, 3000);
            };
        }

        // ============================================================
        // СТАРТ
        // ============================================================
        connectInfoStream();
        console.log('📚 Информационный чат для OBS запущен! Жду команды...');
        console.log(`🏃 Скорость скролла: ${currentScrollSpeed}ms`);
        console.log(`✨ Скорость появления: ${currentAppearSpeed}s`);

		// Закрываем SSE-соединение перед перезагрузкой или закрытием страницы
window.addEventListener('beforeunload', () => {
    if (typeof es !== 'undefined' && es) {
        es.close();
        console.log('SSE соединение принудительно закрыто перед обновлением.');
    }
});
    </script>
</body>
</html>
""";

    }
}