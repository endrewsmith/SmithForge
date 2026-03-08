using System.Windows;
using SmithForge.Main.Models;

namespace SmithForge.Main.Services
{
    public static class WindowStateService
    {
        // Метод "Привязать" — вызывается один раз при создании окна
        public static void Bind(Window window, AppSettings settings)
        {
            // 1. Сразу выставляем позиции из настроек
            window.Top = settings.WindowTop;
            window.Left = settings.WindowLeft;
            window.Width = settings.WindowWidth;
            window.Height = settings.WindowHeight;

            // 2. Подписываемся на закрытие окна ОДИН раз для всех
            window.Closing += (s, e) =>
            {
                settings.WindowTop = window.Top;
                settings.WindowLeft = window.Left;
                settings.WindowWidth = window.Width;
                settings.WindowHeight = window.Height;

                // Сохраняем общий конфиг
                ConfigService.Save(settings);
            };
        }
    }
}
