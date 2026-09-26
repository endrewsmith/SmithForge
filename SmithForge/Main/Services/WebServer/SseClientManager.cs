using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.WebServer
{
    /// <summary>
    /// Менеджер одного SSE-потока.
    /// Инкапсулирует список клиентов, блокировку, broadcast и очистку мёртвых соединений.
    /// </summary>
    class SseClientManager
    {
        private readonly List<SseClient> _clients = new();
        private readonly object _lock = new object();
        private readonly string _name;

        /// <summary>
        /// Событие: изменилось количество клиентов.
        /// </summary>
        public event EventHandler<int>? ClientCountChanged;

        public SseClientManager(string name)
        {
            _name = name ?? "SSE";
        }

        /// <summary>
        /// Текущее количество клиентов.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _clients.Count;
                }
            }
        }

        /// <summary>
        /// Добавить клиента в список.
        /// </summary>
        public void Add(SseClient client)
        {
            if (client == null) return;

            int newCount;
            lock (_lock)
            {
                _clients.Add(client);
                newCount = _clients.Count;
            }

            Debug.WriteLine($"[{_name}] Клиент {client.Id} подключён. Всего: {newCount}");
            ClientCountChanged?.Invoke(this, newCount);
        }

        /// <summary>
        /// Удалить клиента из списка.
        /// </summary>
        public void Remove(SseClient client)
        {
            if (client == null) return;

            int newCount;
            lock (_lock)
            {
                _clients.Remove(client);
                newCount = _clients.Count;
            }

            Debug.WriteLine($"[{_name}] Клиент {client.Id} отключён. Осталось: {newCount}");
            ClientCountChanged?.Invoke(this, newCount);
        }

        /// <summary>
        /// Разослать данные всем клиентам. Мёртвые клиенты удаляются автоматически.
        /// </summary>
        public void Broadcast(string sseData)
        {
            if (string.IsNullOrEmpty(sseData)) return;

            List<SseClient> snapshot;
            lock (_lock)
            {
                if (_clients.Count == 0) return;
                snapshot = new List<SseClient>(_clients);
            }

            var dead = new List<SseClient>();
            foreach (var client in snapshot)
            {
                if (!client.Send(sseData))
                {
                    dead.Add(client);
                }
            }

            if (dead.Count > 0)
            {
                lock (_lock)
                {
                    foreach (var d in dead) _clients.Remove(d);
                }
                foreach (var d in dead) d.Dispose();
                Debug.WriteLine($"[{_name}] 🧹 Удалено {dead.Count} мёртвых клиентов. Осталось: {Count}");
            }
        }

        /// <summary>
        /// Закрыть все соединения и очистить список.
        /// </summary>
        public void CloseAll()
        {
            List<SseClient> toClose;
            lock (_lock)
            {
                toClose = new List<SseClient>(_clients);
                _clients.Clear();
            }

            foreach (var client in toClose)
            {
                client.Dispose();
            }

            Debug.WriteLine($"[{_name}] Все клиенты закрыты ({toClose.Count})");
            ClientCountChanged?.Invoke(this, 0);
        }

        /// <summary>
        /// Асинхронная версия Broadcast.
        /// Используется потоками, которые исторически работали через SendAsync
        /// (например, /info/stream).
        /// </summary>
        public async Task BroadcastAsync(string sseData)
        {
            if (string.IsNullOrEmpty(sseData)) return;

            List<SseClient> snapshot;
            lock (_lock)
            {
                if (_clients.Count == 0) return;
                snapshot = new List<SseClient>(_clients);
            }

            var dead = new List<SseClient>();
            foreach (var client in snapshot)
            {
                if (!await client.SendAsync(sseData))
                {
                    dead.Add(client);
                }
            }

            if (dead.Count > 0)
            {
                lock (_lock)
                {
                    foreach (var d in dead) _clients.Remove(d);
                }
                foreach (var d in dead) d.Dispose();
                Debug.WriteLine($"[{_name}] 🧹 Удалено {dead.Count} мёртвых клиентов (async). Осталось: {Count}");
            }
        }
    }
}