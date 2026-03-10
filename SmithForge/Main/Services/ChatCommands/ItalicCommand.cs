using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class ItalicCommand : BaseCommand
    {
        public override string Name => "italic";
        public override IEnumerable<string> Aliases => new[] { "i", "курсив", "к" };
        public override string Description => "Сделать текст курсивом (стоимость: 2 кармы)";
        public override int Cost => 2;
        public override int MinRank => 0;

        // Бесплатно для 5 ранга
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[ItalicCommand] НАЧАЛО - Текст: '{msg.Message}'");
            Debug.WriteLine($"[ItalicCommand] Аргументы: {string.Join(", ", info.Arguments)}");

            string originalText = msg.Message;
            msg.Message = $"<i>{msg.Message}</i>";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[ItalicCommand] КОНЕЦ - Было: '{originalText}', стало: '{msg.Message}'");
            Debug.WriteLine($"[ItalicCommand] IsProcessedByCommand: {msg.IsProcessedByCommand}");

            int actualCost = GetCostForRank(chater.Rank);
            Debug.WriteLine($"[ItalicCommand] Стоимость: {actualCost}, Ранг: {chater.Rank}");
        }
    }
}