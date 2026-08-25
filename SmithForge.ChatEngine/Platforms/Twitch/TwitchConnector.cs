using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.Twitch.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace SmithForge.ChatEngine.Platforms.Twitch
{
    public class TwitchConnector : IChatConnector
    {
        private readonly ILogger<TwitchConnector>? _logger;
        private TwitchChatClient? _chatClient;
        private CancellationTokenSource? _cts;
        private bool _disposed;
        private string _channelName = string.Empty;
        private string _botName = "justinfan12345";
        private string _botPassword = string.Empty;
        private readonly string _connectorId;
        private bool _isConnected = false;

        // ⭐ ДЕЛЕГАТЫ ДЛЯ СВЯЗИ С UI (Строго по 2 аргумента как в YouTube)
        public static Func<string, bool>? CheckEmojiExists { get; set; }
        public static Action<string, string>? RegisterEmojiInCache { get; set; }

        // ⭐ ЕДИНСТВЕННЫЙ И ПРАВИЛЬНЫЙ МЕТОД РЕГИСТРАЦИИ (Старый дубликат удален)
        public static void RegisterDelegates(Func<string, bool> checkExists, Action<string, string> register)
        {
            CheckEmojiExists = checkExists;
            RegisterEmojiInCache = register;
        }

        public string Id => _connectorId;
        public ChannelType Platform => ChannelType.Twitch;
        public ConnectorStatus Status { get; private set; } = new();

        public event EventHandler<IncomingChatMessage>? MessageReceived;
        public event EventHandler<ConnectorStatus>? StatusChanged;

        public TwitchConnector(
            ILogger<TwitchConnector>? logger = null,
            string channelName = "",
            string botName = "justinfan12345",
            string botPassword = "",
            string connectorId = "")
        {
            _logger = logger;
            _channelName = channelName;
            _botName = string.IsNullOrEmpty(botName) ? "justinfan12345" : botName;
            _botPassword = botPassword;
            _connectorId = string.IsNullOrEmpty(connectorId) ? Guid.NewGuid().ToString() : connectorId;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_channelName))
                {
                    throw new Exception("Не указан канал Twitch");
                }

                _logger?.LogInformation($"▶ Подключение к Twitch чату: {_channelName}");

                _chatClient = new TwitchChatClient();
                _chatClient.OnMessageReceived += OnMessageReceived;
                _chatClient.OnLog += (s, msg) => _logger?.LogInformation(msg);
                _chatClient.OnStatusChanged += OnStatusChanged;

                var success = await Task.Run(() => _chatClient.Connect(_channelName, _botName, _botPassword));

                if (success)
                {
                    _isConnected = true;
                    Status.IsConnected = true;
                    Status.ErrorMessage = null;
                    Status.MarkConnected();
                    StatusChanged?.Invoke(this, Status);

                    _logger?.LogInformation($"✅ Подключен к Twitch чату: {_channelName}");
                }
                else
                {
                    throw new Exception("Не удалось подключиться к Twitch чату");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Status.IsConnected = false;
                Status.ErrorMessage = ex.Message;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogError(ex, $"❌ Ошибка подключения к Twitch: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _cts?.Cancel();
                _chatClient?.Disconnect();

                _isConnected = false;
                Status.IsConnected = false;
                StatusChanged?.Invoke(this, Status);

                _logger?.LogInformation($"⏹ Отключен от Twitch чата: {_channelName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"❌ Ошибка отключения от Twitch: {ex.Message}");
            }
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (_chatClient != null && _isConnected)
            {
                _chatClient.SendMessage(message);
            }
            else
            {
                throw new InvalidOperationException("Не подключен к Twitch чату");
            }
            return Task.CompletedTask;
        }

        public Task<bool> ValidateSettingsAsync()
        {
            return Task.FromResult(!string.IsNullOrEmpty(_channelName));
        }

        public string? GetVideoId()
        {
            return null;
        }

        public void SetChannelName(string channelName)
        {
            _channelName = channelName.ToLower();
            _logger?.LogInformation($"📺 Установлен канал: {_channelName}");
        }

        public void SetCredentials(string botName, string botPassword)
        {
            _botName = botName;
            _botPassword = botPassword;
            _logger?.LogInformation($"🔑 Установлены учетные данные бота");
        }

        private void OnMessageReceived(object? sender, TwitchMessage message)
        {
            if (message == null) return;

            Debug.WriteLine("═══════════════════════════════════════════════");
            Debug.WriteLine($"[TwitchConnector] Сообщение: {message.Text}");
            Debug.WriteLine($"[TwitchConnector] HasEmotes: {message.HasEmotes}");
            Debug.WriteLine($"[TwitchConnector] Emotes count: {message.Emotes?.Count ?? 0}");

            var processedText = message.Text;

            if (message.HasEmotes && message.Emotes != null)
            {
                foreach (var emote in message.Emotes)
                {
                    Debug.WriteLine($"[TwitchConnector] Обработка эмодзи: {emote.Code} (ID: {emote.Id})");

                    // 1. Заменяем на [code]
                    processedText = processedText.Replace(emote.Code, $"[{emote.Code}]");
                    Debug.WriteLine($"[TwitchConnector] Заменено: {emote.Code} -> [{emote.Code}]");

                    // 2. Проверяем кэш
                    bool exists = CheckEmojiExists?.Invoke($"[{emote.Code}]") == true;
                    Debug.WriteLine($"[TwitchConnector] В глобальном кэше: {exists}");

                    // 3. Проверяем файл на диске с очищенным именем
                    var safeFileName = SanitizeFileName(emote.Code);
                    var path = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "SF_Data", "Assets", "Emojis", "Twitch", "Images",
                        $"{safeFileName}.png");
                    bool fileExists = File.Exists(path);
                    Debug.WriteLine($"[TwitchConnector] Файл на диске: {fileExists} -> {safeFileName}.png");

                    // ✅ 4. СИНХРОННО: если нет в кэше — скачиваем и регистрируем
                    if (!exists && !fileExists)
                    {
                        Debug.WriteLine($"[TwitchConnector] ⬇️ Скачиваем синхронно: {emote.Code} -> {safeFileName}.png");

                        try
                        {
                            var url = $"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/light/1.0";

                            var directory = Path.GetDirectoryName(path);
                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                Directory.CreateDirectory(directory);

                            Debug.WriteLine($"[TwitchConnector] URL: {url}");

                            using var client = new HttpClient();
                            client.Timeout = TimeSpan.FromSeconds(10);
                            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                            var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult(); // ← СИНХРОННО!
                            File.WriteAllBytes(path, bytes); // ← СИНХРОННО!

                            // ✅ РЕГИСТРИРУЕМ СИНХРОННО
                            RegisterEmojiInCache?.Invoke($"[{emote.Code}]", path);

                            Debug.WriteLine($"[TwitchConnector] ✅ Скачан и зарегистрирован: {safeFileName}.png ({bytes.Length} байт)");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[TwitchConnector] ❌ Ошибка скачивания {emote.Code}: {ex.Message}");
                        }
                    }
                    else if (fileExists && !exists)
                    {
                        // ✅ Файл есть, но нет в кэше — регистрируем синхронно
                        Debug.WriteLine($"[TwitchConnector] 🔄 Восстанавливаем в кэш: {emote.Code}");
                        RegisterEmojiInCache?.Invoke($"[{emote.Code}]", path);
                    }
                    else
                    {
                        Debug.WriteLine($"[TwitchConnector] ⏭ Уже есть в кэше и на диске");
                    }
                }
            }

            Debug.WriteLine($"[TwitchConnector] Итоговый текст: {processedText}");
            Debug.WriteLine("═══════════════════════════════════════════════");

            var incomingMessage = new IncomingChatMessage
            {
                Platform = ChannelType.Twitch,
                ChannelId = message.Channel,
                UserId = message.UserId ?? message.Login,
                UserName = message.Author,
                Text = processedText,
                Timestamp = message.Timestamp.ToUniversalTime(),
                ConnectorId = Id
            };

            Status.MarkMessageReceived();
            MessageReceived?.Invoke(this, incomingMessage);
        }

        /// <summary>
        /// Очищает имя файла от недопустимых символов Windows
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;

            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c.ToString(), "");
            }

            // Заменяем пробелы на подчеркивания
            fileName = fileName.Replace(" ", "_");

            // Обрезаем длинные имена (если нужно)
            if (fileName.Length > 50)
                fileName = fileName.Substring(0, 50);

            return fileName;
        }

        private void OnStatusChanged(object? sender, string status)
        {
            if (status == "error")
            {
                _isConnected = false;
                Status.IsConnected = false;
                Status.ErrorMessage = "Ошибка чата";
                StatusChanged?.Invoke(this, Status);
            }
            else if (status == "connected" || status == "joined")
            {
                _isConnected = true;
                Status.IsConnected = true;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _chatClient?.Disconnect();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
