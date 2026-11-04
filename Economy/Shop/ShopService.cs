// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Shop

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Реализация покупки в продуктовом магазине.
    /// Зависит только от абстракций: <see cref="IWalletAdapter"/>, <see cref="IInventoryAdapter"/>,
    /// <see cref="IShopCatalog"/>. Ни собственного кошелька, ни инвентаря внутри нет,
    /// Singleton-ов нет, Unity API нет.
    /// </summary>
    /// <remarks>
    /// Порядок операций выбран так, чтобы деньги списывались только после проверки места
    /// в инвентаре, а любой сбой после списания компенсировался:
    /// <list type="number">
    /// <item>Проверка запроса и товара (без побочных эффектов).</item>
    /// <item>Расчёт стоимости и количества единиц с защитой от переполнения.</item>
    /// <item>Проверка баланса (одно чтение).</item>
    /// <item><see cref="IInventoryAdapter.CanApprove"/> — проверка места без изменений.</item>
    /// <item><see cref="IWalletAdapter.TrySpend"/> — списание денег.</item>
    /// <item><see cref="IInventoryAdapter.TryAdd"/> — выдача предметов.</item>
    /// <item>Откат: возврат денег, если шаг 6 не удался.</item>
    /// </list>
    /// Возврат денег при сбое выдачи — не «одна попытка», а журнал незавершённых компенсаций:
    /// если списание прошло, а <see cref="IWalletAdapter.TryRefund"/> отказал или упал, операция
    /// запоминается и повторяется при следующем вызове <see cref="Purchase"/> и в
    /// <see cref="RetryPendingRefunds"/>. Без этого повторная покупка «теряла» бы деньги игрока
    /// навсегда: адаптер кошелька не является журналом транзакций и сам себя не чинит.
    /// Пока компенсация висит, новые покупки не выполняются (код
    /// <see cref="EconomyResultCode.PendingRefund"/>): иначе игрок мог бы потратить уже
    /// возвращённые деньги и получить перерасход.
    /// </remarks>
    public sealed class ShopService : IShopService
    {
        private readonly IShopCatalog _catalog;

        /// <summary>
        /// Журнал незавершённых возвратов: суммы, которые были списаны, но не вернулись
        /// (возврат отказал или упал). Пока журнал не пуст, новые покупки не начинаются.
        /// </summary>
        private readonly List<Money> _pendingRefunds = new List<Money>();

        public ShopService(IShopCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public IShopCatalog Catalog => _catalog;

        /// <inheritdoc />
        public int PendingRefundCount => _pendingRefunds.Count;

        public PurchaseResult Purchase(IWalletAdapter wallet, IInventoryAdapter inventory, PurchaseRequest request)
        {
            // Отсутствие кошелька/инвентаря — ошибка интеграции, а не отказ игрока.
            if (wallet == null)
            {
                throw new ShopServiceException("Не передан кошелёк (IWalletAdapter). Покупка невозможна.");
            }

            if (inventory == null)
            {
                throw new ShopServiceException("Не передан инвентарь (IInventoryAdapter). Покупка невозможна.");
            }

            // 0. Незавершённый возврат блокирует новые покупки: деньги уже списаны,
            // но ещё не вернулись, и повторное списание сделало бы состояние неразрешимым.
            // Сначала пытаемся закрыть долг кошелька — это единственный шанс его исправить.
            FlushPendingRefunds(wallet);

            if (_pendingRefunds.Count > 0)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.PendingRefund, null, request.Quantity, Money.Zero);
            }

            // 1. Существование товара. Пустой идентификатор отсекаем до обращения к каталогу:
            // это дешевле и не зависит от того, как конкретный каталог реагирует на null.
            if (string.IsNullOrEmpty(request.ProductId))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.ProductNotFound, null, request.Quantity, Money.Zero);
            }

            Product product;
            bool found;
            try
            {
                found = _catalog.TryGet(request.ProductId, out product);
            }
            catch (Exception)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InternalError, null, request.Quantity, Money.Zero);
            }

            if (!found || product == null)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.ProductNotFound, null, request.Quantity, Money.Zero);
            }

            return PurchaseKnownProduct(wallet, inventory, product, request.Quantity);
        }

        /// <summary>
        /// Покупка уже найденного товара. Вынесено отдельным методом, чтобы бизнес-правила
        /// не смешивались с обработкой исключений каталога. Метод экземплярный, потому что
        /// ведёт журнал незавершённых возвратов (<see cref="Compensate"/>).
        /// </summary>
        private PurchaseResult PurchaseKnownProduct(
            IWalletAdapter wallet,
            IInventoryAdapter inventory,
            Product product,
            int quantity)
        {
            // 2. Положительное количество.
            if (quantity <= 0)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, Money.Zero);
            }

            // 3. Стоимость: защита от переполнения денежной суммы.
            if (!product.Price.TryMultiply(quantity, out Money totalCost))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, Money.Zero);
            }

            // 4. Количество единиц инвентаря: защита от переполнения int.
            if (!product.TryGetTotalUnits(quantity, out int unitsTotal) || unitsTotal <= 0)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, totalCost);
            }

            // 5. Достаточность денег.
            Money balance;
            if (!TryReadBalance(wallet, out balance))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InternalError, product, quantity, totalCost);
            }

            if (balance < totalCost)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InsufficientFunds, product, quantity, totalCost);
            }

            // 6. Возможность принять товар — до любых изменений.
            bool canApprove;
            if (!SafeCall.Try(() => inventory.CanApprove(product.InventoryItemId, unitsTotal), out canApprove))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InternalError, product, quantity, totalCost);
            }

            if (!canApprove)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InventoryRejected, product, quantity, totalCost);
            }

            // 7. Списание денег. По контракту при false баланс не изменён.
            bool spent;
            if (!SafeCall.Try(() => wallet.TrySpend(totalCost), out spent))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InternalError, product, quantity, totalCost);
            }

            if (!spent)
            {
                // Деньги не списаны — компенсация не требуется.
                return PurchaseResult.Failure(
                    EconomyResultCode.InsufficientFunds, product, quantity, totalCost);
            }

            // 8. Выдача предметов. Сбой здесь требует возврата денег.
            bool added;
            if (!SafeCall.Try(() => inventory.TryAdd(product.InventoryItemId, unitsTotal), out added))
            {
                return Compensate(wallet, product, quantity, totalCost);
            }

            if (!added)
            {
                return Compensate(wallet, product, quantity, totalCost);
            }

            return PurchaseResult.Success(product, quantity, unitsTotal, totalCost);
        }

        /// <summary>
        /// Возврат денег после неудачного добавления предметов.
        /// Предметы здесь не снимаются: <see cref="IInventoryAdapter.TryAdd"/>, вернувший <c>false</c>,
        /// по контракту не изменил состояние. Если адаптер нарушает контракт, сверка — его забота:
        /// магазин не списывает предметы наугад, чтобы не уничтожить уже лежащие у игрока.
        /// </summary>
        private PurchaseResult Compensate(
            IWalletAdapter wallet,
            Product product,
            int quantity,
            Money totalCost)
        {
            // Возврат, который не удался, попадает в журнал и повторяется позже: деньги игрока
            // не «сгорают» из-за одного отказа кошелька. Счётчик попыток — у адаптера внутри.
            if (TryRefund(wallet, totalCost))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InventoryRejected, product, quantity, totalCost);
            }

            _pendingRefunds.Add(totalCost);

            return PurchaseResult.Failure(
                EconomyResultCode.InternalError, product, quantity, totalCost);
        }

        /// <summary>
        /// Повторяет все незавершённые возвраты. Возвращает <c>true</c>, если журнал пуст
        /// (в том числе если он был пуст изначально). Вызывается магазином автоматически
        /// в начале каждой покупки; UI/загрузчик может звать явно, чтобы закрыть долг
        /// до открытия витрины.
        /// </summary>
        public bool RetryPendingRefunds(IWalletAdapter wallet)
        {
            if (wallet == null)
            {
                throw new ShopServiceException("Не передан кошелёк (IWalletAdapter). Возврат невозможен.");
            }

            FlushPendingRefunds(wallet);
            return _pendingRefunds.Count == 0;
        }

        /// <summary>
        /// Пытается вернуть все суммы из журнала. Порядок — от старых к новым; каждая сумма
        /// независима, поэтому частичный успех допустим (успешные удаляются из журнала).
        /// </summary>
        private void FlushPendingRefunds(IWalletAdapter wallet)
        {
            for (int i = 0; i < _pendingRefunds.Count; )
            {
                if (TryRefund(wallet, _pendingRefunds[i]))
                {
                    _pendingRefunds.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Одна попытка возврата. Исключение адаптера — такой же отказ, как и <c>false</c>.
        /// </summary>
        private static bool TryRefund(IWalletAdapter wallet, Money amount)
        {
            bool refunded;
            bool completed = SafeCall.Try(() => wallet.TryRefund(amount), out refunded);

            return completed && refunded;
        }

        private static bool TryReadBalance(IWalletAdapter wallet, out Money balance)
        {
            Money value = Money.Zero;
            bool ok = SafeCall.Try(() => { value = wallet.Balance; });

            balance = ok ? value : Money.Zero;
            return ok;
        }
    }
}
