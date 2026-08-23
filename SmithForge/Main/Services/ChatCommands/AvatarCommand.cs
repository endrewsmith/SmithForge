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
        public override string Name => "аватар";
        public override IEnumerable<string> Aliases => new[] { "avatar", "аватарка", "ava" };

        public override string Description => "Установить аватарку. Форматы: !!ava (своя), !!ava:yt:@username";
        public override int Cost => 10;
        public override int MinRank => 0;
        public override int[] FreeForRanks => new[] { 5, 6 };

        public override async void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[AvatarCommand] НАЧАЛО ВЫПОЛНЕНИЯ");

            string platform = null;
            string targetUsername = null;

            // Проверяем, есть ли аргументы
            if (info.Arguments.Count > 0)
            {
                // Склеиваем все аргументы обратно
                string fullArg = string.Join(":", info.Arguments);
                Debug.WriteLine($"[AvatarCommand] Полный аргумент: {fullArg}");

                // Формат: "yt:UC..." или "yt:@username"
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
                    return;
                }
            }

            // ============ ЕСЛИ АРГУМЕНТОВ НЕТ ============
            if (string.IsNullOrEmpty(targetUsername))
            {
                platform = GetPlatformFromMessage(msg);
                Debug.WriteLine($"[AvatarCommand] Без аргументов. Платформа: {platform}");

                // ✅ Ищем аккаунт пользователя на этой платформе
                var account = chater.Accounts.FirstOrDefault(a =>
                    string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Platform, GetPlatformShort(platform), StringComparison.OrdinalIgnoreCase));

                if (account != null)
                {
                    // ✅ Извлекаем ID из ExternalId (формат: "platform:id")
                    targetUsername = ExtractIdFromExternalId(account.ExternalId);
                    Debug.WriteLine($"[AvatarCommand] Найден ID канала: {targetUsername} из ExternalId: {account.ExternalId}");
                }
                else
                {
                    // ❌ Если аккаунт не найден — используем Login (fallback)
                    targetUsername = chater.Login;
                    Debug.WriteLine($"[AvatarCommand] Аккаунт не найден, используем Login: {targetUsername}");
                }

                Debug.WriteLine($"[AvatarCommand] Итог - платформа: {platform}, пользователь: {targetUsername}");
            }

            // Проверка наличия платформы
            if (string.IsNullOrEmpty(platform))
            {
                Debug.WriteLine($"[AvatarCommand] Платформа не указана");
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Итог - платформа: {platform}, пользователь: {targetUsername}");

            try
            {
                if (platform == "youtube" || platform == "yt")
                {
                    // ✅ Передаем Channel ID (UC...)
                    await LoadYouTubeAvatar(targetUsername, msg, chater);
                }
                else if (platform == "twitch" || platform == "tw")
                {
                    await LoadTwitchAvatar(targetUsername, msg, chater);
                }
                else
                {
                    Debug.WriteLine($"[AvatarCommand] Платформа {platform} не поддерживается");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarCommand] ОШИБКА: {ex.Message}");
                Debug.WriteLine($"[AvatarCommand] Стек: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Извлекает ID из ExternalId (формат: "platform:id")
        /// </summary>
        private string ExtractIdFromExternalId(string externalId)
        {
            if (string.IsNullOrEmpty(externalId))
                return string.Empty;

            var parts = externalId.Split(':');
            return parts.Length > 1 ? parts[1] : externalId;
        }

        /// <summary>
        /// Получает короткое имя платформы (youtube -> yt, twitch -> tw)
        /// </summary>
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

        private async Task LoadYouTubeAvatar(string channelId, CommonMessage msg, Chater caller)
        {
            Debug.WriteLine($"[AvatarCommand] Загрузка аватарки YouTube для канала: {channelId}");

            // ✅ ИСПРАВЛЕНО: используем GetAvatarUrlByChannelId
            string avatarUrl = await YouTubeAvatarService.GetAvatarUrlByChannelId(channelId);

            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки");
                msg.Message = "❌ Не удалось найти аватарку для этого канала";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string savedAvatarPath = await YouTubeAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl);
            if (string.IsNullOrEmpty(savedAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку");
                msg.Message = "❌ Не удалось скачать аватарку";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Аватар сохранён: {savedAvatarPath}");

            // Обновляем данные пользователя
            caller.AvatarFileName = $"{caller.Id}.png";
            ChaterStorage.AddOrUpdate(caller);
            caller.RefreshAvatar();

            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;
            msg.Message = "✅ Аватарка успешно загружена!";
            Debug.WriteLine($"[AvatarCommand] YouTube аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");
        }

        private async Task LoadTwitchAvatar(string username, CommonMessage msg, Chater caller)
        {
            Debug.WriteLine($"[AvatarCommand] Начинаем загрузку аватарки Twitch для {username}");

            string avatarUrl = await TwitchAvatarService.GetAvatarUrlByLogin(username);
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки Twitch");
                return;
            }

            // ✅ DownloadAvatarAsync возвращает путь к сохранённому файлу
            string savedAvatarPath = await TwitchAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl);
            if (string.IsNullOrEmpty(savedAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку Twitch");
                return;
            }

            // ✅ Файл уже сохранён в нужное место, не нужно его копировать!
            Debug.WriteLine($"[AvatarCommand] Аватар Twitch сохранён: {savedAvatarPath}");

            // Обновляем данные пользователя
            caller.AvatarFileName = $"{caller.Id}.png";

            // Добавляем Twitch аккаунт если его нет
            string externalId = $"twitch:{username}";
            var existingAccount = caller.Accounts.FirstOrDefault(a => a.ExternalId == externalId);
            if (existingAccount == null)
            {
                caller.Accounts.Add(new ExternalAccount
                {
                    ExternalId = externalId,
                    Platform = "twitch",
                    OriginalName = username
                });
                Debug.WriteLine($"[AvatarCommand] Добавлен Twitch аккаунт для {username}");
            }

            ChaterStorage.AddOrUpdate(caller);
            caller.RefreshAvatar();

            msg.IsProcessedByCommand = true;
            Debug.WriteLine($"[AvatarCommand] Twitch аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");
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