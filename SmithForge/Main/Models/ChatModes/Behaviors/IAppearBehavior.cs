using SmithForge.Main.Models;

namespace SmithForge.Main.Models.ChatModes.Behaviors
{
    /// <summary>
    /// Поведение появления сообщения
    /// </summary>
    public interface IAppearBehavior
    {
        /// <summary>
        /// Начальная прозрачность (0 - невидимо, 1 - видимо)
        /// </summary>
        double InitialOpacity { get; }

        /// <summary>
        /// Нужна ли анимация масштаба
        /// </summary>
        bool HasScaleAnimation { get; }

        /// <summary>
        /// Подготовить сообщение к анимации
        /// </summary>
        void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull);
    }
}