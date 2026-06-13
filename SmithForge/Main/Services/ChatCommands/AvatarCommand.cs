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

                // Формат: "yt:@username"
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
                    Debug.WriteLine($"[AvatarCommand] Неверный формат. Используйте: !!ava или !!ava:yt:@username");
                    return;
                }
            }

            // Если команда без аргументов (!!ava)
            if (string.IsNullOrEmpty(targetUsername))
            {
                targetUsername = chater.Login;
                platform = GetPlatformFromMessage(msg);
                Debug.WriteLine($"[AvatarCommand] Без аргументов. Платформа: {platform}, пользователь: {targetUsername}");
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

        private async Task LoadYouTubeAvatar(string username, CommonMessage msg, Chater caller)
        {
            Debug.WriteLine($"[AvatarCommand] Начинаем загрузку аватарки YouTube для {username}");

            string avatarUrl = await YouTubeAvatarService.GetAvatarUrlByHandle(username);
            if (string.IsNullOrEmpty(avatarUrl))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось получить URL аватарки");
                return;
            }

            string tempAvatarPath = await YouTubeAvatarService.DownloadAvatarAsync(username, avatarUrl);
            if (string.IsNullOrEmpty(tempAvatarPath))
            {
                Debug.WriteLine($"[AvatarCommand] Не удалось скачать аватарку");
                return;
            }

            // ✅ Используем ID пользователя, который вызвал команду
            string targetUid = caller.Id;

            Debug.WriteLine($"[AvatarCommand] Сохраняем аватар для пользователя: {caller.Login} (ID: {targetUid})");

            // Сохраняем аватар в папку platform
            string platformFolder = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SF_Data", "Assets", "Avatars", "platform");
            Directory.CreateDirectory(platformFolder);

            string destPath = Path.Combine(platformFolder, $"{targetUid}.png");

            if (File.Exists(destPath))
                File.Delete(destPath);

            File.Copy(tempAvatarPath, destPath, true);

            Debug.WriteLine($"[AvatarCommand] Аватар сохранён: {destPath}");

            // Обновляем AvatarFileName у вызвавшего пользователя
            caller.AvatarFileName = $"{targetUid}.png";
            ChaterStorage.AddOrUpdate(caller);

            // Обновляем UI
            caller.RefreshAvatar();

            // Удаляем временный файл
            try { File.Delete(tempAvatarPath); } catch { }

            msg.IsProcessedByCommand = true;
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