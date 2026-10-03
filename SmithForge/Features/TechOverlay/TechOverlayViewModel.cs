using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SmithForge.Features.TechOverlay
{
    public class TechOverlayViewModel : ObservableObject
    {
        private const int MaxEvents = 100;

        public ObservableCollection<TechEventDisplay> Events { get; } = new();

        /// <summary>
        /// Добавить событие в список.
        /// </summary>
        public void AddEvent(TechEvent evt)
        {
            if (evt == null) return;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => AddEvent(evt));
                return;
            }

            Events.Add(new TechEventDisplay
            {
                Icon = IconFor(evt.Kind),
                UserName = evt.UserName,
                Text = evt.Text,
                Timestamp = evt.Timestamp,
                Karma = evt.Karma   // ✅ вот это
            });

            // Ограничиваем историю
            while (Events.Count > MaxEvents)
                Events.RemoveAt(0);
        }

        public void Clear()
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(Clear);
                return;
            }
            Events.Clear();
        }

        private static string IconFor(TechEventKind kind) => kind switch
        {
            TechEventKind.Nick => "✏️",
            TechEventKind.Avatar => "🖼️",
            TechEventKind.Like => "👍",
            TechEventKind.Dislike => "👎",
            TechEventKind.Info => "📖",
            TechEventKind.Help => "❓",
            TechEventKind.KarmaGrant => "🎁",
            TechEventKind.Donation => "💰",
            _ => "⚙️"
        };
    }

    /// <summary>
    /// Модель для отображения в WPF-окне.
    /// </summary>
    public class TechEventDisplay
    {
        public string Icon { get; set; } = "⚙️";
        public string UserName { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }

        // ✅ НОВОЕ
        public double Karma { get; set; }

        // Удобные для XAML свойства
        public string KarmaDisplay => Karma != 0
            ? (Karma > 0 ? $"+{Karma:F2} ⚡" : $"{Karma:F2} ⚡")
            : "";
        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");
        public bool HasKarma => Karma > 0;
    }
}