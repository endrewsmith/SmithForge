using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation.Rules
{
    /// <summary>Топ-3 по текущей карме.</summary>
    public sealed class TopKarmaRule : IStatsRule
    {
        public string Key => "top_karma";
        public string Title => "⚡ Топ-3 Количество кармы на текущий момент";
        public int DisplayDurationSeconds => 15;
        public int Order => 20;

        public IReadOnlyList<StatsEntry> Build(AppSettings settings)
        {
            return ChaterStorage.GetAll()
                .Where(c => c.Karma > 0)
                .OrderByDescending(c => c.Karma)
                .Take(3)
                .Select((c, i) => new StatsEntry
                {
                    Rank = i + 1,
                    DisplayName = c.EffectiveName,
                    ValueText = $"{c.Karma:F0} кармы",
                    AvatarPath = c.FullAvatarPath,
                    UserRank = c.Rank,
                    Platform = c.Accounts.FirstOrDefault()?.Platform ?? ""
                })
                .ToList();
        }
    }
}