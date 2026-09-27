using SmithForge.Main.Models;
using System.Collections.Generic;

namespace SmithForge.Features.StatsRotation.Rules
{
    /// <summary>
    /// Универсальное правило ротации: работает по StatsMetric из каталога.
    /// Один класс обслуживает все метрики, построенные по маркерам в сообщениях.
    /// </summary>
    public sealed class TaggedStatsRule : IStatsRule
    {
        private readonly StatsMetric _metric;
        private readonly StatsQueryService _queryService;

        public TaggedStatsRule(StatsMetric metric, StatsQueryService queryService)
        {
            _metric = metric;
            _queryService = queryService;
        }

        public string Key => _metric.Key;
        public string Title => _metric.Title;
        public int DisplayDurationSeconds => _metric.DurationSeconds;
        public int Order => _metric.Order;

        public IReadOnlyList<StatsEntry> Build(AppSettings settings)
        {
            return _queryService.GetTopByMarker(
                _metric.Marker,
                _metric.ValueSuffix,
                _metric.Scope,
                top: 3);
        }
    }
}