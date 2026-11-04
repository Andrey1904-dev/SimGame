// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Стадия долга / взыскания. Модуль только сообщает о наступлении стадии;
    /// NPC-коллекторов, суда и угроз здесь нет.
    /// </summary>
    public enum DebtStatus
    {
        /// <summary>Долг обслуживается, просрочки нет.</summary>
        Current = 0,

        /// <summary>Просрочка после льготного периода.</summary>
        Overdue = 1,

        /// <summary>Напоминания кредитора.</summary>
        Reminder = 2,

        /// <summary>Усиленное взыскание.</summary>
        Collection = 3,

        /// <summary>Юридическая стадия.</summary>
        LegalAction = 4,

        /// <summary>Долг полностью погашен.</summary>
        Closed = 5
    }
}
