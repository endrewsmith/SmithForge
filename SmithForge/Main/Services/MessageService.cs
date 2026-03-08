using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.Main.Models;
using System.Diagnostics;
using System.IO;

namespace SmithForge.Main.Services
{
    public static class MessageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task StartListeningAsync(
            string url,
            Action<CommonMessage> onMessageReceived,
            CancellationToken token,
            Func<bool> isRunning) // Передаем проверку: жива ли еще Java
        {
            Debug.WriteLine("[WS] Служба прослушивания запущена.");

            while (!token.IsCancellationRequested && isRunning())
            {
                using var client = new ClientWebSocket();
                try
                {
                    Debug.WriteLine($"[WS] Попытка подключения к {url}...");

                    // Тайм-аут на подключение 2 секунды
                    using (var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, connectCts.Token))
                    {
                        await client.ConnectAsync(new Uri(url), linkedCts.Token);
                    }

                    Debug.WriteLine("[WS] СОЕДИНЕНИЕ УСТАНОВЛЕНО!");

                    byte[] buffer = new byte[1024 * 4];

                    while (client.State == WebSocketState.Open && !token.IsCancellationRequested && isRunning())
                    {
                        using var ms = new MemoryStream();
                        WebSocketReceiveResult result;

                        do
                        {
                            result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            ms.Write(buffer, 0, result.Count);
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        ms.Seek(0, SeekOrigin.Begin);
                        using var reader = new StreamReader(ms, Encoding.UTF8);
                        string json = await reader.ReadToEndAsync();

                        try
                        {
                            var msg = JsonSerializer.Deserialize<CommonMessage>(json, JsonOptions);
                            if (msg != null) onMessageReceived(msg);
                        }
                        catch (JsonException ex) { Debug.WriteLine($"[WS] Ошибка JSON: {ex.Message}"); }
                    }
                }
                catch (Exception ex)
                {
                    // Если процесс уже выключен, выходим без пауз и логов
                    if (!isRunning() || token.IsCancellationRequested) break;

                    Debug.WriteLine($"[WS] Реконнект через 2 сек... ({ex.Message})");
                    await Task.Delay(2000, token);
                }
            }

            Debug.WriteLine("[WS] Прослушивание ПОЛНОСТЬЮ ОСТАНОВЛЕНО.");
        }
    }
}
