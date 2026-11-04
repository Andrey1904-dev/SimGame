// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Неизменяемое представление долга для внешних систем (телефон, UI, сюжет).
    /// Защитная копия: не ссылается на внутренний изменяемый <see cref="Debt"/>.
    /// </summary>
    public sealed class DebtView
    {
        public string Id { get; }

        public string CreditorId { get; }

        public string CreditorName { get; }

        public string Title { get; }

        public long PrincipalOriginal { get; }

        public long PrincipalRemaining { get; }

        public long PenaltiesRemaining { get; }

        public long PenaltiesAccrued { get; }

        public long TotalRemaining { get; }

        public long PaidTotal { get; }

        public long DueAtMinutes { get; }

        public long GracePeriodMinutes { get; }

        public long CreatedAtMinutes { get; }

        public long ClosedAtMinutes { get; }

        public DebtStatus Status { get; }

        public int InterestRateBasisPoints { get; }

        public bool PenaltiesEnabled { get; }

        public DebtView(
            string id,
            string creditorId,
            string creditorName,
            string title,
            long principalOriginal,
            long principalRemaining,
            long penaltiesRemaining,
            long penaltiesAccrued,
            long totalRemaining,
            long paidTotal,
            long dueAtMinutes,
            long gracePeriodMinutes,
            long createdAtMinutes,
            long closedAtMinutes,
            DebtStatus status,
            int interestRateBasisPoints,
            bool penaltiesEnabled)
        {
            Id = id;
            CreditorId = creditorId;
            CreditorName = creditorName;
            Title = title;
            PrincipalOriginal = principalOriginal;
            PrincipalRemaining = principalRemaining;
            PenaltiesRemaining = penaltiesRemaining;
            PenaltiesAccrued = penaltiesAccrued;
            TotalRemaining = totalRemaining;
            PaidTotal = paidTotal;
            DueAtMinutes = dueAtMinutes;
            GracePeriodMinutes = gracePeriodMinutes;
            CreatedAtMinutes = createdAtMinutes;
            ClosedAtMinutes = closedAtMinutes;
            Status = status;
            InterestRateBasisPoints = interestRateBasisPoints;
            PenaltiesEnabled = penaltiesEnabled;
        }
    }
}
