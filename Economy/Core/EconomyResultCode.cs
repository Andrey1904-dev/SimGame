// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Core

namespace LifeFromZero.Economy.Core
{
    /// <summary>
    /// Машиночитаемый код исхода экономической операции.
    /// Тексты уведомлений сюда не входят: слой UI/локализации отображает код сам.
    /// </summary>
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
        InternalError = 5
    }
}
