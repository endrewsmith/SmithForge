using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services
{
    internal sealed class SseClient : IDisposable
    {
        private readonly HttpListenerContext _context;
        private readonly CancellationTokenSource _cts;
        private readonly Timer _heartbeatTimer;
        private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
        private bool _disposed;

        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public bool IsAlive { get; private set; } = true;

        public SseClient(HttpListenerContext context, int heartbeatIntervalMs = 15000)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cts = new CancellationTokenSource();

            var response = _context.Response;
            response.Headers.Add("Content-Type", "text/event-stream");
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            response.Headers.Add("X-Accel-Buffering", "no");
            response.StatusCode = 200;

            _heartbeatTimer = new Timer(
                callback: _ => SendHeartbeat(),
                state: null,
                dueTime: heartbeatIntervalMs,
                period: heartbeatIntervalMs);
        }

        public bool Send(string sseData)
        {
            if (_disposed || !IsAlive) return false;

            _writeSemaphore.Wait();
            try
            {
                if (_disposed || !IsAlive) return false;

                var bytes = Encoding.UTF8.GetBytes(sseData);
                _context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                _context.Response.OutputStream.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SseClient {Id}] Send failed: {ex.Message}");
                MarkDead();
                return false;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<bool> SendAsync(string sseData, CancellationToken externalToken = default)
        {
            if (_disposed || !IsAlive) return false;

            try
            {
                await _writeSemaphore.WaitAsync(externalToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            try
            {
                if (_disposed || !IsAlive) return false;

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
            finally
            {
                _writeSemaphore.Release();
            }
        }

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
            Send(": ping\n\n");
        }

        private void MarkDead()
        {
            IsAlive = false;
            try { _cts.Cancel(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;

            // 1. Останавливаем таймер и отменяем токен — SendAsync сразу выйдет по exception
            try { _heartbeatTimer?.Dispose(); } catch { }
            try { _cts?.Cancel(); } catch { }

            // 2. Берём lock на запись — гарантируем, что Write/Flush не идут параллельно
            // (если Send висит внутри Write — Abort ниже его разбудит)
            _writeSemaphore.Wait();
            try
            {
                if (_disposed) return;
                _disposed = true;
                IsAlive = false;

                // 3. ✅ Abort — не ждёт flush, принудительно рвёт TCP-соединение
                try { _context?.Response?.Abort(); } catch { }
            }
            finally
            {
                _writeSemaphore.Release();
            }

            // 4. Освобождаем ресурсы
            try { _cts?.Dispose(); } catch { }
            try { _writeSemaphore?.Dispose(); } catch { }
        }
    }
}