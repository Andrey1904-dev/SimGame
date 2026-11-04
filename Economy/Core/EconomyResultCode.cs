// Life From Zero — Economy module (ТЗ №014-В.1, расширено ТЗ №021)
// Namespace: LifeFromZero.Economy.Core

namespace LifeFromZero.Economy.Core
{
    /// <summary>
    /// Машиночитаемый код исхода экономической операции.
    /// Тексты уведомлений сюда не входят: слой UI/локализации отображает код сам.
    /// </summary>
    /// <remarks>
    /// Существующие значения НЕ перенумеровывались (ТЗ №021): коды — часть публичного
    /// контракта и могут попадать в снимки/логи. Новые коды добавлены в конец.
    /// </remarks>
    public enum EconomyResultCode
    {
        /// <summary>Операция выполнена полностью.</summary>
        Success = 0,

        /// <summary>Недостаточно денег на кошельке.</summary>
        InsufficientFunds = 1,

        /// <summary>Товар с указанным идентификатором отсутствует в каталоге магазина.</summary>
        ProductNotFound = 2,

        /// <summary>Запрошено недопустимое количество (0, отрицательное или переполнение).</summary>
        InvalidQuantity = 3,

        /// <summary>Инвентарь не смог принять товар (нет места, отказ, исключение адаптера).</summary>
        InventoryRejected = 4,

        /// <summary>Внутренняя ошибка модуля или адаптера. Деньги и предметы не изменены.</summary>
        InternalError = 5,

        /// <summary>
        /// Есть незавершённый возврат денег: предыдущая покупка списала средства, но вернуть их
        /// не удалось. Новая покупка не начинается, пока компенсация не закрыта
        /// (<see cref="LifeFromZero.Economy.Shop.ShopService.RetryPendingOperations"/>).
        /// </summary>
        /// <remarks>
        /// Оставлен для совместимости. Новый код ведёт себя так же и называется
        /// <see cref="PendingOperation"/>; сервис возвращает именно его.
        /// </remarks>
        PendingRefund = 6,

        /// <summary>
        /// Есть незавершённая операция (списано, но не выдано; или ждёт возврата).
        /// Новые покупки не начинаются, пока журнал операций не пуст
        /// (<see cref="LifeFromZero.Economy.Shop.ShopService.RetryPendingOperations"/>).
        /// </summary>
        PendingOperation = 7,

        /// <summary>
        /// Исход операции неизвестен: кошелёк не подтвердил и не опроверг списание,
        /// а журнал адаптера недоступен. Повторять с ТЕМ ЖЕ идентификатором;
        /// новая покупка с новым ID заблокирована до восстановления.
        /// </summary>
        OutcomeUnknown = 8,

        /// <summary>Некорректный аргумент вызова (например, недопустимый идентификатор операции).</summary>
        InvalidArgument = 9
    }
}
