using SmithForge.Main.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SmithForge.Features.ChatManager
{
    public partial class ChatManagerWindow : Window
    {
        public ChatManagerWindow()
        {
            InitializeComponent();
            //DataContext = new ChatManagerViewModel();
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
        private async void OnConnectButtonClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var chat = button?.Tag as ChatConnection;

            if (chat == null) return;

            var vm = DataContext as ChatManagerViewModel;
            if (vm == null) return;

            if (chat.IsConnected)
            {
                await vm.DisconnectChat(chat);
            }
            else
            {
                await vm.ConnectChat(chat);
            }
        }

    }
}