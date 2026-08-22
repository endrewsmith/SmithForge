using SmithForge.Main.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SmithForge.Features.ChatManager
{
    public partial class ChangeMethodWindow : Window, INotifyPropertyChanged
    {
        private readonly ChatConnection _chat;
        private readonly ChatManagerViewModel _chatManager;
        private bool _isShortsSelected;
        private bool _isHtmlOnlySelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsShortsSelected 
        { 
            get => _isShortsSelected; 
            set 
            { 
                if (_isShortsSelected != value)
                {
                    _isShortsSelected = value; 
                    _chat.Mode = value ? ChatMode.Shorts : ChatMode.Normal;
                    _chat.RefreshDisplayName();
                    OnPropertyChanged();
                }
            } 
        }

        public bool IsNormalSelected => !_isShortsSelected;

        public bool IsHtmlOnlySelected 
        { 
            get => _isHtmlOnlySelected; 
            set 
            { 
                if (_isHtmlOnlySelected != value)
                {
                    _isHtmlOnlySelected = value; 
                    _chat.PreferredMethod = value ? YouTubeConnectionMethod.HtmlOnly : YouTubeConnectionMethod.ApiOnly;
                    _chat.RefreshDisplayName();
                    OnPropertyChanged();
                }
            } 
        }

        public bool IsApiOnlySelected => !_isHtmlOnlySelected;

        public string ChatName 
        { 
            get => _chat.ChatName; 
            set 
            { 
                if (_chat.ChatName != value)
                {
                    _chat.ChatName = value; 
                    OnPropertyChanged();
                    _chat.RefreshDisplayName();
                }
            } 
        }

        public ChatConnection Chat => _chat;

        public ChangeMethodWindow(ChatConnection chat, ChatManagerViewModel chatManager)
        {
            InitializeComponent();
            _chat = chat;
            _chatManager = chatManager;
            
            // Подписываемся на изменения ChatName
            _chat.PropertyChanged += Chat_PropertyChanged;
            
            DataContext = this;

            // Устанавливаем начальное состояние радиокнопок
            UpdateRadioButtons();
        }

        private void Chat_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatConnection.ChatName) || 
                e.PropertyName == nameof(ChatConnection.DisplayName) ||
                e.PropertyName == nameof(ChatConnection.Mode) ||
                e.PropertyName == nameof(ChatConnection.PreferredMethod))
            {
            OnPropertyChanged(nameof(ChatName));
            }
        }

        ~ChangeMethodWindow()
        {
            // Отписываемся от события
            if (_chat != null)
            {
                _chat.PropertyChanged -= Chat_PropertyChanged;
            }
        }

        private void UpdateRadioButtons()
        {
            // Режим
            IsShortsSelected = _chat.Mode == ChatMode.Shorts;

            // Метод подключения
            IsHtmlOnlySelected = _chat.PreferredMethod == YouTubeConnectionMethod.HtmlOnly;

            // Синхронизируем ChatName
            OnPropertyChanged(nameof(ChatName));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_chat.ChatName))
            {
                MessageBox.Show("Введите название чата!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // DisplayName генерируется автоматически, уведомляем UI
            _chat.RefreshDisplayName();

            // Сохраняем изменения в файл
            _chatManager.SaveChatsToFile();

            DialogResult = true;
            Close();
        }
    }
}