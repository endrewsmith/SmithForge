using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;

public abstract class BaseCommand : IChatCommand
{
    public abstract string Name { get; }
    public abstract IEnumerable<string> Aliases { get; }
    public abstract string Description { get; }

    public virtual int Cost => 0;
    public virtual int MinRank => 0;

    // Ранги, для которых команда бесплатна (по умолчанию ни для кого)
    public virtual int[] FreeForRanks => Array.Empty<int>();

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