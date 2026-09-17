using SmithForge.AlertsEngine.Core.Models;
using System.Windows;
using System.Windows.Controls;

namespace SmithForge.Features.AlertsOverlay
{
    /// <summary>
    /// Выбирает DataTemplate в зависимости от типа провайдера алерта.
    /// </summary>
    public class AlertTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Шаблон для алертов от DonationAlerts
        /// </summary>
        public DataTemplate? DonationAlertsTemplate { get; set; }

        /// <summary>
        /// Шаблон для алертов от DonationPay
        /// </summary>
        public DataTemplate? DonationPayTemplate { get; set; }

        /// <summary>
        /// Шаблон по умолчанию (если провайдер неизвестен)
        /// </summary>
        public DataTemplate? DefaultTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is AlertDisplayModel alert)
            {
                return alert.ProviderType switch
                {
                    AlertProviderType.DonationAlerts => DonationAlertsTemplate ?? DefaultTemplate,
                    AlertProviderType.DonationPay => DonationPayTemplate ?? DefaultTemplate,
                    _ => DefaultTemplate
                };
            }

            return base.SelectTemplate(item, container);
        }
    }
}