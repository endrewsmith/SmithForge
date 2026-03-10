using SmithForge.Main.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services.ChatCommands
{
    public class ColorCommand : BaseCommand
    {
        public override string Name => "color";
        public override IEnumerable<string> Aliases => new[] { "c", "цвет", "ц" };
        public override string Description => "Покрасить текст: !!color:r или !!color:#ff0000";
        public override int Cost => 3;
        public override int MinRank => 0;

        // Бесплатно для 5 ранга
        public override int[] FreeForRanks => new[] { 5 };

        // Соответствие букв и цветов
        private readonly Dictionary<string, string> _colorMap = new()
        {
            ["r"] = "red",
            ["red"] = "red",
            ["g"] = "green",
            ["green"] = "green",
            ["b"] = "blue",
            ["blue"] = "blue",
            ["y"] = "yellow",
            ["yellow"] = "yellow",
            ["p"] = "purple",
            ["purple"] = "purple",
            ["о"] = "orange",
            ["orange"] = "orange",
            ["c"] = "cyan",
            ["cyan"] = "cyan",
            ["m"] = "magenta",
            ["magenta"] = "magenta"
        };

        // Регулярка для проверки HEX-цвета (статическая и скомпилированная)
        private static readonly Regex HexColorRegex = new Regex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$", RegexOptions.Compiled);

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[ColorCommand] ========== НАЧАЛО ==========");
            Debug.WriteLine($"[ColorCommand] Текст ДО: '{msg.Message}'");
            Debug.WriteLine($"[ColorCommand] Аргументы: {string.Join(", ", info.Arguments)}");
            Debug.WriteLine($"[ColorCommand] Ранг пользователя: {chater.Rank}");
            Debug.WriteLine($"[ColorCommand] Карма пользователя: {chater.Karma}");

            string colorParam = GetArg(info, 0, "r").ToLower();
            Debug.WriteLine($"[ColorCommand] Параметр цвета: '{colorParam}'");

            string colorValue;

            // Проверяем, является ли параметр HEX-цветом
            if (colorParam.StartsWith("#") && HexColorRegex.IsMatch(colorParam))
            {
                // Используем HEX напрямую
                colorValue = colorParam;
                Debug.WriteLine($"[ColorCommand] HEX-цвет: {colorValue}");
            }
            else if (_colorMap.TryGetValue(colorParam, out string? mappedColor))
            {
                // Используем предопределенный цвет
                colorValue = mappedColor;
                Debug.WriteLine($"[ColorCommand] Именованный цвет: {colorValue}");
            }
            else
            {
                // По умолчанию красный
                colorValue = "red";
                Debug.WriteLine($"[ColorCommand] Неизвестный параметр '{colorParam}', используем красный");
            }

            string originalText = msg.Message;

            // Применяем цвет
            msg.Message = $"<color={colorValue}>{msg.Message}</color>";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[ColorCommand] Было: '{originalText}'");
            Debug.WriteLine($"[ColorCommand] Стало: '{msg.Message}'");
            Debug.WriteLine($"[ColorCommand] IsProcessedByCommand: {msg.IsProcessedByCommand}");

            int actualCost = GetCostForRank(chater.Rank);
            Debug.WriteLine($"[ColorCommand] Стоимость: {actualCost} (из {Cost})");
            Debug.WriteLine($"[ColorCommand] ========== КОНЕЦ ==========");
        }
    }
}