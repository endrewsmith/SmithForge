using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Linq;
using System.Windows.Input;

namespace SmithForge.Features.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        private bool _autoScrollEnabled = true;
        private bool _isScrolling = false; // Флаг, что идет анимация скролла

        public bool AutoScrollEnabled
        {
            get => _autoScrollEnabled;
            set => SetProperty(ref _autoScrollEnabled, value);
        }

        public ICommand ScrollToBottomCommand { get; }

        public DashboardViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
            ScrollToBottomCommand = new RelayCommand(ForceScrollToBottom);

        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msgVm = new DisplayMessageViewModel(user, msg);
                DisplayMessages.Add(msgVm);

                while (DisplayMessages.Count > 1000)
                    DisplayMessages.RemoveAt(0);

                if (AutoScrollEnabled)
                {
                    SmoothScrollToBottom();
                }

                System.Diagnostics.Debug.WriteLine($"[Dashboard] Добавлено сообщение от {user.Login}");
            });
        }

        public void OnScrollChanged(double verticalOffset, double scrollableHeight)
        {
            // Игнорируем события во время нашей анимации
            if (_isScrolling) return;

            bool isAtBottom = Math.Abs(scrollableHeight - verticalOffset) < 50;

            if (!isAtBottom && AutoScrollEnabled)
            {
                AutoScrollEnabled = false;
                System.Diagnostics.Debug.WriteLine("[Dashboard] Авто-скролл отключен пользователем");
            }
            else if (isAtBottom && !AutoScrollEnabled)
            {
                AutoScrollEnabled = true;
                System.Diagnostics.Debug.WriteLine("[Dashboard] Авто-скролл включен");
            }
        }

        private void ForceScrollToBottom()
        {
            AutoScrollEnabled = true;
            SmoothScrollToBottom();
        }

        private async void SmoothScrollToBottom()
        {
            // Устанавливаем флаг, чтобы не срабатывал автоскролл от событий
            _isScrolling = true;

            var window = Application.Current.Windows.OfType<DashboardWindow>().FirstOrDefault();
            if (window == null)
            {
                _isScrolling = false;
                return;
            }

            var scrollViewer = window.FindName("MainScrollViewer") as ScrollViewer;
            if (scrollViewer == null)
            {
                _isScrolling = false;
                return;
            }

            await System.Threading.Tasks.Task.Delay(50);

            scrollViewer.UpdateLayout();

            double startOffset = scrollViewer.VerticalOffset;
            double endOffset = scrollViewer.ScrollableHeight;

            if (Math.Abs(endOffset - startOffset) < 2)
            {
                _isScrolling = false;
                return;
            }

            if (double.IsNaN(endOffset) || double.IsInfinity(endOffset))
            {
                _isScrolling = false;
                return;
            }

            int duration = 400;

            var animation = new DoubleAnimation(startOffset, endOffset, TimeSpan.FromMilliseconds(duration));
            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);

            var animatable = new AnimatableProxy(startOffset);
            animatable.ValueChanged += (sender, value) =>
            {
                scrollViewer.ScrollToVerticalOffset(value);
            };

            Storyboard.SetTarget(animation, animatable);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Value"));

            storyboard.Begin();

            await System.Threading.Tasks.Task.Delay(duration + 50);

            scrollViewer.UpdateLayout();
            var finalOffset = scrollViewer.VerticalOffset;
            var finalMax = scrollViewer.ScrollableHeight;

            if (Math.Abs(finalMax - finalOffset) > 2)
            {
                var remainingAnim = new DoubleAnimation(finalOffset, finalMax, TimeSpan.FromMilliseconds(150));
                remainingAnim.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };

                var remainingStoryboard = new Storyboard();
                remainingStoryboard.Children.Add(remainingAnim);

                var remainingProxy = new AnimatableProxy(finalOffset);
                remainingProxy.ValueChanged += (p, val) =>
                {
                    scrollViewer.ScrollToVerticalOffset(val);
                };

                Storyboard.SetTarget(remainingAnim, remainingProxy);
                Storyboard.SetTargetProperty(remainingAnim, new PropertyPath("Value"));

                remainingStoryboard.Begin();

                await System.Threading.Tasks.Task.Delay(150);
            }

            // Снимаем флаг после завершения анимации
            _isScrolling = false;
        }

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

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}