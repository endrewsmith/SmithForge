using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Обёртка над одним SSE-соединением (Server-Sent Events).
    /// Управляет жизненным циклом: heartbeat, отмена, безопасное закрытие.
    /// </summary>
    internal sealed class SseClient : IDisposable
    {
        private readonly HttpListenerContext _context;
        private readonly CancellationTokenSource _cts;
        private readonly Timer _heartbeatTimer;
        private readonly object _writeLock = new object();
        private bool _disposed;

        /// <summary>
        /// Уникальный ID клиента (для логов).
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// Указывает, живо ли соединение.
        /// </summary>
        public bool IsAlive { get; private set; } = true;

        public SseClient(HttpListenerContext context, int heartbeatIntervalMs = 15000)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cts = new CancellationTokenSource();

            // Ставим SSE-заголовки
            var response = _context.Response;
            response.Headers.Add("Content-Type", "text/event-stream");
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("X-Accel-Buffering", "no"); // отключает буферизацию в nginx/прокси
            response.StatusCode = 200;

            // Запускаем heartbeat
            _heartbeatTimer = new Timer(
                callback: _ => SendHeartbeat(),
                state: null,
                dueTime: heartbeatIntervalMs,
                period: heartbeatIntervalMs);
        }

        /// <summary>
        /// Отправить произвольные SSE-данные (уже включая "data: ...\n\n").
        /// Возвращает false, если соединение мертво.
        /// </summary>
        public bool Send(string sseData)
        {
            if (_disposed || !IsAlive) return false;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(sseData);
                lock (_writeLock)
                {
                    if (_disposed || !IsAlive) return false;
                    _context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                    _context.Response.OutputStream.Flush();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SseClient {Id}] Send failed: {ex.Message}");
                MarkDead();
                return false;
            }
        }

        /// <summary>
        /// То же, что Send, но с await (используйте в async-методах).
        /// </summary>
        public async Task<bool> SendAsync(string sseData, CancellationToken externalToken = default)
        {
            if (_disposed || !IsAlive) return false;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(sseData);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
                await _context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, linked.Token);
                await _context.Response.OutputStream.FlushAsync(linked.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                MarkDead();
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SseClient {Id}] SendAsync failed: {ex.Message}");
                MarkDead();
                return false;
            }
        }

        /// <summary>
        /// Ждать, пока клиент не отключится или не будет отменён внешний токен.
        /// Используется внутри обработчиков SSE.
        /// </summary>
        public async Task WaitUntilClosedAsync(CancellationToken externalToken = default)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, externalToken);
                await Task.Delay(Timeout.Infinite, linked.Token);
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
            }
        }

        private void SendHeartbeat()
        {
            if (_disposed || !IsAlive) return;
            Send(": ping\n\n"); // SSE-комментарий, не доставляется в onmessage
        }

        private void MarkDead()
        {
            IsAlive = false;
            try { _cts.Cancel(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsAlive = false;

            try { _heartbeatTimer?.Dispose(); } catch { }
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            try { _context?.Response?.Close(); } catch { }
        }
    }
}