// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Payments

namespace LifeFromZero.Debts.Payments
{
    /// <summary>Запись истории: подтверждённый платёж. Неизменяемая.</summary>
    public sealed class DebtPaymentRecord
    {
        public string PaymentId { get; }

        public string DebtId { get; }

        public long AmountRubles { get; }

        public long PenaltiesPartRubles { get; }

        public long PrincipalPartRubles { get; }

        /// <summary>Игровое время подтверждения платежа, минуты.</summary>
        public long AtMinutes { get; }

        public DebtPaymentRecord(
            string paymentId,
            string debtId,
            long amountRubles,
            long penaltiesPartRubles,
            long principalPartRubles,
            long atMinutes)
        {
            PaymentId = paymentId;
            DebtId = debtId;
            AmountRubles = amountRubles;
            PenaltiesPartRubles = penaltiesPartRubles;
            PrincipalPartRubles = principalPartRubles;
            AtMinutes = atMinutes;
        }
    }
}
