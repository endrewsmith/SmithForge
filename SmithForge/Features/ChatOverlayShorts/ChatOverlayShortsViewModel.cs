using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Text.RegularExpressions;
using System.Linq;

namespace SmithForge.Features.ChatOverlayShorts
{
    public partial class ChatOverlayShortsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSetupMode;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ChatOverlayShortsViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void AddMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    // Проверяем, не является ли сообщение реакцией или сменой ника
                    bool isReaction = msg.Message.Contains("<like") || msg.Message.Contains("<dislike");
                    bool isNickChange = msg.Message.Contains("<nick");

                    if (isReaction || isNickChange)
                    {
                        if (isReaction) ProcessReactionTags(msgVm);
                        if (isNickChange) ProcessNickTag(msgVm);

                        System.Diagnostics.Debug.WriteLine($"[Shorts] Скрытое сообщение: {msg.Message}");
                        return;
                    }

                    string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                    if (string.IsNullOrEmpty(cleanText)) return;

                    // 1. Добавляем в коллекцию
                    DisplayMessages.Add(msgVm);

                    // 2. Запускаем анимацию появления через микро-паузу
                    Task.Delay(50).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() => AnimateAppear(msgVm));
                    });

                    // Для Shorts храним больше сообщений (но используем ту же анимацию)
                    if (DisplayMessages.Count > 50)
                    {
                        var oldestMsg = DisplayMessages[0];
                        AnimateAndRemove(oldestMsg);
                    }

                    // Удаление по таймеру
                    Task.Delay(msgVm.DisplayTimeMs).ContinueWith(t =>
                    {
                        if (t.IsCanceled || t.IsFaulted) return;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (DisplayMessages.Contains(msgVm)) AnimateAndRemove(msgVm);
                        });
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Shorts AddMessage] Ошибка: {ex.Message}");
                }
            });
        }
        private void AnimateAppear(DisplayMessageViewModel msgVm)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
            {
                var fe = FindFrameworkElement(msgVm) as FrameworkElement;
                if (fe == null) return;

                double height = fe.ActualHeight;
                if (height <= 0)
                {
                    fe.UpdateLayout();
                    height = fe.ActualHeight;
                }

                // Прячем элемент
                fe.Margin = new Thickness(0, 0, 0, -height);
                fe.Opacity = 0;
                fe.RenderTransform = new TranslateTransform(0, height);

                var sb = new Storyboard();

                var marginAnim = new ThicknessAnimation(fe.Margin, new Thickness(0), TimeSpan.FromMilliseconds(500))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                Storyboard.SetTarget(marginAnim, fe);
                Storyboard.SetTargetProperty(marginAnim, new PropertyPath("Margin"));

                var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
                Storyboard.SetTarget(opacityAnim, fe);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));

                var translateAnim = new DoubleAnimation(height, 0, TimeSpan.FromMilliseconds(500))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                Storyboard.SetTarget(translateAnim, fe);
                Storyboard.SetTargetProperty(translateAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                sb.Children.Add(marginAnim);
                sb.Children.Add(opacityAnim);
                sb.Children.Add(translateAnim);

                sb.Begin();
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
        private void ProcessReactionTags(DisplayMessageViewModel msgVm)
        {
            try
            {
                if (string.IsNullOrEmpty(msgVm.MessageText)) return;

                // ОБРАБОТКА ЛАЙКА
                var likeMatch = Regex.Match(msgVm.MessageText, @"<like msg='(\d+)' user='([^']+)' />");
                if (likeMatch.Success && int.TryParse(likeMatch.Groups[1].Value, out int targetLikeNum))
                {
                    string userId = likeMatch.Groups[2].Value;
                    var targetMsg = DisplayMessages.FirstOrDefault(m => m.MessageNumber == targetLikeNum);

                    if (targetMsg != null)
                    {
                        if (targetMsg.User?.Id == userId)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Shorts React] Запрещено: нельзя лайкать свое сообщение #{targetLikeNum}");
                            msgVm.ShouldChargeReaction = false;
                        }
                        else
                        {
                            targetMsg.Likes++;
                            msgVm.ShouldChargeReaction = true;
                            System.Diagnostics.Debug.WriteLine($"[Shorts React] Лайк на #{targetLikeNum} засчитан");
                        }
                    }
                    else { msgVm.ShouldChargeReaction = false; }

                    msgVm.MessageText = Regex.Replace(msgVm.MessageText, @"<like msg='\d+' user='[^']+' />", "").Trim();
                }

                // ОБРАБОТКА ДИЗЛАЙКА
                var dislikeMatch = Regex.Match(msgVm.MessageText, @"<dislike msg='(\d+)' user='([^']+)' />");
                if (dislikeMatch.Success && int.TryParse(dislikeMatch.Groups[1].Value, out int targetDisNum))
                {
                    string userId = dislikeMatch.Groups[2].Value;
                    var targetMsg = DisplayMessages.FirstOrDefault(m => m.MessageNumber == targetDisNum);

                    if (targetMsg != null)
                    {
                        if (targetMsg.User?.Id == userId)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Shorts React] Запрещено: нельзя дизлайкать свое сообщение #{targetDisNum}");
                            msgVm.ShouldChargeReaction = false;
                        }
                        else
                        {
                            targetMsg.Dislikes++;
                            msgVm.ShouldChargeReaction = true;
                            System.Diagnostics.Debug.WriteLine($"[Shorts React] Дизлайк на #{targetDisNum} засчитан");
                        }
                    }
                    else { msgVm.ShouldChargeReaction = false; }

                    msgVm.MessageText = Regex.Replace(msgVm.MessageText, @"<dislike msg='\d+' user='[^']+' />", "").Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts ProcessReactionTags] Ошибка: {ex.Message}");
            }
        }

        private void ProcessNickTag(DisplayMessageViewModel msgVm)
        {
            try
            {
                var nickMatch = Regex.Match(msgVm.MessageText, @"<nick old='([^']*)' new='([^']*)'></nick>");
                if (nickMatch.Success)
                {
                    string oldName = nickMatch.Groups[1].Value;
                    string newName = nickMatch.Groups[2].Value;
                    System.Diagnostics.Debug.WriteLine($"[Shorts Nick] {oldName} → {newName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts ProcessNickTag] Ошибка: {ex.Message}");
            }
        }

        private void AnimateAndRemove(DisplayMessageViewModel msgVm)
        {
            try
            {
                var fe = FindFrameworkElement(msgVm);
                if (fe != null)
                {
                    int index = DisplayMessages.IndexOf(msgVm);

                    var scaleTransform = new ScaleTransform(1, 1);
                    fe.RenderTransform = scaleTransform;
                    fe.RenderTransformOrigin = new Point(0.5, 1);

                    var scaleYAnimation = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };

                    var opacityAnimation = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(300)
                    };

                    Storyboard.SetTarget(scaleYAnimation, fe);
                    Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

                    Storyboard.SetTarget(opacityAnimation, fe);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));

                    var storyboard = new Storyboard();
                    storyboard.Children.Add(scaleYAnimation);
                    storyboard.Children.Add(opacityAnimation);

                    storyboard.Completed += (s, e) =>
                    {
                        try
                        {
                            fe.RenderTransform = null;
                            DisplayMessages.Remove(msgVm);

                            for (int i = 0; i < index; i++)
                            {
                                if (i < DisplayMessages.Count)
                                {
                                    var upperMsg = DisplayMessages[i];
                                    var upperFe = FindFrameworkElement(upperMsg);

                                    if (upperFe != null)
                                    {
                                        var translateYAnimation = new DoubleAnimation
                                        {
                                            From = -fe.ActualHeight,
                                            To = 0,
                                            Duration = TimeSpan.FromMilliseconds(400),
                                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                                        };

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

                            System.Diagnostics.Debug.WriteLine($"[ShortsViewModel] Сообщение удалено");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Shorts Remove] Ошибка: {ex.Message}");
                        }
                    };

                    storyboard.Begin();
                }
                else
                {
                    DisplayMessages.Remove(msgVm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts AnimateAndRemove] Ошибка: {ex.Message}");
            }
        }

        private FrameworkElement? FindFrameworkElement(DisplayMessageViewModel msgVm)
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is ChatOverlayShortsWindow overlayWindow)
                    {
                        var itemsControl = overlayWindow.FindName("MessagesList") as System.Windows.Controls.ItemsControl;
                        if (itemsControl != null)
                        {
                            var container = itemsControl.ItemContainerGenerator.ContainerFromItem(msgVm);
                            return container as FrameworkElement;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Shorts FindFrameworkElement] Ошибка: {ex.Message}");
            }
            return null;
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    foreach (var msg in DisplayMessages.Where(m => m.User?.Id == updatedChater.Id))
                    {
                        msg.User = updatedChater;
                        msg.UpdateMessageCount();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Shorts OnChaterUpdated] Ошибка: {ex.Message}");
                }
            });
        }

        public void ClearMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
                System.Diagnostics.Debug.WriteLine("[ShortsViewModel] Сообщения очищены");
            });
        }
    }
}