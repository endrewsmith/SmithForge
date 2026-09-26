using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Main.Models;
using System;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор сессий стримов.
    /// Управляет StreamSessionManager и предоставляет UI-свойства.
    /// </summary>
    public partial class SessionCoordinator : ObservableObject
    {
        private readonly StreamSessionManager _sessionManager;
        private readonly AppSettings _settings;

        // ============================================================
        // СВОЙСТВА ДЛЯ UI
        // ============================================================
        [ObservableProperty]
        private StreamSession? _currentSession;

        [ObservableProperty]
        private int _lastStreamNumber;

        // ============================================================
        // СОБЫТИЕ (для MainViewModel, чтобы установить sessionId в messageHandler)
        // ============================================================
        public event EventHandler<string>? SessionIdChanged;

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public SessionCoordinator(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _sessionManager = new StreamSessionManager();

            _currentSession = _sessionManager.CurrentSession;
            _lastStreamNumber = _sessionManager.LastStreamNumber;

            _sessionManager.SessionChanged += (s, session) =>
            {
                CurrentSession = session;
                LastStreamNumber = _sessionManager.LastStreamNumber;
                Debug.WriteLine($"[SessionCoordinator] SessionChanged: #{session.Number}");
            };
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void NextStream()
        {
            _sessionManager.NextStream(CurrentSession?.Title ?? "Без названия", (number, title) =>
            {
                LastStreamNumber = number;
                _settings.LastStreamNumber = number;
                ConfigService.Save(_settings);
            });
        }

        // ============================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ MainViewModel
        // ============================================================
        public void EnsureSession(int requestedNumber)
        {
            if (requestedNumber > 0)
            {
                _sessionManager.EnsureSessionByNumber(requestedNumber, n =>
                {
                    LastStreamNumber = n;
                    _settings.LastStreamNumber = n;
                    ConfigService.Save(_settings);
                    Debug.WriteLine($"[SessionCoordinator] Установлен номер стрима: {n}");
                });
            }

            var sessionId = _sessionManager.CurrentSession?.Id;
            if (!string.IsNullOrEmpty(sessionId))
            {
                SessionIdChanged?.Invoke(this, sessionId);
            }
        }

        public void SetStartTime()
        {
            _sessionManager.SetStartTime();
        }

        public void SaveSessionEndTime()
        {
            _sessionManager.SaveSessionEndTime();
        }
    }
}