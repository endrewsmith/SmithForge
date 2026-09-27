namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Одна строка в блоке статистики: "1. Vasya — 1234 сообщ."
    /// </summary>
    public sealed class StatsEntry
    {
        public int Rank { get; init; }              // 1, 2, 3
        public string DisplayName { get; init; } = "";
        public string ValueText { get; init; } = ""; // "1234 сообщ." / "560 кармы"
        public string? AvatarPath { get; init; }     // локальный путь к аватарке
        public int UserRank { get; init; }            // ранг зрителя (для цвета)
        public string Platform { get; init; } = "";   // "youtube" / "twitch" / "goodgame"
    }
}