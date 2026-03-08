using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.ChatCommands
{
    public interface IChatCommand
    {
        string Name { get; }

        // ВОТ ЭТОЙ СТРОКИ НЕ ХВАТАЛО:
        IEnumerable<string> Aliases { get; }

        void Execute(ChatCommand info, Chater chater, CommonMessage msg, AppSettings settings);
    }
}
