using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel
    {
        // ============================================================
        // ПОЛЯ ЧАТОВ
        // ============================================================
        [ObservableProperty]
        private ObservableCollection<ChatConnection> _chats = new();

        [ObservableProperty]
        private int _connectedChatsCount;

        [ObservableProperty]
        private int _totalMessagesCount;

        private ChatManagerViewModel _chatManager = new();
        private ChatConnectionService _chatConnectionService = null!;

        // ============================================================
        // КОМАНДЫ ПОДКЛЮЧЕНИЯ
        // ============================================================
        [RelayCommand]
        private async Task ConnectChat(ChatConnection? chat)
        {
            if (chat == null) return;

            if (chat.Platform.ToLower() == "youtube" &&
                chat.PreferredMethod == YouTubeConnectionMethod.ManualVideoId)
            {
                if (!string.IsNullOrEmpty(chat.VideoId))
                {
                    if (chat.VideoId.Length != 11)
                    {
                        MessageBox.Show("Video ID должен содержать 11 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    var videoId = await _dialogService.ShowVideoIdDialogAsync();
                    if (string.IsNullOrEmpty(videoId))
                    {
                        chat.Status = "❌ Отменено";
                        return;
                    }
                    chat.VideoId = videoId;
                }
            }

            await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
            {
                chat.Status = name == chat.ChatName ? (connected ? "✅ Подключен" : "❌ Ошибка") : chat.Status;
            }).ConfigureAwait(false);

            UpdateStats();
            _chatManager.SaveChatsToFile();
        }

        [RelayCommand]
        private async Task DisconnectChat(ChatConnection? chat)
        {
            if (chat == null) return;

            await _chatConnectionService.DisconnectChat(chat, () =>
            {
                UpdateStats();
                _chatManager.SaveChatsToFile();
            });
        }

        [RelayCommand]
        private void RemoveChat(ChatConnection? chat)
        {
            if (chat == null) return;

            _chatConnectionService.RemoveChat(chat, Chats,
                () => UpdateStats(),
                () => UpdateStats());
        }

        [RelayCommand]
        private void ChangeMethod(ChatConnection? chat)
        {
            _chatConnectionService.ChangeMethod(chat);
        }

        [RelayCommand]
        private async Task ConnectByVideoId(ChatConnection? chat)
        {
            if (chat == null) return;

            if (string.IsNullOrEmpty(chat.VideoId) || chat.VideoId.Length != 11)
            {
                MessageBox.Show("Введите корректный Video ID (11 символов)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            chat.PreferredMethod = YouTubeConnectionMethod.ManualVideoId;

            await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
            {
                chat.Status = connected ? "✅ Подключен (Video ID)" : $"❌ Ошибка: {chat.LastConnectionError}";
                UpdateStats();
            });
        }

        [RelayCommand]
        private async Task StartAllChats()
        {
            Debug.WriteLine("[MainViewModel] StartAllChats() вызван");

            var chatsToConnect = Chats.Where(c => !c.IsConnected).ToList();

            if (chatsToConnect.Count == 0)
            {
                Debug.WriteLine("[MainViewModel] Все чаты уже подключены");
                return;
            }

            Debug.WriteLine($"[MainViewModel] Подключаем {chatsToConnect.Count} чатов...");

            foreach (var chat in chatsToConnect)
            {
                try
                {
                    Debug.WriteLine($"[MainViewModel] Подключаем: {chat.ChatName}");
                    await ConnectChat(chat);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainViewModel] Ошибка подключения {chat.ChatName}: {ex.Message}");
                }
            }

            Debug.WriteLine("[MainViewModel] Все чаты обработаны");
        }

        [RelayCommand]
        private async Task StopAllChats()
        {
            Debug.WriteLine("[MainViewModel] StopAllChats() вызван");

            var chatsToDisconnect = Chats.Where(c => c.IsConnected).ToList();

            if (chatsToDisconnect.Count == 0)
            {
                Debug.WriteLine("[MainViewModel] Все чаты уже отключены");
                return;
            }

            Debug.WriteLine($"[MainViewModel] Отключаем {chatsToDisconnect.Count} чатов...");

            foreach (var chat in chatsToDisconnect)
            {
                try
                {
                    Debug.WriteLine($"[MainViewModel] Отключаем: {chat.ChatName}");
                    await DisconnectChat(chat);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainViewModel] Ошибка отключения {chat.ChatName}: {ex.Message}");
                }
            }

            Debug.WriteLine("[MainViewModel] Все чаты отключены");
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ============================================================
        public ChatConnectionService GetChatConnectionService() => _chatConnectionService;

        private void OnConnectorMessageReceived(object? sender, IncomingChatMessage message)
        {
            _messageHandler.ProcessConnectorMessage(sender, message);
        }

        private void UpdateStats()
        {
            ConnectedChatsCount = Chats.Count(c => c.IsConnected);
            TotalMessagesCount = Chats.Sum(c => c.MessageCount);
        }

        private void LoadChats()
        {
            foreach (var chat in Chats)
            {
                chat.ConnectRequested += OnChatConnectRequested;
                chat.DisconnectRequested += OnChatDisconnectRequested;
            }

            Chats.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (ChatConnection chat in e.NewItems)
                    {
                        chat.ConnectRequested += OnChatConnectRequested;
                        chat.DisconnectRequested += OnChatDisconnectRequested;
                    }
                }
                UpdateStats();
            };

            UpdateStats();
        }

        private async void OnChatConnectRequested(object? sender, EventArgs e)
        {
            if (sender is ChatConnection chat)
            {
                await ConnectChat(chat);
            }
        }

        private async void OnChatDisconnectRequested(object? sender, EventArgs e)
        {
            if (sender is ChatConnection chat)
            {
                await DisconnectChat(chat);
            }
        }

        public async Task RefreshChats()
        {
            foreach (var chat in Chats.Where(c => c.IsConnected).ToList())
            {
                await DisconnectChat(chat);
            }

            Chats.CollectionChanged += (s, e) => UpdateStats();
            UpdateStats();
        }

        public ChatManagerViewModel GetChatManagerViewModel() => _chatManager;
    }
}