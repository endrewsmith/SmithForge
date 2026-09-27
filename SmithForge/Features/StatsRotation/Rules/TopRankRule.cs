using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation.Rules
{
    /// <summary>Топ-3 по рангу на текущий момент.</summary>
    public sealed class TopRankRule : IStatsRule
    {
        public string Key => "top_rank";
        public string Title => "🌟 Топ-3 по рангу";
        public int DisplayDurationSeconds => 15;
        public int Order => 15;   // между top_messages (10) и top_karma (20)

        public IReadOnlyList<StatsEntry> Build(AppSettings settings)
        {
            return ChaterStorage.GetAll()
                .Where(c => c.Rank > 0)
                .OrderByDescending(c => c.Rank)
                .ThenByDescending(c => c.MessageCount)
                .Take(3)
                .Select((c, i) => new StatsEntry
                {
                    Rank = i + 1,
                    DisplayName = c.EffectiveName,
                    ValueText = GetRankText(c.Rank),
                    AvatarPath = c.FullAvatarPath,
                    UserRank = c.Rank,
                    Platform = c.Accounts.FirstOrDefault()?.Platform ?? ""
                })
                .ToList();
        }

        private static string GetRankText(int rank)
        {
            // Как в твоём RankToTextConverter
            return rank switch
            {
                0 => "☆",
                1 => "★",
                2 => "★★",
                3 => "★★★",
                4 => "★★★★",
                5 => "★★★★★",
                >= 6 => $"★ {rank}",
                _ => "?"
            };
        }
    }
}