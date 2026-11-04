// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

using System;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Запрос на покупку. Отдельный тип вместо набора параметров:
    /// позволит позже добавить магазин/точку доставки, не ломая сигнатуру сервиса.
    /// </summary>
    public readonly struct PurchaseRequest
    {
        /// <summary>Идентификатор товара.</summary>
        public string ProductId { get; }

        /// <summary>
        /// Количество покупок (упаковок), а не единиц предмета.
        /// 1 покупка «Хлеба» = 1 единица; если у товара UnitsPerPurchase = 3,
        /// 2 покупки = 6 единиц инвентаря.
        /// </summary>
        public int Quantity { get; }

        public PurchaseRequest(string productId, int quantity)
        {
            ProductId = productId;
            Quantity = quantity;
        }

        public override string ToString() => (ProductId ?? "<null>") + " x" + Quantity;
    }
}
