using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class HelpCommand : BaseCommand
    {
        private readonly Dictionary<string, IChatCommand> _allCommands;

        public override string Name => "help";
        public override IEnumerable<string> Aliases => new[] { "хелп", "помощь", "h", "х" };
        public override string Description => "Справка по командам: !!help [имя_команды]";

        public HelpCommand(Dictionary<string, IChatCommand> allCommands)
        {
            _allCommands = allCommands;
        }

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            string target = GetArg(info, 0).ToLower();

            if (string.IsNullOrEmpty(target))
            {
                // Все доступные команды (уникальные)
                var availableCommands = _allCommands.Values
                    .Distinct()
                    .Where(c => c.CanExecute(chater))
                    .Cast<BaseCommand>() // Приводим к BaseCommand
                    .OrderBy(c => c.Cost)
                    .ToList();

                string helpText = "📋 Доступные команды:\n";
                foreach (var cmd in availableCommands)
                {
                    helpText += $"!!{cmd.Name} - {cmd.Description} (💰 {cmd.Cost} кармы, 👑 {cmd.MinRank}+ ранг)\n";
                }

                msg.Message = helpText;
                Debug.WriteLine($"[CMD] Help показан для ранга {chater.Rank}");
            }
            else if (_allCommands.TryGetValue(target, out var cmd) && cmd is BaseCommand baseCmd)
            {
                string aliases = baseCmd.Aliases != null ? $" (алиасы: {string.Join(", ", baseCmd.Aliases)})" : "";
                msg.Message = $"!!{baseCmd.Name}{aliases}\n{baseCmd.Description}\n💰 Стоимость: {baseCmd.Cost}\n👑 Требуемый ранг: {baseCmd.MinRank}+";
            }
        }
    }
}