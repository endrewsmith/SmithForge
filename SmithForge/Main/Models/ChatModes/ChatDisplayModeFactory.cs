using System.Collections.Generic;
using SmithForge.Main.Models.ChatModes.Behaviors;


namespace SmithForge.Main.Models.ChatModes
{
    public static class ChatDisplayModeFactory
    {
        private static readonly Dictionary<ChatDisplayMode, IChatDisplayMode> _modes = new()
        {
            // Режимы с анимацией появления и исчезновением
            { ChatDisplayMode.AppearAndFade, new AppearAndFadeMode() },
            { ChatDisplayMode.AppearOnly, new AppearOnlyMode() },
            
            // Режимы без анимации
            { ChatDisplayMode.NoAnimation, new NoAnimationMode() },
            { ChatDisplayMode.Instant, new InstantMode() }, // если есть
            
            // Режимы со скроллом
            { ChatDisplayMode.SmoothScroll, new SmoothScrollMode() },
            { ChatDisplayMode.ScrollAndFade, new ScrollAndFadeMode() },
            
            // Специальные режимы
            { ChatDisplayMode.Slideshow, new SlideshowMode() },
            { ChatDisplayMode.Compact, new CompactMode() },
        };

        public static IChatDisplayMode GetMode(ChatDisplayMode mode)
        {
            return _modes.TryGetValue(mode, out var result)
                ? result
                : _modes[ChatDisplayMode.AppearAndFade];
        }

        public static List<ChatDisplayModeInfo> GetAvailableModes()
        {
            var list = new List<ChatDisplayModeInfo>();
            foreach (var mode in _modes)
            {
                list.Add(new ChatDisplayModeInfo
                {
                    Mode = mode.Key,
                    Name = mode.Value.Name,
                    Description = mode.Value.Description
                });
            }
            return list;
        }
    }
}