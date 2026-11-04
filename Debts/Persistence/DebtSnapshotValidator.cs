// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Persistence
//
// Полная проверка снимка ДО применения. Возвращает false и описание первой ошибки;
// рабочее состояние сервиса при этом не меняется. Проверяются ссылки, уникальность
// ID, неотрицательность сумм, финансовые инварианты и согласованность истории платежей.

using System.Collections.Generic;
using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Payments;

namespace LifeFromZero.Debts.Persistence
{
    /// <summary>Валидатор снимков состояния долгов.</summary>
    public static class DebtSnapshotValidator
    {
        public static bool Validate(DebtSnapshot snapshot, out string error)
        {
            error = null;

            if (snapshot == null)
            {
                error = "Снимок не передан (null).";
                return false;
            }

            if (snapshot.SchemaVersion != DebtSnapshot.CurrentSchemaVersion)
            {
                error = "Неподдерживаемая версия схемы снимка: " + snapshot.SchemaVersion;
                return false;
            }

            if (snapshot.Creditors == null || snapshot.Debts == null
                || snapshot.Payments == null || snapshot.PendingOperations == null)
            {
                error = "Один из массивов снимка равен null.";
                return false;
            }

            if (snapshot.LastNowMinutes < 0L)
            {
                error = "Отрицательное игровое время в снимке.";
                return false;
            }

            // --- Кредиторы ---
            var creditorIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < snapshot.Creditors.Length; i++)
            {
                CreditorSnapshot c = snapshot.Creditors[i];
                if (c == null || string.IsNullOrWhiteSpace(c.Id))
                {
                    error = "Кредитор #" + i + ": пустой ID.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(c.Name))
                {
                    error = "Кредитор " + c.Id + ": пустое имя.";
                    return false;
                }

                if (!creditorIds.Add(c.Id))
                {
                    error = "Дублирующийся ID кредитора: " + c.Id;
                    return false;
                }
            }

            // --- Долги ---
            var debts = new Dictionary<string, DebtEntrySnapshot>(System.StringComparer.Ordinal);
            for (int i = 0; i < snapshot.Debts.Length; i++)
            {
                DebtEntrySnapshot d = snapshot.Debts[i];
                if (d == null || string.IsNullOrWhiteSpace(d.Id))
                {
                    error = "Долг #" + i + ": пустой ID.";
                    return false;
                }

                if (!debts.ContainsKey(d.Id))
                {
                    debts.Add(d.Id, d);
                }
                else
                {
                    error = "Дублирующийся ID долга: " + d.Id;
                    return false;
                }

                if (!creditorIds.Contains(d.CreditorId))
                {
                    error = "Долг " + d.Id + ": неизвестный кредитор " + d.CreditorId;
                    return false;
                }

                if (!ValidateDebtNumbers(d, out error))
                {
                    return false;
                }
            }

            // --- Платежи ---
            var paymentIds = new HashSet<string>(System.StringComparer.Ordinal);
            var sumAmount = new Dictionary<string, long>(System.StringComparer.Ordinal);
            var sumPrincipal = new Dictionary<string, long>(System.StringComparer.Ordinal);
            var sumPenalties = new Dictionary<string, long>(System.StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Payments.Length; i++)
            {
                PaymentRecordSnapshot p = snapshot.Payments[i];
                if (p == null || string.IsNullOrWhiteSpace(p.PaymentId))
                {
                    error = "Платёж #" + i + ": пустой paymentId.";
                    return false;
                }

                if (!paymentIds.Add(p.PaymentId))
                {
                    error = "Дублирующийся paymentId в истории: " + p.PaymentId;
                    return false;
                }

                if (!debts.ContainsKey(p.DebtId))
                {
                    error = "Платёж " + p.PaymentId + ": неизвестный долг " + p.DebtId;
                    return false;
                }

                if (p.AmountRubles <= 0L || p.PenaltiesPartRubles < 0L || p.PrincipalPartRubles < 0L)
                {
                    error = "Платёж " + p.PaymentId + ": некорректные суммы.";
                    return false;
                }

                if (!DebtMath.TryAdd(p.PenaltiesPartRubles, p.PrincipalPartRubles, out long partsSum)
                    || partsSum != p.AmountRubles)
                {
                    error = "Платёж " + p.PaymentId + ": части не равны сумме.";
                    return false;
                }

                if (p.AtMinutes < 0L)
                {
                    error = "Платёж " + p.PaymentId + ": отрицательное время.";
                    return false;
                }

                if (!Accumulate(sumAmount, p.DebtId, p.AmountRubles)
                    || !Accumulate(sumPrincipal, p.DebtId, p.PrincipalPartRubles)
                    || !Accumulate(sumPenalties, p.DebtId, p.PenaltiesPartRubles))
                {
                    error = "Платёж " + p.PaymentId + ": переполнение суммы платежей долга.";
                    return false;
                }
            }

            // Согласованность истории с остатками каждого долга.
            foreach (KeyValuePair<string, DebtEntrySnapshot> pair in debts)
            {
                DebtEntrySnapshot d = pair.Value;
                long paidAmount = GetOrZero(sumAmount, d.Id);
                long paidPrincipal = GetOrZero(sumPrincipal, d.Id);
                long paidPenalties = GetOrZero(sumPenalties, d.Id);

                if (paidAmount != d.PaidTotal)
                {
                    error = "Долг " + d.Id + ": сумма платежей не равна PaidTotal.";
                    return false;
                }

                if (paidPrincipal != d.PrincipalOriginal - d.PrincipalRemaining)
                {
                    error = "Долг " + d.Id + ": выплаченный основной долг не согласован с остатком.";
                    return false;
                }

                if (paidPenalties != d.PenaltiesAccrued - d.PenaltiesRemaining)
                {
                    error = "Долг " + d.Id + ": выплаченные штрафы не согласованы с остатком.";
                    return false;
                }
            }

            // --- Незавершённые операции ---
            var pendingPaymentIds = new HashSet<string>(System.StringComparer.Ordinal);
            var pendingDebtIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < snapshot.PendingOperations.Length; i++)
            {
                PendingOperationSnapshot po = snapshot.PendingOperations[i];
                if (po == null || string.IsNullOrWhiteSpace(po.PaymentId))
                {
                    error = "Незавершённая операция #" + i + ": пустой paymentId.";
                    return false;
                }

                if (!pendingPaymentIds.Add(po.PaymentId))
                {
                    error = "Дублирующийся paymentId в журнале: " + po.PaymentId;
                    return false;
                }

                if (paymentIds.Contains(po.PaymentId))
                {
                    error = "paymentId одновременно в истории и журнале: " + po.PaymentId;
                    return false;
                }

                if (!debts.TryGetValue(po.DebtId, out DebtEntrySnapshot pendingDebt))
                {
                    error = "Незавершённая операция " + po.PaymentId + ": неизвестный долг " + po.DebtId;
                    return false;
                }

                if (!pendingDebtIds.Add(po.DebtId))
                {
                    error = "Более одной незавершённой операции на долг: " + po.DebtId;
                    return false;
                }

                if (po.AmountRubles <= 0L || po.CreatedAtMinutes < 0L)
                {
                    error = "Незавершённая операция " + po.PaymentId + ": некорректные данные.";
                    return false;
                }

                if (po.Phase < 0 || po.Phase > 2)
                {
                    error = "Незавершённая операция " + po.PaymentId + ": неизвестная фаза " + po.Phase;
                    return false;
                }

                long totalRemaining = pendingDebt.PrincipalRemaining + pendingDebt.PenaltiesRemaining;

                // Проверка «сумма не больше задолженности» осмысленна только для фазы
                // DebitPending: там сумма ещё не списана и была сверена с остатком.
                // Для DebitConfirmed/RefundPending деньги уже ушли из кошелька — сумма
                // стала фактом, а долг за это время мог уменьшиться (например, долг
                // закрыли, пока списание было «в полёте»). Такой снимок сервис может
                // создать сам, и отклонять его нельзя: вместе с ним терялась бы
                // единственная запись о деньгах, которые надо вернуть.
                if (po.Phase == (int)DebtOperationPhase.DebitPending && po.AmountRubles > totalRemaining)
                {
                    error = "Незавершённая операция " + po.PaymentId + ": сумма превышает задолженность.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateDebtNumbers(DebtEntrySnapshot d, out string error)
        {
            error = null;

            if (d.PrincipalOriginal <= 0L
                || d.PrincipalRemaining < 0L
                || d.PenaltiesAccrued < 0L
                || d.PenaltiesRemaining < 0L
                || d.PaidTotal < 0L)
            {
                error = "Долг " + d.Id + ": некорректные денежные значения.";
                return false;
            }

            if (d.PrincipalRemaining > d.PrincipalOriginal)
            {
                error = "Долг " + d.Id + ": остаток основного долга больше исходного.";
                return false;
            }

            if (d.PenaltiesRemaining > d.PenaltiesAccrued)
            {
                error = "Долг " + d.Id + ": остаток штрафов больше начисленных.";
                return false;
            }

            // PaidTotal == выплаченный основной + выплаченные штрафы.
            long paidPrincipal = d.PrincipalOriginal - d.PrincipalRemaining;
            long paidPenalties = d.PenaltiesAccrued - d.PenaltiesRemaining;
            if (!DebtMath.TryAdd(paidPrincipal, paidPenalties, out long expectedPaid) || expectedPaid != d.PaidTotal)
            {
                error = "Долг " + d.Id + ": нарушен инвариант выплаченной суммы.";
                return false;
            }

            if (d.DueAtMinutes < 0L || d.GracePeriodMinutes < 0L || d.CreatedAtMinutes < 0L)
            {
                error = "Долг " + d.Id + ": отрицательные сроки.";
                return false;
            }

            if (!DebtMath.TryAdd(d.DueAtMinutes, d.GracePeriodMinutes, out _))
            {
                error = "Долг " + d.Id + ": срок + льготный период переполняет long.";
                return false;
            }

            if (d.InterestRateBasisPoints < 0)
            {
                error = "Долг " + d.Id + ": отрицательная ставка.";
                return false;
            }

            if (d.Status < (int)DebtStatus.Current || d.Status > (int)DebtStatus.Closed)
            {
                error = "Долг " + d.Id + ": неизвестная стадия " + d.Status;
                return false;
            }

            long totalRemaining = d.PrincipalRemaining + d.PenaltiesRemaining;
            bool closed = d.Status == (int)DebtStatus.Closed;
            if (closed && (totalRemaining != 0L || d.ClosedAtMinutes < 0L))
            {
                error = "Долг " + d.Id + ": закрыт, но остаток не нулевой или нет времени закрытия.";
                return false;
            }

            if (!closed && (totalRemaining == 0L || d.ClosedAtMinutes != -1L))
            {
                error = "Долг " + d.Id + ": не закрыт, но остаток нулевой или задано время закрытия.";
                return false;
            }

            if (d.LastPenaltyPeriodIndex < 0L)
            {
                error = "Долг " + d.Id + ": отрицательный индекс периода штрафа.";
                return false;
            }

            if (d.PenaltyEnabled)
            {
                if (d.PenaltyPeriodMinutes <= 0L
                    || d.PenaltyFixedRubles < 0L
                    || d.PenaltyRateBasisPoints < 0
                    || d.PenaltyMaxTotalRubles < 0L)
                {
                    error = "Долг " + d.Id + ": некорректная политика штрафов.";
                    return false;
                }

                if (d.PenaltyFixedRubles == 0L && d.PenaltyRateBasisPoints == 0)
                {
                    error = "Долг " + d.Id + ": включённые штрафы ничего не начисляют.";
                    return false;
                }

                if (d.PenaltiesAccrued > d.PenaltyMaxTotalRubles)
                {
                    error = "Долг " + d.Id + ": начисленные штрафы превышают максимум.";
                    return false;
                }

                if (!DebtMath.TryAdd(d.PrincipalOriginal, d.PenaltyMaxTotalRubles, out _))
                {
                    error = "Долг " + d.Id + ": основной долг + максимум штрафов переполняет long.";
                    return false;
                }
            }
            else
            {
                if (d.PenaltiesAccrued != 0L || d.PenaltiesRemaining != 0L)
                {
                    error = "Долг " + d.Id + ": штрафы отключены, но есть начисления.";
                    return false;
                }
            }

            if (d.ReminderAfterMinutes <= 0L
                || d.CollectionAfterMinutes < d.ReminderAfterMinutes
                || d.LegalActionAfterMinutes < d.CollectionAfterMinutes)
            {
                error = "Долг " + d.Id + ": некорректные пороги стадий взыскания.";
                return false;
            }

            return true;
        }

        private static bool Accumulate(Dictionary<string, long> map, string key, long value)
        {
            long current = GetOrZero(map, key);
            if (!DebtMath.TryAdd(current, value, out long next))
            {
                return false;
            }

            map[key] = next;
            return true;
        }

        private static long GetOrZero(Dictionary<string, long> map, string key)
        {
            return map.TryGetValue(key, out long value) ? value : 0L;
        }
    }
}
