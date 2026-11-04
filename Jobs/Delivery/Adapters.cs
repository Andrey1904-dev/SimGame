// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery
//
// Интерфейсы-адаптеры. Модуль НЕ содержит собственного GameClock, кошелька
// и системы перемещения: он обращается к существующим системам проекта.

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Игровое время. Реализуется адаптером поверх существующего GameClock.
    /// Единица измерения — минуты (дробная часть допустима), только монотонно не убывает.
    /// </summary>
    /// <remarks>
    /// Почему не <c>DateTime</c>: у игры своё время (пауза, ускорение, сон), реальные часы
    /// к срокам отношения не имеют. Время запрашивается по требованию, а модуль сам
    /// не тикает и не подписывается на события.
    /// </remarks>
    public interface IGameClockAdapter
    {
        /// <summary>Текущее игровое время в минутах с начала игры.</summary>
        double NowMinutes { get; }
    }

    /// <summary>
    /// Начисление денег игроку. Реализуется адаптером поверх существующего MoneyWallet.
    /// Используется только для выплаты за выполненный заказ.
    /// </summary>
    public interface IPayoutAdapter
    {
        /// <summary>
        /// Начисляет <paramref name="rubles"/> целых рублей.
        /// </summary>
        /// <returns><c>false</c>, если начисление отклонено; тогда баланс не изменён.</returns>
        bool TryCredit(long rubles);
    }

    /// <summary>Durable wallet must atomically record operation ID, amount and credit.
    /// Repeating the same ID and amount returns true without crediting again; a conflicting
    /// amount returns false. IDs must be scoped to a single save lineage by the adapter.
    /// An exception may mean an unknown outcome; retry with the identical ID.</summary>
    public interface IIdempotentPayoutAdapter : IPayoutAdapter
    {
        bool TryCredit(string operationId, long rubles);
    }

    /// <summary>
    /// Текущее местоположение игрока. Реализуется адаптером поверх существующей системы
    /// перемещения (какая бы она ни была).
    /// </summary>
    /// <remarks>
    /// Сравнение локаций выполняет сам модуль: точка считается совпавшей, если
    /// её <see cref="DeliveryPoint.Id"/> равен тому, что вернул адаптер.
    /// </remarks>
    public interface ILocationProvider
    {
        /// <summary>Идентификатор локации, где сейчас находится игрок. Может быть пустым.</summary>
        string CurrentLocationId { get; }
    }
}
