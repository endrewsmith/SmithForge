using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace SmithForge
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Устанавливаем универсальную культуру (Invariant), где точка — разделитель
            var culture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // 2. Заставляем WPF элементы (Binding) использовать ту же логику точки
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            base.OnStartup(e);
        }
    }
}
