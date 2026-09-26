using System;
using System.Diagnostics;
using System.Net;
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
    }
}