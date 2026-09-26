using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;

public abstract class BaseCommand : IChatCommand
{
    public virtual bool IsDashboardVisible => true;
    public abstract string Name { get; }
    public abstract IEnumerable<string> Aliases { get; }
    public abstract string Description { get; }

    public virtual int Cost => 0;
    public virtual int MinRank => 0;

    // Ранги, для которых команда бесплатна (по умолчанию ни для кого)
    public virtual int[] FreeForRanks => Array.Empty<int>();

    /// <summary>
    /// Техническая команда — сообщение не отображается в основном чате,
    /// не получает MessageNumber и не идёт в веб-оверлей.
    /// Сохраняется в БД с IsVisible = 0.
    ///
    /// Пример: !!nick, !!ava, !!like, !!dislike, !!hide.
    /// По умолчанию — false (команда визуальная).
    /// </summary>
    public virtual bool IsTechnical => false;

    public virtual bool CanExecute(Chater chater)
    {
        return chater.Rank >= MinRank;
    }

    // Новый метод для проверки стоимости
    public int GetCostForRank(int rank)
    {
        return FreeForRanks.Contains(rank) ? 0 : Cost;
    }

    public abstract void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings);

    protected string GetArg(ChatCommandInfo info, int index, string defaultValue = "")
    {
        return info.Arguments.Count > index ? info.Arguments[index] : defaultValue;
    }

    public virtual int GetTotalCost(ChatCommandInfo info, Chater chater)
    {
        // По умолчанию возвращаем базовую стоимость
        return GetCostForRank(chater.Rank);
    }

    public virtual bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
    {
        return true; // по умолчанию списываем
    }
}