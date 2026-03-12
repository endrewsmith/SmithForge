using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class DislikeCommand : BaseCommand
    {
        public override string Name => "dislike";
        public override IEnumerable<string> Aliases => new[] { "дизлайк", "d", "👎" };
        public override string Description => "Поставить дизлайк на сообщение: !!dislike:42";
        public override int Cost => 1;
        public override int MinRank => 0;

        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[DislikeCommand] ========== НАЧАЛО ==========");

            // 1. Парсим номер сообщения
            string messageNumberStr = GetArg(info, 0, "0");
            if (!int.TryParse(messageNumberStr, out int messageNumber) || messageNumber <= 0)
            {
                Debug.WriteLine($"[DislikeCommand] Ошибка: неверный номер '{messageNumberStr}'");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false; // За ошибку ввода не списываем
                return;
            }

            // 2. Проверяем автора в базе данных
            string targetAuthorId = DatabaseService.GetChaterIdByMessageNumber(messageNumber);

            if (string.IsNullOrEmpty(targetAuthorId))
            {
                Debug.WriteLine($"[DislikeCommand] Сообщение #{messageNumber} не найдено");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 3. ПРОВЕРКА НА САМО-ДИЗЛАЙК
            if (targetAuthorId == chater.Id)
            {
                Debug.WriteLine($"[DislikeCommand] ЗАПРЕТ: {chater.Login} пытался дизлайкнуть себя.");
                msg.Message = string.Empty;         // Убираем текст ошибки
                msg.IsProcessedByCommand = true;    // Считаем команду выполненной (чтобы она не вылезла как текст)
                msg.ShouldChargeForCommand = false; // Не списываем карму
                return;
            }

            // 4. Успешное выполнение
            Debug.WriteLine($"[DislikeCommand] Дизлайк разрешен для #{messageNumber}");

            // Формируем тег для UI
            msg.Message = $"<dislike msg='{messageNumber}' user='{chater.Id}' />";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true; // Разрешаем списание 1 кармы

            Debug.WriteLine($"[DislikeCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            // Списываем только если выполнение было валидным
            return msg.ShouldChargeForCommand;
        }
    }
}
