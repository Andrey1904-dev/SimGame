// Life From Zero — Phone integrations (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Integrations

using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>
    /// Данные для экрана «Банк»: баланс, долги, последние банковские уведомления.
    /// Всё только для чтения — телефон не проводит переводы и не меняет баланс.
    /// </summary>
    public readonly struct BankSummary
    {
        /// <summary>Удалось ли прочитать баланс.</summary>
        public bool BalanceAvailable { get; }

        /// <summary>Баланс в целых рублях (осмыслен при <see cref="BalanceAvailable"/>).</summary>
        public long BalanceRubles { get; }

        /// <summary>Долги (пустой массив, если источник недоступен).</summary>
        public DebtDto[] Debts { get; }

        /// <summary>Доступен ли источник долгов.</summary>
        public bool DebtsAvailable { get; }

        public BankSummary(bool balanceAvailable, long balanceRubles, DebtDto[] debts, bool debtsAvailable)
        {
            BalanceAvailable = balanceAvailable;
            BalanceRubles = balanceRubles;
            Debts = debts ?? new DebtDto[0];
            DebtsAvailable = debtsAvailable;
        }

        public override string ToString()
        {
            return "BankSummary(balance="
                   + (BalanceAvailable ? BalanceRubles.ToString(System.Globalization.CultureInfo.InvariantCulture) : "n/a")
                   + ", debts=" + Debts.Length + ")";
        }
    }

    /// <summary>
    /// Собирает данные экрана «Банк». Любая ошибка внешнего источника
    /// превращается в «данные недоступны», а не в исключение.
    /// </summary>
    public sealed class BankSummaryService
    {
        private readonly IBalanceSource _balance;
        private readonly IDebtsSource _debts;

        public BankSummaryService(IBalanceSource balance, IDebtsSource debts)
        {
            _balance = balance;
            _debts = debts;
        }

        /// <summary>Всегда успешно: недоступные части помечаются флагами.</summary>
        private static DebtDto[] RemoveNulls(DebtDto[] source)
        {
            int count = 0;

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    count++;
                }
            }

            if (count == source.Length)
            {
                return source;
            }

            var result = new DebtDto[count];
            int index = 0;

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    result[index++] = source[i];
                }
            }

            return result;
        }

        public PhoneResult<BankSummary> GetSummary()
        {
            bool hasBalance = false;
            long rubles = 0L;

            if (_balance != null)
            {
                try
                {
                    hasBalance = _balance.TryGetBalanceRubles(out rubles) && rubles >= 0L;
                }
                catch (System.Exception)
                {
                    hasBalance = false;
                    rubles = 0L;
                }

                if (!hasBalance)
                {
                    // Отрицательный «баланс» от адаптера считаем недоступным: телефон не показывает долг как деньги.
                    rubles = 0L;
                }
            }

            bool hasDebts = false;
            DebtDto[] debts = new DebtDto[0];

            if (_debts != null)
            {
                try
                {
                    hasDebts = _debts.TryGetDebts(out debts);
                }
                catch (System.Exception)
                {
                    hasDebts = false;
                    debts = new DebtDto[0];
                }
            }

            // Источник может вернуть массив с пустыми записями: телефон не должен
            // отдавать null в UI (там будет «долг без названия» вместо падения).
            if (hasDebts && debts != null)
            {
                debts = RemoveNulls(debts);
            }

            var summary = new BankSummary(hasBalance, rubles, debts, hasDebts);
            return PhoneResult<BankSummary>.Ok(summary);
        }
    }
}
