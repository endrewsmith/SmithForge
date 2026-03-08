using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.Main.Services.SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {
        private readonly MessageProcessor _processor;
        private readonly OverlayService _overlayService;
        private readonly ExternalChatService _chatService = new();
        private CancellationTokenSource? _pollingcts;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isProcessRunning;

        [ObservableProperty] private bool _isOverlaySetupMode = true;
        [ObservableProperty] private string _lastMessageText = "Ожидание сообщений...";
        [ObservableProperty] private AppSettings _settings;
        [ObservableProperty] private StreamSession _currentSession;
        [ObservableProperty] private string _programPath;
        [ObservableProperty] private int _lastStreamNumber;

        public ObservableCollection<Chater> Users { get; } = new();

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();
            DatabaseService.Initialize();

            _overlayService = new OverlayService();
            _overlayService.Initialize(Settings.OverlayTop, Settings.OverlayLeft);
            _overlayService.SetMode(IsOverlaySetupMode);

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

        private void OnMessageProcessed(Chater chater, CommonMessage msg, List<ChatCommand> commands)
        {
            string uiMessage = msg.Message;
            foreach (var cmd in commands) uiMessage = uiMessage.Replace(cmd.Raw, "");
            uiMessage = uiMessage.Trim();

            if (msg.Message.Length >= Settings.MinMessageLength)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    LastMessageText = $"[#{chater.KarmaKey}] {chater.EffectiveName}: {uiMessage}";
                });
            }

            // ОТЛАДКА
            Debug.WriteLine($"[MainViewModel] Получено сообщение от {chater.Login}:");
            Debug.WriteLine($"   - Оригинальный номер из MessageProcessor: {msg.MessageNumber}");
            Debug.WriteLine($"   - Текст: {uiMessage}");

            var overlayMsg = new CommonMessage
            {
                User = chater,
                Login = chater.Login,
                Type = msg.Type.ToLower(),
                Message = uiMessage,
                KarmaKeyDisplay = $"#{chater.KarmaKey}",
                MessageNumber = msg.MessageNumber
            };

            if (!string.IsNullOrWhiteSpace(uiMessage))
            {
                _overlayService.AddMessage(chater, overlayMsg);
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

        partial void OnIsOverlaySetupModeChanged(bool oldValue, bool newValue)
        {
            _overlayService.SetMode(newValue);
            if (!newValue) _overlayService.SavePosition(Settings);
        }

        public void SaveOverlayPosition() => _overlayService.SavePosition(Settings);
        private void OnProcessExited() => Application.Current.Dispatcher.Invoke(() => { IsProcessRunning = false; });
        private bool CanStart() => !IsProcessRunning;
        private bool CanStop() => IsProcessRunning;

        [RelayCommand]
        private void Launch()
        {
            if (System.IO.File.Exists(ProgramPath))
                Process.Start(new ProcessStartInfo(ProgramPath) { UseShellExecute = true });
        }
    }
}