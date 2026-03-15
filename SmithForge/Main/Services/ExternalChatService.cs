using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace SmithForge.Main.Services
{
    public class ExternalChatService
    {
        private Process? _javaProcess;
        private readonly string _failchatDir;
        private readonly string _javaExe;
        private readonly string _agentPath;
        private readonly string _jarPath;

        public event EventHandler? ProcessExited;
        public event EventHandler<string>? OutputDataReceived;
        public event EventHandler<string>? ErrorDataReceived;

        public bool IsRunning => _javaProcess != null && !_javaProcess.HasExited;

        public ExternalChatService()
        {
            string currentDir = Directory.GetCurrentDirectory();
            _failchatDir = Path.Combine(currentDir, "failchat-2.8.6-SNAPSHOT");
            _javaExe = Path.Combine(_failchatDir, "runtime-windows", "bin", "javaw.exe");
            _agentPath = Path.Combine(_failchatDir, "java-agents", "transparent-webview-patch.jar");
            _jarPath = Path.Combine(_failchatDir, "failchat-2.8.6-SNAPSHOT.jar");
        }

        public bool TryAttachExisting()
        {
            try
            {
                var existing = Process.GetProcessesByName("javaw").FirstOrDefault(p =>
                {
                    try
                    {
                        return p.MainModule?.FileName?.Contains("failchat-2.8.6-SNAPSHOT") == true;
                    }
                    catch { return false; }
                });

                if (existing != null)
                {
                    Attach(existing);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExternalChat] Ошибка при поиске существующего процесса: {ex.Message}");
            }
            return false;
        }

        public bool Start()
        {
            try
            {
                // Проверяем существование файлов
                if (!File.Exists(_javaExe))
                {
                    Debug.WriteLine($"[ExternalChat] Java не найден: {_javaExe}");
                    return false;
                }

                if (!File.Exists(_jarPath))
                {
                    Debug.WriteLine($"[ExternalChat] JAR не найден: {_jarPath}");
                    return false;
                }

                //string arguments = $"-Xmx200m -Xms100m -XX:+UseG1GC " +
                // $"-javaagent:\"{_agentPath}\" " +
                // $"-jar \"{_jarPath}\" --g NO_GUI";
                string arguments = $"-Xmx200m -Xms100m -XX:+UseG1GC " +
 $"-javaagent:\"{_agentPath}\" " +
 $"-jar \"{_jarPath}\" --g CHAT_ONLY";


                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _javaExe,
                        Arguments = arguments,
                        WorkingDirectory = _failchatDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                // Подписываемся на вывод
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[failchat] {e.Data}");
                        OutputDataReceived?.Invoke(this, e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[failchat ERROR] {e.Data}");
                        ErrorDataReceived?.Invoke(this, e.Data);
                    }
                };

                if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    Attach(process);
                    Debug.WriteLine("[ExternalChat] Процесс успешно запущен");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExternalChat] Ошибка при запуске: {ex.Message}");
            }
            return false;
        }

        private void Attach(Process p)
        {
            _javaProcess = p;
            _javaProcess.Exited += (s, e) =>
            {
                Debug.WriteLine("[ExternalChat] Процесс завершился");
                _javaProcess = null;
                ProcessExited?.Invoke(s, e);
            };
        }

        public async Task StopAsync()
        {
            Debug.WriteLine("[ExternalChat] Остановка процесса...");

            // Пытаемся gracefully остановить через HTTP
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                await client.GetAsync("http://127.0.0.1:8080/shutdown"); // Возможно, есть эндпоинт
            }
            catch { /* игнорируем */ }

            // Даем время на graceful shutdown
            for (int i = 0; i < 10; i++)
            {
                if (!IsRunning) break;
                await Task.Delay(100);
            }

            // Убиваем, если еще висит
            KillAll();
        }

        public void KillAll()
        {
            Debug.WriteLine("[ExternalChat] Принудительное завершение всех процессов failchat");

            // Убиваем наш процесс
            try
            {
                if (_javaProcess != null && !_javaProcess.HasExited)
                {
                    _javaProcess.Kill();
                    _javaProcess.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExternalChat] Ошибка при убийстве процесса: {ex.Message}");
            }

            // Ищем и убиваем все оставшиеся процессы failchat
            try
            {
                var leftovers = Process.GetProcessesByName("javaw").Where(p =>
                {
                    try
                    {
                        return p.MainModule?.FileName?.Contains("failchat-2.8.6-SNAPSHOT") == true;
                    }
                    catch { return false; }
                });

                foreach (var p in leftovers)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                        Debug.WriteLine($"[ExternalChat] Убит процесс PID: {p.Id}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ExternalChat] Не удалось убить процесс {p.Id}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExternalChat] Ошибка при поиске процессов: {ex.Message}");
            }

            _javaProcess = null;
        }

        // Проверка доступности WebSocket
        public async Task<bool> CheckWebSocketAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
                var response = await client.GetAsync("http://127.0.0.1:12345/health"); // Предполагаемый эндпоинт
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}