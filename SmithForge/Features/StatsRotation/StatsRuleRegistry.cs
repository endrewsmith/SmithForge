using SmithForge.Main.Models;
using SmithForge.Features.StatsRotation.Rules;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Реестр всех правил статистики.
    /// Чтобы добавить новое правило — создай класс в Rules/ и добавь строчку сюда.
    /// HTML и ротация подхватят его автоматически.
    /// </summary>
    public static class StatsRuleRegistry
    {
        private static readonly List<IStatsRule> _all = new()
        {
            new TopMessageSendersRule(),
            new TopRankRule(),
            new TopKarmaRule(),
            new TopTotalMessagesRule(),
            // ← сюда добавляешь новые правила
        };

        /// <summary>Все правила, включая выключенные.</summary>
        public static IReadOnlyList<IStatsRule> All => _all;

        public static IStatsRule? Find(string key) =>
            _all.FirstOrDefault(r => r.Key == key);

        /// <summary>Только те, что включены в настройках, в порядке Order.</summary>
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