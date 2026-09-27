using SmithForge.Main.Models;
using System.Collections.Generic;

namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Правило ротации статистики. Аналог "страницы" в InfoSystem.
    /// Реализации — в Rules/. Регистрируются в StatsRuleRegistry.
    /// </summary>
    public interface IStatsRule
    {
        /// <summary>Уникальный ключ (используется в конфиге и UI-чекбоксах).</summary>
        string Key { get; }

        /// <summary>Заголовок для оверлея ("🏆 Топ-3 по сообщениям").</summary>
        string Title { get; }

        /// <summary>Сколько секунд показывать блок.</summary>
        int DisplayDurationSeconds { get; }

        /// <summary>Порядок в ротации: меньше — раньше.</summary>
        int Order { get; }

        /// <summary>Построить данные для отображения. Может вернуть пустой список — тогда пропускается.</summary>
        IReadOnlyList<StatsEntry> Build(AppSettings settings);
    }
}