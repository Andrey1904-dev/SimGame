// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Penalties

using System;

namespace LifeFromZero.Debts.Penalties
{
    /// <summary>
    /// Политика штрафов за просрочку. Настраивается для каждого долга отдельно;
    /// <see cref="Disabled"/> полностью отключает штрафы.
    /// Штраф за период = FixedRubles + остаток основного долга * RateBasisPoints / 10000.
    /// Суммарные начисленные штрафы за всё время ограничены MaxTotalRubles.
    /// </summary>
    public sealed class PenaltyPolicy
    {
        /// <summary>Штрафы отключены (общий неизменяемый экземпляр).</summary>
        public static PenaltyPolicy Disabled { get; } = new PenaltyPolicy(false, 0L, 0L, 0, 0L);

        /// <summary>Включены ли штрафы для долга.</summary>
        public bool Enabled { get; }

        /// <summary>Длительность расчётного периода в игровых минутах (&gt; 0, если включено).</summary>
        public long PeriodMinutes { get; }

        /// <summary>Фиксированная часть штрафа за период, рубли (&gt;= 0).</summary>
        public long FixedRubles { get; }

        /// <summary>Процентная часть штрафа за период в базисных пунктах (100 = 1%), &gt;= 0.</summary>
        public int RateBasisPoints { get; }

        /// <summary>Максимум штрафов за всё время жизни долга, рубли (&gt;= 0).</summary>
        public long MaxTotalRubles { get; }

        public PenaltyPolicy(bool enabled, long periodMinutes, long fixedRubles, int rateBasisPoints, long maxTotalRubles)
        {
            if (enabled)
            {
                if (periodMinutes <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(periodMinutes), "Период штрафа должен быть положительным.");
                }

                if (fixedRubles < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(fixedRubles), "Фиксированный штраф не может быть отрицательным.");
                }

                if (rateBasisPoints < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(rateBasisPoints), "Ставка штрафа не может быть отрицательной.");
                }

                if (fixedRubles == 0L && rateBasisPoints == 0)
                {
                    throw new ArgumentException("Включённая политика штрафов должна начислять хотя бы что-то (фикс или ставку).");
                }

                if (maxTotalRubles < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxTotalRubles), "Максимум штрафов не может быть отрицательным.");
                }
            }

            Enabled = enabled;
            PeriodMinutes = enabled ? periodMinutes : 0L;
            FixedRubles = enabled ? fixedRubles : 0L;
            RateBasisPoints = enabled ? rateBasisPoints : 0;
            MaxTotalRubles = enabled ? maxTotalRubles : 0L;
        }

        /// <summary>Фиксированный штраф за период с ограничением суммы.</summary>
        public static PenaltyPolicy FixedPerPeriod(long periodMinutes, long fixedRubles, long maxTotalRubles)
        {
            return new PenaltyPolicy(true, periodMinutes, fixedRubles, 0, maxTotalRubles);
        }
    }
}
