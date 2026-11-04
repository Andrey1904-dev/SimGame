// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core
//
// Пример стартового долга игры (38 400 ₽) как ДАННЫЕ, а не логика DebtService.
// Имя кредитора, ставка и срок настраиваются параметрами; обязательных сюжетных
// фактов модуль не задаёт. Наличные (1 270 ₽) и квартира №67 — зона других систем
// (кошелёк, жильё), модуль долгов их не хранит.

using LifeFromZero.Debts.Penalties;

namespace LifeFromZero.Debts.Core
{
    /// <summary>Кредитор и конфигурация стартового долга для передачи в DebtService.</summary>
    public sealed class StartingDebtScenario
    {
        public Creditor Creditor { get; }

        public DebtConfiguration Debt { get; }

        public StartingDebtScenario(Creditor creditor, DebtConfiguration debt)
        {
            Creditor = creditor;
            Debt = debt;
        }
    }

    /// <summary>
    /// Фабрика стартового долга. Сумма 38 400 ₽ — стартовое условие игры (ТЗ №016),
    /// вынесена в данные и может быть переопределена параметром.
    /// </summary>
    public static class StartingDebtFactory
    {
        /// <summary>Стартовый общий долг игрока, рубли.</summary>
        public const long StartingDebtRubles = 38400L;

        /// <summary>
        /// Создаёт сценарий стартового долга. Все сюжетно-значимые поля настраиваются.
        /// </summary>
        /// <param name="creditorId">ID кредитора (данные).</param>
        /// <param name="creditorName">Отображаемое имя кредитора (данные).</param>
        /// <param name="dueAtMinutes">Срок платежа в игровых минутах.</param>
        /// <param name="interestRateBasisPoints">Разовая процентная надбавка, б.п. (0 — без надбавки).</param>
        /// <param name="gracePeriodMinutes">Льготный период после срока, минуты.</param>
        /// <param name="penalties">Политика штрафов (null — отключены).</param>
        /// <param name="collection">Пороги стадий взыскания (null — по умолчанию).</param>
        /// <param name="principalRubles">Основная сумма (по умолчанию 38 400 ₽).</param>
        /// <param name="debtId">ID долга.</param>
        /// <param name="title">Отображаемое название долга.</param>
        public static StartingDebtScenario Create(
            string creditorId,
            string creditorName,
            long dueAtMinutes,
            int interestRateBasisPoints = 0,
            long gracePeriodMinutes = 0L,
            PenaltyPolicy penalties = null,
            CollectionThresholds collection = null,
            long principalRubles = StartingDebtRubles,
            string debtId = "starting-debt",
            string title = "Стартовый долг")
        {
            var creditor = new Creditor(creditorId, creditorName);
            var config = new DebtConfiguration
            {
                DebtId = debtId,
                CreditorId = creditorId,
                Title = title,
                PrincipalRubles = principalRubles,
                InterestRateBasisPoints = interestRateBasisPoints,
                DueAtMinutes = dueAtMinutes,
                GracePeriodMinutes = gracePeriodMinutes,
                Penalties = penalties,
                Collection = collection
            };

            return new StartingDebtScenario(creditor, config);
        }
    }
}
