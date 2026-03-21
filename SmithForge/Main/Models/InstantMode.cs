using SmithForge.Features.ChatOverlay;
using System.Collections.ObjectModel;

namespace SmithForge.Main.Models.ChatModes
{
    /// <summary>
    /// Мгновенный режим - сообщения появляются мгновенно
    /// </summary>
    public class InstantMode : IChatDisplayMode
    {
        public string Name => "Мгновенный";
        public string Description => "Сообщения появляются мгновенно, без анимаций";
        public bool HasAppearAnimation => false;
        public bool HasSmoothScroll => false;

        // Новые обязательные члены интерфейса
        public bool ShouldSkipScaleAnimation(bool isChatFull) => true; // Всегда пропускаем анимацию роста
        public double InitialOpacity => 1.0; // Сразу видимое
        public bool ShouldAutoScroll => false; // Не скроллим автоматически

        public void Apply(DisplayMessageViewModel msgVm)
        {
            // Никаких анимаций
        }

        public void OnMessageAdded(DisplayMessageViewModel msgVm, ObservableCollection<DisplayMessageViewModel> collection)
        {
            // Ничего не делаем
        }

        public int GetDisplayTimeMs(CommonMessage msg)
        {
            return 0; // Сообщения не исчезают
        }

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            // Для мгновенного режима сразу показываем сообщение без анимаций
            msgVm.SkipLayoutAnimation = true;
        }
    }
}