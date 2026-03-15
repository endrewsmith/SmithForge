using SmithForge.Main.Models;
using System;
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

        public override int GetTotalCost(ChatCommandInfo info, Chater chater)
        {
            Debug.WriteLine($"[Extend.GetTotalCost] Ранг: {chater.Rank}, Аргументы: {string.Join(", ", info.Arguments)}");

            // Для 5-го ранга и выше бесплатно
            if (chater.Rank >= 5)
            {
                Debug.WriteLine("[Extend.GetTotalCost] Бесплатно (ранг >=5)");
                return 0;
            }

            // Если аргументов нет (!!ext)
            if (info.Arguments.Count == 0)
            {
                int cost = (int)Math.Min(chater.Karma, MAX_EXTEND);
                Debug.WriteLine($"[Extend.GetTotalCost] Нет аргументов, стоимость: {cost}");
                return cost;
            }

            // Если аргумент есть (!!ext:5)
            if (int.TryParse(info.Arguments[0], out int minutes))
            {
                int cost = Math.Clamp(minutes, 1, MAX_EXTEND);
                Debug.WriteLine($"[Extend.GetTotalCost] Аргумент: {minutes}, стоимость: {cost}");
                return cost;
            }

            Debug.WriteLine("[Extend.GetTotalCost] Дефолт 1");
            return 1;
        }

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[Extend.Execute] НАЧАЛО - Текст: '{msg.Message}'");
            Debug.WriteLine($"[Extend.Execute] Аргументы: {string.Join(", ", info.Arguments)}");
            Debug.WriteLine($"[Extend.Execute] Ранг: {chater.Rank}");
            Debug.WriteLine($"[Extend.Execute] Текущее время: {msg.DisplayTimeMs}мс");

            // Определяем, сколько минут продлевать
            int minutes;

            if (info.Arguments.Count == 0)
            {
                minutes = MAX_EXTEND;
                Debug.WriteLine($"[Extend.Execute] Нет аргументов, продлеваем на {minutes} мин");
            }
            else if (int.TryParse(info.Arguments[0], out int m))
            {
                minutes = Math.Clamp(m, 1, MAX_EXTEND);
                Debug.WriteLine($"[Extend.Execute] Аргумент: {m}, продлеваем на {minutes} мин");
            }
            else
            {
                minutes = 1;
                Debug.WriteLine($"[Extend.Execute] Ошибка парсинга, продлеваем на 1 мин");
            }

            int additionalMs = minutes * 60 * 1000;
            msg.DisplayTimeMs += additionalMs;
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[Extend.Execute] Добавлено {additionalMs}мс");
            Debug.WriteLine($"[Extend.Execute] Новое время: {msg.DisplayTimeMs}мс");
            Debug.WriteLine($"[Extend.Execute] КОНЕЦ");
        }
    }
}