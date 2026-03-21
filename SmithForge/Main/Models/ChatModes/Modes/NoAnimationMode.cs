using System.Collections.ObjectModel;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes.Behaviors;

namespace SmithForge.Main.Models.ChatModes
{
    public class NoAnimationMode : IChatDisplayMode
    {
        public string Name => "Без анимаций";
        public string Description => "Сообщения появляются мгновенно";

        private readonly IAppearBehavior _appear = new InstantAppear();
        private readonly ILifecycleBehavior _lifecycle = new Permanent();

        public bool HasAppearAnimation => _appear.HasScaleAnimation;
        public bool HasSmoothScroll => false;
        public bool ShouldAutoScroll => false;
        public double InitialOpacity => _appear.InitialOpacity;

        public void Apply(DisplayMessageViewModel msgVm) { }

        public void OnMessageAdded(DisplayMessageViewModel msgVm, ObservableCollection<DisplayMessageViewModel> collection) { }

        public int GetDisplayTimeMs(CommonMessage msg) => _lifecycle.GetDisplayTimeMs(msg);

        public bool ShouldSkipScaleAnimation(bool isChatFull) => true;

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            _appear.PrepareMessage(msgVm, isChatFull);
        }
    }
}