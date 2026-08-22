using System;
using System.Diagnostics;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services;

/// <summary>
/// Сервис управления сессиями стримов
/// </summary>
public class StreamSessionManager
{
    private StreamSession? _currentSession;
    private int _lastStreamNumber = 0;

    public StreamSession? CurrentSession => _currentSession;
    public int LastStreamNumber => _lastStreamNumber;
    public event EventHandler<StreamSession>? SessionChanged;

    public StreamSessionManager()
    {
        // Загружаем активную сессию или создаём новую
        var activeSession = DatabaseService.GetActiveSession();
        if (activeSession != null)
        {
            _currentSession = activeSession;
        }
        else
        {
            _lastStreamNumber = DatabaseService.GetMaxStreamNumber();
            CreateNewSession();
        }
    }

    public void SetSessionId(string sessionId)
    {
        // Сессия устанавливается через CurrentSession
    }

    private void CreateNewSession()
    {
        _lastStreamNumber++;
        _currentSession = new StreamSession
        {
            Id = Guid.NewGuid().ToString(),
            Number = _lastStreamNumber,
            Title = "Новый эфир...",
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EndTime = 0
        };

        DatabaseService.SaveSession(_currentSession);
        SessionChanged?.Invoke(this, _currentSession);
    }

    public void EnsureSessionByNumber(int number, Action<int> onNumberChanged)
    {
        if (number <= 0) return;

        var existingSession = DatabaseService.GetSessionByNumber(number);

        if (existingSession != null)
        {
            _currentSession = existingSession;
            _currentSession.EndTime = 0;
            Debug.WriteLine($"[Stream] Продолжаем стрим #{number}");
        }
        else
        {
            _currentSession = new StreamSession
            {
                Id = Guid.NewGuid().ToString(),
                Number = number,
                Title = $"Стрим #{number}",
                StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                EndTime = 0
            };
            DatabaseService.SaveSession(_currentSession);
            Debug.WriteLine($"[Stream] Создан новый стрим #{number}");
        }

        _lastStreamNumber = number;
        onNumberChanged(number);
        SessionChanged?.Invoke(this, _currentSession);
    }

    public void NextStream(string currentTitle, Action<int, string> onSettingsChanged)
    {
        Debug.WriteLine("[NextStream] ========== НАЧАЛО ==========");

        if (_currentSession != null && _currentSession.EndTime == 0)
        {
            _currentSession.EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DatabaseService.SaveSession(_currentSession);
            Debug.WriteLine($"[Stream] Завершен стрим #{_currentSession.Number}");
        }

        int nextNumber = (_currentSession?.Number ?? _lastStreamNumber) + 1;
        Debug.WriteLine($"[NextStream] Следующий номер: {nextNumber}");

        var newSession = new StreamSession
        {
            Id = Guid.NewGuid().ToString(),
            Number = nextNumber,
            Title = currentTitle,
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            EndTime = 0
        };

        DatabaseService.SaveSession(newSession);
        _currentSession = newSession;
        _lastStreamNumber = nextNumber;
        onSettingsChanged(nextNumber, currentTitle);

        Debug.WriteLine($"[NextStream] Новый стрим #{_currentSession.Number} создан, название: {currentTitle}");
        Debug.WriteLine($"[NextStream] LastStreamNumber теперь: {_lastStreamNumber}");
        Debug.WriteLine($"[NextStream] ========== КОНЕЦ ==========");

        SessionChanged?.Invoke(this, _currentSession);
    }

    public void SaveSessionEndTime()
    {
        if (_currentSession != null)
        {
            _currentSession.EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DatabaseService.SaveSession(_currentSession);
        }
    }

    public void SetStartTime()
    {
        if (_currentSession != null && _currentSession.StartTime == 0)
        {
            _currentSession.StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            DatabaseService.SaveSession(_currentSession);
        }
    }
}