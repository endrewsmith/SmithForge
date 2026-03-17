using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.ChatOverlayShorts;
using SmithForge.Main.Models;
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

namespace SmithForge.ViewModels
{
    internal partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isOverlayHidden;
        private readonly ImportantOverlayService _importantService = new();
        private readonly DashboardService _dashboardService = new();
        private readonly MessageProcessor _processor;
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

        private readonly ChatOverlayService _overlayService = new();
        private readonly ChatOverlayShortsService _shortsService = new();

        public MainViewModel()
        {
            FolderManager.EnsureDirectoriesExist();
            Settings = ConfigService.Load();
            _isOverlaySetupMode = Settings.IsOverlaySetupMode;
            _isOverlayHidden = Settings.IsOverlayHidden; // ← Загружаем состояние скрытия
            DatabaseService.Initialize();

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

            // Основной оверлей
            _overlayService.Initialize(
                Settings.OverlayTop,
                Settings.OverlayLeft,
                _isOverlaySetupMode
            );
            _overlayService.LoadPosition(Settings);

            // Применяем состояние скрытия после загрузки позиции
            if (_isOverlayHidden)
            {
                _overlayService.SetHidden(true);
            }

            // Оверлей для шортов
            _shortsService.Initialize(
                Settings.ShortsWindowTop,
                Settings.ShortsWindowLeft,
                _isOverlaySetupMode
            );
            _shortsService.LoadPosition(Settings);

            // Применяем состояние скрытия для шортов
            if (_isOverlayHidden)
            {
                _shortsService.SetHidden(true);
            }

            // В конструкторе после других сервисов:
            _importantService.Initialize(
                Settings.ImportantOverlayTop,
                Settings.ImportantOverlayLeft,
                _isOverlaySetupMode
            );
            _importantService.LoadPosition(Settings);

            if (_isOverlayHidden)
            {
                _importantService.SetHidden(true);
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

            // 1. Обновляем статусную строку в главном окне
            if (uiMessage.Length >= Settings.MinMessageLength)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    LastMessageText = $"[#{chater.KarmaKey}] {chater.EffectiveName}: {uiMessage}";
                });
            }

            if (string.IsNullOrWhiteSpace(uiMessage)) return;

            // 2. Проверяем, была ли выполнена РЕАЛЬНАЯ команда "важно" (через список команд)
            bool isImportantAction = commands != null && commands.Any(c =>
                c.Name.Equals("important", StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals("важно", StringComparison.OrdinalIgnoreCase));

            // 3. Чистим текст от технических тегов <important>, чтобы они не мозолили глаза и не читались Саней
            string cleanUiMessage = uiMessage.Replace("<important>", "").Replace("</important>", "").Trim();

            if (isImportantAction)
            {
                // Создаем клон с ЧИСТЫМ текстом для важного окна
                var importantMsg = CreateOverlayMsg(chater, msg, cleanUiMessage);

                Debug.WriteLine($"[Important] Легальный вызов от {chater.Login}. Отправляем в очередь.");

                // Пауза 200мс для стабильности отрисовки
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    _importantService.ShowImportantMessage(chater, importantMsg);
                });
            }
            else
            {
                // ОБЫЧНЫЕ ОКНА: шлем чистый текст (без тегов <important>, если юзер их ввел сам)
                // Но визуальные теги типа <b> сохранятся, если они там были
                _overlayService.AddMessage(chater, CreateOverlayMsg(chater, msg, cleanUiMessage));
                _shortsService.AddMessage(chater, CreateOverlayMsg(chater, msg, cleanUiMessage));
                _dashboardService.AddMessage(chater, CreateOverlayMsg(chater, msg, cleanUiMessage));
            }
        }

        // Вспомогательный метод для клонирования сообщений
        private CommonMessage CreateOverlayMsg(Chater chater, CommonMessage original, string text)
        {
            return new CommonMessage
            {
                User = chater,
                Login = chater.Login,
                Type = original.Type.ToLower(),
                Message = text,
                KarmaKeyDisplay = $"#{chater.KarmaKey}",
                MessageNumber = original.MessageNumber,
                IsProcessedByCommand = original.IsProcessedByCommand,
                DisplayTimeMs = original.DisplayTimeMs
            };
        }

        [RelayCommand]
        private void ToggleShortsOverlay()
        {
            _shortsService.Toggle();
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
            _shortsService.SetMode(newValue);
            _importantService.SetMode(newValue);

            if (!newValue)
            {
                _overlayService.SavePosition(Settings);
                _shortsService.SavePosition(Settings);
                _importantService.SavePosition(Settings);
            }
        }

        partial void OnIsOverlayHiddenChanged(bool oldValue, bool newValue)
        {
            _overlayService.SetHidden(newValue);
            _shortsService.SetHidden(newValue);
            ConfigService.Save(Settings); // сразу сохраняем настройки
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

        [RelayCommand]
        private void ToggleDashboard()
        {
            if (_dashboardService.IsVisible)
                _dashboardService.Hide();
            else
                _dashboardService.Show();
        }

        public void SaveShortsPosition()
        {
            _shortsService.SavePosition(Settings);
        }

        [RelayCommand]
        private void ToggleImportantOverlay()
        {
            if (_importantService.IsVisible)
                _importantService.Hide();
            else
                _importantService.Show();
        }
    }


}