// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

using System;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Товар магазина. Неизменяемый объект-значение: цены меняются заменой записи
    /// в каталоге (см. <see cref="IShopCatalog"/>), а не мутацией товара.
    /// </summary>
    public sealed class Product
    {
        /// <summary>Уникальный строковый идентификатор товара, например "bread".</summary>
        public string Id { get; }

        /// <summary>Отображаемое название.</summary>
        public string Title { get; }

        /// <summary>Цена за одну покупку (за упаковку из <see cref="UnitsPerPurchase"/> единиц).</summary>
        public Money Price { get; }

        /// <summary>Тип товара.</summary>
        public ProductType Type { get; }

        /// <summary>Сколько единиц предмета выдаётся за одну покупку (обычно 1).</summary>
        public int UnitsPerPurchase { get; }

        /// <summary>Идентификатор предмета для <see cref="IInventoryAdapter"/>.</summary>
        public string InventoryItemId { get; }

        public Product(
            string id,
            string title,
            Money price,
            ProductType type,
            int unitsPerPurchase,
            string inventoryItemId)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Идентификатор товара не должен быть пустым.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Название товара не должно быть пустым.", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(inventoryItemId))
            {
                throw new ArgumentException("Идентификатор предмета инвентаря не должен быть пустым.", nameof(inventoryItemId));
            }

            if (unitsPerPurchase <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitsPerPurchase), unitsPerPurchase, "Количество единиц при покупке должно быть больше нуля.");
            }

            Id = id;
            Title = title;
            Price = price;
            Type = type;
            UnitsPerPurchase = unitsPerPurchase;
            InventoryItemId = inventoryItemId;
        }

        /// <summary>Количество единиц предмета, которое будет выдано за <paramref name="purchases"/> покупок.</summary>
        public bool TryGetTotalUnits(int purchases, out int totalUnits)
        {
            totalUnits = 0;

            if (purchases <= 0)
            {
                return false;
            }

            if (UnitsPerPurchase > int.MaxValue / purchases)
            {
                return false;
            }

            totalUnits = UnitsPerPurchase * purchases;
            return true;
        }

        public override string ToString() => Id;
    }
}
