using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.ChatCommands
{
    public abstract class BaseCommand : IChatCommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        // Список альтернативных имен
        public abstract IEnumerable<string> Aliases { get; }

        public abstract void Execute(ChatCommand info, Chater chater, CommonMessage msg, AppSettings settings);

        protected string GetArg(ChatCommand info, int index)
            => info.Arguments.Count > index ? info.Arguments[index] : string.Empty;
    }
}
