using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.WebServer
{
    /// <summary>
    /// Менеджер нескольких HttpListener на разных портах.
    /// 
    /// Зачем: HttpListener на ОДНОМ порту имеет лимит одновременных соединений
    /// (ServicePointManager.DefaultConnectionLimit, по умолчанию 2).
    /// Если у тебя 5 SSE-потоков к одному порту — они дерутся за слоты,
    /// один из источников в OBS постоянно отваливается.
    /// 
    /// Решение: каждый SSE-поток — на СВОЁМ порту. Каждый порт = свой сокет,
    /// свой пул потоков, свой ServicePoint. Никакой конкуренции.
    /// </summary>
    public class MultiPortServer : IDisposable
    {
        private readonly Dictionary<int, HttpListener> _listeners = new();
        private readonly Dictionary<int, Func<HttpListenerContext, Task>> _handlers = new();
        private readonly Dictionary<int, CancellationTokenSource> _cts = new();
        private readonly Dictionary<int, string> _names = new();
        private bool _disposed;

        /// <summary>
        /// Зарегистрировать порт и его обработчик.
        /// Если порт занят — автоматически ищет следующий свободный.
        /// </summary>
        /// <param name="preferredPort">Желаемый порт</param>
        /// <param name="handler">Обработчик запросов</param>
        /// <param name="name">Имя для логов (например, "Stream", "Info")</param>
        /// <returns>Реально занятый порт (может отличаться от preferredPort)</returns>
        public int RegisterPort(int preferredPort, Func<HttpListenerContext, Task> handler, string name)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (string.IsNullOrEmpty(name)) name = $"Port{preferredPort}";

            int actualPort = FindFreePort(preferredPort);

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{actualPort}/");
            listener.Prefixes.Add($"http://127.0.0.1:{actualPort}/");

            _listeners[actualPort] = listener;
            _handlers[actualPort] = handler;
            _cts[actualPort] = new CancellationTokenSource();
            _names[actualPort] = name;

            if (actualPort != preferredPort)
            {
                Debug.WriteLine($"[MultiPort] ⚠️ Порт {preferredPort} занят, используем {actualPort} для '{name}'");
            }
            else
            {
                Debug.WriteLine($"[MultiPort] Зарегистрирован порт {actualPort} для '{name}'");
            }

            return actualPort;
        }

        /// <summary>
        /// Запустить все зарегистрированные порты.
        /// </summary>
        public async Task StartAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MultiPortServer));

            foreach (var kv in _listeners)
            {
                int port = kv.Key;
                var listener = kv.Value;
                var handler = _handlers[port];
                var cts = _cts[port];
                string name = _names[port];

                try
                {
                    listener.Start();
                    _ = Task.Run(() => ProcessAsync(port, listener, handler, name, cts.Token));
                    Debug.WriteLine($"[MultiPort/{name}] ✅ Запущен на http://localhost:{port}/");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MultiPort/{name}] ❌ Ошибка запуска порта {port}: {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Остановить все порты.
        /// </summary>
        public void Stop()
        {
            foreach (var kv in _listeners)
            {
                int port = kv.Key;
                var listener = kv.Value;
                var cts = _cts[port];
                string name = _names[port];

                try
                {
                    cts.Cancel();
                    listener.Stop();
                    listener.Close();
                    Debug.WriteLine($"[MultiPort/{name}] ⏹ Порт {port} остановлен");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MultiPort/{name}] Ошибка остановки: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Реальный порт, который занял сервер (после fallback).
        /// </summary>
        public int GetActualPort(int preferredPort)
        {
            if (_listeners.ContainsKey(preferredPort)) return preferredPort;

            // Fallback: ищем ближайший зарегистрированный порт >= preferredPort
            var candidates = _listeners.Keys.Where(p => p >= preferredPort).OrderBy(p => p).ToList();
            return candidates.Count > 0 ? candidates[0] : preferredPort;
        }

        // ============================================================
        // ВНУТРЕННЯЯ ЛОГИКА
        // ============================================================

        private async Task ProcessAsync(
            int port,
            HttpListener listener,
            Func<HttpListenerContext, Task> handler,
            string name,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    _ = Task.Run(() => HandleSafelyAsync(context, handler, name));
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    Debug.WriteLine($"[MultiPort/{name}] HttpListener остановлен");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MultiPort/{name}] Ошибка в цикле: {ex.Message}");
                    await Task.Delay(100, ct);
                }
            }
        }

        private async Task HandleSafelyAsync(
            HttpListenerContext context,
            Func<HttpListenerContext, Task> handler,
            string name)
        {
            try
            {
                // CORS-заголовки — общие для всех портов
                try
                {
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                }
                catch { /* заголовки могли быть добавлены в хендлере */ }

                // Preflight
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                    return;
                }

                await handler(context);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MultiPort/{name}] Ошибка обработки: {ex.Message}");
                try
                {
                    if (context.Response.OutputStream.CanWrite)
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Найти свободный порт начиная с preferredPort.
        /// Ищет в диапазоне preferredPort .. preferredPort+100.
        /// </summary>
        private int FindFreePort(int preferredPort)
        {
            for (int port = preferredPort; port < preferredPort + 100; port++)
            {
                if (IsPortFree(port)) return port;
            }

            throw new Exception(
                $"[MultiPort] Не удалось найти свободный порт в диапазоне {preferredPort}..{preferredPort + 100}");
        }

        private bool IsPortFree(int port)
        {
            // Если уже зарегистрирован — занят
            if (_listeners.ContainsKey(port)) return false;

            // Проверяем через временный listener
            var test = new HttpListener();
            try
            {
                test.Prefixes.Add($"http://localhost:{port}/");
                test.Prefixes.Add($"http://127.0.0.1:{port}/");
                test.Start();
                test.Stop();
                test.Close();
                return true;
            }
            catch
            {
                try { test.Close(); } catch { }
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var cts in _cts.Values)
            {
                try { cts.Cancel(); cts.Dispose(); } catch { }
            }

            foreach (var listener in _listeners.Values)
            {
                try { listener.Stop(); listener.Close(); } catch { }
            }

            _listeners.Clear();
            _handlers.Clear();
            _cts.Clear();
            _names.Clear();

            Debug.WriteLine("[MultiPort] ✅ Всё остановлено и освобождено");
        }
    }
}