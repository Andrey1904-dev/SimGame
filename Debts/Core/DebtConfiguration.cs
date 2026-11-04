// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

using LifeFromZero.Debts.Penalties;

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Конфигурация создаваемого долга. Это данные, а не поведение: стартовые долги
    /// игры задаются здесь или фабрикой (StartingDebtFactory), универсальный
    /// DebtService никаких стартовых сумм не знает.
    /// </summary>
    public sealed class DebtConfiguration
    {
        /// <summary>Уникальный ID долга.</summary>
        public string DebtId { get; set; }

        /// <summary>ID зарегистрированного кредитора.</summary>
        public string CreditorId { get; set; }

        /// <summary>Отображаемое название долга (например, «Долг за квартиру»). Может быть пустым.</summary>
        public string Title { get; set; }

        /// <summary>Основная сумма в рублях до начисления стартового процента, &gt; 0.</summary>
        public long PrincipalRubles { get; set; }

        /// <summary>
        /// Процентная ставка в базисных пунктах (100 = 1%). По модели модуля это
        /// простая разовая надбавка к основному долгу при создании (без сложных
        /// процентов и без периодического начисления); периодические начисления
        /// задаются политикой штрафов. Ставка 0 — надбавки нет.
        /// </summary>
        public int InterestRateBasisPoints { get; set; }

        /// <summary>Срок платежа в игровых минутах с начала игры, &gt;= 0.</summary>
        public long DueAtMinutes { get; set; }

        /// <summary>Льготный период после срока в минутах, &gt;= 0. Просрочка начинается строго после DueAt + Grace.</summary>
        public long GracePeriodMinutes { get; set; }

        /// <summary>Политика штрафов; null — штрафы отключены.</summary>
        public PenaltyPolicy Penalties { get; set; }

        /// <summary>Пороги стадий взыскания; null — значения по умолчанию.</summary>
        public CollectionThresholds Collection { get; set; }
    }
}
