using SmithForge.Main.Models;

namespace SmithForge.Main.Models.ChatModes.Behaviors
{
    /// <summary>
    /// Поведение времени жизни сообщения
    /// </summary>
    public interface ILifecycleBehavior
    {
        /// <summary>
        /// Время отображения в миллисекундах (0 - бесконечно)
        /// </summary>
        int GetDisplayTimeMs(CommonMessage msg);
    }
}