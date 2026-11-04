// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Payments

namespace LifeFromZero.Debts.Payments
{
    /// <summary>
    /// Кошелёк игрока для платежей по долгам. Реализуется адаптером поверх
    /// существующего MoneyWallet; модуль НЕ содержит собственного кошелька.
    ///
    /// КРИТИЧЕСКИЙ контракт интеграции (см. INTEGRATION.md):
    /// 1. TryDebit идемпотентен по paymentId: повторный вызов с тем же paymentId
    ///    после успешного списания возвращает true и НЕ списывает деньги повторно.
    /// 2. TryDebit атомарен: либо списана вся сумма, либо ничего; при false баланс не изменён.
    /// 3. TryRefund идемпотентен по paymentId: возвращает списанное ровно один раз,
    ///    повторный вызов — true без повторного начисления.
    /// 4. TryWasDebited отвечает по ДОЛГОВРЕМЕННОМУ журналу адаптера (переживает
    ///    перезапуск игры): было ли реально списание по paymentId. Это единственный
    ///    способ безопасно восстановить «зависший» платёж после сбоя.
    /// 5. Адаптер не бросает исключений в штатных ситуациях; если бросил —
    ///    сервис считает исход операции неизвестным и восстанавливает его через TryWasDebited.
    /// </summary>
    public interface IDebtWalletAdapter
    {
        /// <summary>Чтение баланса в рублях. false — баланс недоступен (платёж не начнётся).</summary>
        bool TryGetBalance(out long rubles);

        /// <summary>Списание суммы платежа. Идемпотентно по paymentId (см. контракт выше).</summary>
        bool TryDebit(string paymentId, long rubles);

        /// <summary>Компенсация: возврат ранее списанной суммы. Идемпотентно по paymentId.</summary>
        bool TryRefund(string paymentId, long rubles);

        /// <summary>
        /// Было ли реально выполнено списание по paymentId (по журналу адаптера).
        /// false (возврат метода) — журнал недоступен, исход неизвестен.
        /// </summary>
        bool TryWasDebited(string paymentId, out bool debited);
    }
}
