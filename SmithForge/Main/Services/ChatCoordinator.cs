using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Main.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор управления чатами: подключение, отключение, обновление статистики.
    /// </summary>
    public partial class ChatCoordinator : ObservableObject
    {
        private readonly DialogService _dialogService;
        private readonly MessageHandlerService _messageHandler;

        // ============================================================
        // КОЛЛЕКЦИИ И СТАТИСТИКА
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
        // КОНСТРУКТОР
        // ============================================================
        public ChatCoordinator(DialogService dialogService, MessageHandlerService messageHandler)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        }

        // ============================================================
        // ИНИЦИАЛИЗАЦИЯ (вызывается из MainViewModel после создания MessageHandler)
        // ============================================================
        public void Initialize()
        {
            _chatManager = new ChatManagerViewModel(Chats, null);
            _chatManager.LoadChatsFromFile();

            _chatConnectionService = new ChatConnectionService(_chatManager);
            _chatConnectionService.MessageReceived += OnConnectorMessageReceived;

            _chatManager = new ChatManagerViewModel(Chats, _chatConnectionService);

            LoadChats();
        }

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
            Debug.WriteLine("[ChatCoordinator] StartAllChats() вызван");

            var chatsToConnect = Chats.Where(c => !c.IsConnected).ToList();

            if (chatsToConnect.Count == 0)
            {
                Debug.WriteLine("[ChatCoordinator] Все чаты уже подключены");
                return;
            }

            Debug.WriteLine($"[ChatCoordinator] Подключаем {chatsToConnect.Count} чатов параллельно...");

            // ✅ ПАРАЛЛЕЛЬНО — запускаем все задачи одновременно
            var connectTasks = chatsToConnect.Select(chat => ConnectChat(chat));
            await Task.WhenAll(connectTasks);

            Debug.WriteLine("[ChatCoordinator] Все чаты подключены (или попытки завершены)");
        }

        [RelayCommand]
        private async Task StopAllChats()
        {
            Debug.WriteLine("[ChatCoordinator] StopAllChats() вызван");

            var chatsToDisconnect = Chats.Where(c => c.IsConnected).ToList();

            if (chatsToDisconnect.Count == 0)
            {
                Debug.WriteLine("[ChatCoordinator] Все чаты уже отключены");
                return;
            }

            Debug.WriteLine($"[ChatCoordinator] Отключаем {chatsToDisconnect.Count} чатов параллельно...");

            // ✅ ПАРАЛЛЕЛЬНО
            var disconnectTasks = chatsToDisconnect.Select(chat => DisconnectChat(chat));
            await Task.WhenAll(disconnectTasks);

            Debug.WriteLine("[ChatCoordinator] Все чаты отключены");
        }

        // ============================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ MainViewModel
        // ============================================================
        public async Task StopAllAsync()
        {
            await StopAllChats();
        }

        public async Task ConnectAllAsync()
        {
            await StartAllChats();
        }

        public ChatManagerViewModel GetChatManagerViewModel() => _chatManager;

        public ChatConnectionService GetChatConnectionService() => _chatConnectionService;

        // ============================================================
        // ОБРАБОТКА СООБЩЕНИЙ
        // ============================================================
        private void OnConnectorMessageReceived(object? sender, IncomingChatMessage message)
        {
            _messageHandler.ProcessConnectorMessage(sender, message);
        }

        // ============================================================
        // СТАТИСТИКА
        // ============================================================
        private void UpdateStats()
        {
            ConnectedChatsCount = Chats.Count(c => c.IsConnected);
            TotalMessagesCount = Chats.Sum(c => c.MessageCount);
        }

        // ============================================================
        // ЗАГРУЗКА И ОБНОВЛЕНИЕ
        // ============================================================
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
    }
}