// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Payments

namespace LifeFromZero.Debts.Payments
{
    /// <summary>
    /// Запрос платежа по долгу. paymentId задаётся вызывающей стороной и уникален
    /// в пределах сервиса долгов: повтор с тем же ID не списывает деньги второй раз.
    /// </summary>
    public readonly struct DebtPaymentRequest
    {
        public string DebtId { get; }

        /// <summary>Сумма платежа в целых рублях, строго больше нуля.</summary>
        public long AmountRubles { get; }

        /// <summary>Уникальный идентификатор платежа (идемпотентность).</summary>
        public string PaymentId { get; }

        public DebtPaymentRequest(string debtId, long amountRubles, string paymentId)
        {
            DebtId = debtId;
            AmountRubles = amountRubles;
            PaymentId = paymentId;
        }
    }
}
