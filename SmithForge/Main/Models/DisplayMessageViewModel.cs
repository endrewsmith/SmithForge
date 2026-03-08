using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Services;
using System.Linq;
using System.Windows;

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

        public string DisplayName => User?.EffectiveName ?? "Unknown";
        public int MessageCount => (int)(User?.MessageCount ?? 0);

        // ДОБАВЬТЕ ЭТО СВОЙСТВО
        public int UserRank => User?.Rank ?? 0;

        public DataTemplate MessageSkin => SkinLoader.GetTemplate(SkinService.GetSkinPath(User));

        public DisplayMessageViewModel(Chater user, string text, int messageNumber = 0)
        {
            User = user;
            MessageText = text;
            MessageNumber = messageNumber;
        }

        partial void OnUserChanged(Chater value)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(MessageCount));
            OnPropertyChanged(nameof(UserRank)); // ДОБАВЬТЕ
            OnPropertyChanged(nameof(MessageSkin));
        }

        public void UpdateMessageCount()
        {
            OnPropertyChanged(nameof(MessageCount));
        }
    }
}