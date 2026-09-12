using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    /// <summary>
    /// Команда !!hidden - отправляет сообщение только стримеру (не показывается в оверлее и на стриме)
    /// </summary>
    class HiddenCommand : BaseCommand
    {
        public override string Name => "hide";
        public override IEnumerable<string> Aliases => new[] {
            "скрыть",       // русский
            "спрятать",     // русский
            "hi",            // короткий
            "секрет",       // русский
            "secret"        // английский
        };

        public override string Description => "Отправить скрытое сообщение (видно только стримеру): !!hidden текст";
        public override int Cost => 0; // Бесплатно
        public override int MinRank => 0;

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[HiddenCommand] ========== НАЧАЛО ==========");
            Debug.WriteLine($"[HiddenCommand] Пользователь: {chater.Login}");
            Debug.WriteLine($"[HiddenCommand] Текст: {msg.Message}");

            // Сохраняем оригинальный текст
            string originalText = msg.Message;

            // Создаем специальный тег для скрытого сообщения
            msg.Message = $"<hide>{originalText}</hide>";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = false; // Бесплатно

            Debug.WriteLine($"[HiddenCommand] КОНЕЦ - стало: {msg.Message}");
            Debug.WriteLine($"[HiddenCommand] ========== КОНЕЦ ==========");
        }
    }
}