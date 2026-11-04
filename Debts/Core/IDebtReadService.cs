// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

using System.Collections.Generic;
using LifeFromZero.Debts.Payments;

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Контракт только для чтения: список долгов, остатки, сроки, статусы, история платежей.
    /// Реализуется DebtService. Телефон подключается через адаптер к этому контракту
    /// и НЕ зависит от внутренностей модуля; сам модуль не ссылается на Phone/.
    /// </summary>
    public interface IDebtReadService
    {
        /// <summary>Все долги в порядке создания (включая закрытые).</summary>
        IReadOnlyList<DebtView> GetDebts();

        /// <summary>Представление одного долга; false — не найден.</summary>
        bool TryGetDebt(string debtId, out DebtView debt);

        /// <summary>История подтверждённых платежей долга (пусто, если долг не найден/без платежей).</summary>
        IReadOnlyList<DebtPaymentRecord> GetPaymentHistory(string debtId);

        /// <summary>Суммарная задолженность по открытым долгам; false при переполнении.</summary>
        bool TryGetTotalRemainingRubles(out long rubles);
    }
}
