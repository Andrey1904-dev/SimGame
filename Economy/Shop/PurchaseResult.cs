// Life From Zero — Economy module (ТЗ №014-В.1, идентификаторы операций — ТЗ №021)
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

        /// <summary>
        /// Идентификатор покупки, под которым операция записана в журнал (null, если покупка
        /// выполнялась без идентификатора). По нему вызов повторяется безопасно.
        /// </summary>
        public string PurchaseId { get; }

        /// <summary>Успешна ли операция целиком.</summary>
        public bool IsSuccess => Code == EconomyResultCode.Success;

        /// <summary>
        /// true, если это ответ на повторную отправку того же <see cref="PurchaseId"/>:
        /// деньги и товар в этом вызове НЕ менялись.
        /// </summary>
        public bool IsReplay { get; }

        private PurchaseResult(
            EconomyResultCode code,
            Product product,
            int requestedQuantity,
            int purchasedQuantity,
            int unitsGranted,
            Money totalCost,
            string purchaseId,
            bool isReplay)
        {
            Code = code;
            Product = product;
            RequestedQuantity = requestedQuantity;
            PurchasedQuantity = purchasedQuantity;
            UnitsGranted = unitsGranted;
            TotalCost = totalCost;
            PurchaseId = purchaseId;
            IsReplay = isReplay;
        }

        internal static PurchaseResult Success(
            Product product,
            int requestedQuantity,
            int unitsGranted,
            Money totalCost,
            string purchaseId)
        {
            return new PurchaseResult(
                EconomyResultCode.Success,
                product,
                requestedQuantity,
                requestedQuantity,
                unitsGranted,
                totalCost,
                purchaseId,
                false);
        }

        /// <summary>
        /// Ответ на повтор: товар был выдан раньше, сейчас ничего не менялось.
        /// </summary>
        internal static PurchaseResult SuccessFromReplay(
            Product product,
            int requestedQuantity,
            int unitsGranted,
            Money totalCost,
            string purchaseId)
        {
            return new PurchaseResult(
                EconomyResultCode.Success,
                product,
                requestedQuantity,
                requestedQuantity,
                unitsGranted,
                totalCost,
                purchaseId,
                true);
        }

        internal static PurchaseResult Failure(
            EconomyResultCode code,
            Product product,
            int requestedQuantity,
            Money totalCost,
            string purchaseId)
        {
            return new PurchaseResult(code, product, requestedQuantity, 0, 0, totalCost, purchaseId, false);
        }

        /// <summary>Ответ на повтор: прежний отказ, сейчас ничего не менялось.</summary>
        internal static PurchaseResult FailureFromReplay(
            EconomyResultCode code,
            Product product,
            int requestedQuantity,
            Money totalCost,
            string purchaseId)
        {
            return new PurchaseResult(code, product, requestedQuantity, 0, 0, totalCost, purchaseId, true);
        }

        public override string ToString()
        {
            return "PurchaseResult(" + Code
                   + ", id=" + (PurchaseId ?? "<none>")
                   + ", product=" + (Product != null ? Product.Id : "<none>")
                   + ", requested=" + RequestedQuantity
                   + ", purchased=" + PurchasedQuantity
                   + ", units=" + UnitsGranted
                   + ", cost=" + TotalCost
                   + (IsReplay ? ", replay" : string.Empty)
                   + ")";
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
        /// новые покупки кодом <see cref="EconomyResultCode.PendingOperation"/>.
        /// </summary>
        int PendingRefundCount { get; }

        /// <summary>
        /// Повторяет незавершённые возвраты. <c>true</c> — журнал пуст (включая случай,
        /// когда он был пуст изначально). Исключения кошелька превращаются в <c>false</c>.
        /// </summary>
        bool RetryPendingRefunds(IWalletAdapter wallet);
    }
}
