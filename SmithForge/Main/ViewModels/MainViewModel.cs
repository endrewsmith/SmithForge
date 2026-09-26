using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.ChatEngine.Core.Models;
using SmithForge.ChatEngine.Platforms.YouTube;
using SmithForge.ChatEngine.Platforms.YouTube.Models;
using SmithForge.Features.ChatManager;
using SmithForge.Features.ChatOverlay;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Features.ImportantOverlay;
using SmithForge.Features.InfoSystem;
using SmithForge.Features.StickersOverlay;
using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
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

        [ObservableProperty]
        private string _karmaAmountText = "10";

        [ObservableProperty]
        private int _voiceRate = 3;

        [ObservableProperty]
        private int _scrollSpeed = 1500;
        [ObservableProperty]
        private double _appearSpeed = 0.3;

        private InfoRotationService? _rotationService;
        private StickerPageService _stickerPageService;
        private SoundPageService _soundPageService;

        [ObservableProperty]
        private int _rotationTotalPages = 0;

        [ObservableProperty]
        private string _rotationStatus = "⏹ Остановлена";

        [ObservableProperty]
        private int _rotationSilentSeconds = 0;

        [ObservableProperty]
        private int _rotationShownPages = 0; // ← НОВОЕ СВОЙСТВО

        
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
        private readonly SmithForge.Features.MediaDashboard.MediaDashboardService _mediaDashboardService = new();
        private readonly MessageHandlerService _messageHandler;
        private readonly OverlayManagerService _overlayManager;
        private readonly SettingsService _settingsService;
        private readonly DialogService _dialogService;
        private readonly ExternalChatService _chatService = new();
        private CancellationTokenSource? _pollingcts;
        private readonly AlertsService _alertsService = new();

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

        public TechOverlayService TechOverlay { get; }


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

        private InfoService _infoService;

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
            // ✅ Фича: технический оверлей
            TechOverlay = new TechOverlayService(_webServer);

            //Task.Run(async () => await StartWebServerAsync());


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
            _mediaDashboardService.Initialize();
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
            //YouTubeManager.MessageReceived += OnYouTubeManagerMessageReceived;

            // ✅ Подписываемся на события AlertsService
            _alertsService.AlertReceived += OnAlertReceived;
            _alertsService.StatusChanged += OnAlertStatusChanged;

            // ✅ Создаём ChatManagerViewModel с общей коллекцией
            _chatManager = new ChatManagerViewModel(Chats, null);

            // ✅ ЗАГРУЖАЕМ ЧАТЫ ИЗ ФАЙЛА (ЭТОГО НЕ ХВАТАЕТ!)
            _chatManager.LoadChatsFromFile();

            // ✅ Инициализация сервиса управления чатами
            _chatConnectionService = new ChatConnectionService(_chatManager);
            _chatConnectionService.MessageReceived += OnConnectorMessageReceived;

            // ✅ Обновляем _chatManager с сервисом
            _chatManager = new ChatManagerViewModel(Chats, _chatConnectionService);

            // ✅ ЗАГРУЖАЕМ СОХРАНЕННУЮ СКОРОСТЬ
            _voiceRate = Settings.VoiceRate;
            VoiceService.SetVoiceRate(_voiceRate);

            Debug.WriteLine($"🎙️ [MainViewModel] СТАРТОВАЯ СКОРОСТЬ: {_voiceRate}");
            Debug.WriteLine($"🎙️ [MainViewModel] VoiceService.GetVoiceRate() = {VoiceService.GetVoiceRate()}");


            // ============================================================
            // ИНИЦИАЛИЗАЦИЯ РОТАЦИИ
            // ============================================================
            _stickerPageService = new StickerPageService();
            _stickerPageService.ScanPacks();  // ← ДОБАВЛЕНО: заполняет _packNumberToId

            _soundPageService = new SoundPageService();

            // ✅ ЕДИНЫЙ InfoService с StickerPageService — используется везде
            _infoService = new InfoService(_stickerPageService);

            var pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Html", "InfoPages");

            _rotationService = new InfoRotationService(_infoService, pagesDir);
            _rotationService.PageSelected += OnRotationPageSelected;
            //_rotationService.Start(30);



            // Обновляем статус
            UpdateRotationStatus();

            Debug.WriteLine("[MainViewModel] InfoRotationService инициализирован");

            // ✅ Инициализация сервиса обработки сообщений
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



        /// <summary>
        /// Корректное завершение веб-сервера
        /// </summary>
        public void ShutdownWebServer()
        {
            try
            {
                Debug.WriteLine("[MainViewModel] Завершение веб-сервера...");

                if (_webServer != null)
                {

                    // 2. Вызываем Dispose (закроет все SSE-соединения и остановит сервер)
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

        // ============================================================
        // ОБРАБОТЧИКИ СОБЫТИЙ ALERTS SERVICE
        // ============================================================

        /// <summary>
        /// Получен новый алерт — показываем в оверлее
        /// </summary>
        private void OnAlertReceived(object? sender, IncomingAlert alert)
        {
            if (alert == null) return;

            Debug.WriteLine($"[MainViewModel] Получен алерт: [{alert.ProviderType}] {alert.DisplayText}");

            // 1. Показываем в WPF-оверлее
            Application.Current.Dispatcher.Invoke(() =>
            {
                _overlayManager.ShowAlert(alert);
            });

            // 2. ⭐ ОТПРАВЛЯЕМ В ВЕБ-ОВЕРЛЕЙ /alerts (для OBS)
            if (_webServer != null)
            {
                try
                {
                    string providerName = alert.ProviderType switch
                    {
                        AlertProviderType.DonationAlerts => "DonationAlerts",
                        AlertProviderType.DonationPay => "DonationPay",
                        _ => "Alert"
                    };

                    string displayAmount = alert.Type switch
                    {
                        AlertType.Donation => $"{alert.Amount:F0} {alert.Currency}",
                        AlertType.Subscription => "Подписка",
                        AlertType.Follow => "Фолловер",
                        _ => alert.Type.ToString()
                    };

                    _webServer.SendAlertToWeb(
                        userName: alert.UserName,
                        message: alert.Message,
                        displayAmount: displayAmount,
                        providerType: alert.ProviderType.ToString().ToLower(),
                        providerName: providerName,
                        durationSeconds: Settings.AlertsAlertDuration
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainViewModel] Ошибка отправки алерта в веб: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Изменился статус провайдера алертов
        /// </summary>
        private void OnAlertStatusChanged(object? sender, AlertStatus status)
        {
            Debug.WriteLine($"[MainViewModel] Статус алертов: {status}");
            // Можно обновить UI-индикатор статуса, если нужно
        }
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

        [RelayCommand(CanExecute = nameof(CanStart))]

        private async Task Start()
        {
            Debug.WriteLine("[MainViewModel] Start() вызван");

            // ✅ 1. ЗАПУСК ВЕБ-СЕРВЕРА
            if (_webServer != null && !_isWebServerRunning)
            {
                Debug.WriteLine("[MainViewModel] Запуск веб-сервера...");
                Task.Run(StartWebServerAsync);
                Debug.WriteLine($"[WebServer] Запущен на http://localhost:{Settings.NetworkPort}/");
            }

            // ✅ 2. ЗАПУСК РОТАЦИИ ИНФО
            if (_rotationService != null)
            {
                _rotationService.Start(RotationSilenceInterval);
                Debug.WriteLine($"[MainViewModel] InfoRotationService запущен (тишина: {RotationSilenceInterval}с)");
                UpdateRotationStatus();
            }

            // ✅ 3. ЗАПУСК ALERTS SERVICE
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
            // ✅ 4. УСТАНОВКА СЕССИИ
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

            // ✅ 5. ПОДКЛЮЧАЕМ ЧАТЫ
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
            Debug.WriteLine("[MainViewModel] Stop() вызван");

            // ✅ 1. ОСТАНОВКА РОТАЦИИ ИНФО
            if (_rotationService != null)
            {
                _rotationService.Stop();
                Debug.WriteLine("[MainViewModel] InfoRotationService остановлен");
                UpdateRotationStatus();
            }

            // ✅ 2. ОСТАНОВКА ALERTS SERVICE
            try
            {
                await _alertsService.StopAsync();
                Debug.WriteLine("[MainViewModel] AlertsService остановлен");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка остановки AlertsService: {ex.Message}");
            }

            // ✅ 3. ОТКЛЮЧЕНИЕ ЧАТОВ
            await StopAllChats();

            // ✅ 4. ОСТАНОВКА ВНЕШНЕГО ПРОЦЕССА И СЕССИИ
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

        public void SaveOverlayPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveShortsPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveImportantPosition() => _overlayManager.SaveAllPositions(Settings);
        public void SaveStickersPosition() => _overlayManager.SaveAllPositions(Settings);

        public void SaveAlertsPosition() => _overlayManager.SaveAllPositions(Settings);


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
        private void ToggleMediaDashboard()
        {
            _mediaDashboardService.Initialize();

            if (_mediaDashboardService.IsVisible)
                _mediaDashboardService.Hide();
            else
                _mediaDashboardService.Show();
        }
        [RelayCommand]
        private void ToggleTechOverlay()
        {
            TechOverlay?.Toggle();
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
        private void ToggleAlertsOverlay()
        {
            Settings.AlertsOverlayVisible = !Settings.AlertsOverlayVisible;
            _overlayManager.SetAlertsVisible(Settings.AlertsOverlayVisible);
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainViewModel] AlertsOverlayVisible: {Settings.AlertsOverlayVisible}");
        }

        [RelayCommand]
        private void OpenAlertsWebOverlay()
        {
            try
            {
                string url = $"http://localhost:{Settings.NetworkPort}/alerts";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Debug.WriteLine($"[MainViewModel] Открыт веб-оверлей алертов: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка открытия веб-оверлея: {ex.Message}");
                MessageBox.Show($"Не удалось открыть браузер: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private async Task OpenAlertsSettings()
        {
            try
            {
                var window = new SmithForge.Features.AlertsSettings.AlertsSettingsWindow(Settings)
                {
                    Owner = Application.Current.MainWindow
                };

                var result = window.ShowDialog();

                if (result == true)
                {
                    Debug.WriteLine("[MainViewModel] Настройки алертов сохранены, перезапускаем AlertsService...");

                    // Перезапускаем сервис с новыми настройками
                    await _alertsService.StartAsync(Settings);

                    // Обновляем видимость оверлея
                    _overlayManager.SetAlertsVisible(Settings.AlertsOverlayVisible);
                    _overlayManager.SetAlertsDuration(Settings.AlertsAlertDuration);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка открытия настроек алертов: {ex.Message}");
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task AddKarmaToAll()
        {
            // 1. Парсим число из текстового поля
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

            // 2. Подтверждение
            var allChaters = ChaterStorage.GetAll();

            var result = MessageBox.Show(
                $"Начислить {amount} кармы всем {allChaters.Count} зрителям?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // 3. Начисление
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

                // ✅ Отправляем техническое событие
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
        // YOUTUBE КОМАНДЫ
        // ============================================================

        //[RelayCommand]
        //private async Task LoadYouTubeStreams()
        //{
        //    // ✅ Делегируем YouTubeManager
        //    await YouTubeManager.FindStreamsViaHtmlAsync();
        //}

        //[RelayCommand]
        //private async Task ConnectYouTubeChat()
        //{
        //    // ✅ Делегируем YouTubeManager
        //    await YouTubeManager.ConnectSelectedAsync();
        //}

        //[RelayCommand]
        //private void DisconnectYouTubeChat()
        //{
        //    // ✅ Делегируем YouTubeManager
        //    YouTubeManager.DisconnectAll();
        //}

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
            if (_isWebServerRunning) return;  // ← защита
            _isWebServerRunning = true;       // ← ставим ДО запуска, чтобы второй вызов не начал работу
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

        [RelayCommand]
        private void SetVoiceRate(object? parameter)
        {
            if (parameter == null) return;

            int value = 3;

            if (parameter is int intValue)
                value = intValue;
            else if (parameter is string stringValue && int.TryParse(stringValue, out int parsed))
                value = parsed;
            else if (parameter is double doubleValue)
                value = (int)doubleValue;
            else
                return;

            Debug.WriteLine($"🎯 SetVoiceRateCommand ВЫЗВАН! rate={value}");

            // ✅ ВСЕГДА УСТАНАВЛИВАЕМ, ДАЖЕ ЕСЛИ ЗНАЧЕНИЕ ТАКОЕ ЖЕ
            _voiceRate = Math.Clamp(value, -10, 10);
            OnPropertyChanged(nameof(VoiceRate)); // Принудительно обновляем UI

            Settings.VoiceRate = _voiceRate;
            ConfigService.Save(Settings);
            VoiceService.SetVoiceRate(_voiceRate);

            Debug.WriteLine($"🎙️ [Command] Установлена скорость: {_voiceRate}");
            Debug.WriteLine($"🎙️ [Command] VoiceService.GetVoiceRate() = {VoiceService.GetVoiceRate()}");
        }

        partial void OnVoiceRateChanged(int value)
        {
            value = Math.Clamp(value, -10, 10);

            Debug.WriteLine($"🎯 OnVoiceRateChanged ВЫЗВАН! value={value}, _voiceRate={_voiceRate}");

            // ✅ УБИРАЕМ ПРОВЕРКУ if (_voiceRate != value) — ВСЕГДА УСТАНАВЛИВАЕМ!
            _voiceRate = value;
            Settings.VoiceRate = value;
            ConfigService.Save(Settings);

            // ✅ ВСЕГДА ВЫЗЫВАЕМ VoiceService.SetVoiceRate
            VoiceService.SetVoiceRate(value);

            Debug.WriteLine($"🎙️ [MainViewModel] Скорость изменена: {value}");
            Debug.WriteLine($"🎙️ [MainViewModel] VoiceService.GetVoiceRate() = {VoiceService.GetVoiceRate()}");
        }

        partial void OnScrollSpeedChanged(int value)
        {
            // Отправляем новую скорость на сервер
            _ = SendScrollSpeed(value);
        }

        private async Task SendScrollSpeed(int speed)
        {
            try
            {
                using var client = new HttpClient();
                var json = $"{{\"speed\":{speed}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"http://localhost:{Settings.NetworkPort}/info/scroll/speed", content);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[InfoChat] Скорость скролла отправлена: {speed}ms");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoChat] Ошибка отправки скорости: {ex.Message}");
            }
        }

        partial void OnAppearSpeedChanged(double value)
        {
            _ = SendAppearSpeed(value);
        }

        private async Task SendAppearSpeed(double speed)
        {
            try
            {
                using var client = new HttpClient();
                var json = $"{{\"speed\":{speed.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await client.PostAsync($"http://localhost:{Settings.NetworkPort}/info/appear/speed", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoChat] Ошибка отправки скорости появления: {ex.Message}");
            }
        }

        // ============================================================
        // ОБРАБОТЧИК СОБЫТИЙ
        // ============================================================

        private void OnRotationPageSelected(object? sender, string pageName)
        {
            try
            {
                var webServer = WebServerService.Instance;
                if (webServer != null && _infoService != null)
                {
                    var html = _infoService.Render(pageName, "system");
                    webServer.SendInfoMessage(html, pageName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Rotation] ❌ Ошибка: {ex.Message}");
            }
        }

        // ============================================================
        // КОМАНДЫ ДЛЯ UI
        // ============================================================

        [RelayCommand]
        private void RotationStart()
        {
            _rotationService?.Start(RotationSilenceInterval);
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationStop()
        {
            _rotationService?.Stop();
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationRefreshPages()
        {
            _rotationService?.RefreshPagesList();
            UpdateRotationStatus();
        }

        [RelayCommand]
        private void RotationActivity()
        {
            _rotationService?.OnUserActivity();
            UpdateRotationStatus();
        }

        // ============================================================
        // СВОЙСТВА ДЛЯ UI
        // ============================================================

        [ObservableProperty]
        private int _rotationSilenceInterval = 30;

        partial void OnRotationSilenceIntervalChanged(int value)
        {
            _rotationService?.SetSilenceInterval(value);
        }



        private void UpdateRotationStatus()
        {
            var status = _rotationService?.GetStatus();
            if (status == null) return;

            RotationTotalPages = status.TotalPages;
            RotationShownPages = status.ShownPages; // ← добавить эту строку
            RotationSilentSeconds = status.SilentSeconds;

            if (!status.IsRunning)
            {
                RotationStatus = "⏹ Остановлена";
            }
            else if (status.IsWaitingForSilence)
            {
                RotationStatus = "📢 Показ страницы...";
            }
            else
            {
                RotationStatus = $"🔇 Тишина: {status.SilentSeconds}с / {status.SilenceIntervalSeconds}с";
            }
        }

        // Обновляем статус по таймеру (раз в секунду)
        private void StartStatusUpdater()
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);
                    if (_rotationService != null)
                    {
                        UpdateRotationStatus();
                    }
                }
            });
        }

        public void NotifyUserActivity()
        {
            _rotationService?.OnUserActivity();
            UpdateRotationStatus();
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

        // Команда для UI
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