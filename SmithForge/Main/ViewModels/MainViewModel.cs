using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Features.InfoSystem;
using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SmithForge.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _karmaAmountText = "10";

        private StickerPageService _stickerPageService;
        private SoundPageService _soundPageService;

        private readonly DashboardService _dashboardService = new();
        private readonly SmithForge.Features.MediaDashboard.MediaDashboardService _mediaDashboardService = new();
        private readonly MessageHandlerService _messageHandler;
        private readonly OverlayManagerService _overlayManager;
        private readonly SettingsService _settingsService;
        private readonly DialogService _dialogService;
        private readonly ExternalChatService _chatService = new();
        private CancellationTokenSource? _pollingcts;

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

        public ObservableCollection<Chater> Users { get; } = new();

        public TechOverlayService TechOverlay { get; }

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

        private InfoService _infoService;

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();

            Settings.NetworkPort = 10881;
            ConfigService.Save(Settings);

            _overlayManager = new OverlayManagerService(Settings);
            _settingsService = new SettingsService(Settings, _overlayManager);
            _dialogService = new DialogService();

            _webServer = new WebServerService((int)Settings.NetworkPort);
            TechOverlay = new TechOverlayService(_webServer);

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

            _streamSessionManager = new StreamSessionManager();

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
            _mediaDashboardService.Initialize();

            _stickerDisplayTime = Settings.StickerDisplayTimeMs;
            _importantSoundVolume = Settings.ImportantSoundVolume;
            _voiceVolume = Settings.VoiceVolume;
            VoiceService.SetImportantSoundVolume(_importantSoundVolume);
            VoiceService.SetVoiceVolume(_voiceVolume);

            _importantPlaybackMode = Settings.ImportantPlaybackMode;
            _importantPlaybackHotkey = Settings.ImportantPlaybackHotkey;

            VoiceService.Initialize(Dispatcher.CurrentDispatcher);

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

            _alertsService.AlertReceived += OnAlertReceived;
            _alertsService.StatusChanged += OnAlertStatusChanged;

            _chatManager = new ChatManagerViewModel(Chats, null);
            _chatManager.LoadChatsFromFile();

            _chatConnectionService = new ChatConnectionService(_chatManager);
            _chatConnectionService.MessageReceived += OnConnectorMessageReceived;

            _chatManager = new ChatManagerViewModel(Chats, _chatConnectionService);

            _voiceRate = Settings.VoiceRate;
            VoiceService.SetVoiceRate(_voiceRate);

            Debug.WriteLine($"🎙️ [MainViewModel] СТАРТОВАЯ СКОРОСТЬ: {_voiceRate}");
            Debug.WriteLine($"🎙️ [MainViewModel] VoiceService.GetVoiceRate() = {VoiceService.GetVoiceRate()}");

            // ============================================================
            // ИНИЦИАЛИЗАЦИЯ РОТАЦИИ
            // ============================================================
            _stickerPageService = new StickerPageService();
            _stickerPageService.ScanPacks();

            _soundPageService = new SoundPageService();

            _infoService = new InfoService(_stickerPageService);

            var pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Html", "InfoPages");

            _rotationService = new InfoRotationService(_infoService, pagesDir);
            _rotationService.PageSelected += OnRotationPageSelected;

            UpdateRotationStatus();

            Debug.WriteLine("[MainViewModel] InfoRotationService инициализирован");

            var processor = new MessageProcessor(Settings, _infoService, _stickerPageService, _soundPageService);
            _messageHandler = new MessageHandlerService(
                processor,
                _overlayManager,
                _dashboardService,
                _mediaDashboardService,
                _webServer);
            _messageHandler.OnProcessed += OnMessageProcessed;

            LoadChats();
        }

        public void ShutdownWebServer()
        {
            try
            {
                Debug.WriteLine("[MainViewModel] Завершение веб-сервера...");

                if (_webServer != null)
                {
                    _webServer.Dispose();
                    _webServer = null;
                }

                _isWebServerRunning = false;

                Debug.WriteLine("[MainViewModel] Веб-сервер завершён");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка завершения веб-сервера: {ex.Message}");
            }
        }

        // ============================================================
        // LOAD INITIAL DATA
        // ============================================================
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
                Application.Current.Dispatcher.Invoke(() =>
                {
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

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task Start()
        {
            Debug.WriteLine("[MainViewModel] Start() вызван");

            if (_webServer != null && !_isWebServerRunning)
            {
                Debug.WriteLine("[MainViewModel] Запуск веб-сервера...");
                Task.Run(StartWebServerAsync);
                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{Settings.NetworkPort}/");
            }

            if (_rotationService != null)
            {
                _rotationService.Start(RotationSilenceInterval);
                Debug.WriteLine($"[MainViewModel] InfoRotationService запущен (тишина: {RotationSilenceInterval}с)");
                UpdateRotationStatus();
            }

            try
            {
                await _alertsService.StartAsync(Settings);
                _overlayManager.SetAlertsVisible(Settings.AlertsOverlayVisible);
                Debug.WriteLine("[MainViewModel] AlertsService запущен");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка запуска AlertsService: {ex.Message}");
            }

            Debug.WriteLine($"[MainViewModel] ДО EnsureSessionByNumber: CurrentSession={CurrentSession?.Number}, LastStreamNumber={LastStreamNumber}");
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

            if (_streamSessionManager.CurrentSession != null)
            {
                _messageHandler.SetSession(_streamSessionManager.CurrentSession.Id);
                Debug.WriteLine($"[MainViewModel] Сессия установлена: {_streamSessionManager.CurrentSession.Id}");
            }
            else
            {
                Debug.WriteLine("[MainViewModel] ⚠️ CurrentSession == null, сессия НЕ установлена!");
            }

            Debug.WriteLine($"[MainViewModel] ПОСЛЕ EnsureSessionByNumber: CurrentSession={CurrentSession?.Number}, LastStreamNumber={LastStreamNumber}");

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

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {
            Debug.WriteLine("[MainViewModel] Stop() вызван");

            if (_rotationService != null)
            {
                _rotationService.Stop();
                Debug.WriteLine("[MainViewModel] InfoRotationService остановлен");
                UpdateRotationStatus();
            }

            try
            {
                await _alertsService.StopAsync();
                Debug.WriteLine("[MainViewModel] AlertsService остановлен");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка остановки AlertsService: {ex.Message}");
            }

            await StopAllChats();

            _pollingcts?.Cancel();
            await _chatService.StopAsync();
            _streamSessionManager.SaveSessionEndTime();
            IsProcessRunning = false;

            Debug.WriteLine("[MainViewModel] Stop() завершён");
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

        private void OnProcessExited() => Application.Current.Dispatcher.Invoke(() => { IsProcessRunning = false; });
        private bool CanStart() => !IsProcessRunning;
        private bool CanStop() => IsProcessRunning;

        [RelayCommand]
        private void Launch()
        {
            if (System.IO.File.Exists(ProgramPath))
                Process.Start(new ProcessStartInfo(ProgramPath) { UseShellExecute = true });
        }

        [RelayCommand]
        private async Task AddKarmaToAll()
        {
            if (!int.TryParse(KarmaAmountText?.Trim(), out int amount))
            {
                MessageBox.Show("Введите целое число", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (amount <= 0)
            {
                MessageBox.Show("Число должно быть больше нуля", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (amount > 100000)
            {
                MessageBox.Show("Слишком большое число (максимум 100000)", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var allChaters = ChaterStorage.GetAll();

            var result = MessageBox.Show(
                $"Начислить {amount} кармы всем {allChaters.Count} зрителям?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                int count = 0;

                foreach (var chater in allChaters)
                {
                    chater.Karma += amount;
                    chater.TotalKarma += amount;

                    DatabaseService.UpdateChaterStats(chater);
                    count++;
                }

                LastMessageText = $"✅ Начислено {amount} кармы {count} зрителям!";
                Debug.WriteLine($"[Karma] Начислено {amount} кармы {count} пользователям");

                TechOverlay?.Emit(TechEventFactory.KarmaGrant(amount, count));

                await VoiceService.PlayImportantSoundAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Karma] Ошибка начисления: {ex.Message}");
                LastMessageText = $"❌ Ошибка начисления: {ex.Message}";
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // КОМАНДЫ ДЛЯ ЧАТОВ С РЕАЛЬНЫМИ КОННЕКТОРАМИ
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

            chat.PreferredMethod = YouTubeConnectionMethod.ManualVideoId;

            await _chatConnectionService.ConnectChat(chat, (name, connected, count) =>
            {
                chat.Status = connected ? "✅ Подключен (Video ID)" : $"❌ Ошибка: {chat.LastConnectionError}";
                UpdateStats();
            });
        }

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

            Chats.CollectionChanged += (s, e) => UpdateStats();
            UpdateStats();
        }

        public ChatManagerViewModel GetChatManagerViewModel() => _chatManager;

        // ============================================================
        // ВЕБ-СЕРВЕР ДЛЯ OBS
        // ============================================================

        private async Task StartWebServerAsync()
        {
            if (_isWebServerRunning) return;
            try
            {
                await _webServer!.StartAsync();
                _isWebServerRunning = true;
                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{Settings.NetworkPort}/");
            }
            catch (Exception ex)
            {
                _isWebServerRunning = false;
                Debug.WriteLine($"[WebServer] Ошибка запуска: {ex.Message}");
            }
        }

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

        [ObservableProperty]
        private string _stickerPagesStatus = "Готово";

        [RelayCommand]
        private void GenerateStickerPages()
        {
            try
            {
                StickerPagesStatus = "⏳ Сканирование папок...";

                var count = _stickerPageService.GenerateAllPages();

                if (count > 0)
                {
                    StickerPagesStatus = $"✅ Сгенерировано {count} страниц стикеров";
                    Debug.WriteLine($"[StickerPages] Сгенерировано {count} страниц");
                }
                else
                {
                    StickerPagesStatus = "❌ Нет паков со стикерами";
                }
            }
            catch (Exception ex)
            {
                StickerPagesStatus = $"❌ Ошибка: {ex.Message}";
                Debug.WriteLine($"[StickerPages] Ошибка: {ex.Message}");
            }
        }

        [ObservableProperty]
        private string _soundPagesStatus = "Готово";

        [RelayCommand]
        private void GenerateSoundPages()
        {
            try
            {
                SoundPagesStatus = "⏳ Сканирование звуков...";
                var count = _soundPageService.GenerateAllPages();
                SoundPagesStatus = count > 0 ? $"✅ Сгенерировано {count} страниц звуков" : "❌ Нет паков со звуками";
            }
            catch (Exception ex)
            {
                SoundPagesStatus = $"❌ Ошибка: {ex.Message}";
            }
        }
    }
}