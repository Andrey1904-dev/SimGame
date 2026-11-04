// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Penalties

using LifeFromZero.Debts.Core;

namespace LifeFromZero.Debts.Penalties
{
    /// <summary>
    /// Расчёт штрафов. Чистая функция без состояния: идемпотентность обеспечивает
    /// DebtService, храня индекс последнего начисленного периода для каждого долга.
    ///
    /// Периоды считаются от момента окончания льготного периода (overdueStart):
    /// период 1 — минуты [1 .. PeriodMinutes] просрочки, период 2 — следующие
    /// PeriodMinutes и т.д. Штраф начисляется один раз за каждый завершившийся
    /// «зашедший» период: при первом же обновлении внутри периода k начисляется
    /// штраф за период k (и за все пропущенные при скачке времени).
    /// </summary>
    public static class PenaltyCalculator
    {
        /// <summary>
        /// Индекс текущего периода просрочки (1-based). 0 — просрочки нет.
        /// </summary>
        public static long GetPeriodIndex(long overdueMinutes, long periodMinutes)
        {
            if (overdueMinutes <= 0L || periodMinutes <= 0L)
            {
                return 0L;
            }

            // (overdueMinutes - 1) / period + 1 — без переполнения при любых long.
            return (overdueMinutes - 1L) / periodMinutes + 1L;
        }

        /// <summary>
        /// Штраф за один период при данном остатке основного долга.
        /// false — переполнение при расчёте.
        /// </summary>
        public static bool TryGetPerPeriodAmount(PenaltyPolicy policy, long principalRemaining, out long amount)
        {
            amount = 0L;
            if (policy == null || !policy.Enabled)
            {
                return true;
            }

            if (!DebtMath.TryPercent(principalRemaining, policy.RateBasisPoints, out long percentPart))
            {
                return false;
            }

            return DebtMath.TryAdd(policy.FixedRubles, percentPart, out amount);
        }

        /// <summary>
        /// Сколько начислить при переходе от lastAccruedPeriod к текущему моменту.
        /// Учитывает максимум за всё время (MaxTotalRubles) и переполнение.
        /// </summary>
        /// <param name="policy">Политика (null/Disabled — начислений нет).</param>
        /// <param name="principalRemaining">Текущий остаток основного долга.</param>
        /// <param name="overdueMinutes">Минуты просрочки (после льготного периода), &gt;= 0.</param>
        /// <param name="lastAccruedPeriod">Индекс последнего начисленного периода (0 — не было).</param>
        /// <param name="accruedTotal">Уже начислено штрафов за всё время.</param>
        /// <param name="amount">Сколько начислить сейчас (может быть 0).</param>
        /// <param name="newLastAccruedPeriod">Новый индекс последнего начисленного периода.</param>
        /// <returns>false — переполнение, которое нельзя ограничить максимумом; начислять нельзя.</returns>
        public static bool TryAccrue(
            PenaltyPolicy policy,
            long principalRemaining,
            long overdueMinutes,
            long lastAccruedPeriod,
            long accruedTotal,
            out long amount,
            out long newLastAccruedPeriod)
        {
            amount = 0L;
            newLastAccruedPeriod = lastAccruedPeriod;

            if (policy == null || !policy.Enabled)
            {
                return true;
            }

            long currentPeriod = GetPeriodIndex(overdueMinutes, policy.PeriodMinutes);
            if (currentPeriod <= lastAccruedPeriod)
            {
                // Тот же период (или время не продвинулось) — повторного начисления нет.
                return true;
            }

            if (!TryGetPerPeriodAmount(policy, principalRemaining, out long perPeriod))
            {
                return false;
            }

            newLastAccruedPeriod = currentPeriod;

            if (perPeriod == 0L)
            {
                return true;
            }

            // Сколько ещё можно начислить до максимума за всё время.
            if (!DebtMath.TrySubtract(policy.MaxTotalRubles, accruedTotal, out long remainingCap))
            {
                // accruedTotal > MaxTotal — повреждённые данные; ничего не начисляем.
                newLastAccruedPeriod = lastAccruedPeriod;
                return false;
            }

            if (remainingCap == 0L)
            {
                return true;
            }

            long periodsToCharge = currentPeriod - lastAccruedPeriod; // оба > 0, разность безопасна

            // Минимум из (perPeriod * periodsToCharge) и remainingCap — без переполнения:
            // если произведение не помещается, оно заведомо больше remainingCap.
            if (periodsToCharge > remainingCap / perPeriod)
            {
                amount = remainingCap;
                return true;
            }

            amount = perPeriod * periodsToCharge;
            return true;
        }
    }
}
