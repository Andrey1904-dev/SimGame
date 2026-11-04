// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Events

using LifeFromZero.Debts.Core;

namespace LifeFromZero.Debts.Events
{
    /// <summary>
    /// Событие модуля долгов. Неизменяемые данные без ссылок на внутреннее состояние:
    /// обработчик не может испортить долг через событие.
    /// </summary>
    public sealed class DebtEvent
    {
        public DebtEventType Type { get; }

        public string DebtId { get; }

        public string CreditorId { get; }

        /// <summary>ID платежа для PaymentCompleted/PaymentRejected, иначе null.</summary>
        public string PaymentId { get; }

        /// <summary>Сумма события в рублях (платёж/штраф/остаток), 0 если неприменимо.</summary>
        public long AmountRubles { get; }

        /// <summary>Игровая временная метка события, минуты.</summary>
        public long AtMinutes { get; }

        /// <summary>Стадия долга на момент события.</summary>
        public DebtStatus Status { get; }

        /// <summary>Предыдущая стадия (для CollectionStageChanged), иначе Current.</summary>
        public DebtStatus PreviousStatus { get; }

        /// <summary>Код причины (для PaymentRejected), иначе Success.</summary>
        public DebtResultCode ReasonCode { get; }

        public DebtEvent(
            DebtEventType type,
            string debtId,
            string creditorId,
            string paymentId,
            long amountRubles,
            long atMinutes,
            DebtStatus status,
            DebtStatus previousStatus,
            DebtResultCode reasonCode)
        {
            Type = type;
            DebtId = debtId;
            CreditorId = creditorId;
            PaymentId = paymentId;
            AmountRubles = amountRubles;
            AtMinutes = atMinutes;
            Status = status;
            PreviousStatus = previousStatus;
            ReasonCode = reasonCode;
        }

        public override string ToString()
        {
            return Type + " (debt=" + (DebtId ?? "-") + ", at=" + AtMinutes + ", amount=" + AmountRubles + ")";
        }
    }
}
