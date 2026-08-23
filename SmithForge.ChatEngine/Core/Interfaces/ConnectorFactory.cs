using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Core.Interfaces;
using SmithForge.ChatEngine.Platforms.YouTube;
using SmithForge.ChatEngine.Platforms.Twitch;
using SmithForge.ChatEngine.Platforms.GoodGame;

namespace SmithForge.ChatEngine.Core.Interfaces
{
    public interface IConnectorFactory
    {
        IChatConnector CreateYouTubeConnector(string videoId = "", string channelId = "", string apiKey = "");
        IChatConnector CreateTwitchConnector(string channelName = "", string botName = "justinfan12345", string botPassword = "");
        IChatConnector CreateGoodGameConnector(string channelName = "");
    }
}

namespace SmithForge.ChatEngine.Core
{
    public class ConnectorFactory : IConnectorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ConnectorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IChatConnector CreateYouTubeConnector(string videoId = "", string channelId = "", string apiKey = "")
        {
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<YouTubeConnector>();

            return new YouTubeConnector(logger, videoId, channelId, apiKey);
        }

        public IChatConnector CreateTwitchConnector(string channelName = "", string botName = "justinfan12345", string botPassword = "")
        {
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<TwitchConnector>();

            return new TwitchConnector(logger, channelName, botName, botPassword);
        }

        public IChatConnector CreateGoodGameConnector(string channelName = "")
        {
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            var logger = loggerFactory?.CreateLogger<GgConnector>();
            return new GgConnector(logger, channelName);
        }
    }
}