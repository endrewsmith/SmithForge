using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.YouTube;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Features.ChatOverlay;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Features.ImportantOverlay;
using SmithForge.Features.StickersOverlay;
using SmithForge.Features.YouTubeManager.ViewModels;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {

        // ✅ Интегрированный YouTube-менеджер
        public YouTubeManagerViewModel YouTubeManager { get; } = new();
        
        [ObservableProperty]
        private string _youTubeApiKey = string.Empty;

        [ObservableProperty]
        private string _youTubeChannelId = string.Empty;

        [ObservableProperty]
        private string _youTubeVideoId = string.Empty;

        [ObservableProperty]
        private string _youTubeChannelName = string.Empty;

        [ObservableProperty]
        private bool _isYouTubeConnected = false;

        [ObservableProperty]
        private string _youTubeStatus = "Не подключен";

        [ObservableProperty]
        private int _youTubeViewersCount = 0;

        [ObservableProperty]
        private ObservableCollection<YouTubeStreamInfo> _youTubeStreams = new();

        [ObservableProperty]
        private YouTubeStreamInfo? _selectedYouTubeStream;

        [ObservableProperty]
        private ImportantPlaybackMode _importantPlaybackMode = ImportantPlaybackMode.Auto;

        [ObservableProperty]
        private string _importantPlaybackHotkey = "F8";

        [ObservableProperty]
        private int _importantQueueCount = 0;

        [ObservableProperty]
        private int _importantSoundVolume = 100;

        [ObservableProperty]
        private int _voiceVolume = 100;

        [ObservableProperty]
        private int _stickerDisplayTime = 5000;

        [ObservableProperty]
        private ChatDisplayMode _mainChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _shortsChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _importantChatMode = ChatDisplayMode.AppearAndFade;

        [ObservableProperty]
        private ChatDisplayMode _stickersChatMode = ChatDisplayMode.AppearAndFade;

        public List<SmithForge.Main.Models.ChatDisplayModeInfo> AvailableModes { get; } = ChatDisplayModeFactory.GetAvailableModes();

        private readonly DashboardService _dashboardService = new();
        private readonly MessageHandlerService _messageHandler;
        private readonly OverlayManagerService _overlayManager;
        private readonly SettingsService _settingsService;
        private readonly DialogService _dialogService;
        private readonly ExternalChatService _chatService = new();
        private CancellationTokenSource? _pollingcts;

        [ObservableProperty]
        private bool _isOverlaySetupMode = true;

        [ObservableProperty]
        private bool _isOverlayHidden = false;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isProcessRunning;

        [ObservableProperty]
        private string _lastMessageText = "Ожидание сообщений...";

        [ObservableProperty]
        private AppSettings _settings;

        [ObservableProperty]
        private StreamSession? _currentSession;

        [ObservableProperty]
        private string _programPath;

        [ObservableProperty]
        private int _lastStreamNumber;

        [ObservableProperty]
        private bool _isStickersVisible = true;

        [ObservableProperty]
        private bool _isAutoSwitchingEnabled = true;

        public ObservableCollection<Chater> Users { get; } = new();


        // ✅ ДОБАВИТЬ:
        private WebServerService? _webServer;
        private bool _isWebServerRunning = false;

        // ============================================================
        // УПРАВЛЕНИЕ ЧАТАМИ
        // ============================================================

        [ObservableProperty]
        private ObservableCollection<ChatConnection> _chats = new();

        [ObservableProperty]
        private int _connectedChatsCount;

        [ObservableProperty]
        private int _totalMessagesCount;

        private ChatManagerViewModel _chatManager = new();
        private ChatConnectionService _chatConnectionService = null!;
        private StreamSessionManager _streamSessionManager = null!;

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();


            // ✅ ПРИНУДИТЕЛЬНО УСТАНАВЛИВАЕМ ПОРТ 10881 И СОХРАНЯЕМ
            Settings.NetworkPort = 10881;
            ConfigService.Save(Settings);


            // ✅ Инициализация оверлеев через сервис
            _overlayManager = new OverlayManagerService(Settings);

            // ✅ Инициализация сервиса настроек (ДО установки свойств!)
            _settingsService = new SettingsService(Settings, _overlayManager);
            
            // ✅ Инициализация сервиса диалогов
            _dialogService = new DialogService();

            _webServer = new WebServerService((int)Settings.NetworkPort);
            _webServer.MessageAdded += OnWebMessageAdded;
            Task.Run(async () => await StartWebServerAsync());

            // ✅ Инициализация сервиса обработки сообщений
            var processor = new MessageProcessor(Settings);
            _messageHandler = new MessageHandlerService(processor, _overlayManager, _dashboardService, _webServer);
            _messageHandler.OnProcessed += OnMessageProcessed;

            // ============================================================
            // СИНХРОНИЗАЦИЯ НАСТРОЕК YOUTUBE ИЗ APP SETTINGS
            // ============================================================
            YouTubeApiKey = Settings.YouTube?.ApiKey ?? string.Empty;
            YouTubeChannelId = Settings.YouTube?.ChannelId ?? string.Empty;
            YouTubeVideoId = Settings.YouTube?.LastVideoId ?? string.Empty;

            _isOverlaySetupMode = Settings.IsOverlaySetupMode;
            _isOverlayHidden = Settings.IsOverlayHidden;
            _isStickersVisible = Settings.IsStickersVisible;
            DatabaseService.Initialize();

            _mainChatMode = Settings.MainChatMode;
            _shortsChatMode = Settings.ShortsChatMode;
            _importantChatMode = Settings.ImportantChatMode;
            _stickersChatMode = Settings.StickersChatMode;

            StickerManager.LoadPacks();

            _overlayManager.Initialize(
                IsOverlaySetupMode,
                IsOverlayHidden,
                IsStickersVisible,
                MainChatMode,
                ShortsChatMode,
                ImportantChatMode,
                StickersChatMode,
                ImportantPlaybackMode,
                ImportantSoundVolume,
                VoiceVolume,
                StickerDisplayTime);
            _overlayManager.ImportantQueueChanged += (s, count) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ImportantQueueCount = count;
                    Debug.WriteLine($"[MainViewModel] Получено событие QueueCountChanged: count={count}");
                });
            };

            ProgramPath = Settings.ProgramPath;

            // ✅ Инициализация менеджера сессий
            _streamSessionManager = new StreamSessionManager();
            
            // Инициализируем CurrentSession из менеджера
            CurrentSession = _streamSessionManager.CurrentSession;
            LastStreamNumber = _streamSessionManager.LastStreamNumber;
            
            _streamSessionManager.SessionChanged += (s, session) =>
            {
                CurrentSession = session;
                LastStreamNumber = _streamSessionManager.LastStreamNumber;
            };

            LoadInitialData();
            _chatService.ProcessExited += (s, e) => OnProcessExited();

            _dashboardService.Initialize();
            _stickerDisplayTime = Settings.StickerDisplayTimeMs;

            _importantSoundVolume = Settings.ImportantSoundVolume;
            _voiceVolume = Settings.VoiceVolume;
            VoiceService.SetImportantSoundVolume(_importantSoundVolume);
            VoiceService.SetVoiceVolume(_voiceVolume);

            _importantPlaybackMode = Settings.ImportantPlaybackMode;
            _importantPlaybackHotkey = Settings.ImportantPlaybackHotkey;

            VoiceService.Initialize(Dispatcher.CurrentDispatcher);

            // Если режим чтения включен и очередь пуста, режим должен быть Auto
            if (IsAutoSwitchingEnabled && ImportantQueueCount == 0 && _importantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                Debug.WriteLine("[MainViewModel] Стартовая синхронизация: очередь пуста, переключаем режим на Auto");
                ImportantPlaybackMode = ImportantPlaybackMode.Auto;
            }
            else if (IsAutoSwitchingEnabled && ImportantQueueCount > 0 && _importantPlaybackMode == ImportantPlaybackMode.Auto)
            {
                Debug.WriteLine($"[MainViewModel] Стартовая синхронизация: в очереди {ImportantQueueCount} сообщений, переключаем режим на Manual");
                ImportantPlaybackMode = ImportantPlaybackMode.Manual;
            }

            // ============================================================
            // ЗАГРУЗКА ЧАТОВ
            // ============================================================

            // ✅ Подписываемся на события YouTubeManager
            YouTubeManager.MessageReceived += OnYouTubeManagerMessageReceived;

            // ✅ Создаём ChatManagerViewModel с общей коллекцией
            _chatManager = new ChatManagerViewModel(Chats, null);

            // ✅ ЗАГРУЖАЕМ ЧАТЫ ИЗ ФАЙЛА (ЭТОГО НЕ ХВАТАЕТ!)
            _chatManager.LoadChatsFromFile();

            // ✅ Инициализация сервиса управления чатами
            _chatConnectionService = new ChatConnectionService(_chatManager);
            _chatConnectionService.MessageReceived += OnConnectorMessageReceived;

            // ✅ Обновляем _chatManager с сервисом
            _chatManager = new ChatManagerViewModel(Chats, _chatConnectionService);

            LoadChats();
        }

        // ============================================================
        // СИНХРОНИЗАЦИЯ НАСТРОЕК YOUTUBE - СОХРАНЕНИЕ ПРИ ИЗМЕНЕНИИ
        // ============================================================

        partial void OnYouTubeApiKeyChanged(string value) => _settingsService.SetYouTubeApiKey(value);
        partial void OnYouTubeChannelIdChanged(string value) => _settingsService.SetYouTubeChannelId(value);
        partial void OnYouTubeVideoIdChanged(string value) => _settingsService.SetYouTubeVideoId(value);

        // ============================================================
        // ОСТАЛЬНЫЕ МЕТОДЫ
        // ============================================================

        partial void OnImportantPlaybackModeChanged(ImportantPlaybackMode value) => _settingsService.SetImportantPlaybackMode(value);
        partial void OnImportantPlaybackHotkeyChanged(string value) => _settingsService.SetImportantPlaybackHotkey(value);
        
        partial void OnIsAutoSwitchingEnabledChanged(bool value) => _settingsService.SetIsAutoSwitchingEnabled(value, ImportantQueueCount, ImportantPlaybackMode);

        partial void OnStickerDisplayTimeChanged(int value) => _settingsService.SetStickerDisplayTime(value);
        partial void OnImportantSoundVolumeChanged(int value) => _settingsService.SetImportantSoundVolume(value);
        partial void OnVoiceVolumeChanged(int value) => _settingsService.SetVoiceVolume(value);

        partial void OnMainChatModeChanged(ChatDisplayMode value) => _settingsService.SetMainChatMode(value);
        partial void OnShortsChatModeChanged(ChatDisplayMode value) => _settingsService.SetShortsChatMode(value);
        partial void OnImportantChatModeChanged(ChatDisplayMode value) => _settingsService.SetImportantChatMode(value);
        partial void OnStickersChatModeChanged(ChatDisplayMode value) => _settingsService.SetStickersChatMode(value);

        partial void OnIsOverlaySetupModeChanged(bool oldValue, bool newValue) => _settingsService.SetOverlaySetupMode(newValue, () => LastMessageText = "✅ Позиции окон сохранены");
        partial void OnIsOverlayHiddenChanged(bool oldValue, bool newValue) => _settingsService.SetOverlayHidden(newValue);
        partial void OnIsStickersVisibleChanged(bool oldValue, bool newValue) => _settingsService.SetStickersVisible(newValue);

        public void SetImportantPlaybackMode(ImportantPlaybackMode mode)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => SetImportantPlaybackMode(mode));
                return;
            }

            if (ImportantPlaybackMode != mode)
            {
                ImportantPlaybackMode = mode;
                Debug.WriteLine($"[MainViewModel] Режим принудительно установлен: {mode}");
            }
        }

        // ============================================================
        // ОБРАБОТКА СООБЩЕНИЙ ИЗ YouTubeManager
        // ============================================================
        
        private void OnYouTubeManagerMessageReceived(object? sender, ChatMessage message)
        {
            try
            {
                var commonMsg = new CommonMessage
                {
                    Type = "youtube",
                    Login = message.Author,
                    Message = message.Text,
                    Timestamp = message.Timestamp.Ticks
                };
                
                var externalId = $"youtube:{message.Author}".ToLower();
                commonMsg.User = ChaterStorage.GetByExternalId(externalId);
                
                if (commonMsg.User == null)
                {
                    commonMsg.User = new Chater
                    {
                        Id = Guid.NewGuid().ToString(),
                        Login = message.Author,
                        DisplayName = message.Author,
                        FirstSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    
                    commonMsg.User.Accounts.Add(new ExternalAccount
                    {
                        ExternalId = externalId,
                        Platform = "youtube",
                        OriginalName = message.Author
                    });
                    
                    ChaterStorage.AddOrUpdate(commonMsg.User);
                    DatabaseService.SaveChater(commonMsg.User);
                }
                
                commonMsg.User.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _messageHandler.ProcessMessage(commonMsg.User, commonMsg, null!);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[YouTubeManager] Ошибка обработки сообщения: {ex.Message}");
            }
        }

        private void LoadInitialData()
        {
            var history = DatabaseService.LoadAll();
            foreach (var chater in history)
            {
                ChaterStorage.AddOrUpdate(chater);
                Users.Add(chater);
            }
        }

        private void OnMessageProcessed(Chater chater, CommonMessage msg, List<ChatCommandInfo> commands)
        {
            string uiMessage = msg.Message;

            if (msg.Message.Length >= Settings.MinMessageLength)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    LastMessageText = $"[#{chater.KarmaKey}] {chater.EffectiveName}: {uiMessage}";
                });
            }





            Debug.WriteLine($"[MainViewModel] Получено сообщение от {chater.Login}:");
            Debug.WriteLine($"   - Оригинальный номер: {msg.MessageNumber}");
            Debug.WriteLine($"   - Текст: {uiMessage}");
            Debug.WriteLine($"   - IsProcessedByCommand: {msg.IsProcessedByCommand}");

            bool isStickerAction = commands != null && commands.Any(c =>
                c.Name.Equals("st", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("стикер", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("sticker", StringComparison.OrdinalIgnoreCase));

            bool isImportantAction = commands != null && commands.Any(c =>
                c.Name.Equals("important", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("важно", StringComparison.OrdinalIgnoreCase));

            string cleanUiMessage = uiMessage;
            if (isImportantAction)
            {
                cleanUiMessage = uiMessage.Replace("<important>", "").Replace("</important>", "").Trim();
            }

            var overlayMsg = new CommonMessage
            {
                User = chater,
                Login = chater.Login,
                Type = msg.Type.ToLower(),
                Message = cleanUiMessage,
                KarmaKeyDisplay = $"#{chater.KarmaKey}",
                MessageNumber = msg.MessageNumber,
                IsProcessedByCommand = msg.IsProcessedByCommand,
                DisplayTimeMs = msg.DisplayTimeMs
            };


            //Debug.WriteLine($"[WebServer] ПЕРЕД ВЫЗОВОМ AddMessageToWebOverlay для {chater.Login}");
            //// Добавляем сообщение в веб-оверлей для OBS
            //AddMessageToWebOverlay(chater, overlayMsg);
            //Debug.WriteLine($"[WebServer] ПОСЛЕ ВЫЗОВА AddMessageToWebOverlay для {chater.Login}");


            _dashboardService.AddMessage(chater, overlayMsg);

            if (isImportantAction)
            {
                Debug.WriteLine($"[Important] Сообщение от {chater.Login}");
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    _overlayManager.AddImportantMessage(chater, overlayMsg);
                });
            }
            else if (isStickerAction)
            {
                Debug.WriteLine($"[Stickers] Стикер от {chater.Login}");
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    _overlayManager.AddStickerMessage(chater, overlayMsg);
                });
            }
            else
            {
                _overlayManager.AddMessage(chater, overlayMsg);
            }


        }

        private void StartPolling()
        {
            _pollingcts?.Cancel();
            _pollingcts?.Dispose();
            _pollingcts = new CancellationTokenSource();

            _ = MessageService.StartListeningAsync(
                $"ws://127.0.0.1:{Settings.NetworkPort}/chat/ws/stream",
                msg => _messageHandler.ProcessExternalMessage(msg),
                _pollingcts.Token,
                () => IsProcessRunning);
        }

        //[RelayCommand(CanExecute = nameof(CanStart))]
        //private void Start()
        //{
        //    int requestedNumber = CurrentSession?.Number ?? 0;

        //    if (requestedNumber > 0)
        //    {
        //        _streamSessionManager.EnsureSessionByNumber(requestedNumber, n =>
        //        {
        //            LastStreamNumber = n;
        //            Settings.LastStreamNumber = n;
        //            ConfigService.Save(Settings);
        //        });
        //    }

        //    if (_chatService.TryAttachExisting() || SafeStart())
        //    {
        //        IsProcessRunning = true;
        //        _streamSessionManager.SetStartTime();
        //        StartPolling();
        //    }
        //}
        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task Start()
        {
            Debug.WriteLine("[MainViewModel] Start() вызван");

            // ✅ СНАЧАЛА ЗАПУСКАЕМ ВЕБ-СЕРВЕР
            if (_webServer != null && !_isWebServerRunning)
            {
                Debug.WriteLine("[MainViewModel] Запуск веб-сервера...");
                await StartWebServerAsync();
                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{Settings.NetworkPort}/");
            }

            // ✅ ТЕПЕРЬ ВСЁ ОСТАЛЬНОЕ
            int requestedNumber = CurrentSession?.Number ?? 0;

            if (requestedNumber > 0)
            {
                _streamSessionManager.EnsureSessionByNumber(requestedNumber, n =>
                {
                    LastStreamNumber = n;
                    Settings.LastStreamNumber = n;
                    ConfigService.Save(Settings);
                    Debug.WriteLine($"[MainViewModel] Установлен номер стрима: {n}");
                });
            }

            // ✅ Устанавливаем сессию в процессоре
            if (_streamSessionManager.CurrentSession != null)
            {
                _messageHandler.SetSession(_streamSessionManager.CurrentSession.Id);
                Debug.WriteLine($"[MainViewModel] Сессия установлена: {_streamSessionManager.CurrentSession.Id}");
            }
            else
            {
                Debug.WriteLine("[MainViewModel] ⚠️ CurrentSession == null, сессия НЕ установлена!");
            }

            // ✅ ПОДКЛЮЧАЕМ ЧАТЫ
            var chatsToConnect = Chats.Where(c => !c.IsConnected).ToList();

            if (chatsToConnect.Any())
            {
                Debug.WriteLine($"[MainViewModel] Подключаем {chatsToConnect.Count} чатов параллельно...");
                var connectTasks = chatsToConnect.Select(chat => ConnectChat(chat));
                await Task.WhenAll(connectTasks);
                Debug.WriteLine("[MainViewModel] Все чаты подключены (или попытки завершены)");
            }

            IsProcessRunning = true;
            _streamSessionManager.SetStartTime();

            Debug.WriteLine($"[MainViewModel] Стрим #{_streamSessionManager.CurrentSession?.Number} запущен");
        }

        private bool SafeStart()
        {
            try { _chatService.Start(); return true; }
            catch (Exception ex) { MessageBox.Show(ex.Message); return false; }
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {

            await StopAllChats();

            _pollingcts?.Cancel();
            await _chatService.StopAsync();
            _streamSessionManager.SaveSessionEndTime();
            IsProcessRunning = false;
        }

        [RelayCommand]
        private void NextStream()
        {
            _streamSessionManager.NextStream(CurrentSession?.Title ?? "Без названия", (number, title) =>
            {
                LastStreamNumber = number;
                Settings.LastStreamNumber = number;
                ConfigService.Save(Settings);
            });
        }

        [RelayCommand]
        private void SaveSettings() => _settingsService.SaveSettings();

        public void SaveOverlayPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveShortsPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveImportantPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveStickersPosition() => _overlayManager.SaveAllPositions(Settings);

        // ============================================================
        // УПРАВЛЕНИЕ ОЧЕРЕДЬЮ ВАЖНЫХ СООБЩЕНИЙ
        // ============================================================

        public void UpdateImportantQueueCount(int count)
        {
            _settingsService.UpdateImportantQueueCount(count, IsAutoSwitchingEnabled, ImportantPlaybackMode,
                (c, mode) =>
                {
                    ImportantQueueCount = c;
                    ImportantPlaybackMode = mode;
                });
        }

        private void OnProcessExited() => Application.Current.Dispatcher.Invoke(() => { IsProcessRunning = false; });
        private bool CanStart() => !IsProcessRunning;
        private bool CanStop() => IsProcessRunning;

        [RelayCommand]
        private async Task PlayNextImportant()
        {
            if (ImportantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                await _overlayManager.PlayNextFromQueueAsync();
                UpdateImportantQueueCount(_overlayManager.QueueSize);
            }
        }

        [RelayCommand]
        private void Launch()
        {
            if (System.IO.File.Exists(ProgramPath))
                Process.Start(new ProcessStartInfo(ProgramPath) { UseShellExecute = true });
        }

        [RelayCommand]
        private void ToggleDashboard()
        {
            // ✅ Инициализируем сервис (один раз)
            _dashboardService.Initialize();

            if (_dashboardService.IsVisible)
                _dashboardService.Hide();
            else
                _dashboardService.Show();
        }

        [RelayCommand]
        private void ToggleShortsOverlay()
        {
            _overlayManager.ToggleShorts();
        }

        [RelayCommand]
        private void ToggleImportantOverlay()
        {
            _overlayManager.ToggleImportant();
        }

        [RelayCommand]
        private void ToggleStickersOverlay()
        {
            IsStickersVisible = !IsStickersVisible;
        }

        [RelayCommand]
        private async Task AddKarmaToAll()
        {
            var result = MessageBox.Show(
                $"Начислить 10 кармы всем {Users.Count} зрителям, которые были в чате за текущий стрим?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                int count = 0;
                foreach (var chater in Users)
                {
                    chater.Karma += 10;
                    chater.TotalKarma += 10;

                    DatabaseService.UpdateChaterStats(chater);
                    ChaterStorage.AddOrUpdate(chater);
                    count++;
                }

                LastMessageText = $"✅ Начислено 10 кармы {count} зрителям!";
                Debug.WriteLine($"[Karma] Начислено 10 кармы {count} пользователям");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var temp = Users.ToList();
                    Users.Clear();
                    foreach (var user in temp)
                    {
                        Users.Add(user);
                    }
                });

                await VoiceService.PlayImportantSoundAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Karma] Ошибка начисления: {ex.Message}");
                LastMessageText = $"❌ Ошибка начисления: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // YOUTUBE КОМАНДЫ
        // ============================================================

        [RelayCommand]
        private async Task LoadYouTubeStreams()
        {
            // ✅ Делегируем YouTubeManager
            await YouTubeManager.FindStreamsViaHtmlAsync();
        }

        [RelayCommand]
        private async Task ConnectYouTubeChat()
        {
            // ✅ Делегируем YouTubeManager
            await YouTubeManager.ConnectSelectedAsync();
        }

        [RelayCommand]
        private void DisconnectYouTubeChat()
        {
            // ✅ Делегируем YouTubeManager
            YouTubeManager.DisconnectAll();
        }

        [RelayCommand]
        private async Task SendYouTubeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Debug.WriteLine($"[YouTube] Отправка сообщения (не поддерживается): {message}");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show("Отправка сообщений в YouTube чат не поддерживается через API.",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        // ============================================================
        // КОМАНДЫ ДЛЯ ЧАТОВ С РЕАЛЬНЫМИ КОННЕКТОРАМИ
        // ============================================================

        [RelayCommand]
        private async Task ConnectChat(ChatConnection? chat)
        {
            if (chat == null) return;

            // Для ручного режима проверяем Video ID
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

            // ✅ Добавляем ConfigureAwait(false) чтобы не блокировать UI поток
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

        public ChatConnectionService GetChatConnectionService() => _chatConnectionService;

        [RelayCommand]
        private async Task ConnectByVideoId(ChatConnection? chat)
        {
            if (chat == null) return;

            if (string.IsNullOrEmpty(chat.VideoId) || chat.VideoId.Length != 11)
            {
                MessageBox.Show("Введите корректный Video ID (11 символов)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Устанавливаем метод подключения на ManualVideoId
            chat.PreferredMethod = YouTubeConnectionMethod.ManualVideoId;

            await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
            {
                chat.Status = connected ? "✅ Подключен (Video ID)" : $"❌ Ошибка: {chat.LastConnectionError}";
                UpdateStats();
            });
        }

        // ============================================================
        // ОБРАБОТКА СООБЩЕНИЙ ИЗ КОННЕКТОРОВ
        // ============================================================

        private void OnConnectorMessageReceived(object? sender, IncomingChatMessage message)
        {
            _messageHandler.ProcessConnectorMessage(sender, message);
        }

        // ============================================================
        // ОБНОВЛЕНИЕ СТАТИСТИКИ
        // ============================================================

        private void UpdateStats()
        {
            ConnectedChatsCount = Chats.Count(c => c.IsConnected);
            TotalMessagesCount = Chats.Sum(c => c.MessageCount);
        }

        // ============================================================
        // ЗАГРУЗКА И ОБНОВЛЕНИЕ ЧАТОВ
        // ============================================================

        private void LoadChats()
        {

            // Подписываемся на события
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

        public async Task RefreshChats()
        {
            foreach (var chat in Chats.Where(c => c.IsConnected).ToList())
            {
                await DisconnectChat(chat);
            }

            // Используем существующий _chatManager с общей коллекцией
            // _chatManager = new ChatManagerViewModel(Chats, _chatConnectionService);
            Chats.CollectionChanged += (s, e) => UpdateStats();
            UpdateStats();
        }

        public ChatManagerViewModel GetChatManagerViewModel() => _chatManager;


        // ============================================================
        // ВЕБ-СЕРВЕР ДЛЯ OBS
        // ============================================================

        private async Task StartWebServerAsync()
        {
            try
            {
                await _webServer!.StartAsync();
                _isWebServerRunning = true;
                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{Settings.NetworkPort}/");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка запуска: {ex.Message}");
            }
        }

        private void OnWebMessageAdded(object? sender, DisplayMessageViewModel msg)
        {
            // Здесь можно добавить дополнительную логику при получении сообщения
        }

        //private void AddMessageToWebOverlay(Chater chater, CommonMessage msg)
        //{

        //    // ✅ ПОКАЗЫВАЕМ ОКНО ДЛЯ ПРОВЕРКИ
        //    Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        MessageBox.Show($"AddMessageToWebOverlay вызван! _webServer={_webServer != null}, _isWebServerRunning={_isWebServerRunning}");
        //    });

        //    Debug.WriteLine($"[WebServer] AddMessageToWebOverlay ВЫЗВАН! _webServer={_webServer != null}, _isWebServerRunning={_isWebServerRunning}");

        //    if (_webServer == null || !_isWebServerRunning)
        //    {
        //        Debug.WriteLine($"[WebServer] НЕ ДОБАВЛЕНО: _webServer={_webServer != null}, _isWebServerRunning={_isWebServerRunning}");
        //        return;
        //    }

        //    var displayMsg = new DisplayMessageViewModel(chater, msg);
        //    _webServer.AddMessage(displayMsg);
        //    Debug.WriteLine($"[WebServer] ✅ ДОБАВЛЕНО сообщение: {chater.EffectiveName}: {msg.Message}");
        //}

        [RelayCommand]
        private void OpenOverlayUrl()
        {
            var url = $"http://localhost:{Settings.NetworkPort}/";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebServer] Ошибка открытия URL: {ex.Message}");
                MessageBox.Show($"Не удалось открыть браузер: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }



}