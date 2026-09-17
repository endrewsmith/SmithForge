using SmithForge.AlertsEngine.Core.Interfaces;
using SmithForge.AlertsEngine.Core.Models;
using SmithForge.AlertsEngine.Providers.DonationAlerts;
using SmithForge.AlertsEngine.Providers.DonatePay;

namespace SmithForge.AlertsEngine
{
    /// <summary>
    /// Фабрика для создания провайдеров алертов
    /// </summary>
    public class AlertProviderFactory : IAlertProviderFactory
    {
        /// <summary>
        /// Создать провайдер DonationAlerts (OAuth)
        /// </summary>
        public IAlertProvider? CreateProvider(
            AlertProviderType providerType,
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            long userId)
        {
            return providerType switch
            {
                AlertProviderType.DonationAlerts => new DonationAlertsProvider(
                    clientId, clientSecret, accessToken, refreshToken, userId),

                _ => null
            };
        }

        /// <summary>
        /// Создать провайдер с простым токеном/ключом (для DonatePay и др.)
        /// </summary>
        public IAlertProvider? CreateProvider(AlertProviderType providerType, string token)
        {
            return providerType switch
            {
                AlertProviderType.DonationPay => new DonatePayProvider(token),

                _ => null
            };
        }
    }
}