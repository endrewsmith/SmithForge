using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

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
                    MessageLength.Short => 0.1,    // короткое
                    MessageLength.Medium => 0.2,   // среднее
                    MessageLength.Long => 0.3,     // длинное
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
                Debug.WriteLine($"   ✅ РАНГ ПОВЫШЕН! Был: {chater.Rank - 1}, стал: {chater.Rank}");

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
                Debug.WriteLine($"   ✅ РАНГ ПОВЫШЕН! Был: {chater.Rank - 1}, стал: {chater.Rank}");

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
    }
}