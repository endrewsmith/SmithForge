using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Features.StatsRotation.Rules
{
    /// <summary>Топ-3 по сообщениям за текущий стрим (состояние Chater.MessageCount).</summary>
    public sealed class TopMessageSendersRule : IStatsRule
    {
        public string Key => "top_messages";
        public string Title => "🏆 Топ-3 по сообщениям на этом стриме";
        public int DisplayDurationSeconds => 15;
        public int Order => 10;

        public IReadOnlyList<StatsEntry> Build(AppSettings settings)
        {
            return ChaterStorage.GetAll()
                .Where(c => c.MessageCount > 0)
                .OrderByDescending(c => c.MessageCount)
                .Take(3)
                .Select((c, i) => new StatsEntry
                {
                    Rank = i + 1,
                    DisplayName = c.EffectiveName,
                    ValueText = $"{c.MessageCount} сообщ.",
                    AvatarPath = c.FullAvatarPath,
                    UserRank = c.Rank,
                    Platform = c.Accounts.FirstOrDefault()?.Platform ?? ""
                })
                .ToList();
        }
    }
}