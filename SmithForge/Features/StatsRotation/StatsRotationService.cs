using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Features.StatsRotation
{
    public sealed class StatsRotationService : IDisposable
    {
        private readonly WebServerService _webServer;
        private readonly AppSettings _settings;
        private readonly object _lock = new();

        private Timer? _silenceTimer;
        private DateTime _lastActivityTime = DateTime.Now;
        private bool _isRunning;
        private bool _isShowing;
        private int _silenceIntervalSeconds = 30;
        private int _nextRuleIndex;

        // ✅ Диагностика: общий счётчик запущенных показов
        private int _showCounter;

        public double SecondsSinceLastActivity
        {
            get
            {
                lock (_lock)
                {
                    return (DateTime.Now - _lastActivityTime).TotalSeconds;
                }
            }
        }

        public event EventHandler<string>? RuleShown;

        public StatsRotationService(WebServerService webServer, AppSettings settings)
        {
            _webServer = webServer ?? throw new ArgumentNullException(nameof(webServer));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Debug.WriteLine($"[StatsRotation] 🆕 Создан экземпляр сервиса. HashCode: {GetHashCode()}");
        }

        // ============================================================
        // УПРАВЛЕНИЕ
        // ============================================================

        public void Start(int silenceIntervalSeconds = 30)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Debug.WriteLine($"[StatsRotation] ⚠️ Start() проигнорирован — уже запущен (HashCode: {GetHashCode()})");
                    return;
                }

                _silenceIntervalSeconds = Math.Max(5, silenceIntervalSeconds);
                _isRunning = true;
                _isShowing = false;
                _lastActivityTime = DateTime.Now;

                _silenceTimer?.Dispose();
                _silenceTimer = new Timer(CheckSilence, null, 5000, 5000);

                Debug.WriteLine($"[StatsRotation] ▶️ Запущена (тишина: {_silenceIntervalSeconds}с, HashCode: {GetHashCode()})");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _isShowing = false;
                _silenceTimer?.Dispose();
                _silenceTimer = null;
                Debug.WriteLine($"[StatsRotation] ⏹ Остановлена (HashCode: {GetHashCode()})");
            }
        }

        public void SetSilenceInterval(int seconds)
        {
            lock (_lock)
            {
                _silenceIntervalSeconds = Math.Max(5, seconds);
                Debug.WriteLine($"[StatsRotation] ⏱ Интервал тишины: {_silenceIntervalSeconds}с");
            }
        }

        public void OnUserActivity()
        {
            lock (_lock)
            {
                _lastActivityTime = DateTime.Now;
            }
        }

        public bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
        }

        // ============================================================
        // ЛОГИКА ТИШИНЫ
        // ============================================================

        private void CheckSilence(object? state)
        {
            double silentSeconds;
            bool shouldShow = false;

            lock (_lock)
            {
                if (!_isRunning) return;

                if (_isShowing)
                {
                    // ✅ Диагностика: сколько раз таймер попал в момент показа
                    Debug.WriteLine($"[StatsRotation] ⏸ CheckSilence: _isShowing = true, пропускаем");
                    return;
                }

                silentSeconds = (DateTime.Now - _lastActivityTime).TotalSeconds;
                if (silentSeconds < _silenceIntervalSeconds) return;

                _isShowing = true;
                shouldShow = true;

                Debug.WriteLine($"[StatsRotation] 🕐 Тишина {silentSeconds:F0}с → показ правила (HashCode: {GetHashCode()})");
            }

            if (shouldShow)
            {
                _ = ShowNextRuleAsync();
            }
        }

        private async Task ShowNextRuleAsync()
        {
            // ✅ Диагностика: уникальный ID для каждого запуска
            int callId = Interlocked.Increment(ref _showCounter);
            var startTime = DateTime.Now;

            Debug.WriteLine($"");
            Debug.WriteLine($"[StatsRotation] ▶️▶️▶️ СТАРТ показа #{callId} (time: {startTime:HH:mm:ss.fff})");

            try
            {
                var rules = StatsRuleRegistry.GetEnabled(_settings);
                Debug.WriteLine($"[StatsRotation] #{callId} Включённых правил: {rules.Count}");

                if (rules.Count == 0)
                {
                    Debug.WriteLine($"[StatsRotation] #{callId} ⚠️ Нет включённых правил — выход");
                    return;
                }

                // Циклический проход по Order (без повторов до смены набора)
                if (_nextRuleIndex >= rules.Count) _nextRuleIndex = 0;
                var rule = rules[_nextRuleIndex];
                _nextRuleIndex = (_nextRuleIndex + 1) % rules.Count;

                Debug.WriteLine($"[StatsRotation] #{callId} Выбрано правило: {rule.Key} (index: {_nextRuleIndex})");

                IReadOnlyList<StatsEntry> entries;
                try
                {
                    entries = rule.Build(_settings);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StatsRotation] #{callId} ❌ Ошибка Build '{rule.Key}': {ex.Message}");
                    return;
                }

                if (entries.Count == 0)
                {
                    Debug.WriteLine($"[StatsRotation] #{callId} ⏭ Правило '{rule.Key}' вернуло пусто, пропускаем");
                    return;
                }

                // 1. Показать
                _webServer.SendStatsToWeb(rule, entries);
                RuleShown?.Invoke(this, rule.Key);
                Debug.WriteLine($"[StatsRotation] #{callId} 📊 Показано: {rule.Key} ({entries.Count} строк) в {DateTime.Now:HH:mm:ss.fff}");

                // 2. Подождать длительность
                int waitMs = rule.DisplayDurationSeconds * 1000;
                Debug.WriteLine($"[StatsRotation] #{callId} ⏳ Ждём {rule.DisplayDurationSeconds}с до hide");
                await Task.Delay(waitMs);
                Debug.WriteLine($"[StatsRotation] #{callId} ⏰ Ожидание завершено в {DateTime.Now:HH:mm:ss.fff}");

                // 3. Скрыть
                _webServer.SendStatsHide();
                Debug.WriteLine($"[StatsRotation] #{callId} 🧹 SendStatsHide отправлен в {DateTime.Now:HH:mm:ss.fff}");

                // 4. Пауза, чтобы CSS успел доиграть анимацию
                await Task.Delay(700);
                Debug.WriteLine($"[StatsRotation] #{callId} 🧹 Скрыто: {rule.Key} (анимация завершена) в {DateTime.Now:HH:mm:ss.fff}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StatsRotation] #{callId} ❌ Ошибка: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _isShowing = false;
                    _lastActivityTime = DateTime.Now;

                    Debug.WriteLine($"[StatsRotation] #{callId} 🔓 Снят флаг _isShowing, lastActivityTime = {DateTime.Now:HH:mm:ss.fff}");
                }

                Debug.WriteLine($"[StatsRotation] ◀️◀️◀️ КОНЕЦ показа #{callId} (длительность: {(DateTime.Now - startTime).TotalSeconds:F2}с)");
                Debug.WriteLine($"");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}