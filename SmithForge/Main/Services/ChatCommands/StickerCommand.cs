using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class StickerCommand : BaseCommand
    {
        public override bool IsDashboardVisible => false;
        public override string Name => "st";
        public override IEnumerable<string> Aliases => new[] { "стикер", "sticker", "стик" };
        public override string Description => "Отправить стикер: !!st:1:2 (пак 1, стикер 2)";
        public override int Cost => 2;
        public override int MinRank => 0;

        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[StickerCommand] НАЧАЛО - Аргументы: {string.Join(", ", info.Arguments)}");
            Debug.WriteLine($"[StickerCommand] Исходный текст: '{msg.Message}'");

            int packId = 1;
            string stickerIdStr = "1";

            // ✅ Парсим пак (только число)
            if (info.Arguments.Count > 0 && int.TryParse(info.Arguments[0], out int p))
            {
                packId = p;
            }

            // ✅ Парсим стикер (число или random)
            if (info.Arguments.Count > 1)
            {
                stickerIdStr = info.Arguments[1];
            }

            string stickerPath = null;
            int finalStickerId = 1;

            // ✅ ОБРАБОТКА "random" — случайный стикер из пака
            if (stickerIdStr.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                var pack = StickerManager.GetPack(packId);

                if (pack == null || pack.StickerFiles.Count == 0)
                {
                    msg.Message = $"❌ Пак {packId} пуст или не найден";
                    msg.IsProcessedByCommand = true;
                    msg.ShouldChargeForCommand = false;
                    return;
                }

                var random = new Random();
                int randomIndex = random.Next(pack.StickerFiles.Count);
                stickerPath = pack.StickerFiles[randomIndex];
                finalStickerId = randomIndex + 1;

                Debug.WriteLine($"[StickerCommand] 🎲 Рандом: выбран стикер #{finalStickerId} из {pack.StickerFiles.Count}");
            }
            else
            {
                // ✅ Конкретный стикер
                if (int.TryParse(stickerIdStr, out int s))
                {
                    finalStickerId = s;
                }

                stickerPath = StickerManager.GetStickerPath(packId, finalStickerId);
            }

            if (string.IsNullOrEmpty(stickerPath))
            {
                msg.Message = $"❌ Стикер пак {packId}, номер {stickerIdStr} не найден";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                Debug.WriteLine($"[StickerCommand] Стикер не найден");
                return;
            }

            // ✅ УДАЛЯЕМ КОМАНДУ ИЗ ТЕКСТА
            string textAfterCommand = msg.Message;
            var commandMatch = System.Text.RegularExpressions.Regex.Match(textAfterCommand, @"!!st:[^ ]+");
            if (commandMatch.Success)
            {
                textAfterCommand = textAfterCommand.Remove(commandMatch.Index, commandMatch.Length).Trim();
            }

            msg.Message = $"<sticker pack='{packId}' id='{finalStickerId}' path='{stickerPath}' />{textAfterCommand}";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[StickerCommand] КОНЕЦ - стикер: пак {packId}, номер {finalStickerId}");
            Debug.WriteLine($"[StickerCommand] Итоговое сообщение: '{msg.Message}'");
        }
    }
}