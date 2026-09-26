using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.WebServer.Handlers
{
    /// <summary>
    /// Обработчик SSE-потока основного чата (/stream).
    /// На этом шаге умеет только принимать подключения.
    /// Логика broadcast и форматирования остаётся в WebServerService
    /// и будет перенесена на следующих подшагах.
    /// </summary>
    class ChatStreamHandler
    {
        private readonly SseClientManager _streamManager;

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
    }
}