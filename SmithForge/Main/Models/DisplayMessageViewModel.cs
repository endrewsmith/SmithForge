using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Services;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        // НОВЫЕ СВОЙСТВА ДЛЯ УПРАВЛЕНИЯ ОТОБРАЖЕНИЕМ
        [ObservableProperty]
        private bool _showAvatar = true;

        [ObservableProperty]
        private bool _showRank = true;

        [ObservableProperty]
        private bool _showTimestamp = false;

        // ДОБАВЛЕНО: Свойство для размера шрифта
        [ObservableProperty]
        private double _fontSize = 12;

        public bool IsSticker => !string.IsNullOrEmpty(StickerPath);
        public string LikesDisplay => Likes > 0 ? Likes.ToString() : string.Empty;
        public string DislikesDisplay => Dislikes > 0 ? Dislikes.ToString() : string.Empty;
        public string DisplayName => User?.EffectiveName ?? "Unknown";
        public int MessageCount => (int)(User?.MessageCount ?? 0);
        public int UserRank => User?.Rank ?? 0;
        public string PlatformColor => User?.Accounts?.FirstOrDefault()?.PlatformColor ?? "#FFFFFF";

        // ДОБАВЛЯЕМ КОНВЕРТЕР ДЛЯ ФОРМАТИРОВАНИЯ
        private static readonly FormattedTextConverter _formattedTextConverter = new();

        // ДОБАВЛЯЕМ СВОЙСТВО ДЛЯ ФОРМАТИРОВАННОГО ТЕКСТА
        public object FormattedMessage
        {
            get
            {
                try
                {
                    return _formattedTextConverter.Convert(MessageText, typeof(object), null, null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormattedMessage Error] {ex.Message}");
                    return MessageText;
                }
            }
        }

        private DataTemplate? _cachedSkin;
        private static DataTemplate? _emergencyTemplate;
        private static readonly object _emergencyLock = new object();
        private static DataTemplate? _cachedStickerTemplate;
        private static readonly object _stickerLock = new object();

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

        private DataTemplate GetStickerTemplate()
        {
            if (_cachedStickerTemplate != null) return _cachedStickerTemplate;
            lock (_stickerLock)
            {
                if (_cachedStickerTemplate != null) return _cachedStickerTemplate;
                try
                {
                    var template = SkinLoader.GetStickerTemplate();
                    _cachedStickerTemplate = CreateSafeTemplate(template);
                    return _cachedStickerTemplate;
                }
                catch
                {
                    return GetEmergencyTemplate();
                }
            }
        }

        private DataTemplate CreateSafeTemplate(DataTemplate? originalTemplate)
        {
            try
            {
                if (originalTemplate == null) return GetEmergencyTemplate();
                var safeTemplate = new DataTemplate();
                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
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
                    // Привязываем FontSize к свойству FontSize
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

        public DisplayMessageViewModel(Chater user, CommonMessage msg)
        {
            User = user;
            MessageText = msg.Message;
            MessageNumber = msg.MessageNumber;
            Type = msg.Type;
            DisplayTimeMs = msg.DisplayTimeMs;
            ShouldChargeReaction = true;
            StickerPath = null;
            ShowAvatar = true;
            ShowRank = true;
            ShowTimestamp = false;
            SkipLayoutAnimation = false;
        }

        public DisplayMessageViewModel(Chater user, CommonMessage msg, string stickerPath) : this(user, msg)
        {
            StickerPath = stickerPath;
        }

        public DisplayMessageViewModel(Chater user, string text, int messageNumber = 0, string type = "")
            : this(user, new CommonMessage { Message = text, MessageNumber = messageNumber, Type = type })
        {
        }

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

        partial void OnMessageTextChanged(string value)
        {
            OnPropertyChanged(nameof(FormattedMessage));
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

                if (!string.IsNullOrEmpty(avatarPath) && System.IO.File.Exists(avatarPath))
                {
                    _cachedAvatarPath = avatarPath;
                    return _cachedAvatarPath;
                }

                string basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "Default");
                _cachedAvatarPath = System.IO.Path.Combine(basePath, "unknown.png");
                return _cachedAvatarPath;
            }
        }
    }
}