using System;
using System.Threading;
using System.Threading.Tasks;
using SmithForge.AlertsEngine.Core.Models;

namespace SmithForge.AlertsEngine.Core.Interfaces
{
    /// <summary>
    /// Интерфейс провайдера алертов
    /// </summary>
    public interface IAlertProvider : IDisposable
    {
        /// <summary>
        /// Уникальный ID провайдера (генерируется при создании)
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Тип провайдера (DonationAlerts, ...)
        /// </summary>
        AlertProviderType ProviderType { get; }

        /// <summary>
        /// Текущий статус подключения
        /// </summary>
        AlertStatus Status { get; }

        /// <summary>
        /// Событие: получен новый алерт
        /// </summary>
        event EventHandler<IncomingAlert>? AlertReceived;

        /// <summary>
        /// Событие: изменился статус подключения
        /// </summary>
        event EventHandler<AlertStatus>? StatusChanged;

        /// <summary>
        /// Подключиться к провайдеру
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Отключиться от провайдера
        /// </summary>
        Task DisconnectAsync(CancellationToken cancellationToken = default);
    }
}