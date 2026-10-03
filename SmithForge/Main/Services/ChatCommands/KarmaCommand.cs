using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SmithForge.Main.Services.ChatCommands
{
    class KarmaCommand : BaseCommand
    {
        /// <summary>
        /// Комиссия за перевод (доля от суммы).
        /// 0.10 = 10%. Сгорает у получателя, не идёт никому.
        /// </summary>
        private const double TRANSFER_FEE_RATE = 0.10;

        /// <summary>
        /// Минимальная сумма перевода. Отсекает бессмысленные микропереводы.
        /// </summary>
        private const double MIN_TRANSFER_AMOUNT = 10.0;

        public override string Name => "karma";
        public override IEnumerable<string> Aliases => new[] { "карма", "перевод", "перевести" };
        public override string Description => "Перевести карму другому зрителю: !!karma:KarmaKey:сколько (комиссия 10%, минимум 10)";
        public override int Cost => 0;      // сама команда бесплатна
        public override int MinRank => 1;
        public override int[] FreeForRanks => Array.Empty<int>();

        public override bool IsTechnical => true;

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            Debug.WriteLine($"[KarmaCommand] ========== НАЧАЛО ==========");
            Debug.WriteLine($"[KarmaCommand] От: {chater.EffectiveName} (#{chater.KarmaKey}), карма: {chater.Karma:F1}");

            msg.Message = string.Empty;
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = false;

            // ── 1. Парсим аргументы ─────────────────────────────────
            if (info.Arguments.Count < 2)
            {
                Debug.WriteLine("[KarmaCommand] Недостаточно аргументов");
                return;
            }

            if (!int.TryParse(info.Arguments[0], out int targetKarmaKey) || targetKarmaKey <= 0)
            {
                Debug.WriteLine($"[KarmaCommand] Неверный KarmaKey: '{info.Arguments[0]}'");
                return;
            }

            if (!double.TryParse(
                    info.Arguments[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double amount)
                || amount <= 0)
            {
                Debug.WriteLine($"[KarmaCommand] Неверная сумма: '{info.Arguments[1]}'");
                return;
            }

            if (amount < MIN_TRANSFER_AMOUNT)
            {
                Debug.WriteLine($"[KarmaCommand] Сумма {amount:F2} меньше минимума {MIN_TRANSFER_AMOUNT}");
                return;
            }

            // ── 2. Считаем комиссию ─────────────────────────────────
            // Без округления — карма и так double, дробные значения поддерживаются.
            double fee = amount * TRANSFER_FEE_RATE;
            double received = amount - fee;

            Debug.WriteLine($"[KarmaCommand] Сумма: {amount:F2}, комиссия (10%): {fee:F2}, получателю: {received:F2}");

            if (received <= 0)
            {
                Debug.WriteLine($"[KarmaCommand] После комиссии получателю уходит {received:F2} — перевод отклонён");
                return;
            }

            // ── 3. Ищем получателя ─────────────────────────────────
            var target = KarmaTransferService.FindChaterByKarmaKey(targetKarmaKey);
            if (target == null)
            {
                Debug.WriteLine($"[KarmaCommand] Получатель #{targetKarmaKey} не найден");
                return;
            }

            if (target.Id == chater.Id)
            {
                Debug.WriteLine("[KarmaCommand] Попытка перевести самому себе — игнорируем");
                return;
            }

            if (chater.Karma < amount)
            {
                Debug.WriteLine($"[KarmaCommand] Недостаточно кармы: у {chater.EffectiveName} {chater.Karma:F1}, нужно {amount:F1}");
                return;
            }

            // ── 4. Переводим ───────────────────────────────────────
            Debug.WriteLine($"[KarmaCommand] Перевод {amount:F2} кармы (получателю {received:F2}): {chater.EffectiveName} → {target.EffectiveName}");

            bool debited = KarmaTransferService.GrantKarma(
                chater,
                -amount,
                reason: $"перевод → {target.EffectiveName} (#{target.KarmaKey}), комиссия {fee:F2}");

            if (!debited)
            {
                Debug.WriteLine("[KarmaCommand] Не удалось списать карму у отправителя");
                return;
            }

            bool credited = KarmaTransferService.GrantKarma(
                target,
                received,
                reason: $"перевод от {chater.EffectiveName} (#{chater.KarmaKey})");

            if (!credited)
            {
                Debug.WriteLine("[KarmaCommand] Не удалось начислить получателю, откатываем");
                KarmaTransferService.GrantKarma(
                    chater,
                    amount,
                    reason: "откат неудачного перевода");
                return;
            }

            // ── 5. Тех-событие ─────────────────────────────────────
            WebServerService.Instance?.SendTechnicalEvent(
                TechEventFactory.KarmaTransfer(chater, target, received, fee));

            Debug.WriteLine($"[KarmaCommand] ✅ Перевод выполнен: +{received:F2} → {target.EffectiveName} (комиссия {fee:F2})");
            Debug.WriteLine($"[KarmaCommand] ========== КОНЕЦ ==========");
        }
    }
}