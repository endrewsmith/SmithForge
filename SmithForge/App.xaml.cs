using SmithForge.Main.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

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

            // 3. Глобальный обработчик непойманных исключений
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 4. Обработчик исключений в UI потоке
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            base.OnStartup(e);

            // Инициализируем сервис эмодзи
            //YouTubeEmojiService.Initialize();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"[FATAL] Необработанное исключение: {ex?.Message}");
            System.Diagnostics.Debug.WriteLine($"[FATAL] StackTrace: {ex?.StackTrace}");

            // Здесь можно добавить логирование в файл
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[UI] Исключение в диспетчере: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"[UI] StackTrace: {e.Exception.StackTrace}");

            // Помечаем как обработанное - программа не упадет
            e.Handled = true;

            // Можно показать сообщение пользователю в дебаг режиме
#if DEBUG
            MessageBox.Show($"Ошибка: {e.Exception.Message}\n\nПрограмма продолжит работу.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
#endif
        }
    }
}