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

            string messageNumberStr = GetArg(info, 0, "0");
            if (!int.TryParse(messageNumberStr, out int messageNumber) || messageNumber <= 0)
            {
                Debug.WriteLine($"[DislikeCommand] Ошибка: неверный формат номера '{messageNumberStr}'");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string targetAuthorId = DatabaseService.GetChaterIdByMessageNumber(messageNumber);

            if (string.IsNullOrEmpty(targetAuthorId))
            {
                Debug.WriteLine($"[DislikeCommand] Сообщение #{messageNumber} не найдено в БД");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            if (targetAuthorId == chater.Id)
            {
                Debug.WriteLine($"[DislikeCommand] ЗАПРЕТ: {chater.Login} пытался дизлайкнуть себя (сообщение #{messageNumber})");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // ✅ Проверяем текущую реакцию
            long chatLogId = DatabaseService.GetMessageIdByNumber(messageNumber);
            if (chatLogId <= 0)
            {
                Debug.WriteLine($"[DislikeCommand] Не удалось получить ChatLogs.Id для #{messageNumber}");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string? existingReaction = DatabaseService.GetUserReaction(chatLogId, chater.Id);
            Debug.WriteLine($"[DislikeCommand] Текущая реакция пользователя: {existingReaction ?? "(нет)"}");

            // ✅ Если уже стоит ДИЗЛАЙК — пропускаем без списания
            if (existingReaction == "dislike")
            {
                Debug.WriteLine($"[DislikeCommand] ⏭ У пользователя уже дизлайк на #{messageNumber}, пропускаем без списания");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[DislikeCommand] ✅ Дизлайк разрешен для #{messageNumber} от {chater.Login} " +
                            $"(было: {existingReaction ?? "ничего"} → станет: dislike)");

            msg.Message = $"<dislike msg='{messageNumber}' user='{chater.Id}' />";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[DislikeCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            return msg.ShouldChargeForCommand;
        }
    }
}