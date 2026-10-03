using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
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
    /// Обработчик SSE-потока основного чата (/stream).
    /// Принимает подключения, рассылает сообщения и форматирует их для веба.
    /// </summary>
    class ChatStreamHandler
    {
        private readonly SseClientManager _streamManager;

        private static string RanksHtmlDir =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html", "Ranks", "html");

        private static string RanksCssDir =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Html", "Ranks", "css");

        public ChatStreamHandler(SseClientManager streamManager)
        {
            _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        }

        /// <summary>
        /// Принять SSE-подключение клиента основного чата.
        /// </summary>
        public async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken serverToken)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[WebServer] 💬 Stream-клиент {client.Id} подключён");

            _streamManager.Add(client);

            try
            {
                // Приветствие — чтобы браузер сразу понял, что соединение живо
                //await client.SendAsync($"data: {{\"type\":\"hello\",\"ts\":\"{DateTime.Now:HH:mm:ss}\"}}\n\n");
                await client.SendAsync(": ping\n\n");

                // Ждём отключения клиента или остановки сервера
                await client.WaitUntilClosedAsync(serverToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Stream-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _streamManager.Remove(client);
            }
        }

        /// <summary>
        /// Разослать произвольные SSE-данные всем клиентам /stream.
        /// Используется для avatar_update и других спец-сообщений.
        /// </summary>
        public void BroadcastRawSse(string sseData)
        {
            _streamManager.Broadcast(sseData);
        }
        /// <summary>
        /// Закрыть все SSE-соединения /stream.
        /// </summary>
        public void CloseAll()
        {
            _streamManager.CloseAll();
        }
        /// <summary>
        /// Разослать сообщение чата всем SSE-клиентам /stream.
        /// </summary>
        public void BroadcastChatMessage(DisplayMessageViewModel msg)
        {
            if (msg == null) return;

            string json;
            try
            {
                string formattedText = GetFormattedMessageForWeb(msg.MessageText);

                var rankTemplate = GetRankTemplate(msg.UserRank);
                var rankCss = GetRankCssContent(msg.UserRank).GetAwaiter().GetResult();

                json = System.Text.Json.JsonSerializer.Serialize(new
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
                    avatarPath = string.IsNullOrEmpty(msg.AvatarPath)
    ? null
    : $"http://localhost:{WebServerService.StaticPort}/avatar/{Path.GetFileName(msg.AvatarPath)}",
                    timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    karmaKey = msg.User?.KarmaKeyDisplay ?? "",
                    karma = msg.User?.KarmaDisplay ?? "",
                    messageNumber = msg.MessageNumber,
                    messageCount = msg.MessageCount,
                    likes = GetLikesCountForMessage(msg.MessageNumber),
                    dislikes = GetDislikesCountForMessage(msg.MessageNumber),
                    platform = msg.Type?.ToLower() ?? "twitch"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка формирования chat_message JSON: {ex.Message}");
                return;
            }

            BroadcastRawSse($"data: {json}\n\n");
        }

        // ============================================================
        // ФОРМАТИРОВАНИЕ СООБЩЕНИЙ (пока не используется)
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
                        return $"<img src='http://localhost:{WebServerService.StaticPort}/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
                    }
                }

                string emojiPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Emojis", "YouTube", "Images",
                    $"{emojiCode}.png");

                if (File.Exists(emojiPath))
                {
                    return $"<img src='http://localhost:{WebServerService.StaticPort}/emoji/{emojiCode}.png' class='emoji youtube-emoji' alt='{emojiCode}' title='{emojiCode}' />";
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
                        return $"<img src='http://localhost:{WebServerService.StaticPort}/emoji/{emojiCode}.png' class='emoji twitch-emoji' alt='{emojiCode}' title='{emojiCode}' />";
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

        // ============================================================
        // РАНГИ (пока не используется)
        // ============================================================

        public string GetRankDisplay(int rank)
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

        public string GetRankClass(int rank)
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

        public async Task<string?> GetRankCssContent(int rank)
        {
            if (rank == 0)
                return null;

            try
            {
                var cssPath = Path.Combine(RanksCssDir, $"rank_{rank}.css");

                if (File.Exists(cssPath))
                    return await File.ReadAllTextAsync(cssPath).ConfigureAwait(false);

                for (int r = rank - 1; r >= 0; r--)
                {
                    var fallbackPath = Path.Combine(RanksCssDir, $"rank_{r}.css");
                    if (File.Exists(fallbackPath))
                    {
                        Debug.WriteLine($"[WebServer] CSS ранга {rank} не найден, используем rank_{r}.css");
                        return await File.ReadAllTextAsync(fallbackPath).ConfigureAwait(false);
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

        public string GetRankTemplate(int rank)
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
        // РЕАКЦИИ (пока не используется)
        // ============================================================

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
    }
}