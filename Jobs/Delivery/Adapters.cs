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

    /// <summary>
    /// Идемпотентное начисление: durable-кошелёк атомарно записывает идентификатор операции,
    /// сумму и факт начисления.
    /// </summary>
    /// <remarks>
    /// Контракт адаптера:
    /// <list type="number">
    /// <item>Повтор с тем же ID и той же суммой возвращает <c>true</c> и НЕ начисляет деньги
    ///       второй раз.</item>
    /// <item>Тот же ID с другой суммой — <c>false</c> (конфликт, ничего не начислено).</item>
    /// <item>Журнал долговечен и переживает перезапуск игры; ключи изолированы по линии
    ///       сохранения (или уникальны как минимум внутри неё).</item>
    /// <item>Исключение означает «исход неизвестен»: сервис повторит операцию с ТЕМ ЖЕ ID.</item>
    /// </list>
    /// </remarks>
    public interface IIdempotentPayoutAdapter : IPayoutAdapter
    {
        /// <summary>Начисление под идентификатором операции (см. контракт выше).</summary>
        bool TryCredit(string operationId, long rubles);

        /// <summary>
        /// Ответ по журналу: было ли реально начисление по этому идентификатору.
        /// </summary>
        /// <returns><c>false</c> — журнал недоступен, исход неизвестен.</returns>
        bool WasCredited(string operationId, out bool credited);
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
