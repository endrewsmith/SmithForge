using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class StickerCommand : BaseCommand
    {
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
            int stickerId = 1;

            // Парсим номер пака (первый аргумент)
            if (info.Arguments.Count > 0 && int.TryParse(info.Arguments[0], out int p))
            {
                packId = p;
            }

            // Парсим номер стикера (второй аргумент)
            if (info.Arguments.Count > 1 && int.TryParse(info.Arguments[1], out int s))
            {
                stickerId = s;
            }

            // Проверяем, существует ли такой стикер
            string stickerPath = StickerManager.GetStickerPath(packId, stickerId);

            if (string.IsNullOrEmpty(stickerPath))
            {
                msg.Message = $"❌ Стикер пак {packId}, номер {stickerId} не найден";
                msg.IsProcessedByCommand = true;
                Debug.WriteLine($"[StickerCommand] Стикер не найден");
                return;
            }

            // СОХРАНЯЕМ исходный текст и ДОБАВЛЯЕМ тег
            string originalText = msg.Message; // Текст до команды
            msg.Message = $"<sticker pack='{packId}' id='{stickerId}' path='{stickerPath}' />{originalText}";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[StickerCommand] КОНЕЦ - стикер: пак {packId}, номер {stickerId}");
            Debug.WriteLine($"[StickerCommand] Итоговое сообщение: '{msg.Message}'");
        }
    }
}