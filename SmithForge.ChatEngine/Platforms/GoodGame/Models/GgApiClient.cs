using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SmithForge.ChatEngine.Platforms.GoodGame.Models;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class GgApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public event EventHandler<string>? OnLog;

        public GgApiClient(HttpClient httpClient, string apiUrl = "https://goodgame.ru/api/")
        {
            _httpClient = httpClient;
            _apiUrl = apiUrl.EndsWith("/") ? apiUrl : apiUrl + "/";
        }

        public async Task<long> RequestChannelIdAsync(string channelName)
        {
            var channelInfo = await RequestChannelInfoAsync(channelName);
            return channelInfo.Id;
        }

        public async Task<GgChannel> RequestChannelInfoAsync(string channelName)
        {
            var url = $"{_apiUrl}getchannelstatus?fmt=json&id={channelName}";
            Log($"📡 Запрос к API: {url}");

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                Log($"📥 Получен ответ");

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    using var enumerator = root.EnumerateObject();
                    if (enumerator.MoveNext())
                    {
                        var property = enumerator.Current;
                        var channelNode = property.Value;

                        if (channelNode.TryGetProperty("stream_id", out var streamIdElement))
                        {
                            var channelId = streamIdElement.GetInt64();
                            var isPremium = channelNode.GetProperty("premium").GetString() == "true";
                            var status = channelNode.GetProperty("status").GetString() ?? "offline";
                            var title = channelNode.TryGetProperty("title", out var titleElement)
                                ? titleElement.GetString() ?? ""
                                : "";
                            var viewers = channelNode.TryGetProperty("viewers", out var viewersElement)
                                ? viewersElement.GetInt32()
                                : 0;
                            var key = channelNode.TryGetProperty("key", out var keyElement)
                                ? keyElement.GetString() ?? channelName
                                : channelName;

                            Log($"✅ ID канала: {channelId}, Статус: {status}, Зрителей: {viewers}");

                            return new GgChannel
                            {
                                Name = key,
                                Id = channelId,
                                IsPremium = isPremium,
                                Status = status,
                                Title = title,
                                Viewers = viewers
                            };
                        }
                    }
                }

                throw new Exception($"Не удалось получить информацию о канале '{channelName}'");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка: {ex.Message}");
                throw;
            }
        }

        private void Log(string message)
        {
            OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}