using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.ChatOverlay;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Features.ImportantOverlay;
using SmithForge.Features.StickersOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Models.ChatModes;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using SmithForge.Main.Services.SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SmithForge.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {


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
        private readonly MessageProcessor _processor;
        private readonly ChatOverlayService _overlayService;
        private readonly ChatOverlayShortsService _shortsService;
        private readonly ImportantOverlayService _importantService;
        private readonly StickersOverlayService _stickersService;
        private readonly ExternalChatService _chatService = new();
        private CancellationTokenSource? _pollingcts;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isProcessRunning;

        [ObservableProperty]
        private bool _isOverlaySetupMode = true;

        [ObservableProperty]
        private bool _isOverlayHidden = false;

        [ObservableProperty]
        private string _lastMessageText = "Ожидание сообщений...";

        [ObservableProperty]
        private AppSettings _settings;

        [ObservableProperty]
        private StreamSession _currentSession;

        [ObservableProperty]
        private string _programPath;

        [ObservableProperty]
        private int _lastStreamNumber;

        [ObservableProperty]
        private bool _isStickersVisible = true;

        [ObservableProperty]
        private bool _isAutoSwitchingEnabled = true;

        public ObservableCollection<Chater> Users { get; } = new();

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();
            _isOverlaySetupMode = Settings.IsOverlaySetupMode;
            _isOverlayHidden = Settings.IsOverlayHidden;
            _isStickersVisible = Settings.IsStickersVisible;
            DatabaseService.Initialize();

            _mainChatMode = Settings.MainChatMode;
            _shortsChatMode = Settings.ShortsChatMode;
            _importantChatMode = Settings.ImportantChatMode;
            _stickersChatMode = Settings.StickersChatMode;

            StickerManager.LoadPacks();

            _overlayService = new ChatOverlayService();
            _overlayService.Initialize(Settings.OverlayTop, Settings.OverlayLeft);
            _overlayService.SetSetupMode(IsOverlaySetupMode);
            _overlayService.SetDisplayMode(MainChatMode);
            _overlayService.LoadPosition(Settings);

            _shortsService = new ChatOverlayShortsService();
            _shortsService.Initialize(
                Settings.ShortsWindowTop,
                Settings.ShortsWindowLeft,
                Settings.ShortsWindowWidth,
                Settings.ShortsWindowHeight,
                IsOverlaySetupMode);
            _shortsService.SetSetupMode(IsOverlaySetupMode);
            _shortsService.SetDisplayMode(ShortsChatMode);
            _shortsService.LoadPosition(Settings);

            _importantService = new ImportantOverlayService(Settings);
            _importantService.IsAutoSwitchingEnabled = IsAutoSwitchingEnabled;
            _importantService.Initialize(
                Settings.ImportantOverlayTop,
                Settings.ImportantOverlayLeft,
                Settings.ImportantOverlayWidth,
                Settings.ImportantOverlayHeight,
                IsOverlaySetupMode);
            _importantService.SetSetupMode(IsOverlaySetupMode);
            _importantService.SetDisplayMode(ImportantChatMode);
            _importantService.LoadPosition(Settings);
            _importantService.QueueCountChanged += (s, count) =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ImportantQueueCount = count;
                    Debug.WriteLine($"[MainViewModel] Получено событие QueueCountChanged: count={count}");
                });
            };

            _stickersService = new StickersOverlayService();
            _stickersService.Initialize(
                Settings.StickersWindowTop,
                Settings.StickersWindowLeft,
                Settings.StickersWindowWidth,
                Settings.StickersWindowHeight,
                IsOverlaySetupMode);
            _stickersService.SetSetupMode(IsOverlaySetupMode);
            _stickersService.SetDisplayMode(StickersChatMode);
            _stickersService.LoadPosition(Settings);

            if (_isOverlayHidden)
            {
                _overlayService.SetHidden(true);
                _shortsService.SetHidden(true);
                _importantService.SetHidden(true);
                _stickersService.SetHidden(true);
            }

            if (_isStickersVisible)
            {
                _stickersService.Show();
            }

            _processor = new MessageProcessor(Settings);
            _processor.OnProcessed += OnMessageProcessed;

            ProgramPath = Settings.ProgramPath;
            LastStreamNumber = DatabaseService.GetMaxStreamNumber();
            var activeSession = DatabaseService.GetActiveSession();

            if (activeSession != null)
            {
                CurrentSession = activeSession;
            }
            else
            {
                int nextNumber = LastStreamNumber + 1;
                CurrentSession = new StreamSession
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = nextNumber,
                    Title = "Новый эфир...",
                    StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    EndTime = 0
                };
                DatabaseService.SaveSession(CurrentSession);
                LastStreamNumber = nextNumber;
            }

            _processor.SetSession(CurrentSession.Id);
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
        }

        partial void OnImportantPlaybackModeChanged(ImportantPlaybackMode value)
        {
            Settings.ImportantPlaybackMode = value;
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Режим: {(value == ImportantPlaybackMode.Auto ? "АВТО" : "РУЧНОЙ")}");
        }

        partial void OnImportantPlaybackHotkeyChanged(string value)
        {
            Settings.ImportantPlaybackHotkey = value;
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Горячая клавиша: {value}");
        }

        partial void OnIsAutoSwitchingEnabledChanged(bool value)
        {
            Debug.WriteLine($"[ReadingMode] Режим чтения: {(value ? "ВКЛ" : "ВЫКЛ")}");

            // Обновляем свойство в ImportantOverlayService
            _importantService.IsAutoSwitchingEnabled = value;

            if (value)
            {
                if (ImportantQueueCount > 0 && ImportantPlaybackMode == ImportantPlaybackMode.Auto)
                {
                    ImportantPlaybackMode = ImportantPlaybackMode.Manual;
                    Debug.WriteLine("[ReadingMode] Есть сообщения в очереди, переключено в РУЧНОЙ режим");
                }
                else if (ImportantQueueCount == 0 && ImportantPlaybackMode == ImportantPlaybackMode.Manual)
                {
                    ImportantPlaybackMode = ImportantPlaybackMode.Auto;
                    Debug.WriteLine("[ReadingMode] Очередь пуста, переключено в АВТО режим");
                }
            }
        }
        public void UpdateImportantQueueCount(int count)
        {
            try
            {
                if (!Application.Current.Dispatcher.CheckAccess())
                {
                    Debug.WriteLine($"[MainViewModel] UpdateImportantQueueCount: перенаправление в UI поток");
                    Application.Current.Dispatcher.BeginInvoke(() => UpdateImportantQueueCount(count));
                    return;
                }

                ImportantQueueCount = count;

                Debug.WriteLine($"[MainViewModel] ==============================================");
                Debug.WriteLine($"[MainViewModel] UpdateImportantQueueCount ВЫЗВАН!");
                Debug.WriteLine($"[MainViewModel] count = {count}");
                Debug.WriteLine($"[MainViewModel] IsAutoSwitchingEnabled = {IsAutoSwitchingEnabled}");
                Debug.WriteLine($"[MainViewModel] ImportantPlaybackMode (до) = {ImportantPlaybackMode}");
                Debug.WriteLine($"[MainViewModel] ImportantPlaybackMode == Auto = {ImportantPlaybackMode == ImportantPlaybackMode.Auto}");
                Debug.WriteLine($"[MainViewModel] ImportantPlaybackMode == Manual = {ImportantPlaybackMode == ImportantPlaybackMode.Manual}");
                Debug.WriteLine($"[MainViewModel] _importantService?.IsPlaying = {_importantService?.IsPlaying}");

                if (IsAutoSwitchingEnabled)
                {
                    Debug.WriteLine("[MainViewModel] Режим чтения ВКЛ, проверяем условия");

                    // Если в очереди есть сообщения И режим авто -> переключаем в ручной
                    if (count > 0 && ImportantPlaybackMode == ImportantPlaybackMode.Auto)
                    {
                        Debug.WriteLine($"[ReadingMode] ✅ УСЛОВИЕ 1: count={count} > 0 и режим Auto");
                        Debug.WriteLine($"[ReadingMode] Переключаем АВТО -> РУЧНОЙ");
                        ImportantPlaybackMode = ImportantPlaybackMode.Manual;
                        Debug.WriteLine($"[ReadingMode] Новый режим: {ImportantPlaybackMode}");
                    }
                    // Если очередь пуста И режим ручной -> переключаем в авто
                    else if (count == 0 && ImportantPlaybackMode == ImportantPlaybackMode.Manual)
                    {
                        Debug.WriteLine($"[ReadingMode] ✅ УСЛОВИЕ 2: count={count} == 0 и режим Manual");
                        Debug.WriteLine($"[ReadingMode] Переключаем РУЧНОЙ -> АВТО");
                        ImportantPlaybackMode = ImportantPlaybackMode.Auto;
                        Debug.WriteLine($"[ReadingMode] Новый режим: {ImportantPlaybackMode}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ReadingMode] ❌ Условия НЕ выполнены:");
                        Debug.WriteLine($"[ReadingMode]   count>0 = {count > 0}");
                        Debug.WriteLine($"[ReadingMode]   count==0 = {count == 0}");
                        Debug.WriteLine($"[ReadingMode]   isAuto = {ImportantPlaybackMode == ImportantPlaybackMode.Auto}");
                        Debug.WriteLine($"[ReadingMode]   isManual = {ImportantPlaybackMode == ImportantPlaybackMode.Manual}");
                    }
                }
                else
                {
                    Debug.WriteLine("[MainViewModel] Режим чтения ВЫКЛ, переключение не выполняется");
                }

                Debug.WriteLine($"[MainViewModel] ImportantPlaybackMode (после) = {ImportantPlaybackMode}");
                Debug.WriteLine($"[MainViewModel] ==============================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] UpdateImportantQueueCount Error: {ex.Message}");
                Debug.WriteLine($"[MainViewModel] StackTrace: {ex.StackTrace}");
            }
        }
        [RelayCommand]
        private async Task PlayNextImportant()
        {
            if (ImportantPlaybackMode == ImportantPlaybackMode.Manual)
            {
                await _importantService.PlayNextFromQueueAsync();
                ImportantQueueCount = _importantService.QueueSize;
            }
        }

        partial void OnStickerDisplayTimeChanged(int value)
        {
            Settings.StickerDisplayTimeMs = value;
            _stickersService.SetDisplayTime(value);
            ConfigService.Save(Settings);
        }

        partial void OnImportantSoundVolumeChanged(int value)
        {
            Settings.ImportantSoundVolume = value;
            ConfigService.Save(Settings);
            VoiceService.SetImportantSoundVolume(value);
            Debug.WriteLine($"[MainVM] Громкость звука важных сообщений: {value}%");
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
        partial void OnVoiceVolumeChanged(int value)
        {
            Settings.VoiceVolume = value;
            ConfigService.Save(Settings);
            VoiceService.SetVoiceVolume(value);
            Debug.WriteLine($"[MainVM] Громкость голоса: {value}%");
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

            _dashboardService.AddMessage(chater, overlayMsg);

            if (isImportantAction)
            {
                Debug.WriteLine($"[Important] Сообщение от {chater.Login}");
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    _importantService.ShowImportantMessage(chater, overlayMsg);
                });
            }
            else if (isStickerAction)
            {
                Debug.WriteLine($"[Stickers] Стикер от {chater.Login}");
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    _stickersService.ShowSticker(chater, overlayMsg);
                });
            }
            else
            {
                _overlayService.AddMessage(chater, overlayMsg);
                _shortsService.AddMessage(chater, overlayMsg);
            }
        }

        private void EnsureSessionByNumber(int number)
        {
            if (number <= 0) return;

            var existingSession = DatabaseService.GetSessionByNumber(number);

            if (existingSession != null)
            {
                CurrentSession = existingSession;
                CurrentSession.EndTime = 0;
                Debug.WriteLine($"[Stream] Продолжаем стрим #{number}");
            }
            else
            {
                CurrentSession = new StreamSession
                {
                    Id = Guid.NewGuid().ToString(),
                    Number = number,
                    Title = $"Стрим #{number}",
                    StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    EndTime = 0
                };
                DatabaseService.SaveSession(CurrentSession);
                Debug.WriteLine($"[Stream] Создан новый стрим #{number}");
            }

            LastStreamNumber = number;
            Settings.LastStreamNumber = number;
            ConfigService.Save(Settings);
            _processor.SetSession(CurrentSession.Id);
        }

        private void StartPolling()
        {
            _pollingcts?.Cancel();
            _pollingcts?.Dispose();
            _pollingcts = new CancellationTokenSource();

            _ = MessageService.StartListeningAsync(
                $"ws://127.0.0.1:{Settings.NetworkPort}/chat/ws/stream",
                msg => _processor.Process(msg),
                _pollingcts.Token,
                () => IsProcessRunning);
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private void Start()
        {
            int requestedNumber = CurrentSession?.Number ?? 0;

            if (requestedNumber > 0)
            {
                EnsureSessionByNumber(requestedNumber);
            }
            else if (CurrentSession == null)
            {
                int nextNumber = LastStreamNumber + 1;
                EnsureSessionByNumber(nextNumber);
            }

            if (_chatService.TryAttachExisting() || SafeStart())
            {
                IsProcessRunning = true;

                if (CurrentSession.StartTime == 0)
                {
                    CurrentSession.StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                DatabaseService.SaveSession(CurrentSession);
                StartPolling();
            }
        }

        private bool SafeStart()
        {
            try { _chatService.Start(); return true; }
            catch (Exception ex) { MessageBox.Show(ex.Message); return false; }
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private async Task Stop()
        {
            _pollingcts?.Cancel();
            await _chatService.StopAsync();

            if (CurrentSession != null)
            {
                CurrentSession.EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DatabaseService.SaveSession(CurrentSession);
            }

            IsProcessRunning = false;
        }

        [RelayCommand]
        private void NextStream()
        {
            string currentTitle = CurrentSession?.Title ?? "Без названия";

            if (CurrentSession != null && CurrentSession.EndTime == 0)
            {
                CurrentSession.EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DatabaseService.SaveSession(CurrentSession);
                Debug.WriteLine($"[Stream] Завершен стрим #{CurrentSession.Number}");
            }

            int nextNumber = CurrentSession?.Number ?? LastStreamNumber + 1;
            EnsureSessionByNumber(nextNumber);
            CurrentSession.Title = currentTitle;
            DatabaseService.SaveSession(CurrentSession);
        }

        [RelayCommand]
        private void SaveSettings()
        {
            ConfigService.Save(Settings);
            LastMessageText = "✅ Настройки сохранены";
        }

        partial void OnMainChatModeChanged(ChatDisplayMode value)
        {
            Settings.MainChatMode = value;
            _overlayService.SetDisplayMode(value);
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Главный чат режим: {value}");
        }

        partial void OnShortsChatModeChanged(ChatDisplayMode value)
        {
            Settings.ShortsChatMode = value;
            _shortsService.SetDisplayMode(value);
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Шорты режим: {value}");
        }

        partial void OnImportantChatModeChanged(ChatDisplayMode value)
        {
            Settings.ImportantChatMode = value;
            _importantService.SetDisplayMode(value);
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Важные сообщения режим: {value}");
        }

        partial void OnStickersChatModeChanged(ChatDisplayMode value)
        {
            Settings.StickersChatMode = value;
            _stickersService.SetDisplayMode(value);
            ConfigService.Save(Settings);
            Debug.WriteLine($"[MainVM] Стикеры режим: {value}");
        }

        partial void OnIsOverlaySetupModeChanged(bool oldValue, bool newValue)
        {
            Debug.WriteLine($"[MainVM] Режим настройки: {oldValue} -> {newValue}");

            _overlayService.SetSetupMode(newValue);
            _shortsService.SetSetupMode(newValue);
            _importantService.SetSetupMode(newValue);
            _stickersService.SetSetupMode(newValue);

            if (!newValue)
            {
                _overlayService.SavePosition(Settings);
                _shortsService.SavePosition(Settings);
                _importantService.SavePosition(Settings);
                _stickersService.SavePosition(Settings);
                ConfigService.Save(Settings);
                LastMessageText = "✅ Позиции окон сохранены";
            }
        }

        partial void OnIsOverlayHiddenChanged(bool oldValue, bool newValue)
        {
            _overlayService.SetHidden(newValue);
            _shortsService.SetHidden(newValue);
            _importantService.SetHidden(newValue);
            _stickersService.SetHidden(newValue);
            Settings.IsOverlayHidden = newValue;
            ConfigService.Save(Settings);
        }

        partial void OnIsStickersVisibleChanged(bool oldValue, bool newValue)
        {
            if (newValue)
                _stickersService.Show();
            else
                _stickersService.Hide();

            Settings.IsStickersVisible = newValue;
            ConfigService.Save(Settings);
        }

        public void SaveOverlayPosition() => _overlayService.SavePosition(Settings);
        public void SaveShortsPosition() => _shortsService.SavePosition(Settings);
        public void SaveImportantPosition() => _importantService.SavePosition(Settings);
        public void SaveStickersPosition() => _stickersService.SavePosition(Settings);

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
        private void ToggleDashboard()
        {
            if (_dashboardService.IsVisible)
                _dashboardService.Hide();
            else
                _dashboardService.Show();
        }

        [RelayCommand]
        private void ToggleShortsOverlay()
        {
            _shortsService.Toggle();
        }

        [RelayCommand]
        private void ToggleImportantOverlay()
        {
            if (_importantService.IsVisible)
                _importantService.Hide();
            else
                _importantService.Show();
        }

        [RelayCommand]
        private void ToggleStickersOverlay()
        {
            IsStickersVisible = !IsStickersVisible;
        }
    }
}