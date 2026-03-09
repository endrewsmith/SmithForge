using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SmithForge.Features.ChatOverlay
{
    public partial class ChatOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ChatOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var msgVm = new DisplayMessageViewModel(user, msg.Message, msg.MessageNumber, msg.Type);

                // Устанавливаем время отображения из CommonMessage
                msgVm.DisplayTimeMs = msg.DisplayTimeMs;

                // ОТЛАДКА
                System.Diagnostics.Debug.WriteLine($"[ChatOverlayViewModel] Создан msgVm: " +
                    $"User={user.Login}, MessageNumber={msgVm.MessageNumber}, " +
                    $"Длина={msg.Message?.Length ?? 0}, Время={msgVm.DisplayTimeMs}мс");

                DisplayMessages.Add(msgVm);

                if (DisplayMessages.Count > 8)
                {
                    // Плавно удаляем самое старое сообщение
                    var oldestMsg = DisplayMessages[0];
                    AnimateAndRemove(oldestMsg);
                }

                // Запускаем таймер для плавного удаления
                Task.Delay(msgVm.DisplayTimeMs).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (DisplayMessages.Contains(msgVm))
                        {
                            AnimateAndRemove(msgVm);
                        }
                    });
                });
            });
        }

        private void AnimateAndRemove(DisplayMessageViewModel msgVm)
        {
            // Находим визуальный элемент для анимации
            var fe = FindFrameworkElement(msgVm);
            if (fe != null)
            {
                // Находим индекс удаляемого элемента
                int index = DisplayMessages.IndexOf(msgVm);

                // Сохраняем оригинальный Margin
                var originalMargin = fe.Margin;

                // Устанавливаем трансформацию
                var scaleTransform = new ScaleTransform(1, 1);
                fe.RenderTransform = scaleTransform;
                fe.RenderTransformOrigin = new Point(0.5, 1);

                // Анимация уменьшения масштаба по Y
                var scaleYAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                // Анимация прозрачности
                var opacityAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300)
                };

                // Запускаем анимации
                Storyboard.SetTarget(scaleYAnimation, fe);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

                Storyboard.SetTarget(opacityAnimation, fe);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

                var storyboard = new Storyboard();
                storyboard.Children.Add(scaleYAnimation);
                storyboard.Children.Add(opacityAnimation);

                // После анимации удаляем из коллекции
                storyboard.Completed += (s, e) =>
                {
                    // Сбрасываем трансформацию
                    fe.RenderTransform = null;

                    // Удаляем элемент
                    DisplayMessages.Remove(msgVm);

                    // Анимируем ТОЛЬКО элементы ВЫШЕ удаленного (они должны опуститься вниз)
                    for (int i = 0; i < index; i++)
                    {
                        if (i < DisplayMessages.Count) // Проверяем что элемент еще существует
                        {
                            var upperMsg = DisplayMessages[i];
                            var upperFe = FindFrameworkElement(upperMsg);

                            if (upperFe != null)
                            {
                                // Анимация смещения вниз
                                var translateYAnimation = new DoubleAnimation
                                {
                                    From = -fe.ActualHeight, // Смещение вверх на высоту удаленного
                                    To = 0,
                                    Duration = TimeSpan.FromMilliseconds(400),
                                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                                };

                                // Используем TranslateTransform для плавного перемещения
                                var translateTransform = new TranslateTransform(0, -fe.ActualHeight);
                                upperFe.RenderTransform = translateTransform;

                                Storyboard.SetTarget(translateYAnimation, upperFe);
                                Storyboard.SetTargetProperty(translateYAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                                var upperStoryboard = new Storyboard();
                                upperStoryboard.Children.Add(translateYAnimation);

                                upperStoryboard.Completed += (_, _) =>
                                {
                                    upperFe.RenderTransform = null;
                                };

                                upperStoryboard.Begin();
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[ChatOverlayViewModel] Сообщение удалено, элементы выше плавно опустились");
                };

                storyboard.Begin();
            }
            else
            {
                // Если не нашли элемент - просто удаляем
                DisplayMessages.Remove(msgVm);
            }
        }

        private void AnimateReposition(int startIndex)
        {
            // Анимируем появление элементов на новых позициях
            for (int i = startIndex; i < DisplayMessages.Count; i++)
            {
                var msg = DisplayMessages[i];
                var fe = FindFrameworkElement(msg);

                if (fe != null)
                {
                    // Небольшая задержка для создания эффекта волны
                    int delay = (i - startIndex) * 30;

                    // Начинаем с почти прозрачного состояния
                    fe.Opacity = 0.5;

                    // Анимация плавного появления
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(400),
                        BeginTime = TimeSpan.FromMilliseconds(delay),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    Storyboard.SetTarget(opacityAnimation, fe);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

                    // Небольшое смещение для эффекта
                    var translateTransform = new TranslateTransform(0, -10);
                    fe.RenderTransform = translateTransform;

                    var translateAnimation = new DoubleAnimation
                    {
                        From = -10,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(400),
                        BeginTime = TimeSpan.FromMilliseconds(delay),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    Storyboard.SetTarget(translateAnimation, fe);
                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                    var storyboard = new Storyboard();
                    storyboard.Children.Add(opacityAnimation);
                    storyboard.Children.Add(translateAnimation);

                    storyboard.Completed += (s, e) =>
                    {
                        fe.RenderTransform = null;
                    };

                    storyboard.Begin();
                }
            }
        }

        private FrameworkElement? FindFrameworkElement(DisplayMessageViewModel msgVm)
        {
            // Поиск визуального элемента по DataContext
            foreach (Window window in Application.Current.Windows)
            {
                if (window is ChatOverlayWindow overlayWindow)
                {
                    // Ищем элемент в ItemsControl
                    var itemsControl = overlayWindow.FindName("MessagesList") as System.Windows.Controls.ItemsControl;
                    if (itemsControl != null)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(msgVm);
                        return container as FrameworkElement;
                    }
                }
            }
            return null;
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
    }
}