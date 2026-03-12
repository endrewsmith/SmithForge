using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class LikeCommand : BaseCommand
    {
        public override string Name => "like";
        public override IEnumerable<string> Aliases => new[] { "лайк", "l", "👍" };
        public override string Description => "Поставить лайк на сообщение: !!like:42";
        public override int Cost => 1;
        public override int MinRank => 0;

        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[LikeCommand] ========== НАЧАЛО ==========");

            // 1. Парсим номер сообщения
            string messageNumberStr = GetArg(info, 0, "0");
            if (!int.TryParse(messageNumberStr, out int messageNumber) || messageNumber <= 0)
            {
                Debug.WriteLine($"[LikeCommand] Ошибка: неверный формат номера '{messageNumberStr}'");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false; // За ошибку ввода карму не списываем
                return;
            }

            // 2. Проверяем автора сообщения через базу данных
            string targetAuthorId = DatabaseService.GetChaterIdByMessageNumber(messageNumber);

            // Если сообщение не найдено в базе
            if (string.IsNullOrEmpty(targetAuthorId))
            {
                Debug.WriteLine($"[LikeCommand] Сообщение #{messageNumber} не найдено в БД");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 3. ПРОВЕРКА НА САМОЛАЙК
            if (targetAuthorId == chater.Id)
            {
                Debug.WriteLine($"[LikeCommand] ЗАПРЕТ: {chater.Login} пытался лайкнуть себя (сообщение #{messageNumber})");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;   // Скрываем команду из чата
                msg.ShouldChargeForCommand = false; // КАРМА НЕ СПИШЕТСЯ
                return;
            }

            // 4. Успешное выполнение
            Debug.WriteLine($"[LikeCommand] Лайк разрешен для #{messageNumber} от {chater.Login}");

            // Формируем тег, который потом обработает UI или другой сервис
            msg.Message = $"<like msg='{messageNumber}' user='{chater.Id}' />";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true; // Теперь процессор спишет 1 карму

            Debug.WriteLine($"[LikeCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            // Используем флаг из сообщения, который мы установили в Execute
            return msg.ShouldChargeForCommand;
        }
    }
}
