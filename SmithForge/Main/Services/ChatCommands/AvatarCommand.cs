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
            bool forceUpdate = false;

            // Проверяем, есть ли аргументы
            if (info.Arguments.Count > 0)
            {
                // Склеиваем все аргументы обратно
                string fullArg = string.Join(":", info.Arguments);
                Debug.WriteLine($"[AvatarCommand] Полный аргумент: {fullArg}");

                // ✅ Проверяем флаг принудительного обновления
                if (fullArg.EndsWith("!"))
                {
                    forceUpdate = true;
                    fullArg = fullArg.TrimEnd('!');
                    Debug.WriteLine($"[AvatarCommand] Принудительное обновление!");
                }

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
                    // ✅ Для Twitch используем OriginalName (логин), для YouTube/GoodGame - ID канала
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

            Debug.WriteLine($"[AvatarCommand] Итог - платформа: {platform}, пользователь: {targetUsername}, forceUpdate: {forceUpdate}");

            try
            {
                if (platform == "youtube" || platform == "yt")
                {
                    // ✅ Передаем Channel ID (UC...)
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
                    msg.Message = $"❌ Платформа {platform} не поддерживается";
                    msg.IsProcessedByCommand = true;
                    msg.ShouldChargeForCommand = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarCommand] ОШИБКА: {ex.Message}");
                Debug.WriteLine($"[AvatarCommand] Стек: {ex.StackTrace}");
                msg.Message = $"❌ Ошибка: {ex.Message}";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
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

        private async Task LoadYouTubeAvatar(string channelId, CommonMessage msg, Chater caller, bool forceUpdate = false)
        {
            Debug.WriteLine($"[AvatarCommand] Загрузка аватарки YouTube для канала: {channelId}");

            if (string.IsNullOrEmpty(channelId))
            {
                Debug.WriteLine("[AvatarCommand] channelId пустой");
                msg.Message = "❌ Не указан ID канала YouTube";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

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

            // ✅ Передаём forceUpdate
            string savedAvatarPath = await YouTubeAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
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
            caller.AvatarFileName = Path.GetFileName(savedAvatarPath);
            ChaterStorage.AddOrUpdate(caller);
            caller.RefreshAvatar();

            //msg.IsProcessedByCommand = true;
            //msg.ShouldChargeForCommand = true;
            //msg.Message = "✅ Аватарка YouTube успешно загружена!";
            Debug.WriteLine($"[AvatarCommand] YouTube аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");
        }

        private async Task LoadTwitchAvatar(string username, CommonMessage msg, Chater caller, bool forceUpdate = false)
        {
            Debug.WriteLine($"[AvatarCommand] Начинаем загрузку аватарки Twitch для {username}");

            if (string.IsNullOrEmpty(username))
            {
                Debug.WriteLine("[AvatarCommand] username пустой");
                msg.Message = "❌ Не указан логин пользователя Twitch";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            string avatarUrl = await TwitchAvatarService.GetAvatarUrlByLogin(username);
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки Twitch");
                msg.Message = $"❌ Не найдена аватарка для @{username}";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            // ✅ Передаём forceUpdate
            string savedAvatarPath = await TwitchAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
            if (string.IsNullOrEmpty(savedAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку Twitch");
                msg.Message = "❌ Не удалось скачать аватарку";
                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = false;
                return;
            }

            Debug.WriteLine($"[AvatarCommand] Аватар Twitch сохранён: {savedAvatarPath}");

            // Обновляем данные пользователя
            caller.AvatarFileName = Path.GetFileName(savedAvatarPath);

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

            //msg.IsProcessedByCommand = true;
            //msg.ShouldChargeForCommand = true;
            //msg.Message = $"✅ Аватарка Twitch для @{username} успешно загружена!";
            Debug.WriteLine($"[AvatarCommand] Twitch аватар УСПЕШНО ЗАГРУЖЕН для {caller.Login}");
        }

        // 1. Меняем дефолтное значение на true, раз метод ОБЯЗАН всегда обновлять
        private async Task LoadGoodGameAvatar(string channelId, CommonMessage msg, Chater caller, bool forceUpdate = true)
        {
            Debug.WriteLine($"[AvatarCommand] Загрузка аватарки GoodGame для канала: {channelId} (Force: {forceUpdate})");

            try
            {
                string channelName = null;
                var account = caller.Accounts.FirstOrDefault(a =>
                    a.Platform.Equals("goodgame", StringComparison.OrdinalIgnoreCase));

                if (account != null && !string.IsNullOrEmpty(account.OriginalName))
                {
                    channelName = account.OriginalName;
                }

                if (string.IsNullOrEmpty(channelName) && !long.TryParse(channelId, out _))
                {
                    channelName = channelId;
                }

                // 🌟 ИСПРАВЛЕНИЕ 1: Передаем forceUpdate в сервис получения URL, 
                // чтобы он сбросил свой внутренний кэш (если он там есть) и сходил в API GoodGame
                string avatarUrl = await GoodGameAvatarService.GetAvatarUrlByChannelId(channelId, channelName);

                if (string.IsNullOrEmpty(avatarUrl))
                {
                    Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки GoodGame для {channelId}");
                    msg.Message = $"❌ Не найдена аватарка для канала {channelId}";
                    msg.IsProcessedByCommand = true;
                    msg.ShouldChargeForCommand = false;
                    return;
                }

                // 🌟 ИСПРАВЛЕНИЕ 2: Перед скачиванием, если forceUpdate == true, 
                // можно принудительно удалить старый файл аватарки с диска, чтобы старый кэш не мешал
                if (forceUpdate && !string.IsNullOrEmpty(caller.AvatarFileName))
                {
                    var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "custom", caller.AvatarFileName);
                    if (!File.Exists(oldPath))
                    {
                        oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars", "platform", caller.AvatarFileName);
                    }

                    if (File.Exists(oldPath))
                    {
                        try { File.Delete(oldPath); Debug.WriteLine($"[AvatarCommand] Старый файл аватарки удален: {oldPath}"); } catch { }
                    }
                }

                // Скачиваем заново
                string savedPath = await GoodGameAvatarService.DownloadAvatarAsync(caller.Id, avatarUrl, forceUpdate);
                if (string.IsNullOrEmpty(savedPath))
                {
                    Debug.WriteLine("[AvatarCommand] Не удалось скачать аватарку GoodGame");
                    msg.Message = "❌ Не удалось скачать аватарку";
                    msg.IsProcessedByCommand = true;
                    msg.ShouldChargeForCommand = false;
                    return;
                }

                Debug.WriteLine($"[AvatarCommand] Аватар GoodGame сохранён: {savedPath}");

                // Обновляем данные пользователя
                caller.AvatarFileName = Path.GetFileName(savedPath);
                caller.RefreshAvatar(); // Метод должен пересчитать FullAvatarPath

                // Синхронизируем регистр с тем, как работает ваш ChaterStorage!
                string externalId = $"goodgame:{channelId}"; // Убрал .ToLower(), если в ChaterStorage вы его тоже убрали

                var existingAccount = caller.Accounts.FirstOrDefault(a =>
                    a.ExternalId.Equals(externalId, StringComparison.OrdinalIgnoreCase));

                if (existingAccount == null)
                {
                    caller.Accounts.Add(new ExternalAccount
                    {
                        ExternalId = externalId,
                        Platform = "goodgame",
                        OriginalName = channelName ?? channelId
                    });
                }
                else if (existingAccount.OriginalName != channelName)
                {
                    existingAccount.OriginalName = channelName ?? channelId;
                }

                // Сохраняем и ОПОВЕЩАЕМ веб-оверлей
                DatabaseService.SaveChater(caller);
                ChaterStorage.AddOrUpdate(caller);

                // 🌟 Это вызовет SendAvatarUpdateOnly, который мы настроили в прошлом шаге
                ChaterStorage.NotifyChaterUpdated(caller);

                msg.IsProcessedByCommand = true;
                msg.ShouldChargeForCommand = true;
                msg.Message = $"✅ Аватарка GoodGame для канала {channelName ?? channelId} успешно обновлена!";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarCommand] Ошибка загрузки GoodGame аватарки: {ex.Message}");
                msg.Message = $"❌ Ошибка: {ex.Message}";
                msg.IsProcessedByCommand = true;
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