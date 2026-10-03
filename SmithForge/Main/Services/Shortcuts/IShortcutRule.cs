namespace SmithForge.Main.Services.Shortcuts
{
    /// <summary>
    /// Правило замены сокращения в сообщении.
    /// Возвращает новое сообщение (или исходное, если правило не сработало).
    /// </summary>
    public interface IShortcutRule
    {
        /// <summary>Имя правила для логов.</summary>
        string Name { get; }

        /// <summary>Применить правило к сообщению.</summary>
        string Apply(string message);
    }
}