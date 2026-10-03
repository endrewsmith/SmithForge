using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    public class NickCommand : BaseCommand
    {
        public override bool IsTechnical => true;
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

            // Команда техническая — сообщение не идёт в оверлей
            msg.Message = string.Empty;
            msg.IsProcessedByCommand = true;

            string newName = GetArg(info, 0, "").Trim();

            if (string.IsNullOrEmpty(newName))
            {
                Debug.WriteLine($"[NickCommand] Ошибка: имя не указано");
                msg.ShouldChargeForCommand = false;
                return;
            }

            if (newName.Length > 30)
            {
                Debug.WriteLine($"[NickCommand] Ошибка: имя слишком длинное");
                msg.ShouldChargeForCommand = false;
                return;
            }

            if (newName.Contains("<") || newName.Contains(">") || newName.Contains("&") || newName.Contains(":"))
            {
                Debug.WriteLine($"[NickCommand] Ошибка: недопустимые символы");
                msg.ShouldChargeForCommand = false;
                return;
            }

            string oldName = chater.DisplayName ?? chater.Login;
            chater.DisplayName = newName;
            chater.IsDisplayNameCustom = true;

            DatabaseService.SaveChater(chater);
            ChaterStorage.AddOrUpdate(chater);

            Debug.WriteLine($"[NickCommand] Имя изменено: '{oldName}' -> '{newName}'");

            // ✅ Событие в технический оверлей
            int actualKarma = GetCostForRank(chater.Rank);
            WebServerService.Instance?.SendTechnicalEvent(
                TechEventFactory.Nick(chater, oldName, newName, -actualKarma));

            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[NickCommand] ========== КОНЕЦ ==========");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            return msg.ShouldChargeForCommand;
        }
    }
}