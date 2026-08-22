using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Connectors;
using SmithForge.ChatEngine.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Main.Models;
using MainChatMode = SmithForge.Main.Models.ChatMode;
using EngineChatMode = SmithForge.ChatEngine.Models.ChatMode;

namespace SmithForge.Main.Services;

/// <summary>
/// Сервис управления подключениями к чатам
/// </summary>
public class ChatConnectionService
{
    private readonly ChatManagerViewModel _chatManager;
    private readonly Dictionary<string, IChatConnector> _activeConnectors = new();
    private readonly Dictionary<string, ChatConnection> _chatConnections = new();
    private readonly object _lockObject = new();

    public event EventHandler<IncomingChatMessage>? MessageReceived;

    public ChatConnectionService(ChatManagerViewModel chatManager)
    {
        _chatManager = chatManager;
    }

    public ObservableCollection<ChatConnection> Chats => _chatManager.Chats;

    public void UpdateStats(ObservableCollection<ChatConnection> chats, Action<int, int> onStatsChanged)
    {
        var connectedCount = chats.Count(c => c.IsConnected);
        var totalMessages = chats.Sum(c => c.MessageCount);
        onStatsChanged(connectedCount, totalMessages);
    }

    public async Task ConnectChat(ChatConnection chat, Action<string, bool, int> onStatusChanged)
    {
        if (chat == null) return;

        lock (_lockObject)
        {
            if (_activeConnectors.ContainsKey(chat.ChatName))
            {
                chat.Status = $"✅ Подключен {chat.VideoIdDisplay}";
                chat.IsConnected = true;
                onStatusChanged(chat.ChatName, true, 0);
                return;
            }
        }

        chat.Status = "🔄 Подключение...";
        chat.IsConnected = false;

        try
        {
            IChatConnector? connector = null;

            switch (chat.Platform.ToLower())
            {
                case "youtube":
                    connector = await ConnectYouTubeChatInternal(chat);
                    break;
                case "twitch":
                    connector = await ConnectTwitchChatInternal(chat);
                    break;
                case "goodgame":
                    connector = await ConnectGoodGameChatInternal(chat);
                    break;
                default:
                    chat.Status = "❌ Неподдерживаемая платформа";
                    return;
            }

            if (connector != null)
            {
                lock (_lockObject)
                {
                    _activeConnectors[chat.ChatName] = connector;
                    _chatConnections[chat.ChatName] = chat;
                }

                connector.MessageReceived += (s, msg) => OnConnectorMessageReceived(s, msg, chat.ChatName);
                connector.StatusChanged += (s, status) => OnConnectorStatusChanged(s, status, chat.ChatName);

                // ✅ Получаем Video ID из коннектора
                if (connector is YouTubeConnector youtubeConnector)
                {
                    var videoId = youtubeConnector.GetVideoId();
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        chat.VideoId = videoId;
                        Debug.WriteLine($"[Chat] VideoId обновлён: {videoId}");
                    }
                }

                chat.IsConnected = true;
                chat.Status = $"✅ Подключен 📺 {chat.VideoId}";
                chat.MessageCount = 0;
                chat.LastUsedMethod = chat.PreferredMethod;

                Debug.WriteLine($"[Chat] Подключен к {chat.ChatName} (VideoId: {chat.VideoId}, ConnectorId: {connector.Id})");
                onStatusChanged(chat.ChatName, true, 0);
            }
            else
            {
                chat.Status = "❌ Ошибка подключения";
            }
        }
        catch (Exception ex)
        {
            chat.Status = $"❌ Ошибка: {ex.Message}";
            chat.LastConnectionError = ex.Message;
            Debug.WriteLine($"[Chat] Ошибка подключения {chat.ChatName}: {ex.Message}");
        }
    }
    public async Task DisconnectChat(ChatConnection chat, Action onSaved)
    {
        if (chat == null) return;
        await DisconnectChatInternal(chat);
        onSaved();
    }

    public void RemoveChat(ChatConnection? chat, ObservableCollection<ChatConnection> chats, Action onSaved, Action onStatsChanged)
    {
        if (chat == null) return;

        var result = MessageBox.Show($"Удалить чат '{chat.ChatName}'?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (chat.IsConnected)
            {
                _ = DisconnectChat(chat, () => { onSaved(); });
            }
            else
            {
                chats.Remove(chat);
                _chatManager.Chats.Remove(chat);
                _chatManager.SaveChatsToFile();
                onStatsChanged();
            }
        }
    }

    public void ChangeMethod(ChatConnection? chat)
    {
        if (chat == null) return;

        var window = new ChangeMethodWindow(chat, _chatManager);
        window.Owner = Application.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _chatManager.SaveChatsToFile();
            MessageBox.Show("Метод подключения обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task<IChatConnector> ConnectYouTubeChatInternal(ChatConnection chat)
    {
        try
        {
            // ✅ Если есть ChannelId — ВСЕГДА ищем актуальный стрим
            if (!string.IsNullOrEmpty(chat.ChannelId))
            {
                var tempConnector = new YouTubeConnector(
                    channelId: chat.ChannelId,
                    apiKey: chat.ApiKey
                );

                var streams = await tempConnector.GetLiveStreamsAsync();

                if (streams.Count > 0)
                {
                    YouTubeStreamInfo? targetStream = null;

                    if (chat.Mode == MainChatMode.Shorts)
                    {
                        targetStream = streams.FirstOrDefault(s => s.IsShorts);
                        if (targetStream == null)
                        {
                            Debug.WriteLine("⚠️ Shorts стрим не найден, используем обычный");
                            targetStream = streams[0];
                        }
                    }
                    else // Normal
                    {
                        targetStream = streams.FirstOrDefault(s => !s.IsShorts);
                        if (targetStream == null)
                        {
                            Debug.WriteLine("⚠️ Обычный стрим не найден, используем первый");
                            targetStream = streams[0];
                        }
                    }

                    chat.VideoId = targetStream.VideoId;
                    chat.RefreshDisplayName();
                    Debug.WriteLine($"✅ Найден {chat.Mode} стрим: {chat.VideoId} ({targetStream.Title})");
                }
                else
                {
                    throw new Exception("Не найдено активных стримов для указанного канала");
                }

                tempConnector.Dispose();
            }
            else if (string.IsNullOrEmpty(chat.VideoId))
            {
                // ❌ Если нет ChannelId и нет VideoId — ошибка
                throw new Exception("Не указан Channel ID и Video ID");
            }

            // Если VideoId всё ещё пустой — ошибка
            if (string.IsNullOrEmpty(chat.VideoId))
            {
                throw new Exception("Не удалось определить Video ID для подключения");
            }

            // ✅ Создаём коннектор с уникальным ID
            var connector = new YouTubeConnector(
                videoId: chat.VideoId,
                channelId: chat.ChannelId,
                apiKey: chat.ApiKey,
                connectorId: $"{chat.ChatName}_{Guid.NewGuid():N}"
            );

            connector.SetChatMode(ConvertChatMode(chat.Mode));

            await connector.ConnectAsync();

            // ✅ Устанавливаем IsConnected = true
            chat.IsConnected = true;
            chat.Status = $"✅ Подключен 📺 {chat.VideoId}";

            return connector;
        }
        catch (Exception ex)
        {
            // ✅ При ошибке устанавливаем IsConnected = false
            chat.IsConnected = false;
            chat.Status = $"❌ Ошибка: {ex.Message}";
            Debug.WriteLine($"[YouTube] Ошибка подключения {chat.ChatName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Конвертирует ChatMode из Main.Models в ChatEngine.Models
    /// </summary>
    private static EngineChatMode ConvertChatMode(MainChatMode mode)
    {
        return mode switch
        {
            MainChatMode.Normal => EngineChatMode.Normal,
            MainChatMode.Shorts => EngineChatMode.Shorts,
            _ => EngineChatMode.Normal
        };
    }

    private async Task<IChatConnector> ConnectTwitchChatInternal(ChatConnection chat)
    {
        await Task.Delay(100);
        throw new NotImplementedException("Twitch коннектор еще не реализован");
    }

    private async Task<IChatConnector> ConnectGoodGameChatInternal(ChatConnection chat)
    {
        await Task.Delay(100);
        throw new NotImplementedException("GoodGame коннектор еще не реализован");
    }

    private async Task DisconnectChatInternal(ChatConnection chat)
    {
        try
        {
            lock (_lockObject)
            {
                if (_activeConnectors.TryGetValue(chat.ChatName, out var connector))
                {
                    connector.MessageReceived -= (s, msg) => OnConnectorMessageReceived(s, msg, chat.ChatName);
                    connector.StatusChanged -= (s, status) => OnConnectorStatusChanged(s, status, chat.ChatName);
                    _activeConnectors.Remove(chat.ChatName);
                    _chatConnections.Remove(chat.ChatName);

                    // Отключаем и удаляем коннектор
                    Task.Run(async () =>
                    {
                        await connector.DisconnectAsync();
                        connector.Dispose();
                    });
                }
            }

            chat.IsConnected = false;
            chat.Status = "⏹ Отключен";

            if (chat.Platform.ToLower() == "youtube" &&
                chat.PreferredMethod == YouTubeConnectionMethod.ManualVideoId)
            {
                chat.VideoId = string.Empty;
            }

            Debug.WriteLine($"[Chat] Отключен от {chat.ChatName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Chat] Ошибка отключения {chat.ChatName}: {ex.Message}");
        }
    }

    private void OnConnectorMessageReceived(object? sender, IncomingChatMessage message, string chatName)
    {
        // ✅ Добавляем имя чата в сообщение для дедупликации
        if (message != null)
        {
            message.ConnectorId = chatName;
            Debug.WriteLine($"[Chat] {chatName} получил сообщение от {message.UserName}: {message.Text}");
            MessageReceived?.Invoke(this, message);
        }
    }

    private void OnConnectorStatusChanged(object? sender, ConnectorStatus status, string chatName)
    {
        Debug.WriteLine($"[Chat] {chatName} статус: {(status.IsConnected ? "Подключен" : "Отключен")}");

        lock (_lockObject)
        {
            if (_chatConnections.TryGetValue(chatName, out var chat))
            {
                chat.IsConnected = status.IsConnected;
                chat.Status = status.IsConnected ? "✅ Подключен" : $"❌ {status.ErrorMessage ?? "Отключен"}";
            }
        }
    }

    public ChatConnection? GetChatByPlatform(string platform)
    {
        lock (_lockObject)
        {
            return _chatConnections.Values.FirstOrDefault(c =>
                c.Platform.ToLower() == platform.ToLower());
        }
    }

    public bool IsChatConnected(string chatName)
    {
        lock (_lockObject)
        {
            return _activeConnectors.ContainsKey(chatName);
        }
    }
}