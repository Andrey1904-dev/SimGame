// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Payments

namespace LifeFromZero.Debts.Payments
{
    /// <summary>Фаза незавершённой платёжной операции в журнале восстановления.</summary>
    internal enum DebtOperationPhase
    {
        /// <summary>Списание запрошено, исход неизвестен (нужен запрос TryWasDebited).</summary>
        DebitPending = 0,

        /// <summary>Списание подтверждено, осталось зачесть платёж в долг.</summary>
        DebitConfirmed = 1,

        /// <summary>Зачёт не завершился, требуется компенсация (возврат денег).</summary>
        RefundPending = 2
    }

    /// <summary>
    /// Запись журнала незавершённых операций. Пишется ДО списания и переживает
    /// сохранение/загрузку, чтобы «зависший» платёж можно было безопасно довести
    /// или компенсировать. Пока журнал непуст, новые платежи блокируются.
    /// </summary>
    internal sealed class DebtPendingOperation
    {
        public string PaymentId { get; }

        public string DebtId { get; }

        public long AmountRubles { get; }

        public DebtOperationPhase Phase { get; set; }

        public long CreatedAtMinutes { get; }

        public DebtPendingOperation(string paymentId, string debtId, long amountRubles, DebtOperationPhase phase, long createdAtMinutes)
        {
            PaymentId = paymentId;
            DebtId = debtId;
            AmountRubles = amountRubles;
            Phase = phase;
            CreatedAtMinutes = createdAtMinutes;
        }
    }
}
