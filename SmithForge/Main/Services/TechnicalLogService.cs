using System;
using System.IO;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Сервис для логирования СЛУЖЕБНЫХ сообщений (!!nick, !!ava, !!like, !!dislike, !!hide).
    /// Эти сообщения НЕ попадают в основной чат, но их нужно где-то фиксировать,
    /// чтобы можно было понять, что происходило в стриме.
    /// 
    /// Лог пишется в SF_Data/Logs/technical_log.txt
    /// </summary>
    public static class TechnicalLogService
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static TechnicalLogService()
        {
            string logDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data",
                "Logs"
            );

            try
            {
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TechnicalLog] Не удалось создать папку логов: {ex.Message}");
            }

            LogFilePath = Path.Combine(logDir, "technical_log.txt");
        }

        /// <summary>
        /// Записать строку в технический лог.
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logEntry);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TechnicalLog] Ошибка записи: {ex.Message}");
            }
        }

        /// <summary>
        /// Очистить технический лог (например, при старте нового стрима).
        /// </summary>
        public static void Clear()
        {
            try
            {
                lock (_lock)
                {
                    if (File.Exists(LogFilePath))
                        File.Delete(LogFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TechnicalLog] Ошибка очистки: {ex.Message}");
            }
        }

        /// <summary>
        /// Путь к файлу лога (для отладки).
        /// </summary>
        public static string GetLogPath() => LogFilePath;
    }
}