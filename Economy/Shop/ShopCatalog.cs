// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Каталог товаров магазина.
    /// Абстракция введена для будущих сценариев: несколько магазинов с разными
    /// ассортиментами, онлайн-заказы, динамические цены.
    /// </summary>
    public interface IShopCatalog
    {
        /// <summary>Ищет товар по идентификатору. Возвращает <c>false</c>, если товара нет.</summary>
        bool TryGet(string productId, out Product product);

        /// <summary>Все товары каталога (для витрины).</summary>
        IReadOnlyList<Product> Products { get; }
    }

    /// <summary>
    /// Каталог в памяти. Не является Singleton: экземпляр создаётся там, где нужен
    /// (магазин, тест, конфиг). Начальные цены ТЗ заданы в <see cref="CreateDefaultProducts"/>.
    /// </summary>
    public sealed class ShopCatalog : IShopCatalog
    {
        private readonly Dictionary<string, Product> _byId;

        public ShopCatalog(IEnumerable<Product> products)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            _byId = new Dictionary<string, Product>(StringComparer.Ordinal);
            var ordered = new List<Product>();

            foreach (Product product in products)
            {
                if (product == null)
                {
                    throw new ArgumentException("Каталог не должен содержать пустых записей.", nameof(products));
                }

                if (_byId.ContainsKey(product.Id))
                {
                    throw new ArgumentException(
                        "Дублирующийся идентификатор товара в каталоге: " + product.Id, nameof(products));
                }

                _byId.Add(product.Id, product);
                ordered.Add(product);
            }

            Products = ordered.AsReadOnly();
        }

        public IReadOnlyList<Product> Products { get; }

        public bool TryGet(string productId, out Product product)
        {
            if (string.IsNullOrEmpty(productId))
            {
                product = null;
                return false;
            }

            return _byId.TryGetValue(productId, out product);
        }

        /// <summary>
        /// Начальный каталог из ТЗ №014-В.1. Цены предварительные: меняются правкой
        /// значений в этом методе, конфиге или через <see cref="WithPrice"/>-сборку нового каталога.
        /// </summary>
        public static IReadOnlyList<Product> CreateDefaultProducts()
        {
            return new List<Product>
            {
                new Product(
                    id: "bread",
                    title: "Хлеб",
                    price: Money.FromRubles(50L),
                    type: ProductType.Food,
                    unitsPerPurchase: 1,
                    inventoryItemId: "bread"),

                new Product(
                    id: "instant_noodles",
                    title: "Лапша быстрого приготовления",
                    price: Money.FromRubles(65L),
                    type: ProductType.Food,
                    unitsPerPurchase: 1,
                    inventoryItemId: "instant_noodles"),

                new Product(
                    id: "prepared_meal",
                    title: "Готовый обед",
                    price: Money.FromRubles(220L),
                    type: ProductType.Food,
                    unitsPerPurchase: 1,
                    inventoryItemId: "prepared_meal")
            }.AsReadOnly();
        }

        /// <summary>Готовый каталог из товаров по умолчанию.</summary>
        public static ShopCatalog CreateDefault()
        {
            return new ShopCatalog(CreateDefaultProducts());
        }
    }

    /// <summary>
    /// Замена цены товара без мутации: возвращает новый товар с той же остальной начинкой.
    /// Задел под «изменение цен» (ТЗ п.4), сама функция изменения цен не реализуется.
    /// </summary>
    public static class ProductPrice
    {
        public static Product WithPrice(Product source, Money newPrice)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new Product(
                source.Id,
                source.Title,
                newPrice,
                source.Type,
                source.UnitsPerPurchase,
                source.InventoryItemId);
        }
    }
}
