using System;

namespace SmithForge.ChatEngine.Core.Models;

/// <summary>
/// Состояние подключения коннектора к чат-платформе
/// </summary>
public class ConnectorStatus
{
    private DateTime _connectedSince;
    private DateTime _lastMessageReceived;

    /// <summary>
    /// Подключен ли коннектор в данный момент
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Текст ошибки (если есть)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Количество попыток переподключения
    /// </summary>
    public int ReconnectAttempts { get; set; }

    /// <summary>
    /// Время последнего полученного сообщения (UTC)
    /// </summary>
    public DateTime LastMessageReceived
    {
        get => _lastMessageReceived;
        set
        {
            _lastMessageReceived = value;
            // Если сообщение получено, обновляем время активности
            if (value != DateTime.MinValue)
            {
                _lastActivityTime = value;
            }
        }
    }

    private DateTime _lastActivityTime = DateTime.UtcNow;

    /// <summary>
    /// Время последней активности (UTC)
    /// </summary>
    public DateTime LastActivityTime
    {
        get => _lastActivityTime;
        set => _lastActivityTime = value;
    }

    /// <summary>
    /// Время подключения (UTC)
    /// </summary>
    public DateTime ConnectedSince
    {
        get => _connectedSince;
        set => _connectedSince = value;
    }

    /// <summary>
    /// Время работы подключения
    /// </summary>
    public TimeSpan Uptime
    {
        get
        {
            if (!IsConnected || _connectedSince == DateTime.MinValue)
                return TimeSpan.Zero;
            return DateTime.UtcNow - _connectedSince;
        }
    }

    /// <summary>
    /// Время бездействия (с последнего сообщения)
    /// </summary>
    public TimeSpan IdleTime
    {
        get
        {
            if (_lastMessageReceived == DateTime.MinValue)
                return Uptime;
            return DateTime.UtcNow - _lastMessageReceived;
        }
    }

    /// <summary>
    /// Количество полученных сообщений
    /// </summary>
    public int MessagesReceived { get; set; }

    /// <summary>
    /// Количество отправленных сообщений
    /// </summary>
    public int MessagesSent { get; set; }

    /// <summary>
    /// Задержка соединения (в миллисекундах)
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Версия API/протокола
    /// </summary>
    public string? ProtocolVersion { get; set; }

    /// <summary>
    /// Дополнительные данные состояния
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    public ConnectorStatus()
    {
        IsConnected = false;
        ErrorMessage = null;
        ReconnectAttempts = 0;
        MessagesReceived = 0;
        MessagesSent = 0;
        LatencyMs = 0;
        _connectedSince = DateTime.MinValue;
        _lastMessageReceived = DateTime.MinValue;
        _lastActivityTime = DateTime.UtcNow;
        Metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// Сброс состояния
    /// </summary>
    public void Reset()
    {
        IsConnected = false;
        ErrorMessage = null;
        ReconnectAttempts = 0;
        MessagesReceived = 0;
        MessagesSent = 0;
        LatencyMs = 0;
        _connectedSince = DateTime.MinValue;
        _lastMessageReceived = DateTime.MinValue;
        _lastActivityTime = DateTime.UtcNow;
        Metadata?.Clear();
    }

    /// <summary>
    /// Отметить подключение
    /// </summary>
    public void MarkConnected()
    {
        IsConnected = true;
        ErrorMessage = null;
        _connectedSince = DateTime.UtcNow;
        _lastActivityTime = DateTime.UtcNow;
        ReconnectAttempts = 0;
    }

    /// <summary>
    /// Отметить отключение
    /// </summary>
    public void MarkDisconnected(string? errorMessage = null)
    {
        IsConnected = false;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Отметить получение сообщения
    /// </summary>
    public void MarkMessageReceived()
    {
        MessagesReceived++;
        LastMessageReceived = DateTime.UtcNow;
        _lastActivityTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Отметить отправку сообщения
    /// </summary>
    public void MarkMessageSent()
    {
        MessagesSent++;
        _lastActivityTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Обновить задержку
    /// </summary>
    public void UpdateLatency(int latencyMs)
    {
        LatencyMs = latencyMs;
        _lastActivityTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Увеличить счётчик переподключений
    /// </summary>
    public void IncrementReconnectAttempts()
    {
        ReconnectAttempts++;
    }

    /// <summary>
    /// Проверка, активен ли коннектор (подключён и не слишком долго бездействует)
    /// </summary>
    public bool IsActive(int idleTimeoutSeconds = 60)
    {
        if (!IsConnected)
            return false;

        // Если нет сообщений, считаем активным
        if (_lastMessageReceived == DateTime.MinValue)
            return true;

        return IdleTime.TotalSeconds < idleTimeoutSeconds;
    }

    /// <summary>
    /// Строковое представление статуса
    /// </summary>
    public override string ToString()
    {
        if (IsConnected)
        {
            return $"✅ Подключено (время: {Uptime:hh\\:mm\\:ss}, сообщений: {MessagesReceived})";
        }
        else if (!string.IsNullOrEmpty(ErrorMessage))
        {
            return $"❌ Ошибка: {ErrorMessage}";
        }
        else
        {
            return "⚪ Отключено";
        }
    }

    /// <summary>
    /// Создать копию состояния
    /// </summary>
    public ConnectorStatus Clone()
    {
        return new ConnectorStatus
        {
            IsConnected = IsConnected,
            ErrorMessage = ErrorMessage,
            ReconnectAttempts = ReconnectAttempts,
            MessagesReceived = MessagesReceived,
            MessagesSent = MessagesSent,
            LatencyMs = LatencyMs,
            _connectedSince = _connectedSince,
            _lastMessageReceived = _lastMessageReceived,
            _lastActivityTime = _lastActivityTime,
            ProtocolVersion = ProtocolVersion,
            Metadata = Metadata != null ? new Dictionary<string, object>(Metadata) : null
        };
    }
}