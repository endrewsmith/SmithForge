using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Services;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmithForge.Main.Models
{
    public partial class DisplayMessageViewModel : ObservableObject
    {
        [ObservableProperty]
        private Chater _user;

        [ObservableProperty]
        private string _messageText;

        [ObservableProperty]
        private int _messageNumber;

        [ObservableProperty]
        private string _type;

        [ObservableProperty]
        private int _displayTimeMs = 5000; // значение по умолчанию

        [ObservableProperty]
        private int _likes;

        [ObservableProperty]
        private int _dislikes;

        [ObservableProperty]
        private bool _shouldChargeReaction = true; // Добавлено новое свойство

        public string LikesDisplay => Likes > 0 ? Likes.ToString() : string.Empty;
        public string DislikesDisplay => Dislikes > 0 ? Dislikes.ToString() : string.Empty;

        public string DisplayName => User?.EffectiveName ?? "Unknown";
        public int MessageCount => (int)(User?.MessageCount ?? 0);
        public int UserRank => User?.Rank ?? 0;
        public string PlatformColor => User?.Accounts?.FirstOrDefault()?.PlatformColor ?? "#FFFFFF";

        private DataTemplate? _cachedSkin;
        private static DataTemplate? _emergencyTemplate;
        private static readonly object _emergencyLock = new object();

        public DataTemplate MessageSkin
        {
            get
            {
                if (_cachedSkin != null)
                    return _cachedSkin;

                try
                {
                    string skinPath = SkinService.GetSkinPath(User);
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Путь к скину: {skinPath}");

                    var template = SkinLoader.GetTemplate(skinPath);

                    // Оборачиваем шаблон в безопасный контейнер
                    _cachedSkin = CreateSafeTemplate(template);
                    return _cachedSkin;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Ошибка создания шаблона: {ex.Message}");
                    return GetEmergencyTemplate();
                }
            }
        }

        private DataTemplate CreateSafeTemplate(DataTemplate? originalTemplate)
        {
            try
            {
                if (originalTemplate == null)
                    return GetEmergencyTemplate();

                // Создаем безопасную обертку
                var safeTemplate = new DataTemplate();

                var borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 45, 48)));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                borderFactory.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));
                borderFactory.SetValue(Border.MarginProperty, new Thickness(2));

                var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenterFactory.SetValue(ContentPresenter.ContentProperty, new Binding("."));
                contentPresenterFactory.SetValue(ContentPresenter.ContentTemplateProperty, originalTemplate);

                borderFactory.AppendChild(contentPresenterFactory);
                safeTemplate.VisualTree = borderFactory;
                safeTemplate.Seal();

                return safeTemplate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SAFE] Ошибка создания безопасного шаблона: {ex.Message}");
                return GetEmergencyTemplate();
            }
        }

        private DataTemplate GetEmergencyTemplate()
        {
            if (_emergencyTemplate != null)
                return _emergencyTemplate;

            lock (_emergencyLock)
            {
                if (_emergencyTemplate != null)
                    return _emergencyTemplate;

                try
                {
                    var template = new DataTemplate();
                    var textBlock = new FrameworkElementFactory(typeof(TextBlock));
                    textBlock.SetBinding(TextBlock.TextProperty, new Binding("MessageText"));
                    textBlock.SetValue(TextBlock.ForegroundProperty, Brushes.White);
                    textBlock.SetValue(TextBlock.MarginProperty, new Thickness(5));
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

        // ЕДИНСТВЕННЫЙ КОНСТРУКТОР
        public DisplayMessageViewModel(Chater user, CommonMessage msg)
        {
            User = user;
            MessageText = msg.Message;
            MessageNumber = msg.MessageNumber;
            Type = msg.Type;
            DisplayTimeMs = msg.DisplayTimeMs;
            ShouldChargeReaction = true; // По умолчанию списываем

            System.Diagnostics.Debug.WriteLine($"[VM] Создано сообщение: User={user.Login}, " +
                $"MessageNumber={MessageNumber}, Длина={msg.LengthCategory}, Время={DisplayTimeMs}мс");
        }

        // Дополнительный конструктор для обратной совместимости (если нужен)
        public DisplayMessageViewModel(Chater user, string text, int messageNumber = 0, string type = "")
            : this(user, new CommonMessage { Message = text, MessageNumber = messageNumber, Type = type })
        {
        }

        partial void OnUserChanged(Chater value)
        {
            _cachedSkin = null;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(UserRank));
            OnPropertyChanged(nameof(PlatformColor));
            OnPropertyChanged(nameof(MessageSkin));
        }

        public void UpdateMessageCount()
        {
            OnPropertyChanged(nameof(MessageCount));
        }
    }
}