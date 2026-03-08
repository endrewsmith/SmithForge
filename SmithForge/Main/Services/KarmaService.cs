using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace SmithForge.Main.Services
{
    public static class KarmaService
    {
        public static void AddExperience(Chater chater, double baseAmount, string platform, AppSettings settings)
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
            Debug.WriteLine($"   - Platform: {platform}");
            Debug.WriteLine($"   - BaseAmount: {baseAmount}");
            Debug.WriteLine($"   - KarmaRateTwitch: {settings.KarmaRateTwitch}");
            Debug.WriteLine($"   - KarmaRateYouTube: {settings.KarmaRateYouTube}");
            Debug.WriteLine($"   - KarmaRateGoodGame: {settings.KarmaRateGoodGame}");
            Debug.WriteLine($"   - RankThresholds: {string.Join(", ", settings.RankThresholds)}");

            // 3. Расчет множителей
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

            // 4. Начисление
            chater.Karma += earned;
            chater.TotalKarma += earned;

            long oldMessageCount = chater.MessageCount;
            chater.MessageCount++;
            chater.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            Debug.WriteLine($"4. ПОСЛЕ НАЧИСЛЕНИЯ:");
            Debug.WriteLine($"   - MessageCount было: {oldMessageCount}, стало: {chater.MessageCount}");
            Debug.WriteLine($"   - Karma: {chater.Karma}");
            Debug.WriteLine($"   - TotalKarma: {chater.TotalKarma}");

            // 5. Проверка порогов
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