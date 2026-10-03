using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.ChatCommands
{
    public class AvatarCommand : BaseCommand
    {
        public override bool IsTechnical => true;
        public override string Name => "аватар";
        public override IEnumerable<string> Aliases => new[] { "avatar", "аватарка", "ava" };

        public override string Description => "Установить аватарку. Форматы: !!ava (своя), !!ava:yt:@username";
        public override int Cost => 10;
        public override int MinRank => 0;
        public override int[] FreeForRanks => new[] { 5, 6 };

        // ✅ Синхронная точка входа — ждём выполнения всей асинхронной логики,
        // чтобы msg.ShouldChargeForCommand был актуален к моменту возврата в MessageProcessor.
        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            ExecuteAsync(info, chater, msg, settings).GetAwaiter().GetResult();
        }

        private async Task ExecuteAsync(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[AvatarCommand] НАЧАЛО ВЫПОЛНЕНИЯ");

            // Команда техническая — сообщение не пойдёт в оверлей.
            msg.Message = string.Empty;
            msg.IsProcessedByCommand = true;

            string platform = null;
            string targetUsername = null;
            bool forceUpdate = false;

            // Проверяем, есть ли аргументы
            if (info.Arguments.Count > 0)
            {
                string fullArg = string.Join(":", info.Arguments);
                Debug.WriteLine($"[AvatarCommand] Полный аргумент: {fullArg}");

                if (fullArg.EndsWith("!"))
                {
                    forceUpdate = true;
                    fullArg = fullArg.TrimEnd('!');
                    Debug.WriteLine($"[AvatarCommand] Принудительное обновление!");
                }

                if (fullArg.Contains(':'))
                {
                    int colonIndex = fullArg.IndexOf(':');
                    platform = fullArg.Substring(0, colonIndex).ToLower();

                    string afterColon = fullArg.Substring(colonIndex + 1);
                    targetUsername = afterColon.TrimStart('@');

                    Debug.WriteLine($"[AvatarCommand] Платформа: {platform}, имя: {targetUsername}");
                }
                else
                {
                    Debug.WriteLine($"[AvatarCommand] Неверный формат. Используйте: !!ava или !!ava:yt:UC...");
                    msg.ShouldChargeForCommand = false;
                    return;
                }
            }

            // ============ ЕСЛИ АРГУМЕНТОВ НЕТ ============
            if (string.IsNullOrEmpty(targetUsername))
            {
                platform = GetPlatformFromMessage(msg);
                Debug.WriteLine($"[AvatarCommand] Без аргументов. Платформа: {platform}");

                var account = chater.Accounts.FirstOrDefault(a =>
                    string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Platform, GetPlatformShort(platform), StringComparison.OrdinalIgnoreCase));

                if (account != null)
                {
                    if (platform == "twitch" || platform == "tw")
                    {
                        targetUsername = account.OriginalName;
                        Debug.WriteLine($"[AvatarCommand] Найден логин Twitch: {targetUsername} из аккаунта {account.ExternalId}");
                    }
                    else
                    {
                        targetUsername = ExtractIdFromExternalId(account.ExternalId);
                        Debug.WriteLine($"[AvatarCommand] Найден ID канала: {targetUsername} из ExternalId: {account.ExternalId}");
                    }
                }
                else
                {
                    targetUsername = chater.Login;
                    Debug.WriteLine($"[AvatarCommand] Аккаунт не найден, используем Login: {targetUsername}");
                }

                Debug.WriteLine($"[AvatarCommand] Итог - платформа: {platform}, пользователь: {targetUsername}");
            }

            if (string.IsNullOrEmpty(platform))
            {
                Debug.WriteLine($"[AvatarCommand] Платформа не указана");
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Итог - платформа: {platform}, пользователь: {targetUsername}, forceUpdate: {forceUpdate}");

            try
            {
                if (platform == "youtube" || platform == "yt")
                {
                    await LoadYouTubeAvatar(targetUsername, msg, chater, forceUpdate);
                }
                else if (platform == "twitch" || platform == "tw")
                {
                    await LoadTwitchAvatar(targetUsername, msg, chater, forceUpdate);
                }
                else if (platform == "goodgame" || platform == "gg")
                {
                    await LoadGoodGameAvatar(targetUsername, msg, chater, forceUpdate);
                }
                else
                {
                    Debug.WriteLine($"[AvatarCommand] Платформа {platform} не поддерживается");
                    msg.ShouldChargeForCommand = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarCommand] ОШИБКА: {ex.Message}");
                Debug.WriteLine($"[AvatarCommand] Стек: {ex.StackTrace}");
                msg.ShouldChargeForCommand = false;
            }
        }

        private string ExtractIdFromExternalId(string externalId)
        {
            if (string.IsNullOrEmpty(externalId))
                return string.Empty;

            var parts = externalId.Split(':');
            return parts.Length > 1 ? parts[1] : externalId;
        }

        private string GetPlatformShort(string platform)
        {
            return platform.ToLower() switch
            {
                "youtube" => "yt",
                "twitch" => "tw",
                "goodgame" => "gg",
                _ => platform
            };
        }

        private async Task LoadYouTubeAvatar(string channelId, CommonMessage msg, Chater caller, bool forceUpdate = false)
        {
            Debug.WriteLine($"[AvatarCommand] Загрузка аватарки YouTube для канала: {channelId}");

            if (string.IsNullOrEmpty(channelId))
            {
                Debug.WriteLine("[AvatarCommand] channelId пустой");
                msg.ShouldChargeForCommand = false;
                return;
            }

            string avatarUrl = await YouTubeAvatarService.GetAvatarUrlByChannelId(channelId);

            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки");
                msg.ShouldChargeForCommand = false;
                return;
            }

            caller.RefreshAvatar();
            await Task.Delay(100);

            string savedAvatarPath = await YouTubeAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
            if (string.IsNullOrEmpty(savedAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку");
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Аватар сохранён: {savedAvatarPath}");

            caller.AvatarFileName = Path.GetFileName(savedAvatarPath);
            ChaterStorage.AddOrUpdate(caller);
            caller.RefreshAvatar();

            Debug.WriteLine($"[AvatarCommand] YouTube аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");

            // ✅ Событие в технический оверлей
            int actualKarma = GetCostForRank(caller.Rank);
            WebServerService.Instance?.SendTechnicalEvent(
                TechEventFactory.Avatar(caller, "YouTube", -actualKarma));
        }

        private async Task LoadTwitchAvatar(string username, CommonMessage msg, Chater caller, bool forceUpdate = false)
        {
            Debug.WriteLine($"[AvatarCommand] Начинаем загрузку аватарки Twitch для {username}");

            if (string.IsNullOrEmpty(username))
            {
                Debug.WriteLine("[AvatarCommand] username пустой");
                msg.ShouldChargeForCommand = false;
                return;
            }

            string avatarUrl = await TwitchAvatarService.GetAvatarUrlByLogin(username);
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки Twitch");
                msg.ShouldChargeForCommand = false;
                return;
            }

            caller.RefreshAvatar();
            await Task.Delay(100);

            string savedAvatarPath = await TwitchAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
            if (string.IsNullOrEmpty(savedAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку Twitch");
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Аватар Twitch сохранён: {savedAvatarPath}");

            caller.AvatarFileName = Path.GetFileName(savedAvatarPath);

            var twitchAccount = caller.Accounts.FirstOrDefault(a =>
                a.Platform.Equals("twitch", StringComparison.OrdinalIgnoreCase));

            if (twitchAccount != null)
            {
                if (twitchAccount.OriginalName != username)
                {
                    Debug.WriteLine($"[AvatarCommand] Обновляем OriginalName: '{twitchAccount.OriginalName}' → '{username}'");
                    twitchAccount.OriginalName = username;
                }
            }
            else
            {
                Debug.WriteLine($"[AvatarCommand] ⚠️ Twitch-аккаунт не найден у {caller.Login}.");
            }

            ChaterStorage.AddOrUpdate(caller);
            DatabaseService.SaveChater(caller);
            caller.RefreshAvatar();

            Debug.WriteLine($"[AvatarCommand] Twitch аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");

            // ✅ Событие в технический оверлей
            int actualKarma = GetCostForRank(caller.Rank);
            WebServerService.Instance?.SendTechnicalEvent(
                TechEventFactory.Avatar(caller, "Twitch", actualKarma));
        }

        private async Task LoadGoodGameAvatar(string userId, CommonMessage msg, Chater caller, bool forceUpdate = true)
        {
            Debug.WriteLine($"[AvatarCommand] Загрузка аватарки GoodGame для ID: {userId} (Force: {forceUpdate})");

            try
            {
                var existingAccount = caller.Accounts.FirstOrDefault(a =>
                    a.Platform.Equals("goodgame", StringComparison.OrdinalIgnoreCase));

                string channelId = userId;
                string channelName = null;

                if (existingAccount != null && !string.IsNullOrEmpty(existingAccount.OriginalName))
                {
                    channelName = existingAccount.OriginalName;

                    if (!long.TryParse(userId, out _))
                    {
                        var parts = existingAccount.ExternalId.Split(':');
                        if (parts.Length > 1 && long.TryParse(parts[1], out long numericId))
                        {
                            channelId = parts[1];
                        }
                    }
                }
                else
                {
                    channelName = userId;
                }

                if (!long.TryParse(channelId, out _) && !string.IsNullOrEmpty(channelName))
                {
                    channelId = channelName;
                }

                Debug.WriteLine($"[AvatarCommand] GG: channelId={channelId}, channelName={channelName}");

                string avatarUrl = await GoodGameAvatarService.GetAvatarUrlByChannelId(channelId, channelName);

                if (string.IsNullOrEmpty(avatarUrl))
                {
                    Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки GoodGame для {channelId}");
                    msg.ShouldChargeForCommand = false;
                    return;
                }

                caller.RefreshAvatar();
                await Task.Delay(150);

                if (forceUpdate && !string.IsNullOrEmpty(caller.AvatarFileName))
                {
                    var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "custom", caller.AvatarFileName);
                    if (!File.Exists(oldPath))
                    {
                        oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "platform", caller.AvatarFileName);
                    }

                    if (File.Exists(oldPath))
                    {
                        try
                        {
                            File.Delete(oldPath);
                            Debug.WriteLine($"[AvatarCommand] Старый файл аватарки удален: {oldPath}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AvatarCommand] Не удалось удалить старый файл: {ex.Message}");
                        }
                    }
                }

                string savedPath = await GoodGameAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
                if (string.IsNullOrEmpty(savedPath))
                {
                    Debug.WriteLine("[AvatarCommand] Не удалось скачать аватарку GoodGame");
                    msg.ShouldChargeForCommand = false;
                    return;
                }

                Debug.WriteLine($"[AvatarCommand] Аватар GoodGame сохранён: {savedPath}");

                caller.AvatarFileName = Path.GetFileName(savedPath);

                if (existingAccount != null)
                {
                    if (existingAccount.OriginalName != channelName)
                    {
                        Debug.WriteLine($"[AvatarCommand] Обновляем OriginalName: '{existingAccount.OriginalName}' → '{channelName}'");
                        existingAccount.OriginalName = channelName;
                    }
                }
                else
                {
                    Debug.WriteLine($"[AvatarCommand] ⚠️ GG-аккаунт не найден у {caller.Login}.");
                }

                DatabaseService.SaveChater(caller);
                ChaterStorage.AddOrUpdate(caller);
                caller.RefreshAvatar();
                ChaterStorage.NotifyChaterUpdated(caller);

                Debug.WriteLine($"[AvatarCommand] Аккаунты пользователя {caller.Login}: {string.Join(", ", caller.Accounts.Select(a => a.ExternalId))}");

                // ✅ Событие в технический оверлей
                int actualKarma = GetCostForRank(caller.Rank);
                WebServerService.Instance?.SendTechnicalEvent(
                    TechEventFactory.Avatar(caller, "GoodGame", actualKarma));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarCommand] Ошибка загрузки GoodGame аватарки: {ex.Message}");
                msg.ShouldChargeForCommand = false;
            }
        }

        private string GetPlatformFromMessage(CommonMessage msg)
        {
            string type = msg.Type?.ToLower() ?? "";

            return type switch
            {
                "youtube" or "yt" => "youtube",
                "twitch" or "tw" => "twitch",
                "goodgame" or "gg" => "goodgame",
                _ => type
            };
        }
    }
}