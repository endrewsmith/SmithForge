using SmithForge.AlertsEngine.Providers.DonationAlerts.Models;
using SocketIOClient;
using SocketIOClient.Common;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmithForge.AlertsEngine.Providers.DonationAlerts
{
    public class DonationAlertsSocketClient : IDisposable
    {
        private const string SocketUrl = "http://socket.donationalerts.ru:3001";

        private SocketIO? _socket;
        private readonly string _widgetToken;
        private bool _disposed;

        public event EventHandler<DonationAlertsMessage>? OnDonationReceived;
        public event EventHandler? OnConnected;
        public event EventHandler<string>? OnDisconnected;
        public event EventHandler<Exception>? OnError;
        public event EventHandler<string>? OnLog;

        public bool IsConnected => _socket?.Connected ?? false;

        public DonationAlertsSocketClient(string widgetToken)
        {
            if (string.IsNullOrWhiteSpace(widgetToken))
                throw new ArgumentException("Токен виджета не может быть пустым", nameof(widgetToken));

            _widgetToken = widgetToken;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_socket != null)
            {
                Log("⚠️ Уже подключён");
                return;
            }

            try
            {
                Log($"🔄 Подключение к {SocketUrl}...");

                var options = new SocketIOOptions
                {
                    // ✅ КЛЮЧЕВОЕ: используем старый протокол Engine.IO v3
                    // DonationAlerts работает на Socket.IO v2 (EIO=3)
                    EIO = EngineIO.V3,

                    // DonationAlerts поддерживает WebSocket
                    Transport = TransportProtocol.WebSocket,

                    // Таймаут подключения
                    ConnectionTimeout = TimeSpan.FromSeconds(20)
                };

                _socket = new SocketIO(new Uri(SocketUrl), options);

                // === СОБЫТИЯ ===

                _socket.OnConnected += async (sender, e) =>
                {
                    Log("✅ Socket.IO подключён. Отправляем 'add-user'...");

                    // Отправляем токен для аутентификации
                    await _socket.EmitAsync("add-user", new object[]
                    {
                        new
                        {
                            token = _widgetToken,
                            type = "minor"
                        }
                    });

                    OnConnected?.Invoke(this, EventArgs.Empty);
                };

                _socket.OnDisconnected += (sender, reason) =>
                {
                    Log($"⚠️ Socket.IO отключён: {reason}");
                    OnDisconnected?.Invoke(this, reason);
                };

                _socket.OnError += (sender, error) =>
                {
                    Log($"❌ Socket.IO ошибка: {error}");
                    OnError?.Invoke(this, new Exception(error));
                };

                // === ГЛАВНОЕ СОБЫТИЕ: ДОНАТ ===
                _socket.On("donation", async response =>
                {
                    await Task.CompletedTask;

                    try
                    {
                        string? json = null;

                        try
                        {
                            json = response.GetValue<string>(0);
                        }
                        catch { }

                        DonationAlertsMessage? donation = null;

                        if (!string.IsNullOrEmpty(json))
                        {
                            Log($"📥 Получен донат (JSON): {TrimForLog(json)}");
                            donation = JsonSerializer.Deserialize<DonationAlertsMessage>(json);
                        }
                        else
                        {
                            Log("📥 Получен донат (объект)");
                            donation = response.GetValue<DonationAlertsMessage>(0);
                        }

                        if (donation != null)
                        {
                            Log($"✅ Донат: {donation.Username} → {donation.Amount} {donation.Currency}");
                            OnDonationReceived?.Invoke(this, donation);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Ошибка обработки доната: {ex.Message}");
                        OnError?.Invoke(this, ex);
                    }
                });

                await _socket.ConnectAsync();
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка подключения: {ex.Message}");
                OnError?.Invoke(this, ex);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_socket == null) return;

            try
            {
                if (_socket.Connected)
                    await _socket.DisconnectAsync();

                _socket.Dispose();
                _socket = null;

                Log("✅ Отключено");
            }
            catch (Exception ex)
            {
                Log($"⚠️ Ошибка при отключении: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            Debug.WriteLine($"[DA Socket] {message}");
            OnLog?.Invoke(this, message);
        }

        private static string TrimForLog(string text, int max = 200)
        {
            if (string.IsNullOrEmpty(text)) return "(пусто)";
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _socket?.Dispose();
                _socket = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DA Socket] Ошибка Dispose: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}