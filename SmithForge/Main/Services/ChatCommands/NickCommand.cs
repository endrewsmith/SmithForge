using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class NickCommand : BaseCommand
    {
        public override string Name => "nick";
        public override IEnumerable<string> Aliases => new[] { "n", "имя", "ник" };
        public override string Description => "Сменить отображаемое имя: !!nick:НовоеИмя";
        public override int Cost => 10;
        public override int MinRank => 0;

        public override int[] FreeForRanks => Array.Empty<int>();

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[NickCommand] ========== НАЧАЛО ==========");
            Debug.WriteLine($"[NickCommand] Аргументы: {string.Join(", ", info.Arguments)}");

            // Получаем новое имя из аргументов (первый после двоеточия)
            string newName = GetArg(info, 0, "").Trim();

            if (string.IsNullOrEmpty(newName))
            {
                Debug.WriteLine($"[NickCommand] Ошибка: имя не указано");
                msg.Message = "❌ Укажите новое имя: !!nick:НовоеИмя";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // Проверяем длину имени
            if (newName.Length > 30)
            {
                Debug.WriteLine($"[NickCommand] Ошибка: имя слишком длинное");
                msg.Message = "❌ Имя не должно превышать 30 символов";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // Проверяем на недопустимые символы
            if (newName.Contains("<") || newName.Contains(">") || newName.Contains("&") || newName.Contains(":"))
            {
                Debug.WriteLine($"[NickCommand] Ошибка: недопустимые символы");
                msg.Message = "❌ Имя содержит недопустимые символы (<, >, &, :)";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string oldName = chater.DisplayName ?? chater.Login;
            chater.DisplayName = newName;

            DatabaseService.SaveChater(chater);
            ChaterStorage.AddOrUpdate(chater);

            Debug.WriteLine($"[NickCommand] Имя изменено: '{oldName}' -> '{newName}'");

            msg.Message = $"<nick old='{oldName}' new='{newName}'></nick>";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[NickCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            return msg.ShouldChargeForCommand;
        }
    }
}