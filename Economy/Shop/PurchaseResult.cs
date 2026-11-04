// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

using System;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Результат покупки. Структурированный: UI сам решает, каким текстом
    /// показать <see cref="Code"/>. Тексты уведомлений в модуле отсутствуют.
    /// </summary>
    public readonly struct PurchaseResult
    {
        /// <summary>Машиночитаемый исход.</summary>
        public EconomyResultCode Code { get; }

        /// <summary>Товар, если он был найден в каталоге; иначе <c>null</c>.</summary>
        public Product Product { get; }

        /// <summary>Сколько покупок запрошено (упаковок).</summary>
        public int RequestedQuantity { get; }

        /// <summary>Сколько покупок фактически совершено. При ошибке — 0.</summary>
        public int PurchasedQuantity { get; }

        /// <summary>Сколько единиц предмета фактически добавлено в инвентарь. При ошибке — 0.</summary>
        public int UnitsGranted { get; }

        /// <summary>
        /// Итоговая стоимость запроса. Заполняется во всех случаях, когда её удалось посчитать
        /// (включая «недостаточно денег»); при ошибке до расчёта — <see cref="Money.Zero"/>.
        /// </summary>
        public Money TotalCost { get; }

        /// <summary>Успешна ли операция целиком.</summary>
        public bool IsSuccess => Code == EconomyResultCode.Success;

        private PurchaseResult(
            EconomyResultCode code,
            Product product,
            int requestedQuantity,
            int purchasedQuantity,
            int unitsGranted,
            Money totalCost)
        {
            Code = code;
            Product = product;
            RequestedQuantity = requestedQuantity;
            PurchasedQuantity = purchasedQuantity;
            UnitsGranted = unitsGranted;
            TotalCost = totalCost;
        }

        internal static PurchaseResult Success(
            Product product,
            int requestedQuantity,
            int unitsGranted,
            Money totalCost)
        {
            return new PurchaseResult(
                EconomyResultCode.Success,
                product,
                requestedQuantity,
                requestedQuantity,
                unitsGranted,
                totalCost);
        }

        internal static PurchaseResult Failure(
            EconomyResultCode code,
            Product product,
            int requestedQuantity,
            Money totalCost)
        {
            return new PurchaseResult(code, product, requestedQuantity, 0, 0, totalCost);
        }

        public override string ToString()
        {
            return "PurchaseResult(" + Code
                   + ", product=" + (Product != null ? Product.Id : "<none>")
                   + ", requested=" + RequestedQuantity
                   + ", purchased=" + PurchasedQuantity
                   + ", units=" + UnitsGranted
                   + ", cost=" + TotalCost + ")";
        }
    }

    /// <summary>
    /// Исключение для случаев, когда сервис вызван неправильно (нарушен контракт кода,
    /// а не бизнес-правило). Штатные отказы возвращаются кодом в <see cref="PurchaseResult"/>.
    /// </summary>
    public sealed class ShopServiceException : Exception
    {
        public ShopServiceException(string message) : base(message)
        {
        }

        public ShopServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Сервис покупки товаров магазина.
    /// </summary>
    public interface IShopService
    {
        /// <summary>
        /// Выполняет покупку атомарно на уровне игровой логики:
        /// либо деньги списаны и товар выдан, либо не изменено ничего.
        /// Не бросает исключений из-за отказов адаптеров — возвращает код.
        /// </summary>
        PurchaseResult Purchase(IWalletAdapter wallet, IInventoryAdapter inventory, PurchaseRequest request);

        /// <summary>Доступный каталог (для витрины).</summary>
        IShopCatalog Catalog { get; }

        /// <summary>
        /// Сколько сумм ждёт возврата (0 — всё согласовано). Непустой журнал блокирует
        /// новые покупки кодом <see cref="EconomyResultCode.PendingRefund"/>.
        /// </summary>
        int PendingRefundCount { get; }

        /// <summary>
        /// Повторяет незавершённые возвраты. <c>true</c> — журнал пуст (включая случай,
        /// когда он был пуст изначально). Исключения кошелька превращаются в <c>false</c>.
        /// </summary>
        bool RetryPendingRefunds(IWalletAdapter wallet);
    }
}
