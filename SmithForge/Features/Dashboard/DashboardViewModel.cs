using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Linq;

namespace SmithForge.Features.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public DashboardViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msgVm = new DisplayMessageViewModel(user, msg);

                // Добавляем В КОНЕЦ (новые сообщения снизу)
                DisplayMessages.Add(msgVm);

                // Ограничиваем количество сообщений (удаляем старые СВЕРХУ)
                while (DisplayMessages.Count > 1000)
                    DisplayMessages.RemoveAt(0);  // удаляем первое (самое старое)

                // Плавный скролл к последнему сообщению (вниз)
                SmoothScrollToBottom();

                System.Diagnostics.Debug.WriteLine($"[Dashboard] Добавлено сообщение от {user.Login}");
            });
        }

        private void SmoothScrollToBottom()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.Windows.OfType<DashboardWindow>().FirstOrDefault();
                if (window == null) return;

                var scrollViewer = window.FindName("MainScrollViewer") as ScrollViewer;
                if (scrollViewer == null) return;

                double startOffset = scrollViewer.VerticalOffset;
                double endOffset = scrollViewer.ScrollableHeight;
                double duration = 300;

                var animation = new DoubleAnimation(startOffset, endOffset, TimeSpan.FromMilliseconds(duration));
                animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                var storyboard = new Storyboard();
                storyboard.Children.Add(animation);

                var animatable = new AnimatableProxy(startOffset);
                animatable.ValueChanged += (s, e) =>
                {
                    scrollViewer.ScrollToVerticalOffset(animatable.Value);
                };

                Storyboard.SetTarget(animation, animatable);
                Storyboard.SetTargetProperty(animation, new PropertyPath("Value"));

                storyboard.Begin();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void SmoothScrollToTop()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.Windows.OfType<DashboardWindow>().FirstOrDefault();
                if (window == null) return;

                var scrollViewer = window.FindName("MainScrollViewer") as ScrollViewer;
                if (scrollViewer == null) return;

                // Используем анимацию через DoubleAnimation с ScrollToVerticalOffset
                double startOffset = scrollViewer.VerticalOffset;
                double endOffset = 0;
                double duration = 300; // мс

                var animation = new DoubleAnimation(startOffset, endOffset, TimeSpan.FromMilliseconds(duration));
                animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                var storyboard = new Storyboard();
                storyboard.Children.Add(animation);

                // Создаем временный объект для анимации
                var animatable = new AnimatableProxy(startOffset);
                animatable.ValueChanged += (s, e) =>
                {
                    scrollViewer.ScrollToVerticalOffset(animatable.Value);
                };

                Storyboard.SetTarget(animation, animatable);
                Storyboard.SetTargetProperty(animation, new PropertyPath("Value"));

                storyboard.Begin();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // Вспомогательный класс для анимации
        public class AnimatableProxy : FrameworkElement
        {
            public static readonly DependencyProperty ValueProperty =
                DependencyProperty.Register("Value", typeof(double), typeof(AnimatableProxy),
                    new PropertyMetadata(0.0, OnValueChanged));

            public double Value
            {
                get { return (double)GetValue(ValueProperty); }
                set { SetValue(ValueProperty, value); }
            }

            public event EventHandler<double> ValueChanged;

            public AnimatableProxy(double initialValue)
            {
                Value = initialValue;
            }

            private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var proxy = (AnimatableProxy)d;
                proxy.ValueChanged?.Invoke(proxy, (double)e.NewValue);
            }
        }
        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var msg in DisplayMessages.Where(m => m.User?.Id == updatedChater.Id))
                {
                    msg.User = updatedChater;
                    msg.UpdateMessageCount();
                }
            });
        }

        public void ClearMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                System.Diagnostics.Debug.WriteLine("[Dashboard] Сообщения очищены");
            });
        }
    }
}