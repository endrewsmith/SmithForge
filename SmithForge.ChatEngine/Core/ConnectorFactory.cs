using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmithForge.ChatEngine.Connectors;
using SmithForge.ChatEngine.Core.Interfaces;

namespace SmithForge.ChatEngine.Core;

public interface IConnectorFactory
{
    IChatConnector CreateYouTubeConnector(string videoId = "", string channelId = "", string apiKey = "");
    //IChatConnector CreateTwitchConnector(string channelName, string oauthToken, string clientId);
}

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

    //public IChatConnector CreateTwitchConnector(string channelName, string oauthToken, string clientId)
    //{
    //    var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
    //    var logger = loggerFactory?.CreateLogger<TwitchConnector>();

    //    return new TwitchConnector(logger, channelName, oauthToken, clientId);
    //}
}