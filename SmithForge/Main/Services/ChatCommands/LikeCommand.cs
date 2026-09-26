using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class LikeCommand : BaseCommand
    {
        public override bool IsTechnical => true;
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
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 2. Проверяем автора сообщения через базу данных
            string targetAuthorId = DatabaseService.GetChaterIdByMessageNumber(messageNumber);

            if (string.IsNullOrEmpty(targetAuthorId))
            {
                Debug.WriteLine($"[LikeCommand] Сообщение #{messageNumber} не найдено в БД");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 3. Проверка на самолайк
            if (targetAuthorId == chater.Id)
            {
                Debug.WriteLine($"[LikeCommand] ЗАПРЕТ: {chater.Login} пытался лайкнуть себя (сообщение #{messageNumber})");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 4. Находим ChatLogs.Id и проверяем текущую реакцию
            long chatLogId = DatabaseService.GetMessageIdByNumber(messageNumber);
            if (chatLogId <= 0)
            {
                Debug.WriteLine($"[LikeCommand] Не удалось получить ChatLogs.Id для #{messageNumber}");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string? existingReaction = DatabaseService.GetUserReaction(chatLogId, chater.Id);
            Debug.WriteLine($"[LikeCommand] Текущая реакция пользователя: {existingReaction ?? "(нет)"}");

            // 5. Если уже стоит ЛАЙК — ничего не делаем, карму не списываем
            if (existingReaction == "like")
            {
                Debug.WriteLine($"[LikeCommand] ⏭ У пользователя уже лайк на #{messageNumber}, пропускаем без списания");
                msg.Message = string.Empty;
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // 6. Если стоит ДИЗЛАЙК → это смена реакции, списываем карму
            //    Если ничего не стоит → новый лайк, списываем карму
            Debug.WriteLine($"[LikeCommand] ✅ Лайк разрешен для #{messageNumber} от {chater.Login} " +
                            $"(было: {existingReaction ?? "ничего"} → станет: like)");

            // ✅ СТАВИМ ЛАЙК СРАЗУ В БД
            try
            {
                DatabaseService.LikeMessage(chatLogId, chater.Id);

                var counts = DatabaseService.GetReactionCounts(chatLogId);
                Debug.WriteLine($"[LikeCommand] 👍 Лайк поставлен. Likes={counts.Likes}, Dislikes={counts.Dislikes}");

                // ✅ УВЕДОМЛЯЕМ ВЕБ-ОВЕРЛЕЙ
                WebServerService.Instance?.UpdateReactionInWeb(
                    messageNumber: messageNumber,
                    reactionType: "like",
                    newCount: counts.Likes);

                // ✅ Событие в технический оверлей
                int actualKarma = GetCostForRank(chater.Rank);
                WebServerService.Instance?.SendTechnicalEvent(
                    TechEventFactory.Like(chater, messageNumber, actualKarma));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LikeCommand] ❌ Ошибка постановки лайка: {ex.Message}");
            }

            // Техническое сообщение — пустое, чтобы не шло в оверлей
            msg.Message = string.Empty;
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[LikeCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            return msg.ShouldChargeForCommand;
        }
    }
}