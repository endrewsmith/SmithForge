using SmithForge.Main.Services;
using SmithForge.Main.Services.WebServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Features.StatsRotation.Handlers
{
    internal sealed class StatsStreamHandler
    {
        private readonly SseClientManager _statsManager;

        public StatsStreamHandler(SseClientManager statsManager)
        {
            _statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));
        }

        public int ClientCount => _statsManager.Count;

        // ============================================================
        // 1. ПОДКЛЮЧЕНИЕ КЛИЕНТА SSE
        // ============================================================
        public async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken serverToken)
        {
            using var client = new SseClient(context, heartbeatIntervalMs: 15000);
            Debug.WriteLine($"[Stats] Stats-клиент {client.Id} подключён");

            _statsManager.Add(client);

            try
            {
                await client.SendAsync(": ping\n\n");
                await client.WaitUntilClosedAsync(serverToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Stats] Stats-клиент {client.Id} ошибка: {ex.Message}");
            }
            finally
            {
                _statsManager.Remove(client);
            }
        }

        // ============================================================
        // 2. ПОКАЗАТЬ БЛОК СТАТИСТИКИ
        // ============================================================
        public void ShowStats(IStatsRule rule, IReadOnlyList<StatsEntry> entries)
        {
            if (rule == null || entries == null || entries.Count == 0) return;

            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = entries[i];

                string? avatarUrl = null;
                if (!string.IsNullOrEmpty(e.AvatarPath))
                {
                    var fileName = Path.GetFileName(e.AvatarPath);
                    avatarUrl = $"http://localhost:{WebServerService.StaticPort}/avatar/{fileName}";
                }

                sb.Append(JsonSerializer.Serialize(new
                {
                    rank = e.Rank,
                    displayName = e.DisplayName,
                    valueText = e.ValueText,
                    avatarUrl,
                    userRank = e.UserRank,
                    platform = e.Platform
                }));
            }
            sb.Append(']');

            var json =
                $"{{\"type\":\"stats_show\"," +
                $"\"ruleKey\":{JsonSerializer.Serialize(rule.Key)}," +
                $"\"title\":{JsonSerializer.Serialize(rule.Title)}," +
                $"\"duration\":{rule.DisplayDurationSeconds}," +
                $"\"entries\":{sb}}}";

            _statsManager.Broadcast($"data: {json}\n\n");
            Debug.WriteLine($"[Stats] 📤 {rule.Key} → {_statsManager.Count} клиентов");
        }

        // ============================================================
        // 3. СКРЫТЬ БЛОК СТАТИСТИКИ
        // ============================================================
        public void HideStats()
        {
            _statsManager.Broadcast("data: {\"type\":\"stats_hide\"}\n\n");
        }

        // ============================================================
        // 4. ЗАКРЫТЬ ВСЕ СОЕДИНЕНИЯ
        // ============================================================
        public void CloseAll()
        {
            _statsManager.CloseAll();
        }
    }
}