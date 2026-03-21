using System.Collections.ObjectModel;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes.Behaviors;

namespace SmithForge.Main.Models.ChatModes
{
    public class SlideshowMode : IChatDisplayMode
    {
        public string Name => "Слайдшоу";
        public string Description => "Сообщения отображаются по одному в режиме слайдшоу";

        private readonly IAppearBehavior _appear = new FadeInAppear();
        private readonly ILifecycleBehavior _lifecycle = new ShortLife(5000);

        public bool HasAppearAnimation => _appear.HasScaleAnimation;
        public bool HasSmoothScroll => false;
        public bool ShouldAutoScroll => false;
        public double InitialOpacity => _appear.InitialOpacity;

        public void Apply(DisplayMessageViewModel msgVm) { }

        public void OnMessageAdded(DisplayMessageViewModel msgVm, ObservableCollection<DisplayMessageViewModel> collection)
        {
            // Для слайдшоу удаляем предыдущие сообщения
            var toRemove = new System.Collections.Generic.List<DisplayMessageViewModel>();
            foreach (var item in collection)
            {
                if (item != msgVm)
                    toRemove.Add(item);
            }
            foreach (var item in toRemove)
            {
                collection.Remove(item);
            }
        }

        public int GetDisplayTimeMs(CommonMessage msg) => _lifecycle.GetDisplayTimeMs(msg);

        public bool ShouldSkipScaleAnimation(bool isChatFull) => false;

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            _appear.PrepareMessage(msgVm, isChatFull);
        }
    }
}