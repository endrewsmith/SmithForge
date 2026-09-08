// Features/InfoSystem/InfoRotationService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.Features.InfoSystem
{
    public class InfoRotationService : IDisposable
    {
        private readonly InfoService _infoService;
        private readonly string _pagesDir;
        private readonly List<string> _availablePages = new();
        private readonly List<string> _shownPages = new();
        private readonly Random _random = new();
        private readonly object _lock = new();

        private Timer? _silenceTimer;
        private DateTime _lastActivityTime = DateTime.Now;
        private bool _isRunning = false;
        private bool _isWaitingForSilence = false;
        private int _silenceIntervalSeconds = 30;

        public event EventHandler<string>? PageSelected;

        public InfoRotationService(InfoService infoService, string pagesDir)
        {
            _infoService = infoService;
            _pagesDir = pagesDir;
            RefreshPagesList();
        }

        // ============================================================
        // УПРАВЛЕНИЕ
        // ============================================================

        public void Start(int silenceIntervalSeconds = 30)
        {
            lock (_lock)
            {
                if (_isRunning) return;

                _silenceIntervalSeconds = Math.Max(5, silenceIntervalSeconds);
                _isRunning = true;
                _isWaitingForSilence = false;
                _lastActivityTime = DateTime.Now;

                _shownPages.Clear();

                _silenceTimer?.Dispose();
                _silenceTimer = new Timer(CheckSilence, null, 5000, 5000);

                Debug.WriteLine($"[InfoRotation] ▶️ Запущена (тишина: {_silenceIntervalSeconds}с, страниц: {_availablePages.Count})");
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                _isWaitingForSilence = false;
                _silenceTimer?.Dispose();
                _silenceTimer = null;
                Debug.WriteLine("[InfoRotation] ⏹ Остановлена");
            }
        }

        public void SetSilenceInterval(int seconds)
        {
            _silenceIntervalSeconds = Math.Max(5, seconds);
            Debug.WriteLine($"[InfoRotation] ⏱ Интервал тишины: {_silenceIntervalSeconds}с");
        }

        public void OnUserActivity()
        {
            lock (_lock)
            {
                _lastActivityTime = DateTime.Now;

                if (_isWaitingForSilence)
                {
                    _isWaitingForSilence = false;
                    Debug.WriteLine("[InfoRotation] 💬 Активность, сброс ожидания");
                }
            }
        }

        // ============================================================
        // ✅ РЕКУРСИВНОЕ СКАНИРОВАНИЕ ВСЕХ ПАПОК
        // ============================================================

        public void RefreshPagesList()
        {
            lock (_lock)
            {
                _availablePages.Clear();
                _shownPages.Clear();

                if (!Directory.Exists(_pagesDir))
                {
                    Directory.CreateDirectory(_pagesDir);
                    return;
                }

                // ✅ РЕКУРСИВНОЕ СКАНИРОВАНИЕ
                var allFiles = GetHtmlFilesRecursive(_pagesDir);

                // Преобразуем полные пути в относительные имена
                foreach (var file in allFiles)
                {
                    // Получаем путь относительно корня Pages
                    var relativePath = Path.GetRelativePath(_pagesDir, file);

                    // Убираем расширение .html
                    var pageName = relativePath.Replace(".html", "").Replace("\\", "/");

                    // Исключаем index.html и пустые имена
                    if (!string.IsNullOrEmpty(pageName) && !pageName.EndsWith("index"))
                    {
                        _availablePages.Add(pageName);
                    }
                }

                // Исключаем help из ротации (если хочешь)
                // _availablePages.Remove("help");

                Debug.WriteLine($"[InfoRotation] 📋 Найдено страниц: {_availablePages.Count}");
                foreach (var page in _availablePages.Take(10))
                {
                    Debug.WriteLine($"  - {page}");
                }
                if (_availablePages.Count > 10)
                {
                    Debug.WriteLine($"  ... и еще {_availablePages.Count - 10} страниц");
                }
            }
        }

        /// <summary>
        /// Рекурсивно собирает все .html файлы в папке и подпапках
        /// </summary>
        private List<string> GetHtmlFilesRecursive(string directory)
        {
            var files = new List<string>();

            try
            {
                // Добавляем файлы из текущей папки
                files.AddRange(Directory.GetFiles(directory, "*.html"));

                // Рекурсивно обходим подпапки
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    // Пропускаем системные папки
                    var dirName = Path.GetFileName(subDir);
                    if (dirName.StartsWith(".")) continue;
                    if (dirName == "Thumbs.db") continue;

                    files.AddRange(GetHtmlFilesRecursive(subDir));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoRotation] Ошибка сканирования: {ex.Message}");
            }

            return files;
        }

        public RotationStatus GetStatus()
        {
            lock (_lock)
            {
                return new RotationStatus
                {
                    IsRunning = _isRunning,
                    IsWaitingForSilence = _isWaitingForSilence,
                    SilenceIntervalSeconds = _silenceIntervalSeconds,
                    TotalPages = _availablePages.Count,
                    ShownPages = _shownPages.Count,
                    Pages = new List<string>(_availablePages),
                    SilentSeconds = (int)(DateTime.Now - _lastActivityTime).TotalSeconds
                };
            }
        }

        // ============================================================
        // ВНУТРЕННЯЯ ЛОГИКА
        // ============================================================

        private void CheckSilence(object? state)
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                var silentSeconds = (DateTime.Now - _lastActivityTime).TotalSeconds;

                if (silentSeconds >= _silenceIntervalSeconds && !_isWaitingForSilence)
                {
                    _isWaitingForSilence = true;
                    Debug.WriteLine($"[InfoRotation] 🕐 Тишина {silentSeconds:F0}с -> показ");

                    _ = ShowRandomPageAsync();
                }
            }
        }

        private async Task ShowRandomPageAsync()
        {
            string pageName;

            lock (_lock)
            {
                if (_shownPages.Count >= _availablePages.Count && _availablePages.Count > 0)
                {
                    _shownPages.Clear();
                    Debug.WriteLine("[InfoRotation] 🔄 Все страницы показаны, сброс списка");
                }

                var availableToShow = _availablePages
                    .Where(p => !_shownPages.Contains(p))
                    .ToList();

                if (availableToShow.Count == 0)
                {
                    Debug.WriteLine("[InfoRotation] ⚠️ Нет доступных страниц для показа");
                    _isWaitingForSilence = false;
                    return;
                }

                var randomIndex = _random.Next(availableToShow.Count);
                pageName = availableToShow[randomIndex];

                _shownPages.Add(pageName);

                Debug.WriteLine($"[InfoRotation] 📄 Показана: {pageName} ({_shownPages.Count}/{_availablePages.Count})");
            }

            try
            {
                var html = _infoService.Render(pageName, "system");
                PageSelected?.Invoke(this, pageName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoRotation] ❌ Ошибка: {ex.Message}");

                lock (_lock)
                {
                    _shownPages.Remove(pageName);
                }
            }

            lock (_lock)
            {
                _isWaitingForSilence = false;
                _lastActivityTime = DateTime.Now;
            }
        }

        public void Dispose()
        {
            _silenceTimer?.Dispose();
            _silenceTimer = null;
        }
    }

    public class RotationStatus
    {
        public bool IsRunning { get; set; }
        public bool IsWaitingForSilence { get; set; }
        public int SilenceIntervalSeconds { get; set; }
        public int TotalPages { get; set; }
        public int ShownPages { get; set; }
        public List<string> Pages { get; set; } = new();
        public int SilentSeconds { get; set; }
    }
}