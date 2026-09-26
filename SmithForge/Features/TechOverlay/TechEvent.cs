using System;

namespace SmithForge.Features.TechOverlay
{
    /// <summary>
    /// Тип технического события — определяет иконку и цвет в оверлее.
    /// </summary>
    public enum TechEventKind
    {
        Other,
        Nick,
        Avatar,
        Like,
        Dislike,
        Info,
        Help,
        KarmaGrant          
    }

    /// <summary>
    /// Одно техническое событие, отправляется в /tech/stream.
    /// </summary>
    public class TechEvent
    {
        public string UserName { get; set; } = "Аноним";
        public string UserLogin { get; set; } = "";
        public TechEventKind Kind { get; set; } = TechEventKind.Other;
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public int Karma { get; set; } = 0;
    }
}