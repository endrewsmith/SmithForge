using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class BoldCommand : BaseCommand
    {
        public override string Name => "bold";
        public override IEnumerable<string> Aliases => new[] { "b", "жирный", "ж" };
        public override string Description => "Сделать текст жирным (стоимость: 2 кармы)";
        public override int Cost => 2;
        public override int MinRank => 0;

        // Бесплатно для 5 ранга
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[BoldCommand] НАЧАЛО - Текст: '{msg.Message}'");
            Debug.WriteLine($"[BoldCommand] Аргументы: {string.Join(", ", info.Arguments)}");

            string originalText = msg.Message;
            msg.Message = $"<b>{msg.Message}</b>";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[BoldCommand] КОНЕЦ - Было: '{originalText}', стало: '{msg.Message}'");
            Debug.WriteLine($"[BoldCommand] IsProcessedByCommand: {msg.IsProcessedByCommand}");

            int actualCost = GetCostForRank(chater.Rank);
            Debug.WriteLine($"[BoldCommand] Стоимость: {actualCost}, Ранг: {chater.Rank}");
        }
    }
}