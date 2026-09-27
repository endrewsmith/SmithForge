using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Универсальный сервис запросов топа по маркеру.
    /// Кэширует результаты, чтобы не молотить БД каждый тик.
    /// </summary>
    public sealed class StatsQueryService
    {
        private readonly Func<string?> _getSessionId;
        private readonly object _lock = new();

        private readonly Dictionary<(string Marker, StatsScope Scope), List<(string ChaterId, int Count)>> _cache = new();
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private string? _lastSessionId;

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

        public StatsQueryService(Func<string?> getSessionId)
        {
            _getSessionId = getSessionId ?? throw new ArgumentNullException(nameof(getSessionId));
        }

        public IReadOnlyList<StatsEntry> GetTopByMarker(
            string marker, string valueSuffix, StatsScope scope, int top = 3)
        {
            EnsureFresh();

            List<(string ChaterId, int Count)>? raw;
            lock (_lock)
            {
                if (!_cache.TryGetValue((marker, scope), out raw) || raw == null)
                    return Array.Empty<StatsEntry>();
            }

            return raw
                .Take(top)
                .Select((r, i) =>
                {
                    var c = ChaterStorage.GetById(r.ChaterId);
                    return new StatsEntry
                    {
                        Rank = i + 1,
                        DisplayName = c?.EffectiveName ?? "Unknown",
                        ValueText = $"{r.Count} {valueSuffix}",
                        AvatarPath = c?.FullAvatarPath,
                        UserRank = c?.Rank ?? 0,
                        Platform = c?.Accounts.FirstOrDefault()?.Platform ?? ""
                    };
                })
                .ToList();
        }

        private void EnsureFresh()
        {
            var sessionId = _getSessionId();

            lock (_lock)
            {
                bool sessionChanged = sessionId != _lastSessionId;
                bool expired = (DateTime.UtcNow - _lastRefreshUtc) > CacheTtl;

                if (!sessionChanged && !expired) return;

                _cache.Clear();
                _lastSessionId = sessionId;
                _lastRefreshUtc = DateTime.UtcNow;

                // Пересчитываем все метрики каталога
                foreach (var metric in StatsMetricsCatalog.All)
                {
                    var key = (metric.Marker, metric.Scope);
                    if (_cache.ContainsKey(key)) continue;  // уже посчитали для этого маркера+scope

                    List<(string, int)> result;
                    try
                    {
                        result = DatabaseService.GetMarkerCounts(
                            metric.Marker, metric.Scope, sessionId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[StatsQuery] Ошибка GetMarkerCounts '{metric.Marker}' {metric.Scope}: {ex.Message}");
                        result = new List<(string, int)>();
                    }

                    _cache[key] = result;
                }
            }
        }
    }
}