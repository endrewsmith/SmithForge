using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmithForge.ChatEngine.Models;

namespace SmithForge.ChatEngine.Services;

public class YoutubeHtmlParser
{
    private static readonly Regex ConfigStatementPattern =
        new Regex(@"ytcfg\.set\((\{.+\})\);", RegexOptions.Compiled);

    private static readonly Regex YoutubeInitialDataPattern1 =
        new Regex(@"window\[""ytInitialData""\] ?= ?(\{.+\});", RegexOptions.Compiled);

    private static readonly Regex YoutubeInitialDataPattern2 =
        new Regex(@"var\s+ytInitialData\s*=\s*(\{.+\});", RegexOptions.Compiled);

    private static readonly Regex YoutubeInitialDataPattern3 =
        new Regex(@"ytInitialData""?\s*:\s*(\{.+\})", RegexOptions.Compiled);

    private static readonly Regex ApiKeyPattern =
        new Regex(@"""INNERTUBE_API_KEY""\s*:\s*""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex ChannelNamePattern =
        new Regex(@"""authorName""\s*:\s*\{""simpleText""\s*:\s*""([^""]+)""", RegexOptions.Compiled);

    public JsonDocument ParseYoutubeConfig(string pageHtml)
    {
        var matcher = ConfigStatementPattern.Match(pageHtml);
        if (!matcher.Success)
        {
            throw new Exception("Failed to extract youtube config from html page");
        }
        return JsonDocument.Parse(matcher.Groups[1].Value);
    }

    public JsonDocument ParseInitialData(string pageHtml)
    {
        var patterns = new[]
        {
            YoutubeInitialDataPattern1,
            YoutubeInitialDataPattern2,
            YoutubeInitialDataPattern3
        };

        foreach (var pattern in patterns)
        {
            var matcher = pattern.Match(pageHtml);
            if (matcher.Success)
            {
                try
                {
                    var json = matcher.Groups[1].Value;
                    json = CleanJson(json);
                    return JsonDocument.Parse(json);
                }
                catch
                {
                    continue;
                }
            }
        }

        throw new Exception("Failed to extract youtube initial data from html page");
    }

    public string CleanJson(string json)
    {
        json = json.Replace("\n", "").Replace("\r", "");
        json = Regex.Replace(json, @",\s*}", "}");
        json = Regex.Replace(json, @",\s*]", "]");
        return json;
    }

    public string ExtractInnertubeApiKey(JsonDocument youtubeConfig)
    {
        if (youtubeConfig.RootElement.TryGetProperty("INNERTUBE_API_KEY", out var apiKeyElement))
        {
            return apiKeyElement.GetString()
                ?? throw new Exception("INNERTUBE_API_KEY is null");
        }
        throw new Exception("Youtube config doesn't have 'INNERTUBE_API_KEY'");
    }

    public string ExtractInitialContinuation(JsonDocument ytInitialData)
    {
        try
        {
            var root = ytInitialData.RootElement;

            string[] paths = new[]
            {
                "contents.twoColumnWatchNextResults.results.results.contents.0.itemSectionRenderer.contents.0.liveChatRenderer.continuations.0",
                "contents.liveChatRenderer.continuations.0",
                "contents.twoColumnWatchNextResults.conversationBar.liveChatRenderer.continuations.0",
                "engagementPanels.0.engagementPanelSectionListRenderer.content.liveChatRenderer.continuations.0"
            };

            JsonElement? continuation = null;
            foreach (var path in paths)
            {
                try
                {
                    continuation = GetElementByPath(root, path);
                    if (continuation.HasValue)
                        break;
                }
                catch { }
            }

            if (!continuation.HasValue)
            {
                var contents = root.GetProperty("contents");
                var liveChatRenderer = contents.GetProperty("liveChatRenderer");
                var continuations = liveChatRenderer.GetProperty("continuations");
                continuation = continuations.EnumerateArray().First();
            }

            var cont = continuation.Value;

            if (cont.TryGetProperty("invalidationContinuationData", out var invalidationData))
            {
                return invalidationData.GetProperty("continuation").GetString()
                    ?? throw new Exception("Continuation is null");
            }
            else if (cont.TryGetProperty("timedContinuationData", out var timedData))
            {
                return timedData.GetProperty("continuation").GetString()
                    ?? throw new Exception("Continuation is null");
            }
            else
            {
                throw new Exception("No continuation in youtube initial data");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to extract continuation: {ex.Message}");
        }
    }

    private JsonElement? GetElementByPath(JsonElement root, string path)
    {
        var parts = path.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            if (part.EndsWith("]"))
            {
                var bracketIndex = part.IndexOf('[');
                var propName = part.Substring(0, bracketIndex);
                var indexStr = part.Substring(bracketIndex + 1, part.Length - bracketIndex - 2);
                var index = int.Parse(indexStr);

                current = current.GetProperty(propName)[index];
            }
            else
            {
                current = current.GetProperty(part);
            }
        }

        return current;
    }

    public string ExtractChannelName(JsonDocument ytInitialData)
    {
        try
        {
            var root = ytInitialData.RootElement;
            var contents = root.GetProperty("contents");
            var liveChatRenderer = contents.GetProperty("liveChatRenderer");
            var participantsList = liveChatRenderer.GetProperty("participantsList");
            var participantsListRenderer = participantsList.GetProperty("liveChatParticipantsListRenderer");
            var participants = participantsListRenderer.GetProperty("participants");

            var firstParticipant = participants.EnumerateArray().First();
            var participantRenderer = firstParticipant.GetProperty("liveChatParticipantRenderer");
            var authorName = participantRenderer.GetProperty("authorName");

            return authorName.GetProperty("simpleText").GetString()
                ?? throw new Exception("Channel name is null");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to extract channel name: {ex.Message}");
        }
    }

    public string ExtractApiKeyDirectly(string html)
    {
        var match = ApiKeyPattern.Match(html);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        try
        {
            using var config = ParseYoutubeConfig(html);
            return ExtractInnertubeApiKey(config);
        }
        catch
        {
            throw new Exception("Failed to extract API key from html page");
        }
    }

    public string ExtractContinuationSmart(string html, Action<string>? log = null)
    {
        log?.Invoke("🔍 Начинаем поиск continuation...");

        try
        {
            log?.Invoke("  - Пробуем парсить ytInitialData...");
            using var initialData = ParseInitialData(html);
            var continuation = ExtractInitialContinuation(initialData);
            if (!string.IsNullOrEmpty(continuation))
            {
                log?.Invoke($"  ✅ Найден в ytInitialData: {continuation.Substring(0, Math.Min(30, continuation.Length))}...");
                return continuation;
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"  ❌ Ошибка парсинга ytInitialData: {ex.Message}");
        }

        log?.Invoke("  - Ищем через регулярные выражения...");
        var patterns = new[]
        {
            @"""continuation""\s*:\s*""([^""]{50,})""",
            @"""continuation""\s*:\s*""(0ap[^""]+)""",
            @"""continuation""\s*:\s*""(Eg[^""]+)""",
            @"""timedContinuationData""\s*:\s*\{[^}]*""continuation""\s*:\s*""([^""]+)""",
            @"""invalidationContinuationData""\s*:\s*\{[^}]*""continuation""\s*:\s*""([^""]+)""",
            @"""liveChatContinuation""[^}]*""continuation""\s*:\s*""([^""]+)""",
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.Singleline);
            if (matches.Count > 0)
            {
                log?.Invoke($"  - Паттерн дал {matches.Count} совпадений");
            }

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var continuation = match.Groups[1].Value;
                    if (continuation.Length > 50 &&
                        (continuation.Contains("0ap") ||
                         continuation.Contains("Eg") ||
                         continuation.Contains("gQB")))
                    {
                        log?.Invoke($"  ✅ Найден через регулярку: {continuation.Substring(0, Math.Min(30, continuation.Length))}...");
                        return continuation;
                    }
                }
            }
        }

        log?.Invoke("  - Ищем в скриптах...");
        var scriptMatches = Regex.Matches(html, @"<script[^>]*>(.*?)</script>", RegexOptions.Singleline);
        foreach (Match scriptMatch in scriptMatches)
        {
            var scriptContent = scriptMatch.Groups[1].Value;
            if (scriptContent.Contains("continuation"))
            {
                var innerMatch = Regex.Match(scriptContent, @"""continuation""\s*:\s*""([^""]{50,})""", RegexOptions.Singleline);
                if (innerMatch.Success)
                {
                    var continuation = innerMatch.Groups[1].Value;
                    log?.Invoke($"  ✅ Найден в скрипте: {continuation.Substring(0, Math.Min(30, continuation.Length))}...");
                    return continuation;
                }
            }
        }

        log?.Invoke("  ❌ Continuation не найден!");
        throw new Exception("No continuation found in HTML");
    }

    public string ExtractChannelNameDirectly(string html)
    {
        var match = ChannelNamePattern.Match(html);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        var titleMatch = Regex.Match(html, @"<title>([^<]+)</title>");
        if (titleMatch.Success)
        {
            var title = titleMatch.Groups[1].Value;
            if (title.EndsWith(" - YouTube"))
            {
                title = title.Substring(0, title.Length - 10);
            }
            return title;
        }

        try
        {
            using var initialData = ParseInitialData(html);
            return ExtractChannelName(initialData);
        }
        catch
        {
            return "Unknown Channel";
        }
    }

    public List<YouTubeStreamInfo> ParseLiveStreamsFromHtml(string html, Action<string>? log = null)
    {
        var streams = new List<YouTubeStreamInfo>();
        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            log?.Invoke("🔍 ===== НАЧАЛО ПАРСИНГА =====");

            // ШАГ 1: Находим ВСЕ videoId
            var videoIdPattern = @"""videoId""\s*:\s*""([^""]{11})""";
            var videoMatches = Regex.Matches(html, videoIdPattern);

            log?.Invoke($"📊 Найдено videoId: {videoMatches.Count}");

            if (videoMatches.Count == 0)
            {
                log?.Invoke("❌ videoId не найдены");
                return streams;
            }

            var allIds = new HashSet<string>();
            foreach (Match m in videoMatches)
            {
                if (m.Success && m.Groups.Count > 1)
                {
                    var videoId = m.Groups[1].Value;
                    if (!string.IsNullOrEmpty(videoId) && videoId.Length == 11)
                    {
                        allIds.Add(videoId);
                    }
                }
            }

            log?.Invoke($"📊 Уникальных videoId: {allIds.Count}");

            // ШАГ 2: Собираем информацию о каждом videoId
            var videoInfo = new Dictionary<string, (string title, bool isLive, bool isShorts)>();

            foreach (var videoId in allIds)
            {
                var searchPattern = $"\"videoId\":\"{videoId}\"";
                var index = html.IndexOf(searchPattern);
                if (index == -1) continue;

                var start = Math.Max(0, index - 3000);
                var length = Math.Min(6000, html.Length - start);
                var context = html.Substring(start, length);

                var title = ExtractTitleFromContext(context);

                // Проверяем LIVE
                var isLive = context.Contains("В ЭФИРЕ") ||
                             context.Contains("LIVE") ||
                             context.Contains("ПРЯМОЙ ЭФИР") ||
                             context.Contains("BADGE_STYLE_TYPE_LIVE_NOW") ||
                             context.Contains("live") ||
                             context.Contains("эфир");

                if (!isLive)
                {
                    continue;
                }

                // Определяем SHORTS
                var isShorts = false;

                if (context.Contains($"/shorts/{videoId}") ||
                    context.Contains($"shorts%2F{videoId}"))
                {
                    isShorts = true;
                }

                if (context.Contains("reelItemRenderer", StringComparison.OrdinalIgnoreCase))
                {
                    isShorts = true;
                }

                //if (context.Contains("liveChatRenderer", StringComparison.OrdinalIgnoreCase))
                //{
                //    isShorts = false;
                //}

                if (title.Contains("#shorts", StringComparison.OrdinalIgnoreCase))
                {
                    isShorts = true;
                }

                if (context.Contains("\"isShorts\":true", StringComparison.OrdinalIgnoreCase))
                {
                    isShorts = true;
                }

                var durationMatch = Regex.Match(context, @"""lengthSeconds""\s*:\s*""?(\d+)""?");
                if (durationMatch.Success)
                {
                    var seconds = int.Parse(durationMatch.Groups[1].Value);
                    if (seconds <= 60)
                    {
                        isShorts = true;
                    }
                }

                if (context.Contains("shorts") && context.Contains("badge"))
                {
                    isShorts = true;
                }

                videoInfo[videoId] = (title, isLive, isShorts);
            }

            // ШАГ 3: Формируем результат
            var liveCount = 0;
            foreach (var kvp in videoInfo)
            {
                var videoId = kvp.Key;
                var (title, isLive, isShorts) = kvp.Value;

                if (isLive)
                {
                    liveCount++;
                    streams.Add(new YouTubeStreamInfo
                    {
                        VideoId = videoId,
                        Title = title,
                        IsShorts = isShorts
                    });

                    log?.Invoke($"✅ Найден LIVE #{liveCount}: '{title}' (ID: {videoId}) {(isShorts ? "🎬 [SHORTS]" : "📺")}");
                }
            }

            log?.Invoke($"📊 Найдено стримов: {streams.Count}, Время: {totalStopwatch.ElapsedMilliseconds}мс");
            return streams;
        }
        catch (Exception ex)
        {
            log?.Invoke($"❌ Ошибка: {ex.Message}");
            return new List<YouTubeStreamInfo>();
        }
    }

    private string ExtractTitleFromContext(string context)
    {
        var title = "Без названия";

        var titlePatterns = new[]
        {
            @"""title""\s*:\s*\{[^}]*""content""\s*:\s*""([^""]+)""",
            @"""title""\s*:\s*\{[^}]*""simpleText""\s*:\s*""([^""]+)""",
            @"""title""\s*:\s*\{[^}]*""text""\s*:\s*""([^""]+)""",
            @"""title""\s*:\s*""([^""]+)""",
            @"""title"":""([^""]+)""",
            @"""text""\s*:\s*""([^""]+)""",
            @"<title>([^<]+)</title>"
        };

        foreach (var pattern in titlePatterns)
        {
            var match = Regex.Match(context, pattern);
            if (match.Success)
            {
                var found = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(found) &&
                    !found.Contains("LIVE") &&
                    !found.Contains("BADGE") &&
                    !found.Contains("STYLE") &&
                    found.Length > 2 &&
                    found.Length < 200)
                {
                    title = found;
                    break;
                }
            }
        }

        return title;
    }
}