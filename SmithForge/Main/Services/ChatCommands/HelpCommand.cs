using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.ChatCommands
{
    public class HelpCommand : BaseCommand
    {
        private readonly Dictionary<string, IChatCommand> _allCommands;

        public override string Name => "help";
        // ВСЕ АЛЬТЕРНАТИВЫ ТУТ:
        public override IEnumerable<string> Aliases => new[] { "хелп", "помощь", "h", "х" };

        public override string Description => "Справка по командам: !!help:имя";

        public HelpCommand(Dictionary<string, IChatCommand> allCommands)
        {
            _allCommands = allCommands;
        }

        public override void Execute(ChatCommand info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            string target = GetArg(info, 0).ToLower();

            if (string.IsNullOrEmpty(target))
            {
                // Вывод всех уникальных имен (без дубликатов от алиасов)
                var names = _allCommands.Values.Distinct().Select(c => "!!" + c.Name);
                Debug.WriteLine($"[BOT] Доступно: {string.Join(", ", names)}");
            }
            else if (_allCommands.TryGetValue(target, out var cmd))
            {
                var bc = (BaseCommand)cmd;
                Debug.WriteLine($"[BOT] !!{bc.Name} ({string.Join("/", bc.Aliases)}): {bc.Description}");
            }
        }
    }
}
