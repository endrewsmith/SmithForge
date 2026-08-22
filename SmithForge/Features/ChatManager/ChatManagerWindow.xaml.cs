using System.Windows;
using System.Windows.Controls;

namespace SmithForge.Features.ChatManager
{
    public partial class ChatManagerWindow : Window
    {
        public ChatManagerWindow()
        {
            InitializeComponent();
            DataContext = new ChatManagerViewModel();
        }

        private void YouTubeApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChatManagerViewModel vm)
            {
                vm.YouTubeApiKey = ((PasswordBox)sender).Password;
            }
        }

        private void TwitchOAuthBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChatManagerViewModel vm)
            {
                vm.TwitchOAuthToken = ((PasswordBox)sender).Password;
            }
        }
    }
}