// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core
// Unity 6000.3 / C# 9. Без Unity API и сторонних пакетов.

using LifeFromZero.Debts.Penalties;

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Долг игрока. Публичные getters, мутация только внутри сборки (internal set),
    /// поэтому внешние системы (телефон, сюжет) читают, но не меняют состояние.
    /// Инварианты (проверяются при создании и в валидаторе снимков):
    ///  - PrincipalRemaining &lt;= PrincipalOriginal; PenaltiesRemaining &lt;= PenaltiesAccrued;
    ///  - PaidTotal == (PrincipalOriginal - PrincipalRemaining) + (PenaltiesAccrued - PenaltiesRemaining);
    ///  - PrincipalOriginal + Penalties.MaxTotalRubles не переполняет long,
    ///    поэтому TotalRemaining и PaidTotal никогда не переполняются;
    ///  - Closed ⟺ TotalRemaining == 0.
    /// </summary>
    public sealed class Debt
    {
        public string Id { get; }

        public string CreditorId { get; }

        public string Title { get; internal set; }

        /// <summary>Основной долг при создании (включая разовую процентную надбавку).</summary>
        public long PrincipalOriginal { get; }

        public long PrincipalRemaining { get; internal set; }

        /// <summary>Штрафов начислено за всё время (для ограничения максимумом).</summary>
        public long PenaltiesAccrued { get; internal set; }

        /// <summary>Непогашенные штрафы.</summary>
        public long PenaltiesRemaining { get; internal set; }

        /// <summary>Всего выплачено (основной долг + штрафы).</summary>
        public long PaidTotal { get; internal set; }

        public long DueAtMinutes { get; }

        public long GracePeriodMinutes { get; }

        public long CreatedAtMinutes { get; internal set; }

        /// <summary>Момент закрытия; -1 пока долг открыт.</summary>
        public long ClosedAtMinutes { get; internal set; }

        public DebtStatus Status { get; internal set; }

        public int InterestRateBasisPoints { get; }

        public PenaltyPolicy Penalties { get; }

        public CollectionThresholds Collection { get; }

        /// <summary>Индекс последнего начисленного периода штрафа (0 — не было).</summary>
        public long LastPenaltyPeriodIndex { get; internal set; }

        /// <summary>Одноразовое событие PaymentDue уже отправлено.</summary>
        public bool DueEventFired { get; internal set; }

        /// <summary>Одноразовое событие DebtOverdue уже отправлено.</summary>
        public bool OverdueEventFired { get; internal set; }

        /// <summary>Общая задолженность (основной долг + штрафы). Переполнение исключено инвариантом.</summary>
        public long TotalRemaining => PrincipalRemaining + PenaltiesRemaining;

        public bool IsClosed => Status == DebtStatus.Closed;

        /// <summary>Момент начала просрочки (окончание льготного периода).</summary>
        public long OverdueStartMinutes => DueAtMinutes + GracePeriodMinutes;

        internal Debt(
            string id,
            string creditorId,
            string title,
            long principalOriginal,
            long dueAtMinutes,
            long gracePeriodMinutes,
            long createdAtMinutes,
            int interestRateBasisPoints,
            PenaltyPolicy penalties,
            CollectionThresholds collection)
        {
            Id = id;
            CreditorId = creditorId;
            Title = title ?? string.Empty;
            PrincipalOriginal = principalOriginal;
            PrincipalRemaining = principalOriginal;
            PenaltiesAccrued = 0L;
            PenaltiesRemaining = 0L;
            PaidTotal = 0L;
            DueAtMinutes = dueAtMinutes;
            GracePeriodMinutes = gracePeriodMinutes;
            CreatedAtMinutes = createdAtMinutes;
            ClosedAtMinutes = -1L;
            Status = DebtStatus.Current;
            InterestRateBasisPoints = interestRateBasisPoints;
            Penalties = penalties ?? PenaltyPolicy.Disabled;
            Collection = collection ?? CollectionThresholds.CreateDefault();
            LastPenaltyPeriodIndex = 0L;
            DueEventFired = false;
            OverdueEventFired = false;
        }
    }
}
