using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class ExtendCommand : BaseCommand
    {
        public override string Name => "extend";
        public override IEnumerable<string> Aliases => new[] { "e", "ext", "продлить" };
        private const int MAX_EXTEND = 10;
        public override string Description => "Увеличить время показа сообщения: !!ext или !!ext:5";

        // Этот метод вызовет MessageProcessor ПЕРЕД выполнением
        public override int GetTotalCost(ChatCommandInfo info, Chater chater)
        {
            /// Для 10-го ранга и выше бесплатно
            if (chater.Rank >= 10) return 0;

            // Если аргументов нет (!!ext) -> списываем максимум, сколько есть у юзера (но не больше 10)
            if (info.Arguments.Count == 0)
            {
                return (int)Math.Min(chater.Karma, MAX_EXTEND);
            }

            // Если аргумент есть (!!ext:5) -> берем число
            if (int.TryParse(info.Arguments[0], out int minutes))
            {
                return Math.Clamp(minutes, 1, MAX_EXTEND);
            }

            return 1; // Дефолт
        }

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            // В MessageProcessor карма уже списана методом GetTotalCost
            // Нам нужно просто применить время
            int minutes = GetTotalCost(info, chater);

            // Если у игрока 5 ранг, GetTotalCost вернул 0, но продлить-то надо!
            if (chater.Rank >= 5 && info.Arguments.Count == 0) minutes = MAX_EXTEND;
            else if (chater.Rank >= 5 && int.TryParse(info.Arguments[0], out int m)) minutes = m;

            if (minutes <= 0) minutes = 1;

            msg.DisplayTimeMs += minutes * 60 * 1000;
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[Extend] Продлено на {minutes} мин. (Карма списана процессором)");
        }
    }

}