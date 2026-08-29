using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace SmithForge.Main.Services
{
    public static class KarmaService
    {
        // НОВЫЙ МЕТОД - принимает CommonMessage
        public static void AddExperience(Chater chater, CommonMessage msg, AppSettings settings)
        {
            if (chater == null) throw new ArgumentNullException(nameof(chater));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Debug.WriteLine("");
            Debug.WriteLine("╔══════════════════════════════════════════╗");
            Debug.WriteLine("║         KARMA SERVICE DEBUG             ║");
            Debug.WriteLine("╚══════════════════════════════════════════╝");

            // 1. Информация о пользователе ДО
            Debug.WriteLine($"1. ПОЛЬЗОВАТЕЛЬ ДО:");
            Debug.WriteLine($"   - ID: {chater.Id}");
            Debug.WriteLine($"   - Login: {chater.Login}");
            Debug.WriteLine($"   - EffectiveName: {chater.EffectiveName}");
            Debug.WriteLine($"   - MessageCount ДО: {chater.MessageCount}");
            Debug.WriteLine($"   - Rank ДО: {chater.Rank}");
            Debug.WriteLine($"   - TotalKarma: {chater.TotalKarma}");

            // 2. Настройки
            Debug.WriteLine($"2. НАСТРОЙКИ:");
            Debug.WriteLine($"   - Platform: {msg.Type}");
            Debug.WriteLine($"   - Длина сообщения: {msg.Message?.Length ?? 0} символов");
            Debug.WriteLine($"   - Категория длины: {msg.LengthCategory}");
            Debug.WriteLine($"   - KarmaRateTwitch: {settings.KarmaRateTwitch}");
            Debug.WriteLine($"   - KarmaRateYouTube: {settings.KarmaRateYouTube}");
            Debug.WriteLine($"   - KarmaRateGoodGame: {settings.KarmaRateGoodGame}");
            Debug.WriteLine($"   - RankThresholds: {string.Join(", ", settings.RankThresholds)}");

            long oldMessageCount = chater.MessageCount;
            int oldRank = chater.Rank;
            chater.MessageCount++;
            chater.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 3. Проверка - первые 10 сообщений без кармы
            if (chater.MessageCount <= 10)
            {
                Debug.WriteLine($"3. ПЕРВЫЕ 10 СООБЩЕНИЙ:");
                Debug.WriteLine($"   - Сообщение #{chater.MessageCount} из 10");
                Debug.WriteLine($"   - Карма НЕ начисляется");
            }
            else
            {
                // 4. Базовая карма в зависимости от длины сообщения
                double baseAmount = msg.LengthCategory switch
                {
                    MessageLength.Short => 0.1,
                    MessageLength.Medium => 0.2,
                    MessageLength.Long => 0.3,
                    _ => 10
                };
                Debug.WriteLine($"3. БАЗОВАЯ КАРМА:");
                Debug.WriteLine($"   - BaseAmount: {baseAmount} ({msg.LengthCategory})");

                // 5. Расчет множителей
                double platformMultiplier = msg.Type.ToLower() switch
                {
                    "tw" or "twitch" => settings.KarmaRateTwitch,
                    "yt" or "youtube" => settings.KarmaRateYouTube,
                    "gg" or "goodgame" => settings.KarmaRateGoodGame,
                    _ => 0.0
                };
                Debug.WriteLine($"4. МНОЖИТЕЛИ:");
                Debug.WriteLine($"   - PlatformMultiplier: {platformMultiplier}");

                double rankMultiplier = 1.0 + (chater.Rank * 0.1);
                Debug.WriteLine($"   - RankMultiplier: {rankMultiplier} (1.0 + {chater.Rank}*0.1)");

                double earned = baseAmount * (1 + platformMultiplier) * rankMultiplier;
                Debug.WriteLine($"   - Earned: {earned} = {baseAmount} * {1 + platformMultiplier} * {rankMultiplier}");

                // 6. Начисление кармы
                chater.Karma += earned;
                chater.TotalKarma += earned;

                Debug.WriteLine($"5. ПОСЛЕ НАЧИСЛЕНИЯ:");
                Debug.WriteLine($"   - Karma: {chater.Karma}");
                Debug.WriteLine($"   - TotalKarma: {chater.TotalKarma}");
            }

            Debug.WriteLine($"   - MessageCount было: {oldMessageCount}, стало: {chater.MessageCount}");

            // 7. Проверка порогов
            Debug.WriteLine($"6. ПРОВЕРКА ПОРОГОВ:");
            List<int> thresholds = settings.RankThresholds;
            Debug.WriteLine($"   - Список порогов: {string.Join(", ", thresholds)}");
            Debug.WriteLine($"   - Проверяем значение: {(int)chater.MessageCount}");

            bool contains = thresholds.Contains((int)chater.MessageCount);
            Debug.WriteLine($"   - Содержится? {contains}");

            if (contains)
            {
                chater.Rank++;
                Debug.WriteLine($"   ✅ РАНГ ПОВЫШЕН! Был: {oldRank}, стал: {chater.Rank}");

                // ✅ НОВАЯ ФУНКЦИЯ: Проверяем, достиг ли пользователь 4-го ранга
                if (chater.Rank >= 4 && oldRank < 4)
                {
                    Debug.WriteLine($"🎯 ПОЛЬЗОВАТЕЛЬ ДОСТИГ 4-го РАНГА! Загружаем аватарку...");

                    // ✅ ИСПОЛЬЗУЕМ СУЩЕСТВУЮЩИЙ МЕТОД ИЗ AvatarCommand
                    _ = Task.Run(async () => await LoadAvatarForUserAsync(chater, msg));
                }

                Debug.WriteLine($"7. СОХРАНЕНИЕ (SaveChater)");
                DatabaseService.SaveChater(chater);
                ChaterStorage.AddOrUpdate(chater);
            }
            else
            {
                Debug.WriteLine($"   ❌ РАНГ НЕ ИЗМЕНИЛСЯ: {chater.Rank}");

                Debug.WriteLine($"7. СОХРАНЕНИЕ (UpdateChaterStats)");
                DatabaseService.UpdateChaterStats(chater);
            }

            Debug.WriteLine("═══════════════════════════════════════════");
            Debug.WriteLine("");
        }

        // СТАРЫЙ МЕТОД - оставляем для обратной совместимости
        public static void AddExperience(Chater chater, double baseAmount, string platform, AppSettings settings)
        {
            if (chater == null) throw new ArgumentNullException(nameof(chater));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            Debug.WriteLine("");
            Debug.WriteLine("╔══════════════════════════════════════════╗");
            Debug.WriteLine("║         KARMA SERVICE DEBUG (LEGACY)    ║");
            Debug.WriteLine("╚══════════════════════════════════════════╝");

            // 1. Информация о пользователе ДО
            Debug.WriteLine($"1. ПОЛЬЗОВАТЕЛЬ ДО:");
            Debug.WriteLine($"   - ID: {chater.Id}");
            Debug.WriteLine($"   - Login: {chater.Login}");
            Debug.WriteLine($"   - EffectiveName: {chater.EffectiveName}");
            Debug.WriteLine($"   - MessageCount ДО: {chater.MessageCount}");
            Debug.WriteLine($"   - Rank ДО: {chater.Rank}");
            Debug.WriteLine($"   - TotalKarma: {chater.TotalKarma}");

            // 2. Настройки
            Debug.WriteLine($"2. НАСТРОЙКИ:");
            Debug.WriteLine($"   - Platform: {platform}");
            Debug.WriteLine($"   - BaseAmount: {baseAmount}");
            Debug.WriteLine($"   - KarmaRateTwitch: {settings.KarmaRateTwitch}");
            Debug.WriteLine($"   - KarmaRateYouTube: {settings.KarmaRateYouTube}");
            Debug.WriteLine($"   - KarmaRateGoodGame: {settings.KarmaRateGoodGame}");
            Debug.WriteLine($"   - RankThresholds: {string.Join(", ", settings.RankThresholds)}");

            long oldMessageCount = chater.MessageCount;
            int oldRank = chater.Rank;
            chater.MessageCount++;
            chater.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 3. Проверка - первые 10 сообщений без кармы
            if (chater.MessageCount <= 10)
            {
                Debug.WriteLine($"3. ПЕРВЫЕ 10 СООБЩЕНИЙ:");
                Debug.WriteLine($"   - Сообщение #{chater.MessageCount} из 10");
                Debug.WriteLine($"   - Карма НЕ начисляется");
            }
            else
            {
                // 4. Расчет множителей
                double platformMultiplier = platform.ToLower() switch
                {
                    "tw" or "twitch" => settings.KarmaRateTwitch,
                    "yt" or "youtube" => settings.KarmaRateYouTube,
                    "gg" or "goodgame" => settings.KarmaRateGoodGame,
                    _ => 1.0
                };
                Debug.WriteLine($"3. МНОЖИТЕЛИ:");
                Debug.WriteLine($"   - PlatformMultiplier: {platformMultiplier}");

                double rankMultiplier = 1.0 + (chater.Rank * 0.1);
                Debug.WriteLine($"   - RankMultiplier: {rankMultiplier} (1.0 + {chater.Rank}*0.1)");

                double earned = baseAmount * platformMultiplier * rankMultiplier;
                Debug.WriteLine($"   - Earned: {earned} = {baseAmount} * {platformMultiplier} * {rankMultiplier}");

                // 5. Начисление кармы
                chater.Karma += earned;
                chater.TotalKarma += earned;

                Debug.WriteLine($"4. ПОСЛЕ НАЧИСЛЕНИЯ:");
                Debug.WriteLine($"   - Karma: {chater.Karma}");
                Debug.WriteLine($"   - TotalKarma: {chater.TotalKarma}");
            }

            Debug.WriteLine($"   - MessageCount было: {oldMessageCount}, стало: {chater.MessageCount}");

            // 6. Проверка порогов
            Debug.WriteLine($"5. ПРОВЕРКА ПОРОГОВ:");
            List<int> thresholds = settings.RankThresholds;
            Debug.WriteLine($"   - Список порогов: {string.Join(", ", thresholds)}");
            Debug.WriteLine($"   - Проверяем значение: {(int)chater.MessageCount}");

            bool contains = thresholds.Contains((int)chater.MessageCount);
            Debug.WriteLine($"   - Содержится? {contains}");

            if (contains)
            {
                chater.Rank++;
                Debug.WriteLine($"   ✅ РАНГ ПОВЫШЕН! Был: {oldRank}, стал: {chater.Rank}");

                // ✅ НОВАЯ ФУНКЦИЯ: Проверяем, достиг ли пользователь 4-го ранга
                if (chater.Rank >= 4 && oldRank < 4)
                {
                    Debug.WriteLine($"🎯 ПОЛЬЗОВАТЕЛЬ ДОСТИГ 4-го РАНГА! Загружаем аватарку...");

                    // ✅ ИСПОЛЬЗУЕМ СУЩЕСТВУЮЩИЙ МЕТОД ИЗ AvatarCommand
                    _ = Task.Run(async () => await LoadAvatarForUserAsync(chater, platform));
                }

                Debug.WriteLine($"6. СОХРАНЕНИЕ (SaveChater)");
                DatabaseService.SaveChater(chater);
                ChaterStorage.AddOrUpdate(chater);
            }
            else
            {
                Debug.WriteLine($"   ❌ РАНГ НЕ ИЗМЕНИЛСЯ: {chater.Rank}");

                Debug.WriteLine($"6. СОХРАНЕНИЕ (UpdateChaterStats)");
                DatabaseService.UpdateChaterStats(chater);
            }

            Debug.WriteLine("═══════════════════════════════════════════");
            Debug.WriteLine("");
        }

        public static void AdjustRank(Chater chater, int delta, string reason)
        {
            if (chater == null) throw new ArgumentNullException(nameof(chater));

            chater.Rank += delta;
            Debug.WriteLine($"[RANK] {chater.EffectiveName} ранг изменен на {delta} ({reason}). Теперь: {chater.Rank}");

            DatabaseService.SaveChater(chater);
            ChaterStorage.AddOrUpdate(chater);
        }

        // ============================================================
        // ✅ НОВЫЙ МЕТОД: Загрузка аватарки через существующий AvatarCommand
        // ============================================================

        /// <summary>
        /// Загрузить аватарку для пользователя при достижении 4-го ранга
        /// Использует существующий код из AvatarCommand
        /// </summary>
        private static async Task LoadAvatarForUserAsync(Chater chater, CommonMessage msg)
        {
            try
            {
                Debug.WriteLine($"[AvatarLoader] Загрузка аватарки для {chater.EffectiveName} (ранг {chater.Rank})");

                string platform = msg.Type?.ToLower() ?? "";
                await LoadAvatarUsingAvatarCommand(chater, platform);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarLoader] Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузить аватарку для пользователя (перегрузка для legacy метода)
        /// </summary>
        private static async Task LoadAvatarForUserAsync(Chater chater, string platform)
        {
            try
            {
                Debug.WriteLine($"[AvatarLoader] Загрузка аватарки для {chater.EffectiveName} (ранг {chater.Rank})");

                await LoadAvatarUsingAvatarCommand(chater, platform);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarLoader] Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Загружает аватарку используя логику из AvatarCommand
        /// </summary>
        /// <summary>
        /// Загружает аватарку используя логику из AvatarCommand
        /// </summary>
        /// <summary>
        /// Загружает аватарку используя логику из AvatarCommand
        /// </summary>
        private static async Task LoadAvatarUsingAvatarCommand(Chater chater, string platform)
        {
            try
            {
                // Определяем ID пользователя и имя для разных платформ
                string targetId = null;
                string channelName = null;

                var account = chater.Accounts.FirstOrDefault(a =>
                    string.Equals(a.Platform, platform, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Platform, GetPlatformShort(platform), StringComparison.OrdinalIgnoreCase));

                if (account == null)
                {
                    Debug.WriteLine($"[AvatarLoader] Аккаунт на платформе {platform} не найден");
                    return;
                }

                // Определяем ID для поиска аватарки
                if (platform == "twitch" || platform == "tw")
                {
                    targetId = account.OriginalName;
                    Debug.WriteLine($"[AvatarLoader] Логин Twitch: {targetId}");
                }
                else if (platform == "goodgame" || platform == "gg")
                {
                    // ✅ Для GoodGame используем OriginalName (имя канала) для URL
                    targetId = account.OriginalName;
                    channelName = account.OriginalName;
                    Debug.WriteLine($"[AvatarLoader] Имя канала GoodGame: {targetId}");
                }
                else
                {
                    targetId = ExtractIdFromExternalId(account.ExternalId);
                    Debug.WriteLine($"[AvatarLoader] ID: {targetId}");
                }

                if (string.IsNullOrEmpty(targetId))
                {
                    Debug.WriteLine($"[AvatarLoader] Не удалось определить ID для {chater.EffectiveName}");
                    return;
                }

                string avatarUrl = null;
                string savedPath = null;

                // Используем существующие сервисы
                if (platform == "youtube" || platform == "yt")
                {
                    avatarUrl = await YouTubeAvatarService.GetAvatarUrlByChannelId(targetId);
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        savedPath = await YouTubeAvatarService.DownloadAvatarAsync(chater.Id, avatarUrl);
                    }
                }
                else if (platform == "twitch" || platform == "tw")
                {
                    avatarUrl = await TwitchAvatarService.GetAvatarUrlByLogin(targetId);
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        savedPath = await TwitchAvatarService.DownloadAvatarAsync(chater.Id, avatarUrl);
                    }
                }
                else if (platform == "goodgame" || platform == "gg")
                {
                    // ✅ Для GoodGame передаём ID и имя канала
                    string channelId = ExtractIdFromExternalId(account.ExternalId);
                    avatarUrl = await GoodGameAvatarService.GetAvatarUrlByChannelId(channelId, channelName);
                    if (!string.IsNullOrEmpty(avatarUrl))
                    {
                        savedPath = await GoodGameAvatarService.DownloadAvatarAsync(chater.Id, avatarUrl);
                    }
                }

                // Если аватарка скачалась - обновляем пользователя
                if (!string.IsNullOrEmpty(savedPath))
                {
                    chater.AvatarFileName = Path.GetFileName(savedPath);

                    // ✅ Обновляем в хранилище
                    ChaterStorage.AddOrUpdate(chater);

                    // ✅ Сохраняем в БД
                    DatabaseService.SaveChater(chater);

                    // ✅ Принудительно обновляем UI
                    chater.RefreshAvatar();

                    // ✅ Уведомляем всех подписчиков
                    ChaterStorage.NotifyChaterUpdated(chater);

                    Debug.WriteLine($"[AvatarLoader] ✅ Аватарка загружена и UI обновлён для {chater.EffectiveName}");
                }
                else
                {
                    Debug.WriteLine($"[AvatarLoader] ❌ Не удалось загрузить аватарку для {chater.EffectiveName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AvatarLoader] Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Извлекает ID из ExternalId (формат: "platform:id")
        /// </summary>
        private static string ExtractIdFromExternalId(string externalId)
        {
            if (string.IsNullOrEmpty(externalId))
                return string.Empty;

            var parts = externalId.Split(':');
            return parts.Length > 1 ? parts[1] : externalId;
        }

        /// <summary>
        /// Получает короткое имя платформы
        /// </summary>
        private static string GetPlatformShort(string platform)
        {
            return platform.ToLower() switch
            {
                "youtube" => "yt",
                "twitch" => "tw",
                "goodgame" => "gg",
                _ => platform
            };
        }
    }
}