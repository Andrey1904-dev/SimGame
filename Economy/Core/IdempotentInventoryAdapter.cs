// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Core
//
// Идемпотентный контракт инвентаря: защита от ДВОЙНОЙ ВЫДАЧИ товара.
//
// ЗАЧЕМ. Деньги защищены operationId кошелька, а выдача товара — нет. Если процесс
// упал сразу после TryAdd, но до записи состояния Granted, восстановление не может
// отличить «товар выдан» от «товар не выдан», и наивное «выдать ещё раз» удваивает хлеб.
// Идемпотентный инвентарь отвечает на этот вопрос по журналу выдачи.

namespace LifeFromZero.Economy.Core
{
    /// <summary>Что инвентарь знает о выдаче по идентификатору операции.</summary>
    public enum InventoryGrantStatus
    {
        /// <summary>Выдачи с таким идентификатором не было.</summary>
        NotFound = 0,

        /// <summary>Товар выдан (ровно один раз).</summary>
        Succeeded = 1,

        /// <summary>Выдача отклонена, инвентарь не изменён.</summary>
        Declined = 2,

        /// <summary>Исход неизвестен (журнал выдачи недоступен или повреждён).</summary>
        Unknown = 3
    }

    /// <summary>
    /// Инвентарь с идемпотентной выдачей по идентификатору операции.
    /// Расширяет <see cref="IInventoryAdapter"/>: старые адаптеры продолжают работать,
    /// но теряют защиту от двойной выдачи после сбоя.
    /// </summary>
    /// <remarks>
    /// Обязанности адаптера:
    /// <list type="number">
    /// <item><see cref="TryAdd(string, string, int)"/> идемпотентен по operationId: повтор
    ///       возвращает <c>true</c> и НЕ добавляет товар второй раз.</item>
    /// <item><see cref="TryGetGrantState"/> отвечает по журналу выдачи — это единственный
    ///       способ отличить «не выдавали» от «выдали, но ответ потерялся».</item>
    /// <item><see cref="IsDurable"/> = true означает, что журнал переживает перезапуск игры.
    ///       Для «Режима A» допустимо false: тогда повтор внутри сессии защищён, а после
    ///       перезапуска снимок магазина может запросить выдачу повторно.</item>
    /// </list>
    /// </remarks>
    public interface IIdempotentInventoryAdapter : IInventoryAdapter
    {
        /// <summary>Журнал выдачи долговечен (переживает перезапуск игры).</summary>
        bool IsDurable { get; }

        /// <summary>
        /// Добавляет предметы под идентификатором операции.
        /// Повтор с тем же идентификатором и тем же количеством — <c>true</c> без добавления.
        /// </summary>
        bool TryAdd(string operationId, string itemId, int quantity);

        /// <summary>
        /// Состояние выдачи по журналу.
        /// </summary>
        /// <returns><c>false</c> — журнал недоступен, исход неизвестен.</returns>
        bool TryGetGrantState(string operationId, out InventoryGrantStatus result);
    }
}
