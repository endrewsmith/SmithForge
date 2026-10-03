using SmithForge.Main.Models;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services
{
    class KarmaTransferService
    {
        /// <summary>
        /// Регулярка для поиска упоминания KarmaKey.
        /// Поддерживает: #1234, id1234, ID1234, номер1234, Номер 1234, №1234
        /// </summary>
        private static readonly Regex KarmaKeyRegex = new Regex(
            @"(?:(?:#|id|ID|номер|Номер|№)\s*(\d+))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Найти Chater по упоминанию KarmaKey в тексте.
        /// Возвращает null, если упоминание не найдено или зритель не существует.
        /// </summary>
        public static Chater? FindChaterByMention(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var match = KarmaKeyRegex.Match(text);
            if (!match.Success)
            {
                Debug.WriteLine($"[KarmaTransfer] Упоминание KarmaKey не найдено в тексте");
                return null;
            }

            if (!int.TryParse(match.Groups[1].Value, out int karmaKey))
            {
                Debug.WriteLine($"[KarmaTransfer] Не удалось распарсить KarmaKey: '{match.Value}'");
                return null;
            }

            Debug.WriteLine($"[KarmaTransfer] Найден KarmaKey: #{karmaKey}");

            // Сначала пробуем через ChaterStorage (быстрее)
            var chater = ChaterStorage.GetAll()
                .Find(c => c.KarmaKey == karmaKey);

            if (chater == null)
            {
                // Fallback: пробуем из БД
                chater = DatabaseService.GetChaterByKarmaKey(karmaKey);
                if (chater != null)
                    ChaterStorage.AddOrUpdate(chater);
            }

            if (chater == null)
            {
                Debug.WriteLine($"[KarmaTransfer] Зритель с KarmaKey #{karmaKey} не найден");
                return null;
            }

            Debug.WriteLine($"[KarmaTransfer] ✅ Найден: {chater.EffectiveName} (#{chater.KarmaKey})");
            return chater;
        }

        /// <summary>
        /// Начислить карму зрителю. Возвращает true, если начисление прошло успешно.
        /// </summary>
        /// <param name="chater">Кому начисляем</param>
        /// <param name="amount">Сколько кармы (может быть отрицательным для списания)</param>
        /// <param name="reason">Причина для лога</param>
        public static bool GrantKarma(Chater chater, double amount, string reason = "")
        {
            if (chater == null) return false;

            if (amount == 0)
            {
                Debug.WriteLine($"[KarmaTransfer] Нулевая сумма, пропускаем");
                return false;
            }

            try
            {
                double oldKarma = chater.Karma;
                double oldTotal = chater.TotalKarma;

                chater.Karma += amount;
                chater.TotalKarma += amount;

                DatabaseService.UpdateChaterStats(chater);
                ChaterStorage.AddOrUpdate(chater);
                ChaterStorage.NotifyChaterUpdated(chater);

                Debug.WriteLine($"[KarmaTransfer] ✅ {chater.EffectiveName}: " +
                                $"карма {oldKarma:F1} → {chater.Karma:F1} " +
                                $"(+{amount:F1}, причина: {reason})");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KarmaTransfer] ❌ Ошибка начисления: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удобный метод: найти по тексту И начислить.
        /// </summary>
        public static bool TryGrantKarmaByMention(string text, double amount, string reason = "")
        {
            var chater = FindChaterByMention(text);
            if (chater == null) return false;

            return GrantKarma(chater, amount, reason);
        }

        /// <summary>
        /// Найти Chater по числовому KarmaKey (без префикса).
        /// </summary>
        public static Chater? FindChaterByKarmaKey(int karmaKey)
        {
            if (karmaKey <= 0) return null;

            var chater = ChaterStorage.GetAll()
                .Find(c => c.KarmaKey == karmaKey);

            if (chater == null)
            {
                chater = DatabaseService.GetChaterByKarmaKey(karmaKey);
                if (chater != null)
                    ChaterStorage.AddOrUpdate(chater);
            }

            return chater;
        }
    }
}