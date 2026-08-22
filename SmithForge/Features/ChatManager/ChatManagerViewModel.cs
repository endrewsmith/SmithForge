using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SmithForge.Features.ChatManager
{
    public partial class ChatManagerViewModel : ObservableObject
    {
        // ========== КОЛЛЕКЦИИ ==========

        [ObservableProperty]
        private ObservableCollection<ChatConnection> _chats = new();

        [ObservableProperty]
        private ChatConnection? _selectedChat;

        private ChatConnectionService? _chatConnectionService;

        // ========== СОСТОЯНИЕ ==========

        [ObservableProperty]
        private bool _isAddingChat;

        [ObservableProperty]
        private string _selectedPlatform = "youtube";

        [ObservableProperty]
        private ChatMode _selectedMode = ChatMode.Normal;

        // ========== НАЗВАНИЕ ЧАТА ==========

        [ObservableProperty]
        private string _chatName = string.Empty;

        // ========== НАСТРОЙКИ YOUTUBE ==========

        [ObservableProperty]
        private string _youTubeChannelId = string.Empty;

        [ObservableProperty]
        private string _youTubeApiKey = string.Empty;

        [ObservableProperty]
        private YouTubeConnectionMethod _selectedYouTubeMethod = YouTubeConnectionMethod.ApiOnly;

        [ObservableProperty]
        private YouTubeMethodItem? _selectedYouTubeMethodItem;

        partial void OnSelectedYouTubeMethodItemChanged(YouTubeMethodItem? value)
        {
            if (value != null)
            {
                SelectedYouTubeMethod = value.Method;
            }
        }

        // ========== НАСТРОЙКИ TWITCH ==========

        [ObservableProperty]
        private string _twitchChannelName = string.Empty;

        [ObservableProperty]
        private string _twitchOAuthToken = string.Empty;

        [ObservableProperty]
        private string _twitchClientId = string.Empty;

        // ========== НАСТРОЙКИ GOODGAME ==========

        [ObservableProperty]
        private string _goodGameChannelId = string.Empty;

        // ========== СПИСКИ ДЛЯ UI ==========

        public ObservableCollection<PlatformItem> AvailablePlatforms { get; } = new()
        {
            new PlatformItem { Value = "youtube", DisplayName = "🎥 YouTube", Icon = "▶️" },
            new PlatformItem { Value = "twitch", DisplayName = "🟣 Twitch", Icon = "🔴" },
            new PlatformItem { Value = "goodgame", DisplayName = "🟢 GoodGame", Icon = "🎮" }
        };

        public ObservableCollection<ChatMode> AvailableModes { get; } = new()
        {
            ChatMode.Normal,
            ChatMode.Shorts
        };

        public ObservableCollection<YouTubeMethodItem> YouTubeMethods { get; } = new()
        {
            new YouTubeMethodItem {
                Method = YouTubeConnectionMethod.ApiOnly,
                DisplayName = "🔑 Только API (тратит квоту)",
                Description = "Использует Google API. Требует API Key и Channel ID"
            },
            new YouTubeMethodItem {
                Method = YouTubeConnectionMethod.HtmlOnly,
                DisplayName = "🌐 Только парсинг HTML (бесплатно)",
                Description = "Парсит страницу канала. Требует только Channel ID"
            }
        };

        // Путь к файлу сохранения чатов
        private static readonly string ChatsConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SF_Data",
            "Config",
            "chats_config.json");

        public ChatManagerViewModel()
        {
            SelectedYouTubeMethodItem = YouTubeMethods[0];
            LoadChatsFromFilePrivate();  // ← вызываем приватный метод
        }

        public ChatManagerViewModel(ObservableCollection<ChatConnection> sharedChats, ChatConnectionService? chatConnectionService = null)
        {
            Chats = sharedChats;
            _chatConnectionService = chatConnectionService;
            SelectedYouTubeMethodItem = YouTubeMethods[0];
        }

        // ========== КОМАНДЫ ==========

        [RelayCommand]
        private void AddChat()
        {
            IsAddingChat = !IsAddingChat;
            if (!IsAddingChat)
            {
                ClearFields();
            }
            else
            {
                // При открытии формы генерируем имя по умолчанию
                GenerateDefaultChatName();
            }
        }

        [RelayCommand]
        private void CancelAddChat()
        {
            IsAddingChat = false;
            ClearFields();
        }

        [RelayCommand]
        private void SaveChat()
        {
            if (string.IsNullOrEmpty(SelectedPlatform))
            {
                MessageBox.Show("Выберите платформу!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(ChatName))
            {
                MessageBox.Show("Введите название чата!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var chat = new ChatConnection
            {
                Platform = SelectedPlatform,
                Mode = SelectedMode,
                Status = "Не подключен",
                PreferredMethod = SelectedYouTubeMethod,
                VideoId = string.Empty,
                ChatName = ChatName // DisplayName сгенерируется автоматически
            };

            switch (SelectedPlatform.ToLower())
            {
                case "youtube":
                    if (!ValidateYouTube(chat)) return;
                    break;

                case "twitch":
                    if (!ValidateTwitch(chat)) return;
                    break;

                case "goodgame":
                    if (!ValidateGoodGame(chat)) return;
                    break;

                default:
                    MessageBox.Show("Неизвестная платформа!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
            }

            Chats.Add(chat);
            SaveChatsToFile();

            ClearFields();
            IsAddingChat = false;

            MessageBox.Show($"Чат '{chat.ChatName}' добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task RemoveChat(ChatConnection? chat)
        {
            if (chat == null) return;

            var result = MessageBox.Show($"Удалить чат '{chat.ChatName}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (chat.IsConnected)
                {
                    await DisconnectChat(chat);
                }
                Chats.Remove(chat);
                SaveChatsToFile();
            }
        }

        [RelayCommand]
        private async Task ConnectChat(ChatConnection? chat)
        {
            if (chat == null) return;

            if (_chatConnectionService != null)
            {
                await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
                {
                    chat.Status = connected ? "✅ Подключен" : $"❌ Ошибка: {chat.LastConnectionError}";
                    SaveChatsToFile();
                });
            }
            else
            {
                chat.Status = "❌ Сервис подключения недоступен";
            }
        }

        [RelayCommand]
        public async Task ConnectByVideoId(ChatConnection? chat)
        {
            if (chat == null) return;

            if (string.IsNullOrEmpty(chat.VideoId) || chat.VideoId.Length != 11)
            {
                MessageBox.Show("Введите корректный Video ID (11 символов)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_chatConnectionService == null)
            {
                MessageBox.Show("Сервис подключения недоступен!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Устанавливаем метод подключения на HtmlOnly (бесплатно, без API)
            chat.PreferredMethod = YouTubeConnectionMethod.HtmlOnly;
            chat.Status = "🔄 Подключение по Video ID (HTML-парсинг)...";

            // Вызываем реальный метод подключения
            await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
            {
                if (connected)
                {
                    chat.Status = $"✅ Подключен (Video ID: {chat.VideoId})";
                }
                else
                {
                    chat.Status = $"❌ Ошибка: {chat.LastConnectionError}";
                }
            });

            SaveChatsToFile();
        }

        [RelayCommand]
        private async Task DisconnectChat(ChatConnection? chat)
        {
            if (chat == null) return;

            if (_chatConnectionService != null)
            {
                await _chatConnectionService.DisconnectChat(chat, () =>
                {
                    SaveChatsToFile();
                });
            }
            else
            {
                // Fallback если сервис недоступен
                chat.IsConnected = false;
                chat.Status = "⏹ Отключен";
                SaveChatsToFile();
            }
        }

        [RelayCommand]
        private void SaveAll()
        {
            SaveChatsToFile();
            MessageBox.Show("Все чаты сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ChangeMethod(ChatConnection? chat)
        {
            if (chat == null) return;

            var window = new ChangeMethodWindow(chat, this);
            window.Owner = Application.Current.MainWindow;
            if (window.ShowDialog() == true)
            {
                SaveChatsToFile();
                MessageBox.Show("Изменения сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

        private void GenerateDefaultChatName()
        {
            // Генерируем имя по умолчанию на основе платформы
            string platformName = SelectedPlatform.ToLower() switch
            {
                "youtube" => "YouTube",
                "twitch" => "Twitch",
                "goodgame" => "GoodGame",
                _ => "Чат"
            };

            // Считаем сколько уже есть чатов на этой платформе
            int count = 0;
            foreach (var chat in Chats)
            {
                if (chat.Platform.ToLower() == SelectedPlatform.ToLower())
                {
                    count++;
                }
            }

            ChatName = $"{platformName} #{count + 1}";
        }

        partial void OnSelectedPlatformChanged(string value)
        {
            ClearFields();
            if (IsAddingChat)
            {
                GenerateDefaultChatName();
            }
        }

        // ========== СОХРАНЕНИЕ И ЗАГРУЗКА В ФАЙЛ ==========

        public void SaveChatsToFile()
        {
            try
            {
                string? directory = Path.GetDirectoryName(ChatsConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var chatDataList = new List<ChatSaveData>();
                foreach (var chat in Chats)
                {
                    chatDataList.Add(new ChatSaveData
                    {
                        ChatName = chat.ChatName,
                        Platform = chat.Platform,
                        ChannelId = chat.ChannelId,
                        VideoId = chat.VideoId,
                        ApiKey = chat.ApiKey,
                        DisplayName = chat.DisplayName,
                        Mode = chat.Mode,
                        PreferredMethod = chat.PreferredMethod,
                        LastUsedMethod = chat.LastUsedMethod,
                        MessageCount = chat.MessageCount
                    });
                }

                var saveData = new ChatConfigFile
                {
                    Version = 1,
                    LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Chats = chatDataList
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(saveData, options);
                File.WriteAllText(ChatsConfigPath, json);

                System.Diagnostics.Debug.WriteLine($"[ChatManager] Сохранено {chatDataList.Count} чатов");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatManager] Ошибка сохранения: {ex.Message}");
            }
        }

        private void LoadChatsFromFilePrivate()
        {
            try
            {
                if (!File.Exists(ChatsConfigPath))
                {
                    System.Diagnostics.Debug.WriteLine("[ChatManager] Файл с чатами не найден");
                    return;
                }

                string json = File.ReadAllText(ChatsConfigPath);
                var saveData = JsonSerializer.Deserialize<ChatConfigFile>(json);

                if (saveData == null || saveData.Chats == null || saveData.Chats.Count == 0)
                {
                    return;
                }

                Chats.Clear();

                foreach (var data in saveData.Chats)
                {
                    var chat = new ChatConnection
                    {
                        ChatName = data.ChatName ?? "Без названия",
                        Platform = data.Platform,
                        ChannelId = data.ChannelId,
                        VideoId = data.VideoId,
                        ApiKey = data.ApiKey,
                        Mode = data.Mode,
                        PreferredMethod = data.PreferredMethod,
                        LastUsedMethod = data.LastUsedMethod,
                        MessageCount = data.MessageCount,
                        IsConnected = false,
                        Status = "Не подключен"
                    };

                    // Обновляем DisplayName после загрузки
                    chat.RefreshDisplayName();
                    Chats.Add(chat);
                }

                System.Diagnostics.Debug.WriteLine($"[ChatManager] Загружено {Chats.Count} чатов");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatManager] Ошибка загрузки: {ex.Message}");
            }
        }

        // ========== ПРИВАТНЫЕ МЕТОДЫ ==========

        private bool ValidateYouTube(ChatConnection chat)
        {
            switch (SelectedYouTubeMethod)
            {
                case YouTubeConnectionMethod.ApiOnly:
                    if (string.IsNullOrEmpty(YouTubeApiKey))
                    {
                        MessageBox.Show("Введите API Key!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    if (string.IsNullOrEmpty(YouTubeChannelId))
                    {
                        MessageBox.Show("Введите Channel ID!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    chat.ApiKey = YouTubeApiKey;
                    chat.ChannelId = YouTubeChannelId;
                    break;

                case YouTubeConnectionMethod.HtmlOnly:
                    if (string.IsNullOrEmpty(YouTubeChannelId))
                    {
                        MessageBox.Show("Введите Channel ID!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    chat.ChannelId = YouTubeChannelId;
                    chat.ApiKey = string.Empty;
                    break;

                default:
                    MessageBox.Show("Неизвестный метод подключения!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
            }

            chat.DisplayName = $"YouTube: {chat.ChatName}";
            chat.RefreshDisplayName();
            return true;
        }

        private bool ValidateTwitch(ChatConnection chat)
        {
            if (string.IsNullOrEmpty(TwitchChannelName))
            {
                MessageBox.Show("Введите имя канала!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            chat.ChannelId = TwitchChannelName;
            chat.DisplayName = $"Twitch: {TwitchChannelName}";
            if (SelectedMode == ChatMode.Shorts)
            {
                chat.DisplayName += " 📱";
            }
            chat.RefreshDisplayName();
            return true;
        }

        private bool ValidateGoodGame(ChatConnection chat)
        {
            if (string.IsNullOrEmpty(GoodGameChannelId))
            {
                MessageBox.Show("Введите ID канала!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            chat.ChannelId = GoodGameChannelId;
            chat.DisplayName = $"GoodGame: {GoodGameChannelId}";
            if (SelectedMode == ChatMode.Shorts)
            {
                chat.DisplayName += " 📱";
            }
            chat.RefreshDisplayName();
            return true;
        }

        private void ClearFields()
        {
            ChatName = string.Empty;
            YouTubeChannelId = string.Empty;
            YouTubeApiKey = string.Empty;
            TwitchChannelName = string.Empty;
            TwitchOAuthToken = string.Empty;
            TwitchClientId = string.Empty;
            GoodGameChannelId = string.Empty;
            SelectedMode = ChatMode.Normal;
            SelectedYouTubeMethod = YouTubeConnectionMethod.ApiOnly;
            SelectedYouTubeMethodItem = YouTubeMethods.Count > 0 ? YouTubeMethods[0] : null;
        }

        //private void LoadChats()
        //{
        //    LoadChatsFromFile();
        //}

        public void LoadChatsFromFile()
        {
            LoadChatsFromFilePrivate();
        }
    }

    // ========== КЛАССЫ ДЛЯ СОХРАНЕНИЯ ==========

    public class ChatConfigFile
    {
        public int Version { get; set; } = 1;
        public string LastModified { get; set; } = string.Empty;
        public List<ChatSaveData> Chats { get; set; } = new();
    }

    public class ChatSaveData
    {
        public string ChatName { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public ChatMode Mode { get; set; } = ChatMode.Normal;
        public YouTubeConnectionMethod PreferredMethod { get; set; } = YouTubeConnectionMethod.ApiOnly;
        public YouTubeConnectionMethod LastUsedMethod { get; set; } = YouTubeConnectionMethod.ApiOnly;
        public int MessageCount { get; set; }
    }

    public class PlatformItem
    {
        public string Value { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
}