using System.Collections.ObjectModel;

namespace SmithForge.Main.Models.ChatModes
{
    public interface IChatDisplayMode
    {
        string Name { get; }
        string Description { get; }

        /// <summary>
        /// Применить настройки режима к сообщению
        /// </summary>
        void Apply(DisplayMessageViewModel msgVm);

        /// <summary>
        /// Обработать добавление сообщения
        /// </summary>
        void OnMessageAdded(DisplayMessageViewModel msgVm, ObservableCollection<DisplayMessageViewModel> collection);

        /// <summary>
        /// Получить время отображения (0 - бесконечно)
        /// </summary>
        int GetDisplayTimeMs(CommonMessage msg);

        /// <summary>
        /// Нужна ли анимация появления
        /// </summary>
        bool HasAppearAnimation { get; }

        /// <summary>
        /// Нужен ли плавный скролл
        /// </summary>
        bool HasSmoothScroll { get; }

        // НОВЫЕ УНИВЕРСАЛЬНЫЕ МЕТОДЫ:

        /// <summary>
        /// Нужно ли пропустить анимацию роста (ScaleY) для нового сообщения
        /// </summary>
        bool ShouldSkipScaleAnimation(bool isChatFull);

        /// <summary>
        /// Начальная прозрачность сообщения (0 - невидимо, 1 - видимо)
        /// </summary>
        double InitialOpacity { get; }

        /// <summary>
        /// Нужно ли автоматически скроллить вниз при добавлении сообщения
        /// </summary>
        bool ShouldAutoScroll { get; }

        /// <summary>
        /// Подготовить сообщение к отображению (универсальный метод)
        /// </summary>
        void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull);
    }
}