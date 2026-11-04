// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Core
//
// Наблюдаемое состояние одной операции в журнале кошелька.
// Адаптер отвечает этим значением на вопрос «что реально произошло с деньгами
// по этому operationId?» — без этого нельзя отличить отказ от потерянного ответа.

namespace LifeFromZero.Economy.Core
{
    /// <summary>Что адаптер знает об операции.</summary>
    public enum WalletOperationStatus
    {
        /// <summary>Адаптер не знает такой операции (она не начиналась или журнал потерян).</summary>
        NotFound = 0,

        /// <summary>Операция выполнена: деньги реально списаны/начислены.</summary>
        Succeeded = 1,

        /// <summary>Операция отклонена, деньги не изменены.</summary>
        Declined = 2,

        /// <summary>Исход неизвестен (журнал недоступен или запись повреждена).</summary>
        Unknown = 3
    }

    /// <summary>Вид записи в журнале кошелька.</summary>
    public enum WalletOperationKind
    {
        None = 0,

        /// <summary>Списание.</summary>
        Spend = 1,

        /// <summary>Начисление/возврат.</summary>
        Refund = 2
    }

    /// <summary>Неизменяемое состояние операции.</summary>
    public readonly struct WalletOperationState
    {
        /// <summary>Пустое состояние: адаптер ничего не знает.</summary>
        public static readonly WalletOperationState Unknown =
            new WalletOperationState(WalletOperationKind.None, Money.Zero, WalletOperationStatus.Unknown);

        public WalletOperationKind Kind { get; }

        public Money Amount { get; }

        public WalletOperationStatus Status { get; }

        public WalletOperationState(WalletOperationKind kind, Money amount, WalletOperationStatus status)
        {
            Kind = kind;
            Amount = amount;
            Status = status;
        }

        public bool IsSucceeded => Status == WalletOperationStatus.Succeeded;

        public bool IsUnknown => Status == WalletOperationStatus.Unknown;

        public override string ToString()
        {
            return Kind + "/" + Status + " " + Amount;
        }
    }
}
