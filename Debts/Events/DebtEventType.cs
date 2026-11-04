// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Events

namespace LifeFromZero.Debts.Events
{
    /// <summary>Типы событий долгов для внешних систем (телефон, сюжет).</summary>
    public enum DebtEventType
    {
        /// <summary>Долг создан.</summary>
        DebtCreated = 0,

        /// <summary>Платёж подтверждён и зачтён.</summary>
        PaymentCompleted = 1,

        /// <summary>Платёж отклонён (код причины — в событии).</summary>
        PaymentRejected = 2,

        /// <summary>Наступил срок платежа (одноразовое на долг).</summary>
        PaymentDue = 3,

        /// <summary>Началась просрочка после льготного периода (одноразовое на долг).</summary>
        DebtOverdue = 4,

        /// <summary>Начислен штраф (одно событие на обновление времени с суммой начисления).</summary>
        PenaltyApplied = 5,

        /// <summary>Стадия взыскания изменилась.</summary>
        CollectionStageChanged = 6,

        /// <summary>Долг полностью погашен (одноразовое на долг).</summary>
        DebtClosed = 7
    }
}
