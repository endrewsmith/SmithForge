using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class ImportantCommand : BaseCommand
    {
        public override string Name => "важно";
        public override IEnumerable<string> Aliases => new[] { "important", "важное", "imp" };
        public override string Description => "Пометить сообщение как важное (показывается в отдельном окне)";
        public override int Cost => 5;
        public override int MinRank => 0;

        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[ImportantCommand] НАЧАЛО - Текст: '{msg.Message}'");

            // Помечаем сообщение как важное
            msg.Message = $"<important>{msg.Message}</important>";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[ImportantCommand] КОНЕЦ - стало: '{msg.Message}'");
            Debug.WriteLine($"[ImportantCommand] IsProcessedByCommand: {msg.IsProcessedByCommand}");
        }
    }
}