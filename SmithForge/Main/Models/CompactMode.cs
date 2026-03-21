using SmithForge.Features.ChatOverlay;
using System.Collections.ObjectModel;

namespace SmithForge.Main.Models.ChatModes
{
    public class CompactMode : IChatDisplayMode
    {
        public string Name => "Компактный";
        public string Description => "Компактный режим без аватаров и рангов";
        public bool HasAppearAnimation => false;
        public bool HasSmoothScroll => false;

        public bool ShouldSkipScaleAnimation(bool isChatFull) => true;
        public double InitialOpacity => 1.0;
        public bool ShouldAutoScroll => false;

        public void Apply(DisplayMessageViewModel msgVm)
        {
            // Скрываем аватар и ранг для компактного режима
            msgVm.ShowAvatar = false;
            msgVm.ShowRank = false;
            msgVm.ShowTimestamp = false;
        }

        public void OnMessageAdded(DisplayMessageViewModel msgVm, ObservableCollection<DisplayMessageViewModel> collection)
        {
            // Ничего не делаем
        }

        public int GetDisplayTimeMs(CommonMessage msg)
        {
            return 0;
        }

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            msgVm.SkipLayoutAnimation = ShouldSkipScaleAnimation(isChatFull);
        }
    }
}