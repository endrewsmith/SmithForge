using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Models
{
    public class ChatCommand
    {
        // Само название команды (например: "mute")
        public string Name { get; set; } = string.Empty;

        // Список всех параметров через двоеточие (например: ["ivan", "10", "flood"])
        public List<string> Arguments { get; set; } = new();

        // Полная сырая строка команды для логов или замены (например: "!!mute:ivan:10")
        public string Raw { get; set; } = string.Empty;
    }
}
