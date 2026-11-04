// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core

namespace LifeFromZero.Debts.Core
{
    /// <summary>Машиночитаемый код исхода операции с долгом. Тексты — на стороне UI.</summary>
    public enum DebtResultCode
    {
        /// <summary>Операция выполнена.</summary>
        Success = 0,

        /// <summary>Долг с указанным ID не найден.</summary>
        DebtNotFound = 1,

        /// <summary>Некорректный аргумент (пустой ID, неположительная сумма и т.п.).</summary>
        InvalidArgument = 2,

        /// <summary>paymentId уже использован (в истории или журнале операций).</summary>
        DuplicatePayment = 3,

        /// <summary>Долг закрыт, операция над ним невозможна.</summary>
        InvalidState = 4,

        /// <summary>Недостаточно средств на кошельке.</summary>
        InsufficientFunds = 5,

        /// <summary>Сумма платежа превышает текущую задолженность.</summary>
        Overpayment = 6,

        /// <summary>Кошелёк вернул отказ (false), деньги не списаны.</summary>
        WalletFailure = 7,

        /// <summary>Адаптер выбросил исключение или вернул недопустимые данные.</summary>
        AdapterError = 8,

        /// <summary>Переполнение long при денежном расчёте.</summary>
        Overflow = 9,

        /// <summary>Есть незавершённая операция; новые платежи заблокированы до восстановления.</summary>
        PendingRecovery = 10,

        /// <summary>Долг с таким ID уже существует.</summary>
        DuplicateDebt = 11,

        /// <summary>Снимок состояния повреждён и не прошёл валидацию.</summary>
        InvalidSnapshot = 12,

        /// <summary>Кредитор с указанным ID не зарегистрирован.</summary>
        CreditorNotFound = 13,

        /// <summary>Кредитор с таким ID уже зарегистрирован.</summary>
        DuplicateCreditor = 14
    }
}
