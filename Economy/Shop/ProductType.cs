// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Категория товара. Отдельный тип (а не bool «еда») — задел под покупку
    /// непродовольственных товаров, не требующий менять API магазина.
    /// Питательность в магазине НЕ хранится: эффектами на сытость владеет
    /// существующая система FoodInventory/NeedsRuntime.
    /// </summary>
    public enum ProductType
    {
        /// <summary>Продукты питания.</summary>
        Food = 0,

        /// <summary>Резерв под непродовольственные товары ТЗ №014-В.1 п.4. Сейчас не используется.</summary>
        NonFood = 1
    }
}
