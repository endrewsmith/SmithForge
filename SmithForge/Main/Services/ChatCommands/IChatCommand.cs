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
        IEnumerable<string> Aliases { get; }
        int Cost { get; } // стоимость команды
        int MinRank { get; } // минимальный ранг для использования

        bool CanExecute(Chater chater); // проверка возможности выполнения
        void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings);
    }
}
