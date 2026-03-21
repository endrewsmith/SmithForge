using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Models
{
    public enum ChatDisplayMode
    {
        /// <summary>
        /// Анимация появления + сообщения исчезают
        /// </summary>
        NoAnimation = 0,

        /// <summary>
        /// Плавный скроллинг + сообщения не исчезают
        /// </summary>
        SmoothScroll = 1,

        /// <summary>
        /// Мгновенное появление + сообщения не исчезают
        /// </summary>
        Instant = 2,

        /// <summary>
        /// Анимация появления + сообщения не исчезают (накапливаются)
        /// </summary>
        AppearOnly = 3,

        /// <summary>
        /// Плавный скроллинг + сообщения исчезают через время
        /// </summary>
        ScrollAndFade = 4,

        /// <summary>
        /// Слайд-шоу (одно сообщение за раз)
        /// </summary>
        Slideshow = 5,

        /// <summary>
        /// Компактный режим (только текст, без аватаров)
        /// </summary>
        Compact = 6,
        AppearAndFade = 7
    }
}
