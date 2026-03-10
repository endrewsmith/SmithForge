using System.Collections.Generic;

namespace SmithForge.Main.Services.ChatCommands
{
    /// <summary>
    /// Информация о найденной в сообщении команде
    /// </summary>
    public class ChatCommandInfo
    {
        /// <summary>
        /// Имя команды (без !!, без аргументов)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Аргументы команды (части после : разделенные :)
        /// </summary>
        public List<string> Arguments { get; set; } = new();

        /// <summary>
        /// Сырой текст команды (включая !!)
        /// </summary>
        public string Raw { get; set; } = string.Empty;

        /// <summary>
        /// Индекс начала команды в исходном сообщении
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Длина команды в символах
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Полный текст команды с аргументами (например "!!color:red")
        /// </summary>
        public string FullCommand => Raw;

        /// <summary>
        /// Получить аргумент по индексу или значение по умолчанию
        /// </summary>
        public string GetArg(int index, string defaultValue = "")
        {
            return Arguments.Count > index ? Arguments[index] : defaultValue;
        }

        /// <summary>
        /// Проверить наличие аргумента
        /// </summary>
        public bool HasArg(int index) => Arguments.Count > index;

        public override string ToString()
        {
            return $"!!{Name}" + (Arguments.Count > 0 ? ":" + string.Join(":", Arguments) : "");
        }
    }
}