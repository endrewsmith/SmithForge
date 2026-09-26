using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class HelpCommand : BaseCommand
    {
        private readonly Dictionary<string, IChatCommand> _allCommands;

        public override bool IsTechnical => true;
        public override string Name => "help";
        public override IEnumerable<string> Aliases => new[] { "хелп", "помощь", "h", "х" };
        public override string Description => "Справка по командам: !!help [имя_команды]";

        public HelpCommand(Dictionary<string, IChatCommand> allCommands)
        {
            _allCommands = allCommands;
        }

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[HelpCommand] ========== НАЧАЛО ==========");

            // Команда техническая — в основной чат и дашборд не идёт
            msg.Message = string.Empty;
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = false; // бесплатно

            string target = GetArg(info, 0).ToLower();
            string responseText;

            if (string.IsNullOrEmpty(target))
            {
                // Все доступные команды (уникальные)
                var availableCommands = _allCommands.Values
                    .Distinct()
                    .Where(c => c.CanExecute(chater))
                    .Cast<BaseCommand>()
                    .OrderBy(c => c.Cost)
                    .ToList();

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("📋 Доступные команды:");
                sb.AppendLine();
                foreach (var cmd in availableCommands)
                {
                    sb.AppendLine($"<b>!!{cmd.Name}</b> — {cmd.Description}");
                    sb.AppendLine($"  💰 {cmd.Cost} кармы, 👑 {cmd.MinRank}+ ранг");
                }

                responseText = sb.ToString();
                Debug.WriteLine($"[HelpCommand] Список команд для ранга {chater.Rank} ({availableCommands.Count} шт.)");
            }
            else if (_allCommands.TryGetValue(target, out var cmd) && cmd is BaseCommand baseCmd)
            {
                string aliases = baseCmd.Aliases != null && baseCmd.Aliases.Any()
                    ? $" (алиасы: {string.Join(", ", baseCmd.Aliases)})"
                    : "";

                responseText = $@"<b>!!{baseCmd.Name}</b>{aliases}
{baseCmd.Description}
💰 Стоимость: {baseCmd.Cost}
👑 Требуемый ранг: {baseCmd.MinRank}+";

                Debug.WriteLine($"[HelpCommand] Справка по команде: {baseCmd.Name}");
            }
            else
            {
                responseText = $"❌ Команда '<b>{target}</b>' не найдена";
                Debug.WriteLine($"[HelpCommand] Команда не найдена: {target}");
            }

            // ✅ Отправляем в инфо-канал
            var webServer = WebServerService.Instance;
            if (webServer != null)
            {
                webServer.SendInfoMessage(responseText, "help");
                Debug.WriteLine($"[HelpCommand] Отправлено в /info/stream: help");

                // ✅ Событие в технический оверлей
                int actualKarma = GetCostForRank(chater.Rank);
                webServer.SendTechnicalEvent(
                    TechEventFactory.Help(chater, target, actualKarma));
            }
            else
            {
                Debug.WriteLine($"[HelpCommand] ⚠️ WebServerService.Instance == null, ответ не отправлен");
            }

            Debug.WriteLine($"[HelpCommand] ========== КОНЕЦ ==========");
        }
    }
}