using SmithForge.Main.Models;

namespace SmithForge.Features.TechOverlay
{
    public static class TechEventFactory
    {
        public static TechEvent Nick(Chater chater, string oldName, string newName, int karma = 0)
            => new()
            {
                UserName = newName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Nick,
                Text = $"сменил ник (было: {oldName})",
                Karma = karma
            };

        public static TechEvent Avatar(Chater chater, string platformName, int karma = 0)
            => new()
            {
                UserName = chater.EffectiveName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Avatar,
                Text = $"обновил аватарку (с платформы:{platformName})",
                Karma = karma
            };

        public static TechEvent Like(Chater chater, int messageNumber, int karma = 0)
            => new()
            {
                UserName = chater.EffectiveName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Like,
                Text = $"поставил 👍 сообщению #{messageNumber}",
                Karma = karma
            };

        public static TechEvent Dislike(Chater chater, int messageNumber, int karma = 0)
            => new()
            {
                UserName = chater.EffectiveName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Dislike,
                Text = $"поставил 👎 сообщению #{messageNumber}",
                Karma = karma
            };

        public static TechEvent Info(Chater chater, string pagePath, int karma = 0)
            => new()
            {
                UserName = chater.EffectiveName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Info,
                Text = $"открыл справку: {pagePath}",
                Karma = karma
            };

        public static TechEvent Help(Chater chater, string target, int karma = 0)
            => new()
            {
                UserName = chater.EffectiveName,
                UserLogin = chater.Login,
                Kind = TechEventKind.Help,
                Text = string.IsNullOrEmpty(target)
                    ? "запросил список команд"
                    : $"запросил справку по !!{target}",
                Karma = karma
            };

        public static TechEvent KarmaGrant(int amount, int userCount)
    => new()
    {
        UserName = "Смит",
        UserLogin = "Smith",
        Kind = TechEventKind.KarmaGrant,
        Text = $"начислил {amount} кармы {userCount} зрителям",
        Karma = 0
    };

    }
}