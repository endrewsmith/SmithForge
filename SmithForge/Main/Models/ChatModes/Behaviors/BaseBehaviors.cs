using SmithForge.Main.Models;

namespace SmithForge.Main.Models.ChatModes.Behaviors
{
    // ============================================================
    // ПОВЕДЕНИЯ ПОЯВЛЕНИЯ
    // ============================================================

    /// <summary>
    /// Появление с анимацией затухания и масштабирования
    /// </summary>
    public class FadeInAppear : IAppearBehavior
    {
        public double InitialOpacity => 0;
        public bool HasScaleAnimation => true;

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            msgVm.Opacity = 0;
            msgVm.ScaleY = 0;
            msgVm.SkipLayoutAnimation = isChatFull;
        }
    }

    /// <summary>
    /// Мгновенное появление (без анимации)
    /// </summary>
    public class InstantAppear : IAppearBehavior
    {
        public double InitialOpacity => 1;
        public bool HasScaleAnimation => false;

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            msgVm.Opacity = 1;
            msgVm.ScaleY = 1;
            msgVm.SkipLayoutAnimation = true;
        }
    }

    /// <summary>
    /// Появление с эффектом "вылетания" снизу
    /// </summary>
    public class SlideUpAppear : IAppearBehavior
    {
        public double InitialOpacity => 0;
        public bool HasScaleAnimation => false;

        public void PrepareMessage(DisplayMessageViewModel msgVm, bool isChatFull)
        {
            msgVm.Opacity = 0;
            // Для слайда нужен TranslateTransform в XAML
            msgVm.SkipLayoutAnimation = isChatFull;
        }
    }

    // ============================================================
    // ПОВЕДЕНИЯ ВРЕМЕНИ ЖИЗНИ
    // ============================================================

    /// <summary>
    /// Сообщение исчезает через заданное время
    /// </summary>
    public class FadeAfterTime : ILifecycleBehavior
    {
        private readonly int _defaultMs;

        public FadeAfterTime(int defaultMs = 10000)
        {
            _defaultMs = defaultMs;
        }

        public int GetDisplayTimeMs(CommonMessage msg)
        {
            return msg.DisplayTimeMs > 0 ? msg.DisplayTimeMs : _defaultMs;
        }
    }

    /// <summary>
    /// Сообщение не исчезает (висечно)
    /// </summary>
    public class Permanent : ILifecycleBehavior
    {
        public int GetDisplayTimeMs(CommonMessage msg) => 0;
    }

    /// <summary>
    /// Короткое время показа (для слайдшоу)
    /// </summary>
    /// <summary>
    /// Короткое время показа (для слайдшоу)
    /// </summary>
    public class ShortLife : ILifecycleBehavior
    {
        private readonly int _ms;

        public ShortLife(int ms = 5000)
        {
            _ms = ms;
        }

        public int GetDisplayTimeMs(CommonMessage msg) => _ms;
    }
}