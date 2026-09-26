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
        private readonly StickerPageService? _stickerPageService;

        public InfoService(StickerPageService? stickerPageService = null)
        {
            _stickerPageService = stickerPageService;
            _pagesDir = Path.Combine(
                 AppDomain.CurrentDomain.BaseDirectory,
                 "Html", "InfoPages");

            Debug.WriteLine($"[InfoService] Pages dir: {_pagesDir}");
        }

        // ========== ПУБЛИЧНЫЙ API ==========

        // InfoService.cs
        public string Render(string pagePath, string userId)
        {
            Debug.WriteLine($"[InfoService] 🎯 Render вызван: pagePath='{pagePath}'");

            if (string.IsNullOrEmpty(pagePath))
                pagePath = "help";
            if (pagePath.StartsWith("stickers/", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = pagePath.Substring("stickers/".Length);

                // Если суффикс — чистое число (например, "86"), разрешаем в полное имя папки
                if (int.TryParse(suffix, out int packNumber))
                {
                    var packId = _stickerPageService?.GetPackIdByNumber(packNumber);

                    if (!string.IsNullOrEmpty(packId))
                    {
                        Debug.WriteLine($"[InfoService] 🔄 stickers/{packNumber} → stickers/{packId}");
                        pagePath = $"stickers/{packId}";
                    }
                    else
                    {
                        Debug.WriteLine($"[InfoService] ⚠️ Пак с номером {packNumber} не найден");
                    }
                }
            }

            // ============================================================
            // ✅ РЕЗОЛВ КОРОТКИХ ИМЁН: profile → 01_profile
            // Ищем в корне InfoPages файл вида NN_<pagePath>.html
            // ============================================================
            if (!pagePath.Contains("/") && !pagePath.Contains("\\") && !pagePath.Contains(":"))
            {
                var directFile = Path.Combine(_pagesDir, pagePath + ".html");

                if (!File.Exists(directFile))
                {
                    // Ищем файл с префиксом NN_: "01_profile.html", "02_profile.html"
                    var candidates = Directory.GetFiles(_pagesDir, $"*_{pagePath}.html");

                    if (candidates.Length > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(candidates[0]);
                        Debug.WriteLine($"[InfoService] 🔄 {pagePath} → {fileName}");
                        pagePath = fileName;
                    }
                    else
                    {
                        Debug.WriteLine($"[InfoService] ⚠️ Короткое имя '{pagePath}' не разрезолвилось (нет NN_{pagePath}.html)");
                    }
                }
            }

            // ============================================================
            // ✅ РЕЗОЛВ ПО НОМЕРУ: 1 → 01_xxx
            // Ищем в корне InfoPages файл вида NN_*.html с нужным номером
            // ============================================================
            if (int.TryParse(pagePath, out int pageNumber) && pageNumber > 0)
            {
                string prefix = pageNumber.ToString("D2");  // 1 → "01", 12 → "12", 100 → "100"
                var candidates = Directory.GetFiles(_pagesDir, $"{prefix}_*.html");

                if (candidates.Length > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(candidates[0]);
                    Debug.WriteLine($"[InfoService] 🔄 {pagePath} → {fileName}");
                    pagePath = fileName;
                }
                else
                {
                    Debug.WriteLine($"[InfoService] ⚠️ Страница с номером {pageNumber} (префикс {prefix}_) не найдена");
                }
            }
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
                    // Пробуем найти help.html в папке (если путь ведет к папке)
                    var indexPath = Path.Combine(_pagesDir, relativePath, "help.html");
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
    }
}
