using SmithForge.Features.InfoSystem.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SmithForge.Features.InfoSystem
{
    // InfoSystem/InfoService.cs
    public class InfoService
    {
        private readonly Dictionary<string, InfoPage> _pageCache = new();     // кеш страниц
        private readonly Dictionary<string, string> _userContext = new();     // где пользователь
        private readonly string _pagesDir;
        private readonly string _defaultPage = "help";
        private static readonly object _cacheLock = new object();

        public InfoService()
        {
            _pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "InfoWeb", "Pages");

            Directory.CreateDirectory(_pagesDir);

            // Создаём дефолтную страницу если её нет
            EnsureDefaultPagesExist();
        }

        // ========== ПУБЛИЧНЫЙ API ==========

        // InfoService.cs
        public string Render(string pagePath, string userId)
        {
            Debug.WriteLine($"[InfoService] 🎯 Render вызван: pagePath='{pagePath}'");

            if (string.IsNullOrEmpty(pagePath))
                pagePath = "help";

            // Проверяем кеш
            if (_pageCache.TryGetValue(pagePath, out InfoPage? cachedPage))
            {
                Debug.WriteLine($"[InfoService] ✅ Из кеша: {pagePath}");
                return cachedPage.Content;
            }

            // ============================================================
            // ✅ ПОДДЕРЖКА ВЛОЖЕННОСТИ ЛЮБОЙ ГЛУБИНЫ
            // ============================================================

            // Преобразуем путь: "sounds/sound_1_1" -> "sounds\sound_1_1.html"
            // или "stickers/spotty/cat" -> "stickers\spotty\cat.html"
            var relativePath = pagePath.Replace("/", "\\");
            var filePath = Path.Combine(_pagesDir, relativePath + ".html");

            Debug.WriteLine($"[InfoService] 📂 Ищем файл: {filePath}");

            // Если файл не найден — пробуем найти в корне (для обратной совместимости)
            if (!File.Exists(filePath))
            {
                var fallbackPath = Path.Combine(_pagesDir, pagePath + ".html");
                Debug.WriteLine($"[InfoService] 📂 Пробуем fallback: {fallbackPath}");

                if (File.Exists(fallbackPath))
                {
                    filePath = fallbackPath;
                }
                else
                {
                    // Пробуем найти index.html в папке (если путь ведет к папке)
                    var indexPath = Path.Combine(_pagesDir, relativePath, "index.html");
                    Debug.WriteLine($"[InfoService] 📂 Пробуем index: {indexPath}");

                    if (File.Exists(indexPath))
                    {
                        filePath = indexPath;
                    }
                    else
                    {
                        Debug.WriteLine($"[InfoService] ❌ Файл НЕ НАЙДЕН: {pagePath}");
                        return Render("help", userId);
                    }
                }
            }

            string html = File.ReadAllText(filePath, Encoding.UTF8);
            Debug.WriteLine($"[InfoService] ✅ Загружена: {pagePath} (length: {html.Length})");

            // Кешируем
            _pageCache[pagePath] = new InfoPage
            {
                Id = pagePath,
                Content = html,
                IsCached = true
            };
            _userContext[userId] = pagePath;

            return html;
        }

        public string Search(string query, string userId)
        {
            if (string.IsNullOrEmpty(query))
                return "❌ Укажите что искать: !!info:search:текст";

            var results = new List<(string PageId, string Title, string Snippet)>();
            string lowerQuery = query.ToLower();

            // Ищем по всем закешированным страницам
            foreach (var page in _pageCache.Values)
            {
                if (page.Content.ToLower().Contains(lowerQuery))
                {
                    string title = ExtractTitle(page.Content) ?? page.Id;
                    string snippet = ExtractSnippet(page.Content, query);
                    results.Add((page.Id, title, snippet));
                }
            }

            // Ищем по файлам на диске (рекурсивно)
            var allFiles = Directory.GetFiles(_pagesDir, "*.html", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                string relativePath = Path.GetRelativePath(_pagesDir, file);
                string id = relativePath.Replace(".html", "").Replace("\\", "/");

                if (_pageCache.ContainsKey(id)) continue;

                string content = File.ReadAllText(file, Encoding.UTF8);
                if (content.ToLower().Contains(lowerQuery))
                {
                    string title = ExtractTitle(content) ?? id;
                    string snippet = ExtractSnippet(content, query);
                    results.Add((id, title, snippet));

                    // Кешируем найденную страницу
                    _pageCache[id] = new InfoPage { Id = id, Content = content, IsCached = true };
                }
            }

            if (results.Count == 0)
                return $"❌ По запросу '<b>{query}</b>' ничего не найдено";

            // ✅ Передаем userId
            return RenderSearchResults(query, results, userId);
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _pageCache.Clear();
            }
        }

        public List<string> GetCachedPages() => _pageCache.Keys.ToList();

        // ========== ВНУТРЕННЯЯ ЛОГИКА ==========

        private string ShowPage(string pageName, string userId)
        {
            // 1. Проверяем кеш
            if (_pageCache.TryGetValue(pageName, out InfoPage? cachedPage))
            {
                _userContext[userId] = pageName;
                return cachedPage.Content;
            }

            // 2. Ищем на диске
            string filePath = Path.Combine(_pagesDir, $"{pageName}.html");

            if (!File.Exists(filePath))
            {
                // Страница не найдена → показываем главную
                return ShowPage(_defaultPage, userId);
            }

            // 3. Загружаем и кешируем
            string html = File.ReadAllText(filePath, Encoding.UTF8);

            var page = new InfoPage
            {
                Id = pageName,
                Content = html,
                LoadedAt = DateTime.Now,
                IsCached = true
            };

            lock (_cacheLock)
            {
                _pageCache[pageName] = page;
            }

            _userContext[userId] = pageName;

            // 4. Добавляем навигацию (если её нет)
            html = EnsureNavigation(html, pageName);

            return html;
        }

        private string GoBack(string userId)
        {
            if (!_userContext.TryGetValue(userId, out string? currentPage))
            {
                return ShowPage(_defaultPage, userId);
            }

            // Определяем родительскую страницу
            string parentPage = GetParentPage(currentPage);

            if (parentPage != null && File.Exists(Path.Combine(_pagesDir, $"{parentPage}.html")))
            {
                return ShowPage(parentPage, userId);
            }

            return ShowPage(_defaultPage, userId);
        }

        private string EnsureNavigation(string html, string pageName)
        {
            // Если в HTML уже есть навигация — не добавляем
            if (html.Contains("{{navigation}}") || html.Contains("<!--navigation-->"))
                return html;

            // Определяем родителя
            string parent = GetParentPage(pageName);

            var nav = new StringBuilder();
            nav.AppendLine("<div class='info-nav'>");
            nav.AppendLine("  <hr/>");

            if (parent != null)
            {
                nav.AppendLine($"  <a href='!!info:{parent}'>⬅️ Назад</a>");
            }

            nav.AppendLine($"  <a href='!!info:{_defaultPage}'>🏠 Главная</a>");
            nav.AppendLine($"  <a href='!!info:search:'>🔍 Поиск</a>");
            nav.AppendLine("</div>");

            // Вставляем перед закрывающим тегом </div> или в конец
            if (html.Contains("</body>"))
            {
                html = html.Replace("</body>", nav.ToString() + "</body>");
            }
            else if (html.Contains("</div>"))
            {
                int lastDiv = html.LastIndexOf("</div>");
                html = html.Insert(lastDiv, nav.ToString());
            }
            else
            {
                html += nav.ToString();
            }

            return html;
        }

        private string GetParentPage(string pageName)
        {
            var parentMap = new Dictionary<string, string>
            {
                ["help"] = null!,
                ["formatting"] = "help",
                ["interaction"] = "help",
                ["important"] = "help",
                ["stickers"] = "help",
                ["profile"] = "help",
                ["commands"] = "help",
                ["karma"] = "help",
                ["rules"] = "help",
                ["about"] = "help",
            };

            return parentMap.TryGetValue(pageName, out string? parent) ? parent : "help";
        }

        private string ExtractTitle(string html)
        {
            // Ищем <title> или <h1>
            var match = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(html, @"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            return null;
        }

        private string ExtractSnippet(string html, string query)
        {
            // Ищем текст вокруг запроса
            int index = html.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "";

            int start = Math.Max(0, index - 50);
            int end = Math.Min(html.Length, index + query.Length + 50);
            string snippet = html.Substring(start, end - start);

            // Убираем HTML-теги
            snippet = Regex.Replace(snippet, @"<[^>]*>", " ");
            snippet = Regex.Replace(snippet, @"\s+", " ").Trim();

            if (start > 0) snippet = "..." + snippet;
            if (end < html.Length) snippet = snippet + "...";

            return snippet;
        }

        private string RenderSearchResults(string query, List<(string PageId, string Title, string Snippet)> results, string userId)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<div class='search-results'>");
            sb.AppendLine($"  <h2>🔍 Результаты поиска: '{query}'</h2>");
            sb.AppendLine($"  <p>Найдено: {results.Count}</p>");
            sb.AppendLine($"  <hr/>");

            foreach (var (id, title, snippet) in results.Take(10))
            {
                sb.AppendLine($"  <div class='result-item'>");
                sb.AppendLine($"    <a href='!!info:{id}'><b>{title}</b></a>");
                if (!string.IsNullOrEmpty(snippet))
                {
                    sb.AppendLine($"    <p class='snippet'>{snippet}</p>");
                }
                sb.AppendLine($"  </div>");
            }

            if (results.Count > 10)
            {
                sb.AppendLine($"  <p>... и еще {results.Count - 10} результатов</p>");
            }

            sb.AppendLine($"  <hr/>");
            sb.AppendLine($"  <a href='!!info:{_defaultPage}'>🏠 Главная</a>");
            sb.AppendLine($"</div>");

            return sb.ToString();
        }

        private string ExtractQuery(string target)
        {
            // target = "search:текст" → "текст"
            int colonIndex = target.IndexOf(':');
            if (colonIndex > 0 && colonIndex < target.Length - 1)
            {
                return target.Substring(colonIndex + 1);
            }
            return string.Empty;
        }

        private void EnsureDefaultPagesExist()
        {
            string defaultPath = Path.Combine(_pagesDir, "help.html");
            if (!File.Exists(defaultPath))
            {
                File.WriteAllText(defaultPath, DefaultHelpPage, Encoding.UTF8);
            }
        }

        private const string DefaultHelpPage = @"
<div class='page-root'>
    <h1>📚 Справочник команд SmithForge</h1>
    <p>Выберите раздел для просмотра:</p>
    
    <div class='page-grid'>
        <div class='page-card'>
            <span class='icon'>🎨</span>
            <a href='!!info:formatting'>Форматирование текста</a>
            <span class='desc'>Жирный, курсив, цвет</span>
        </div>
        <div class='page-card'>
            <span class='icon'>💰</span>
            <a href='!!info:karma'>Карма и ранги</a>
            <span class='desc'>Как заработать и тратить</span>
        </div>
        <div class='page-card'>
            <span class='icon'>📢</span>
            <a href='!!info:important'>Важные сообщения</a>
            <span class='desc'>Озвучивание в эфире</span>
        </div>
        <div class='page-card'>
            <span class='icon'>🎨</span>
            <a href='!!info:stickers'>Стикеры</a>
            <span class='desc'>Отправка стикеров</span>
        </div>
        <div class='page-card'>
            <span class='icon'>👤</span>
            <a href='!!info:profile'>Профиль</a>
            <span class='desc'>Аватарка и настройки</span>
        </div>
        <div class='page-card'>
            <span class='icon'>📋</span>
            <a href='!!info:commands'>Все команды</a>
            <span class='desc'>Полный список</span>
        </div>
    </div>
</div>";
    }
}
