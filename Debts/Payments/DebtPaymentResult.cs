// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Payments

using LifeFromZero.Debts.Core;

namespace LifeFromZero.Debts.Payments
{
    /// <summary>Результат платежа: код, распределение суммы и остаток после платежа.</summary>
    public readonly struct DebtPaymentResult
    {
        public DebtResultCode Code { get; }

        public string DebtId { get; }

        public string PaymentId { get; }

        /// <summary>Сколько списано и зачтено (0 при отказе).</summary>
        public long PaidRubles { get; }

        /// <summary>Из них погашено штрафов.</summary>
        public long PaidPenaltiesRubles { get; }

        /// <summary>Из них погашено основного долга.</summary>
        public long PaidPrincipalRubles { get; }

        /// <summary>Остаток общей задолженности после платежа (при отказе — до).</summary>
        public long RemainingRubles { get; }

        /// <summary>Диагностика для лога, не для UI.</summary>
        public string Message { get; }

        public bool IsSuccess => Code == DebtResultCode.Success;

        public DebtPaymentResult(
            DebtResultCode code,
            string debtId,
            string paymentId,
            long paidRubles,
            long paidPenaltiesRubles,
            long paidPrincipalRubles,
            long remainingRubles,
            string message)
        {
            Code = code;
            DebtId = debtId;
            PaymentId = paymentId;
            PaidRubles = paidRubles;
            PaidPenaltiesRubles = paidPenaltiesRubles;
            PaidPrincipalRubles = paidPrincipalRubles;
            RemainingRubles = remainingRubles;
            Message = message;
        }

        public static DebtPaymentResult Fail(DebtResultCode code, string debtId, string paymentId, long remaining, string message)
        {
            return new DebtPaymentResult(code, debtId, paymentId, 0L, 0L, 0L, remaining < 0L ? 0L : remaining, message);
        }

        public override string ToString()
        {
            return Code + " (debt=" + (DebtId ?? "-") + ", payment=" + (PaymentId ?? "-")
                + ", paid=" + PaidRubles + ", remaining=" + RemainingRubles + ")";
        }
    }
}
