using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.WebServer.Handlers
{
    /// <summary>
    /// Обработчик SSE-потока информационного чата (/info/stream).
    /// На этом шаге умеет только принимать подключения.
    /// Логика broadcast и отдачи страниц будет перенесена на следующих подшагах.
    /// </summary>
    class InfoStreamHandler
    {
        private readonly SseClientManager _infoManager;
        private readonly string _infoPagesDir;
        private readonly Dictionary<string, string> _infoPageCache;

        public InfoStreamHandler(
            SseClientManager infoManager,
            string infoPagesDir,
            Dictionary<string, string> infoPageCache)
        {
            _infoManager = infoManager ?? throw new ArgumentNullException(nameof(infoManager));
            _infoPagesDir = infoPagesDir ?? throw new ArgumentNullException(nameof(infoPagesDir));
            _infoPageCache = infoPageCache ?? throw new ArgumentNullException(nameof(infoPageCache));
        }

        /// <summary>
        /// Принять SSE-подключение клиента информационного чата.
        /// </summary>
        public async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken serverToken)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[WebServer] 📡 Info-клиент {client.Id} подключён");

            _infoManager.Add(client);

            try
            {
                // Приветственный пакет, чтобы браузер сразу понял, что соединение живое
                //await client.SendAsync($"data: {{\"type\":\"hello\",\"ts\":\"{DateTime.Now:HH:mm:ss}\"}}\n\n");
                await client.SendAsync(": ping\n\n");

                // Ждём, пока клиент отключится ИЛИ сервер остановится (Dispose/Stop)
                await client.WaitUntilClosedAsync(serverToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Info-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _infoManager.Remove(client);
            }
        }

        /// <summary>
        /// Текущее количество подключённых клиентов.
        /// </summary>
        public int Count => _infoManager.Count;

        /// <summary>
        /// Уведомить клиентов о смене страницы.
        /// </summary>
        public async Task NotifyInfoClients(string pageName)
        {
            if (_infoManager.Count == 0) return;

            var json = $"{{\"type\":\"page_update\",\"page\":\"{pageName}\"}}";
            var data = $"data: {json}\n\n";
            await NotifyInfoClientsRaw(data);
        }

        /// <summary>
        /// Отправить произвольные SSE-данные всем info-клиентам.
        /// </summary>
        public async Task NotifyInfoClientsRaw(string data)
        {
            await _infoManager.BroadcastAsync(data);
        }

        /// <summary>
        /// Отправить HTML-сообщение в информационный чат (для OBS).
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

        // ============================================================
        // ОТДАЧА HTML-СТРАНИЦ
        // ============================================================

        /// <summary>
        /// Отдать HTML-страницу информационного чата (или контент раздела).
        /// </summary>
        public async Task ServeInfoPageAsync(HttpListenerContext context, string pageName)
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

        /// <summary>
        /// Тонкая обёртка — делегирует в ServeInfoPageAsync.
        /// </summary>
        public async Task HandleInfoPageRequestAsync(HttpListenerContext context, string pageName)
        {
            await ServeInfoPageAsync(context, pageName);
        }

        /// <summary>
        /// Поиск по страницам справочника. Отдаёт HTML с результатами.
        /// </summary>
        public async Task HandleInfoSearchRequestAsync(HttpListenerContext context, string query)
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
        // ============================================================
        // РЕНДЕРИНГ HTML-СТРАНИЦ (пока не используется)
        // ============================================================

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
    }
}