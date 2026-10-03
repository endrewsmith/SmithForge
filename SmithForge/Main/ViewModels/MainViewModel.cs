using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.InfoSystem;
using SmithForge.Features.StatsRotation;
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
        private string _programPath;

        public ObservableCollection<Chater> Users { get; } = new();

        public TechOverlayService TechOverlay { get; }

        private WebServerService? _webServer;
        private bool _isWebServerRunning = false;

        private InfoService _infoService;

        private StickerPageService _stickerPageService;
        private SoundPageService _soundPageService;

        // ============================================================
        // КООРДИНАТОРЫ
        // ============================================================
        public InfoRotationCoordinator InfoRotation { get; private set; } = null!;
        public AudioSettingsCoordinator AudioSettings { get; private set; } = null!;
        public YouTubeSettingsCoordinator YouTube { get; private set; } = null!;
        public StickersCoordinator Stickers { get; private set; } = null!;
        public SessionCoordinator Session { get; private set; } = null!;
        public AlertsCoordinator Alerts { get; private set; } = null!;
        public OverlayTogglesCoordinator Overlays { get; private set; } = null!;
        public ChatCoordinator ChatsManager { get; private set; } = null!;
        public StatsRotationCoordinator Stats { get; private set; } = null!;

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();

            Settings.NetworkPort = 10881;
            ConfigService.Save(Settings);

            _overlayManager = new OverlayManagerService(Settings);
            _settingsService = new SettingsService(Settings, _overlayManager);
            _dialogService = new DialogService();

            VoiceService.Initialize(Dispatcher.CurrentDispatcher);

            AudioSettings = new AudioSettingsCoordinator(_settingsService, Settings);
            YouTube = new YouTubeSettingsCoordinator(_settingsService, Settings);

            _webServer = new WebServerService((int)Settings.NetworkPort, Settings.StaticPort);
            TechOverlay = new TechOverlayService(_webServer);


            Alerts = new AlertsCoordinator(_settingsService, _overlayManager, _webServer, Settings);

            Overlays = new OverlayTogglesCoordinator(
                _settingsService,
                _overlayManager,
                _dashboardService,
                _mediaDashboardService,
                TechOverlay,
                Settings);

            Overlays.PositionsSaved += (s, e) => LastMessageText = "✅ Позиции окон сохранены";

            DatabaseService.Initialize();

            StickerManager.LoadPacks();

            _overlayManager.Initialize(
                Overlays.IsOverlaySetupMode,
                Overlays.IsOverlayHidden,
                Overlays.IsStickersVisible,
                Overlays.MainChatMode,
                Overlays.ShortsChatMode,
                Overlays.ImportantChatMode,
                Overlays.StickersChatMode,
                Alerts.ImportantPlaybackMode,
                AudioSettings.ImportantSoundVolume,
                AudioSettings.VoiceVolume,
                AudioSettings.StickerDisplayTime);

            _overlayManager.ImportantQueueChanged += (s, count) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Alerts.ImportantQueueCount = count;
                    Debug.WriteLine($"[MainViewModel] Получено событие QueueCountChanged: count={count}");
                });
            };

            ProgramPath = Settings.ProgramPath;

            Session = new SessionCoordinator(Settings);
            Session.SessionIdChanged += (s, sessionId) =>
            {
                _messageHandler?.SetSession(sessionId);
                Debug.WriteLine($"[MainViewModel] Сессия установлена: {sessionId}");
            };

            // ✅ Инициализация реестра правил статистики
            StatsRuleRegistry.Initialize(() => Session?.CurrentSession?.Id);
            Debug.WriteLine("[MainViewModel] StatsRuleRegistry инициализирован");

            // ✅ ТОЛЬКО ТЕПЕРЬ создаём координатор — реестр уже наполнен
            Stats = new StatsRotationCoordinator(_webServer, Settings);
            Debug.WriteLine($"[MainViewModel] StatsRotationCoordinator создан. Правил в UI: {Stats.Rules.Count}");

            LoadInitialData();
            _chatService.ProcessExited += (s, e) => OnProcessExited();

            _dashboardService.Initialize();
            _mediaDashboardService.Initialize();

            if (Alerts.IsAutoSwitchingEnabled && Alerts.ImportantQueueCount == 0 && Alerts.ImportantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                Debug.WriteLine("[MainViewModel] Стартовая синхронизация: очередь пуста, переключаем режим на Auto");
                Alerts.ImportantPlaybackMode = ImportantPlaybackMode.Auto;
            }
            else if (Alerts.IsAutoSwitchingEnabled && Alerts.ImportantQueueCount > 0 && Alerts.ImportantPlaybackMode == ImportantPlaybackMode.Auto)
            {
                Debug.WriteLine($"[MainViewModel] Стартовая синхронизация: в очереди {Alerts.ImportantQueueCount} сообщений, переключаем режим на Manual");
                Alerts.ImportantPlaybackMode = ImportantPlaybackMode.Manual;
            }

            // ============================================================
            // ИНИЦИАЛИЗАЦИЯ РОТАЦИИ
            // ============================================================
            _stickerPageService = new StickerPageService();
            _stickerPageService.ScanPacks();

            _soundPageService = new SoundPageService();

            Stickers = new StickersCoordinator(_stickerPageService, _soundPageService);

            _infoService = new InfoService(_stickerPageService);

            var pagesDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Html", "InfoPages");

            var rotationService = new InfoRotationService(_infoService, pagesDir);
            InfoRotation = new InfoRotationCoordinator(rotationService, _infoService, _webServer);
            InfoRotation.UpdateStatus();

            Debug.WriteLine("[MainViewModel] InfoRotationCoordinator инициализирован");

            var processor = new MessageProcessor(Settings, _infoService, _stickerPageService, _soundPageService);
            _messageHandler = new MessageHandlerService(
                processor,
                _overlayManager,
                _dashboardService,
                _mediaDashboardService,
                _webServer);
            _messageHandler.OnProcessed += OnMessageProcessed;

            // ✅ Создаём координатор чатов ПОСЛЕ MessageHandler
            ChatsManager = new ChatCoordinator(_dialogService, _messageHandler);
            ChatsManager.Initialize();
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
            // ✅ КРИТИЧНО: сообщения из Twitch/GoodGame приходят в фоновых потоках.
            // Всё, что дальше — работа с WPF UI (Dashboard, Overlay, TechOverlay).
            // Если не в UI-потоке — перенаправляем себя же через Dispatcher.Invoke.
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnMessageProcessed(chater, msg, commands));
                return;
            }

            try
            {
                string uiMessage = msg.Message;

                if (msg.Message.Length >= Settings.MinMessageLength)
                {
                    LastMessageText = $"[#{chater.KarmaKey}] {chater.EffectiveName}: {uiMessage}";
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

                    // ✅ УБРАЛИ Task.Run — мы уже в UI-потоке.
                    // Задержку делаем через DispatcherTimer, чтобы не уходить в фон.
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(200)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        _overlayManager.AddImportantMessage(chater, overlayMsg);
                    };
                    timer.Start();
                }
                else if (isStickerAction)
                {
                    Debug.WriteLine($"[Stickers] Стикер от {chater.Login}");

                    // ✅ То же самое для стикера
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(200)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        _overlayManager.AddStickerMessage(chater, overlayMsg);
                    };
                    timer.Start();
                }
                else
                {
                    _overlayManager.AddMessage(chater, overlayMsg);
                }

                InfoRotation?.NotifyUserActivity();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка OnMessageProcessed: {ex.Message}");
                Debug.WriteLine($"[MainViewModel] StackTrace: {ex.StackTrace}");
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

            if (InfoRotation != null)
            {
                InfoRotation.StartCommand.Execute(null);
                Debug.WriteLine($"[MainViewModel] InfoRotation запущен (тишина: {InfoRotation.SilenceInterval}с)");
            }

            try
            {
                await Alerts.StartAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка запуска AlertsService: {ex.Message}");
            }

            Debug.WriteLine($"[MainViewModel] ДО EnsureSession: CurrentSession={Session.CurrentSession?.Number}, LastStreamNumber={Session.LastStreamNumber}");

            int requestedNumber = Session.CurrentSession?.Number ?? 0;
            Session.EnsureSession(requestedNumber);

            Debug.WriteLine($"[MainViewModel] ПОСЛЕ EnsureSession: CurrentSession={Session.CurrentSession?.Number}, LastStreamNumber={Session.LastStreamNumber}");

            // ✅ Подключаем все чаты через координатор
            await ChatsManager.ConnectAllAsync();

            IsProcessRunning = true;
            Session.SetStartTime();

            Debug.WriteLine($"[MainViewModel] Стрим #{Session.CurrentSession?.Number} запущен");
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {
            Debug.WriteLine("[MainViewModel] Stop() вызван");

            if (InfoRotation != null)
            {
                InfoRotation.StopCommand.Execute(null);
                Debug.WriteLine("[MainViewModel] InfoRotation остановлен");
            }

            try
            {
                await Alerts.StopAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Ошибка остановки AlertsService: {ex.Message}");
            }

            // ✅ Отключаем все чаты через координатор
            await ChatsManager.StopAllAsync();

            _pollingcts?.Cancel();
            await _chatService.StopAsync();
            Session.SaveSessionEndTime();
            IsProcessRunning = false;

            Debug.WriteLine("[MainViewModel] Stop() завершён");
        }

        [RelayCommand]
        private void SaveSettings() => _settingsService.SaveSettings();

        public void NotifyUserActivity()
        {
            InfoRotation?.NotifyUserActivity();
            Alerts?.NotifyUserActivity();
            Stats?.NotifyUserActivity();
        }

        public void SetImportantPlaybackMode(ImportantPlaybackMode mode)
        {
            Alerts?.SetImportantPlaybackMode(mode);
        }

        public void UpdateImportantQueueCount(int count)
        {
            Alerts?.UpdateImportantQueueCount(count);
        }

        public void SaveAlertsPosition()
        {
            Alerts?.SavePosition();
        }

        public void SaveOverlayPosition() => Overlays?.SaveAllPositions();
        public void SaveShortsPosition() => Overlays?.SaveAllPositions();
        public void SaveImportantPosition() => Overlays?.SaveAllPositions();
        public void SaveStickersPosition() => Overlays?.SaveAllPositions();

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

        // В MainViewModel
        [RelayCommand]
        private void TestDonation()
        {
            Alerts?.SimulateDonation(
                userName: "Тестер",
                amount: 100,
                currency: "RUB",
                message: "Вася, лови #1 за стрим!");
        }
    }
}