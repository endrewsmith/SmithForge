// Файл: DisplayMessageViewModel.cs (в основном проекте SmithForge)
using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Converters;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SmithForge.Main.Models
{
    public partial class DisplayMessageViewModel : ObservableObject
    {
        [ObservableProperty]
        private double _opacity = 1;

        [ObservableProperty]
        private double _scaleY = 1;

        [ObservableProperty]
        private bool _skipLayoutAnimation;

        [ObservableProperty]
        private bool _isRemoving = false;

        [ObservableProperty]
        private Chater _user;

        [ObservableProperty]
        private string _messageText;

        [ObservableProperty]
        private int _messageNumber;

        [ObservableProperty]
        private string _type;

        [ObservableProperty]
        private int _displayTimeMs = 5000;

        [ObservableProperty]
        private int _likes;

        [ObservableProperty]
        private int _dislikes;

        [ObservableProperty]
        private bool _shouldChargeReaction = true;

        [ObservableProperty]
        private string _stickerPath;

        [ObservableProperty]
        private double _animationDuration = 400;

        [ObservableProperty]
        private bool _showAvatar = true;

        [ObservableProperty]
        private bool _showRank = true;

        [ObservableProperty]
        private bool _showTimestamp = false;

        [ObservableProperty]
        private double _fontSize = 12;

        // ⭐ ДОБАВЛЕНО: Кэшируемое свойство для отрендеренного текста
        [ObservableProperty]
        private object _formattedMessage;

        public ICommand Action1Command { get; }
        public ICommand Action2Command { get; }
        public ICommand Action3Command { get; }
        public bool IsSticker => !string.IsNullOrEmpty(StickerPath);
        public string LikesDisplay => Likes > 0 ? Likes.ToString() : string.Empty;
        public string DislikesDisplay => Dislikes > 0 ? Dislikes.ToString() : string.Empty;
        public string DisplayName => User?.EffectiveName ?? "Unknown";
        public int MessageCount => (int)(User?.MessageCount ?? 0);
        public int UserRank => User?.Rank ?? 0;
        public string PlatformColor => User?.Accounts?.FirstOrDefault()?.PlatformColor ?? "#FFFFFF";

        private static readonly FormattedTextWithEmojiConverter _formattedTextConverter = new();

        private DataTemplate? _cachedSkin;
        private static DataTemplate? _emergencyTemplate;
        private static readonly object _emergencyLock = new object();
        private static DataTemplate? _cachedStickerTemplate;
        private static readonly object _stickerLock = new object();
        private static DataTemplate? _cachedDashboardTemplate;
        private static readonly object _dashboardLock = new object();

        public DataTemplate MessageSkin
        {
            get
            {
                if (IsSticker) return GetStickerTemplate();
                if (_cachedSkin != null) return _cachedSkin;

                try
                {
                    string skinPath = SkinService.GetSkinPath(User);
                    var template = SkinLoader.GetTemplate(skinPath);
                    _cachedSkin = CreateSafeTemplate(template);
                    return _cachedSkin;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] {ex.Message}");
                    return GetEmergencyTemplate();
                }
            }
        }

        public DataTemplate DashboardMessageSkin
        {
            get
            {
                if (_cachedDashboardTemplate != null) return _cachedDashboardTemplate;

                lock (_dashboardLock)
                {
                    if (_cachedDashboardTemplate != null) return _cachedDashboardTemplate;

                    try
                    {
                        string dashboardPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "SF_Data", "Assets", "Skins", "Unique", "dashboard.xaml");

                        if (File.Exists(dashboardPath))
                        {
                            var resourceDict = new ResourceDictionary { Source = new Uri(dashboardPath, UriKind.Absolute) };

                            if (resourceDict.Contains("ChatMessageTemplate"))
                            {
                                var template = resourceDict["ChatMessageTemplate"] as DataTemplate;
                                if (template != null)
                                {
                                    _cachedDashboardTemplate = CreateSafeTemplate(template);
                                    return _cachedDashboardTemplate;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Dashboard] ❌ Ошибка дашборд-шаблона: {ex.Message}");
                    }
                    return GetEmergencyTemplate();
                }
            }
        }

        private DataTemplate CreateSafeTemplate(DataTemplate originalTemplate)
        {
            try
            {
                if (originalTemplate == null) return GetEmergencyTemplate();
                var safeTemplate = new DataTemplate();
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                borderFactory.SetValue(Border.PaddingProperty, new Thickness(0));
                borderFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 4));
                var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenterFactory.SetValue(ContentPresenter.ContentProperty, new Binding("."));
                contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateProperty, originalTemplate);
                borderFactory.AppendChild(contentPresenterFactory);
                safeTemplate.VisualTree = borderFactory;
                safeTemplate.Seal();
                return safeTemplate;
            }
            catch
            {
                return GetEmergencyTemplate();
            }
        }

        private DataTemplate GetEmergencyTemplate()
        {
            if (_emergencyTemplate != null) return _emergencyTemplate;
            lock (_emergencyLock)
            {
                if (_emergencyTemplate != null) return _emergencyTemplate;
                try
                {
                    var template = new DataTemplate();
                    var textBlock = new FrameworkElementFactory(typeof(TextBlock));
                    textBlock.SetBinding(TextBlock.TextProperty, new Binding("MessageText"));
                    textBlock.SetValue(TextBlock.ForegroundProperty, Brushes.White);
                    textBlock.SetValue(TextBlock.MarginProperty, new Thickness(5));
                    textBlock.SetBinding(TextBlock.FontSizeProperty, new Binding("FontSize"));
                    template.VisualTree = textBlock;
                    template.Seal();
                    _emergencyTemplate = template;
                    return template;
                }
                catch
                {
                    return new DataTemplate();
                }
            }
        }

        private DataTemplate GetStickerTemplate()
        {
            if (_cachedStickerTemplate != null) return _cachedStickerTemplate;
            lock (_stickerLock)
            {
                if (_cachedStickerTemplate != null) return _cachedStickerTemplate;
                try
                {
                    var template = new DataTemplate();
                    var grid = new FrameworkElementFactory(typeof(Grid));
                    var image = new FrameworkElementFactory(typeof(Image));
                    image.SetBinding(Image.SourceProperty, new Binding("StickerPath"));
                    image.SetValue(Image.WidthProperty, 120.0);
                    image.SetValue(Image.HeightProperty, 120.0);
                    grid.AppendChild(image);
                    template.VisualTree = grid;
                    template.Seal();
                    _cachedStickerTemplate = template;
                    return template;
                }
                catch
                {
                    return new DataTemplate();
                }
            }
        }

        // ========== КОНСТРУКТОРЫ ==========

        public DisplayMessageViewModel(Chater user, CommonMessage msg)
        {
            User = user;
            MessageText = msg.Message; // ← Запись вызовет OnMessageTextChanged автоматически
            MessageNumber = msg.MessageNumber;
            Type = msg.Type;
            DisplayTimeMs = msg.DisplayTimeMs;
            ShouldChargeReaction = true;
            StickerPath = null;
            ShowAvatar = true;
            ShowRank = true;
            ShowTimestamp = false;
            SkipLayoutAnimation = false;

            //ChaterStorage.OnChaterUpdated += OnChaterUpdated;

            Action1Command = new SmithForge.Features.Dashboard.RelayCommand(OpenProfile);
            Action2Command = new SmithForge.Features.Dashboard.RelayCommand(Action2);
            Action3Command = new SmithForge.Features.Dashboard.RelayCommand(Action3);
        }

        public DisplayMessageViewModel(Chater user, CommonMessage msg, string stickerPath) : this(user, msg)
        {
            StickerPath = stickerPath;
        }

        public DisplayMessageViewModel(Chater user, string text, int messageNumber = 0, string type = "")
            : this(user, new CommonMessage { Message = text, MessageNumber = messageNumber, Type = type })
        {
        }

        // ========== ЧАСТИЧНЫЕ МЕТОДЫ ИЗМЕНЕНИЯ СВОЙСТВ ==========

        partial void OnUserChanged(Chater value)
        {
            _cachedSkin = null;
            _cachedAvatarPath = null;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(UserRank));
            OnPropertyChanged(nameof(PlatformColor));
            OnPropertyChanged(nameof(MessageSkin));
            OnPropertyChanged(nameof(AvatarPath));
        }

        // ⭐ ИСПРАВЛЕНО: Теперь конвертер вызывается строго ОДИН раз при получении текста!
        partial void OnMessageTextChanged(string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    FormattedMessage = string.Empty;
                    return;
                }

                // Генерируем Span со смайлами один раз и намертво сохраняем в кэш свойства
                FormattedMessage = _formattedTextConverter.Convert(value, typeof(object), null, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormattedMessage Cache Error] {ex.Message}");
                FormattedMessage = value;
            }
        }

        public void UpdateMessageCount() => OnPropertyChanged(nameof(MessageCount));

        private string? _cachedAvatarPath;

        public string? AvatarPath
        {
            get
            {
                if (_cachedAvatarPath != null) return _cachedAvatarPath;
                if (User == null) return null;

                string avatarPath = User.FullAvatarPath;

                if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
                {
                    _cachedAvatarPath = avatarPath;
                    return _cachedAvatarPath;
                }

                string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "Default");
                _cachedAvatarPath = Path.Combine(basePath, "unknown.png");
                return _cachedAvatarPath;
            }
        }

        private void OpenProfile()
        {
            try
            {
                if (User == null) return;

                var profileWindow = new SmithForge.Features.ChaterProfile.ChaterProfileWindow(User);
                profileWindow.Owner = Application.Current.MainWindow;
                profileWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Ошибка при открытии профиля: {ex.Message}");
            }
        }

        private void Action2() => MessageBox.Show($"Кнопка 2 - Сообщение #{MessageNumber}");
        private void Action3() => MessageBox.Show($"Кнопка 3 - Сообщение #{MessageNumber}");

        //private void OnChaterUpdated(Chater updatedChater)
        //{
        //    if (User?.Id == updatedChater.Id)
        //    {
        //        User = updatedChater;
        //        OnPropertyChanged(nameof(User));
        //        OnPropertyChanged(nameof(UserRank));
        //        OnPropertyChanged(nameof(DisplayName));
        //        OnPropertyChanged(nameof(MessageCount));
        //        OnPropertyChanged(nameof(PlatformColor));

        //        _cachedSkin = null;
        //        OnPropertyChanged(nameof(MessageSkin));

        //        _cachedAvatarPath = null;
        //        OnPropertyChanged(nameof(AvatarPath));

        //        // ✅ Если обновился пользователь — пересоздаём форматированный текст (ранг влияет на цвет)
        //        if (!string.IsNullOrEmpty(MessageText))
        //        {
        //            try
        //            {
        //                FormattedMessage = _formattedTextConverter.Convert(MessageText, typeof(object), null, null);
        //            }
        //            catch (Exception ex)
        //            {
        //                System.Diagnostics.Debug.WriteLine($"[FormattedMessage Rebuild Error] {ex.Message}");
        //            }
        //        }
        //    }
        //}

        //public void Dispose()
        //{
        //    ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        //}
    }
}