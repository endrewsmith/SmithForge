using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation.Rules
{
    /// <summary>Топ-3 за всё время (TotalKarma как прокси долгой активности).</summary>
    public sealed class TopTotalMessagesRule : IStatsRule
    {
        public string Key => "top_total_messages";
        public string Title => "🏆 Топ-3 Количество сообщений за все время";
        public int DisplayDurationSeconds => 15;
        public int Order => 30;

        public IReadOnlyList<StatsEntry> Build(AppSettings settings)
        {
            return ChaterStorage.GetAll()
                .Where(c => c.TotalKarma > 0)
                .OrderByDescending(c => c.TotalKarma)
                .Take(3)
                .Select((c, i) => new StatsEntry
                {
                    Rank = i + 1,
                    DisplayName = c.EffectiveName,
                    ValueText = $"{c.TotalKarma:F0} всего",
                    AvatarPath = c.FullAvatarPath,
                    UserRank = c.Rank,
                    Platform = c.Accounts.FirstOrDefault()?.Platform ?? ""
                })
                .ToList();
        }
    }
}