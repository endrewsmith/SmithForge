using System;
using System.Diagnostics;
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

        public InfoStreamHandler(SseClientManager infoManager)
        {
            _infoManager = infoManager ?? throw new ArgumentNullException(nameof(infoManager));
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