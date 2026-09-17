using SmithForge.AlertsEngine.Core.Models;

namespace SmithForge.AlertsEngine.Core.Interfaces
{
    public interface IAlertProviderFactory
    {
        /// <summary>
        /// Создать провайдер с OAuth-параметрами (DonationAlerts)
        /// </summary>
        IAlertProvider? CreateProvider(
            AlertProviderType providerType,
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            long userId);

        /// <summary>
        /// Создать провайдер с простым токеном
        /// </summary>
        IAlertProvider? CreateProvider(AlertProviderType providerType, string token);
    }
}