using System;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.ChatEngine.Core.Models;

namespace SmithForge.ChatEngine.Core.Interfaces;

/// <summary>
/// Интерфейс для подключения к чат-платформам (Twitch, YouTube, GoodGame, DonationAlerts и др.)
/// </summary>
public interface IChatConnector : IDisposable
{
    /// <summary>
    /// Уникальный идентификатор коннектора
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Тип платформы (Twitch, YouTube, GoodGame, DonationAlerts)
    /// </summary>
    ChannelType Platform { get; }

    /// <summary>
    /// Текущее состояние подключения
    /// </summary>
    ConnectorStatus Status { get; }

    /// <summary>
    /// Событие при получении нового сообщения из чата
    /// </summary>
    event EventHandler<IncomingChatMessage>? MessageReceived;

    /// <summary>
    /// Событие при изменении статуса подключения
    /// </summary>
    event EventHandler<ConnectorStatus>? StatusChanged;

    /// <summary>
    /// Подключение к чату
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отключение от чата
    /// </summary>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправка сообщения в чат
    /// </summary>
    /// <param name="message">Текст сообщения</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверка валидности настроек коннектора
    /// </summary>
    /// <returns>True если настройки валидны, иначе False</returns>
    Task<bool> ValidateSettingsAsync();

    /// <summary>
    /// Получить ID видео, к которому подключён коннектор
    /// </summary>
    string? GetVideoId();
}