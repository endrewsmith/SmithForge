using SmithForge.Main.Models;
using SmithForge.Features.StatsRotation.Rules;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation
{
    public static class StatsRuleRegistry
    {
        private static readonly List<IStatsRule> _all = new();
        private static bool _initialized;

        /// <summary>
        /// Инициализация. Вызывается один раз из MainViewModel.
        /// </summary>
        public static void Initialize(Func<string?> getSessionId)
        {
            if (_initialized) return;
            _initialized = true;

            _all.Clear();

            // ─── Прямые правила (по полям Chater) ──────────────────
            _all.Add(new TopMessageSendersRule());
            _all.Add(new TopRankRule());
            _all.Add(new TopKarmaRule());
            _all.Add(new TopTotalMessagesRule());

            // ─── Универсальные правила (по маркерам) ───────────────
            var queryService = new StatsQueryService(getSessionId);
            foreach (var metric in StatsMetricsCatalog.All)
            {
                _all.Add(new TaggedStatsRule(metric, queryService));
            }
        }

        public static IReadOnlyList<IStatsRule> All => _all;

        public static IStatsRule? Find(string key) =>
            _all.FirstOrDefault(r => r.Key == key);

        public static List<IStatsRule> GetEnabled(AppSettings settings)
        {
            var enabledKeys = settings.EnabledStatsRules ?? new List<string>();

            return _all
                .Where(r => enabledKeys.Contains(r.Key))
                .OrderBy(r => r.Order)
                .ToList();
        }
    }
}