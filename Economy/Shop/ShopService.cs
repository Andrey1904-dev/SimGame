// Life From Zero — Economy module (ТЗ №014-В.1, финансовая безопасность — ТЗ №021)
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
    /// <para>
    /// ПОРЯДОК ОПЕРАЦИЙ. Деньги списываются только после проверки места в инвентаре,
    /// а любой сбой после списания компенсируется:
    /// </para>
    /// <list type="number">
    /// <item>Повтор операции с известным <c>PurchaseId</c> — возврат прежнего исхода (без денег).</item>
    /// <item>Проверка запроса и товара (без побочных эффектов).</item>
    /// <item>Расчёт стоимости и количества единиц с защитой от переполнения.</item>
    /// <item>Проверка баланса (одно чтение).</item>
    /// <item><see cref="IInventoryAdapter.CanApprove"/> — проверка места без изменений.</item>
    /// <item><see cref="IWalletAdapter.TrySpend"/> — списание денег (с operationId, если адаптер его поддерживает).</item>
    /// <item><see cref="IInventoryAdapter.TryAdd"/> — выдача предметов.</item>
    /// <item>Откат: возврат денег, если шаг 7 не удался (с operationId возврата).</item>
    /// </list>
    /// <para>
    /// ЧТО ДОБАВЛЕНО В ТЗ №021 (и почему это не косметика):
    /// </para>
    /// <list type="bullet">
    /// <item><b>Идентификаторы операций.</b> Списание — <c>shop:&lt;id&gt;</c>, возврат —
    /// <c>shop-refund:&lt;id&gt;</c>. Если кошелёк реализует
    /// <see cref="IIdempotentWalletAdapter"/>, повторный вызов с тем же ID не двигает деньги.</item>
    /// <item><b>Журнал операций.</b> Каждая покупка — запись с явным состоянием
    /// (<see cref="ShopOperationState"/>). Незавершённые записи блокируют новые покупки
    /// (код <see cref="EconomyResultCode.PendingOperation"/>): нельзя списать деньги,
    /// пока не ясна судьба предыдущего списания.</item>
    /// <item><b>Неоднозначный исход.</b> Исключение кошелька или отказ чужого адаптера больше
    /// не превращаются молча в «денег нет»: сервис спрашивает журнал адаптера
    /// (<see cref="IIdempotentWalletAdapter.TryGetOperationState"/>) и либо доигрывает
    /// выдачу, либо возвращает деньги, либо сообщает <see cref="EconomyResultCode.OutcomeUnknown"/>.</item>
    /// <item><b>Снимок журнала.</b> <see cref="CreateSnapshot"/> / <see cref="RestoreSnapshot"/>
    /// сохраняют незавершённые операции вместе с их идентификаторами.</item>
    /// <item><b>Режимы.</b> <see cref="IsDurable"/> = false означает «Режим A»: покупки и
    /// возвраты работают в текущей сессии, повтор защищён внутри сессии, но аварийное
    /// завершение игры не переживается. См. <c>ECONOMY_INTEGRATION_QUICKSTART.md</c>.</item>
    /// </list>
    /// </remarks>
    public sealed class ShopService : IShopService
    {
        /// <summary>
        /// Сколько завершённых операций помнить для защиты от повторного применения.
        /// Хватает на сессию; лимит не даёт журналу расти бесконечно в долгой игре.
        /// </summary>
        public const int DefaultRememberedOperations = 256;

        private readonly IShopCatalog _catalog;
        private readonly int _rememberedOperations;

        /// <summary>Журнал операций в порядке создания (включая завершённые).</summary>
        private readonly List<ShopOperation> _operations = new List<ShopOperation>();

        private readonly Dictionary<string, ShopOperation> _byPurchaseId =
            new Dictionary<string, ShopOperation>(StringComparer.Ordinal);

        /// <summary>Подписчики уведомлений о смене состояния операции.</summary>
        private Action<ShopOperation> _changed;

        /// <summary>
        /// true, пока выполняется покупка или восстановление. Защищает от повторного входа
        /// (например, из обработчика <see cref="Changed"/>): вложенный вызов не начнёт
        /// вторую операцию и не «доиграет» ту, которая ещё выполняется.
        /// </summary>
        private bool _busy;

        public ShopService(IShopCatalog catalog)
            : this(catalog, DefaultRememberedOperations)
        {
        }

        /// <summary>
        /// Конструктор с настраиваемым размером памяти о завершённых операциях.
        /// Меньше 1 — берётся значение по умолчанию.
        /// </summary>
        public ShopService(IShopCatalog catalog, int rememberedOperations)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rememberedOperations = rememberedOperations < 1 ? DefaultRememberedOperations : rememberedOperations;
        }

        public IShopCatalog Catalog => _catalog;

        /// <summary>
        /// Уведомление о смене состояния операции (для UI/логов). Возникает ПОСЛЕ того,
        /// как состояние зафиксировано, и никогда не влияет на исход покупки.
        /// </summary>
        /// <remarks>
        /// Гарантии:
        /// <list type="bullet">
        /// <item>исключение обработчика проглатывается: финансовый результат уже определён
        ///       и не зависит от подписчика;</item>
        /// <item>обработчик может читать <see cref="CreateSnapshot"/>, баланс и инвентарь
        ///       через свои адаптеры;</item>
        /// <item>обработчик НЕ может запустить вторую покупку: пока операция не завершена,
        ///       журнал блокирует её кодом <see cref="EconomyResultCode.PendingOperation"/>;</item>
        /// <item>при <see cref="RestoreSnapshot"/> уведомления не посылаются (это загрузка,
        ///       а не событие gameplay), а при восстановлении незавершённых операций — посылаются.</item>
        /// </list>
        /// </remarks>
        public event Action<ShopOperation> Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        /// <summary>
        /// Сколько сумм ждёт возврата. Сохранено для совместимости; непустое значение
        /// означает, что журнал операций не пуст.
        /// </summary>
        public int PendingRefundCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _operations.Count; i++)
                {
                    if (_operations[i].State == ShopOperationState.RefundPending
                        || _operations[i].State == ShopOperationState.RefundFailed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Сколько операций «висит» (деньги списаны, итог не зафиксирован).</summary>
        public int PendingOperationCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _operations.Count; i++)
                {
                    if (_operations[i].IsPending)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// true, если переданный кошелёк умеет идемпотентные операции с идентификатором
        /// и его журнал долговечен. Только в этом случае доступны гарантии «Режима B»
        /// со стороны кошелька; согласованная фиксация с инвентарём всё равно нужна отдельно.
        /// </summary>
        public bool IsDurable(IWalletAdapter wallet)
        {
            var idempotent = wallet as IIdempotentWalletAdapter;
            return idempotent != null && idempotent.IsDurable;
        }

        /// <summary>
        /// Содержимое журнала незавершённых возвратов (суммы, которые списаны, но ещё
        /// не вернулись). Сохранено для совместимости с ТЗ №017; для сохранений
        /// используйте <see cref="CreateSnapshot"/>, иначе теряются идентификаторы.
        /// </summary>
        public IReadOnlyList<Money> PendingRefunds
        {
            get
            {
                var amounts = new List<Money>();

                for (int i = 0; i < _operations.Count; i++)
                {
                    ShopOperation operation = _operations[i];

                    if (operation.State == ShopOperationState.RefundPending
                        || operation.State == ShopOperationState.RefundFailed)
                    {
                        amounts.Add(operation.Amount);
                    }
                }

                return amounts.AsReadOnly();
            }
        }

        /// <summary>Текущая запись журнала по идентификатору покупки, если она ещё помнится.</summary>
        public bool TryGetOperation(string purchaseId, out ShopOperation operation)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                operation = null;
                return false;
            }

            return _byPurchaseId.TryGetValue(purchaseId, out operation);
        }

        // ---------------------------------------------------------------------
        // Покупка
        // ---------------------------------------------------------------------

        /// <summary>
        /// Покупка без идентификатора операции. Поведение прежних версий: деньги и товар
        /// защищены от сбоя, но повторный вызов — это НОВАЯ покупка. Оставлено для
        /// совместимости; в новом коде используйте перегрузку с <c>purchaseId</c>.
        /// </summary>
        public PurchaseResult Purchase(IWalletAdapter wallet, IInventoryAdapter inventory, PurchaseRequest request)
        {
            return Purchase(wallet, inventory, request, null);
        }

        /// <summary>
        /// Покупка с идентификатором операции.
        /// </summary>
        /// <param name="purchaseId">
        /// Уникальный идентификатор покупки (например, "buy-0001"). Повторный вызов с тем же
        /// идентификатором не списывает деньги второй раз и не выдаёт товар повторно.
        /// Пустой/null — поведение без идентификатора (как <see cref="Purchase(IWalletAdapter,IInventoryAdapter,PurchaseRequest)"/>).
        /// </param>
        public PurchaseResult Purchase(
            IWalletAdapter wallet,
            IInventoryAdapter inventory,
            PurchaseRequest request,
            string purchaseId)
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

            // Повторный вход (обработчик Changed, UI-колбэк, реентрантный адаптер) запрещён:
            // вложенная покупка могла бы увидеть собственные промежуточные состояния.
            if (_busy)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.PendingOperation,
                    null,
                    request.Quantity,
                    Money.Zero,
                    purchaseId);
            }

            bool identified = !string.IsNullOrEmpty(purchaseId);

            if (identified && !FinancialOperationId.IsValidPurchaseId(purchaseId))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidArgument, null, request.Quantity, Money.Zero, purchaseId);
            }

            _busy = true;

            try
            {
                return PurchaseCore(wallet, inventory, request, purchaseId, identified);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Тело покупки. Вынесено, чтобы флаг <see cref="_busy"/> снимался в одном месте
        /// (try/finally) и реентрантность не зависела от ветки возврата.
        /// </summary>
        private PurchaseResult PurchaseCore(
            IWalletAdapter wallet,
            IInventoryAdapter inventory,
            PurchaseRequest request,
            string purchaseId,
            bool identified)
        {
            // 0a. Повтор уже известной операции: возвращаем ПРЕЖНИЙ исход, деньги не трогаем.
            if (identified)
            {
                ShopOperation known;

                if (_byPurchaseId.TryGetValue(purchaseId, out known))
                {
                    return Replay(known);
                }
            }

            // 0b. Незавершённая операция блокирует новые покупки. Сначала пытаемся её
            // закрыть: это единственный шанс исправить состояние без потери денег.
            ResolvePending(wallet, inventory);

            if (PendingOperationCount > 0)
            {
                ShopOperation pending = FindPending();

                // Долг по возврату сообщается прежним кодом PendingRefund (совместимость),
                // «зависшая» выдача — новым PendingOperation. Смысл один: журнал не пуст.
                EconomyResultCode blocked =
                    pending.State == ShopOperationState.RefundPending
                    || pending.State == ShopOperationState.RefundFailed
                        ? EconomyResultCode.PendingRefund
                        : EconomyResultCode.PendingOperation;

                return PurchaseResult.Failure(
                    blocked,
                    FindProductOrNull(pending.ProductId),
                    request.Quantity,
                    Money.Zero,
                    purchaseId);
            }

            // 1. Существование товара. Пустой идентификатор отсекаем до обращения к каталогу:
            // это дешевле и не зависит от того, как конкретный каталог реагирует на null.
            if (string.IsNullOrEmpty(request.ProductId))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.ProductNotFound, null, request.Quantity, Money.Zero, purchaseId);
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
                    EconomyResultCode.InternalError, null, request.Quantity, Money.Zero, purchaseId);
            }

            if (!found || product == null)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.ProductNotFound, null, request.Quantity, Money.Zero, purchaseId);
            }

            return PurchaseKnownProduct(wallet, inventory, product, request.Quantity, purchaseId, identified);
        }

        private PurchaseResult PurchaseKnownProduct(
            IWalletAdapter wallet,
            IInventoryAdapter inventory,
            Product product,
            int quantity,
            string purchaseId,
            bool identified)
        {
            // 2. Положительное количество.
            if (quantity <= 0)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, Money.Zero, purchaseId);
            }

            // 3. Стоимость: защита от переполнения денежной суммы.
            if (!product.Price.TryMultiply(quantity, out Money totalCost))
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, Money.Zero, purchaseId);
            }

            // 4. Количество единиц инвентаря: защита от переполнения int.
            if (!product.TryGetTotalUnits(quantity, out int unitsTotal) || unitsTotal <= 0)
            {
                return PurchaseResult.Failure(
                    EconomyResultCode.InvalidQuantity, product, quantity, totalCost, purchaseId);
            }

            ShopOperation operation = null;

            if (identified)
            {
                // Запись журнала создаётся ДО любых изменений: если процесс упадёт на списании,
                // сохранённый снимок будет знать, какую сумму и какой товар надо разрешить.
                operation = new ShopOperation(purchaseId, product.Id, quantity, unitsTotal, totalCost);
                Remember(operation);
            }

            // 5. Достаточность денег.
            Money balance;
            if (!TryReadBalance(wallet, out balance))
            {
                return Decline(operation, EconomyResultCode.InternalError, product, quantity, totalCost,
                    "Баланс кошелька недоступен.");
            }

            if (balance < totalCost)
            {
                return Decline(operation, EconomyResultCode.InsufficientFunds, product, quantity, totalCost,
                    "Недостаточно средств.");
            }

            // 6. Возможность принять товар — до любых изменений.
            bool canApprove;
            if (!SafeCall.Try(() => inventory.CanApprove(product.InventoryItemId, unitsTotal), out canApprove))
            {
                return Decline(operation, EconomyResultCode.InternalError, product, quantity, totalCost,
                    "Инвентарь не смог проверить место.");
            }

            if (!canApprove)
            {
                return Decline(operation, EconomyResultCode.InventoryRejected, product, quantity, totalCost,
                    "Инвентарь отказал: нет места.");
            }

            // 7. Списание денег.
            bool spent;
            bool confirmed = TryDebit(wallet, operation, totalCost, out spent);

            if (!confirmed)
            {
                // Исход неизвестен: запись остаётся в журнале и блокирует новые покупки,
                // пока журнал адаптера не ответит (если адаптер его вообще имеет).
                if (operation != null)
                {
                    operation.MoveTo(ShopOperationState.Reserved, "Списание не подтверждено: исход неизвестен.");
                }

                return PurchaseResult.Failure(
                    EconomyResultCode.OutcomeUnknown, product, quantity, Money.Zero, purchaseId);
            }

            if (!spent)
            {
                // Деньги не списаны — компенсация не требуется.
                return Decline(operation, EconomyResultCode.InsufficientFunds, product, quantity, totalCost,
                    "Кошелёк отказал в списании.");
            }

            if (operation != null)
            {
                operation.MoveTo(ShopOperationState.Debited, "Деньги списаны, товар ещё не выдан.");
                RaiseChanged(operation);
            }

            // 8. Выдача предметов.
            bool grantUnknown;
            bool awarded = TryGrant(inventory, operation, product, unitsTotal, out grantUnknown);

            if (!awarded)
            {
                if (grantUnknown)
                {
                    // Исход выдачи неизвестен: возвращать деньги НЕЛЬЗЯ (товар мог быть выдан),
                    // выдавать повторно ТОЖЕ. Запись остаётся Debited и ждёт journal-ответа.
                    if (operation != null)
                    {
                        operation.MoveTo(
                            ShopOperationState.Debited,
                            "Исход выдачи неизвестен: журнал инвентаря недоступен.");
                    }

                    return PurchaseResult.Failure(
                        EconomyResultCode.OutcomeUnknown, product, quantity, totalCost, purchaseId);
                }

                return Compensate(wallet, inventory, operation, product, quantity, totalCost,
                    "Инвентарь не выдал товар после списания.");
            }

            if (operation != null)
            {
                operation.MoveTo(ShopOperationState.Granted, "Товар выдан.");
                RaiseChanged(operation);
            }

            return PurchaseResult.Success(product, quantity, unitsTotal, totalCost, purchaseId);
        }

        /// <summary>
        /// Возврат денег после неудачного добавления предметов.
        /// Предметы здесь не снимаются: <see cref="IInventoryAdapter.TryAdd"/>, вернувший <c>false</c>,
        /// по контракту не изменил состояние. Если адаптер нарушает контракт, сверка — его забота:
        /// магазин не списывает предметы наугад, чтобы не уничтожить уже лежащие у игрока.
        /// </summary>
        private PurchaseResult Compensate(
            IWalletAdapter wallet,
            IInventoryAdapter inventory,
            ShopOperation operation,
            Product product,
            int quantity,
            Money totalCost,
            string reason)
        {
            if (operation == null)
            {
                // Покупка без идентификатора: прежнее поведение (журнал сумм).
                ShopOperation refundOnly = RefundOnlyRecord(product, quantity, totalCost);
                Remember(refundOnly);
                operation = refundOnly;
            }

            operation.MoveTo(ShopOperationState.RefundPending, reason);

            bool returned;
            bool confirmed = TryRefund(wallet, operation, totalCost, out returned);

            if (confirmed && returned)
            {
                operation.MoveTo(ShopOperationState.Refunded, "Деньги возвращены после отказа инвентаря.");
                RaiseChanged(operation);
                return PurchaseResult.Failure(
                    EconomyResultCode.InventoryRejected, product, quantity, totalCost, operation.PurchaseId);
            }

            operation.MoveTo(
                ShopOperationState.RefundFailed,
                confirmed ? "Возврат отклонён кошельком; будет повторён." : "Возврат не подтверждён; будет повторён.");
            RaiseChanged(operation);

            // Возврат не удался: деньги игрока ещё у кошелька, товар не выдан. Прежний код
            // InternalError сохранён (совместимость), а состояние записи RefundFailed делает
            // ситуацию явной: операция ждёт восстановления, а не «закрыта отказом инвентаря».
            return PurchaseResult.Failure(
                EconomyResultCode.InternalError,
                product,
                quantity,
                totalCost,
                operation.PurchaseId);
        }

        // ---------------------------------------------------------------------
        // Восстановление незавершённых операций
        // ---------------------------------------------------------------------

        /// <summary>
        /// Повторяет все незавершённые операции. <c>true</c> — журнал «висящих» операций пуст.
        /// </summary>
        /// <remarks>
        /// Что делает восстановление по состояниям:
        /// <list type="bullet">
        /// <item><c>Debited</c> — деньги списаны, товар не выдан. Если инвентарь теперь принимает
        /// товар, выдача доигрывается (игрок получает то, за что заплатил); иначе деньги
        /// возвращаются. Товар в этой ветке не удаляется: контракт <see cref="IInventoryAdapter"/>
        /// не даёт безопасного отката, а выдача могла произойти до сбоя учёта.</item>
        /// <item><c>RefundPending</c> / <c>RefundFailed</c> — повторный возврат по тому же
        /// <c>shop-refund:&lt;id&gt;</c>. Идемпотентный кошелёк не начислит дважды.</item>
        /// <item><c>Reserved</c> — журнал кошелька уточняет исход списания.</item>
        /// </list>
        /// </remarks>
        public bool RetryPendingOperations(IWalletAdapter wallet, IInventoryAdapter inventory)
        {
            if (wallet == null)
            {
                throw new ShopServiceException("Не передан кошелёк (IWalletAdapter). Восстановление невозможно.");
            }

            if (_busy)
            {
                return PendingOperationCount == 0;
            }

            _busy = true;

            try
            {
                ResolvePending(wallet, inventory);
                return PendingOperationCount == 0;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Совместимость с ТЗ №017: повтор незавершённых возвратов.
        /// Если инвентарь не передан, «висящая» выдача не доигрывается — деньги возвращаются.
        /// </summary>
        public bool RetryPendingRefunds(IWalletAdapter wallet)
        {
            return RetryPendingOperations(wallet, null);
        }

        private void ResolvePending(IWalletAdapter wallet, IInventoryAdapter inventory)
        {
            if (PendingOperationCount == 0)
            {
                return;
            }

            // Снимок списка: восстановление меняет журнал.
            ShopOperation[] pending = CollectPending();

            for (int i = 0; i < pending.Length; i++)
            {
                switch (pending[i].State)
                {
                    case ShopOperationState.Reserved:
                        ResolveReserved(wallet, pending[i]);
                        break;

                    case ShopOperationState.Debited:
                        ResolveDebited(wallet, inventory, pending[i]);
                        break;

                    case ShopOperationState.RefundPending:
                    case ShopOperationState.RefundFailed:
                        ResolveRefund(wallet, pending[i]);
                        break;
                }
            }
        }

        private void ResolveReserved(IWalletAdapter wallet, ShopOperation operation)
        {
            bool debited;

            if (IsSpendConfirmed(wallet, operation.DebitOperationId, operation.Amount, out debited))
            {
                if (debited)
                {
                    operation.MoveTo(ShopOperationState.Debited, "Списание подтверждено журналом кошелька.");
                }
                else
                {
                    // Списания не было — операция отменена, деньги не тронуты.
                    operation.MoveTo(ShopOperationState.Declined, "Списание не состоялось.");
                    operation.DeclinedCode = EconomyResultCode.InsufficientFunds;
                }

                return;
            }

            // Журнал недоступен. Если адаптер даже не умеет идемпотентных операций,
            // восстановить исход невозможно: запись остаётся и блокирует покупки,
            // чтобы не списать деньги второй раз.
            var idempotent = wallet as IIdempotentWalletAdapter;

            if (idempotent == null)
            {
                operation.MoveTo(
                    ShopOperationState.Declined,
                    "Старый адаптер кошелька не хранит журнал операций: исход списания неизвестен, "
                    + "операция закрыта без повторного списания. Сверьте баланс.");
                operation.DeclinedCode = EconomyResultCode.OutcomeUnknown;
            }
        }

        private void ResolveDebited(IWalletAdapter wallet, IInventoryAdapter inventory, ShopOperation operation)
        {
            // Сначала спрашиваем журнал выдачи: если товар уже выдан, повторять нельзя.
            if (IsGrantConfirmed(inventory, operation))
            {
                operation.MoveTo(ShopOperationState.Granted, "Выдача подтверждена журналом инвентаря.");
                RaiseChanged(operation);
                return;
            }

            // Без инвентаря исход выдачи установить нечем: деньги не возвращаем наугад.
            if (inventory == null)
            {
                operation.Message = "Инвентарь не передан: исход выдачи неизвестен, операция ждёт.";
                return;
            }

            // Доигрываем выдачу, только если товар ещё можно принять. Иначе — возврат денег.
            bool canApprove = false;
            bool canAsk = true;

            if (canAsk)
            {
                Product product = FindProductOrNull(operation.ProductId);

                if (product != null)
                {
                    canAsk = SafeCall.Try(
                        () => inventory.CanApprove(product.InventoryItemId, operation.Units), out canApprove);
                }
                else
                {
                    // Товара больше нет в каталоге: выдавать нечего, вернуть деньги — единственный
                    // безопасный исход (идентификатор предмета сохранён, но без товара неизвестен).
                    canAsk = false;
                    canApprove = false;
                }
            }

            if (canAsk && canApprove)
            {
                Product product = FindProductOrNull(operation.ProductId);
                bool grantUnknown;
                bool awarded = TryGrant(inventory, operation, product, operation.Units, out grantUnknown);

                if (awarded)
                {
                    operation.MoveTo(ShopOperationState.Granted, "Выдача товара доиграна после сбоя.");
                    RaiseChanged(operation);
                    return;
                }

                if (grantUnknown)
                {
                    // Ни денег, ни товара трогать нельзя: исход выдачи неизвестен.
                    operation.Message = "Исход выдачи неизвестен: журнал инвентаря недоступен.";
                    return;
                }
            }

            operation.MoveTo(ShopOperationState.RefundPending, "Товар не выдан; возвращаем деньги.");
            ResolveRefund(wallet, operation);
        }

        private void ResolveRefund(IWalletAdapter wallet, ShopOperation operation)
        {
            bool returned;
            bool confirmed = TryRefund(wallet, operation, operation.Amount, out returned);

            if (confirmed && returned)
            {
                operation.MoveTo(ShopOperationState.Refunded, "Деньги возвращены.");
                RaiseChanged(operation);
                return;
            }

            operation.MoveTo(
                ShopOperationState.RefundFailed,
                confirmed ? "Возврат отклонён кошельком; будет повторён." : "Возврат не подтверждён; будет повторён.");
            RaiseChanged(operation);
        }

        /// <summary>
        /// Разрешает операцию с НЕИЗВЕСТНЫМ исходом по журналам адаптеров.
        /// </summary>
        /// <remarks>
        /// Это единственный безопасный способ выйти из состояния
        /// <see cref="EconomyResultCode.OutcomeUnknown"/>, не списывая и не начисляя деньги
        /// повторно. Что делает метод:
        /// <list type="bullet">
        /// <item>списание не подтверждено журналом кошелька → запись закрывается отказом
        ///       (<see cref="ShopOperationState.Declined"/>), деньги не тронуты;</item>
        /// <item>списание подтверждено, выдача подтверждена журналом инвентаря → успех;</item>
        /// <item>списание подтверждено, выдачи не было → деньги возвращаются
        ///       (идемпотентно, по идентификатору возврата);</item>
        /// <item>исход всё ещё неизвестен → запись остаётся, метод возвращает <c>false</c>.</item>
        /// </list>
        /// Работает только с адаптерами, которые ведут журнал; для старых контрактов
        /// исход установить невозможно, и метод честно возвращает <c>false</c>.
        /// </remarks>
        public bool TryResolveUnknown(IWalletAdapter wallet, IInventoryAdapter inventory, string purchaseId)
        {
            if (wallet == null)
            {
                throw new ShopServiceException("Не передан кошелёк (IWalletAdapter). Разрешение невозможно.");
            }

            ShopOperation operation;

            if (string.IsNullOrEmpty(purchaseId) || !_byPurchaseId.TryGetValue(purchaseId, out operation))
            {
                return false;
            }

            if (!operation.IsPending)
            {
                return true;
            }

            if (_busy)
            {
                return false;
            }

            bool debited;

            if (!IsSpendConfirmed(wallet, operation.DebitOperationId, operation.Amount, out debited))
            {
                return false;
            }

            if (!debited)
            {
                operation.MoveTo(ShopOperationState.Declined, "Журнал кошелька: списания не было.");
                operation.DeclinedCode = EconomyResultCode.InsufficientFunds;
                return true;
            }

            operation.MoveTo(ShopOperationState.Debited, "Журнал кошелька подтвердил списание.");
            ResolveDebited(wallet, inventory, operation);

            return !operation.IsPending;
        }

        // ---------------------------------------------------------------------
        // Совместимость: журнал сумм (ТЗ №017)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Заменяет журнал незавершённых возвратов содержимым сохранения.
        /// Вызывается загрузчиком на свежем экземпляре сервиса; нулевые суммы
        /// отбрасываются, потому что возвращать нечего.
        /// </summary>
        /// <remarks>
        /// ВНИМАНИЕ. У сумм из этого метода нет идентификаторов: после загрузки они
        /// восстанавливаются как «ожидающие возврата» с синтетическими идентификаторами
        /// восстановления, и идемпотентность возврата гарантировать нельзя (кошелёк может
        /// начислить дважды). Для сохранений используйте <see cref="RestoreSnapshot"/>.
        /// </remarks>
        public void RestorePendingRefunds(IEnumerable<Money> amounts)
        {
            if (amounts == null)
            {
                throw new ArgumentNullException(nameof(amounts));
            }

            // Удаляем только записи, появившиеся из старого формата.
            for (int i = _operations.Count - 1; i >= 0; i--)
            {
                if (_operations[i].PurchaseId.StartsWith("legacy-refund-", StringComparison.Ordinal))
                {
                    _byPurchaseId.Remove(_operations[i].PurchaseId);
                    _operations.RemoveAt(i);
                }
            }

            int index = 0;

            foreach (Money amount in amounts)
            {
                if (amount.Rubles <= 0L)
                {
                    continue;
                }

                index++;
                string syntheticId = "legacy-refund-" + index;
                var operation = new ShopOperation(syntheticId, "<legacy>", 1, 1, amount);
                operation.MoveTo(ShopOperationState.RefundFailed, "Восстановлено из старого журнала сумм.");
                Remember(operation);
            }
        }

        // ---------------------------------------------------------------------
        // Снимок
        // ---------------------------------------------------------------------

        /// <summary>
        /// Снимок журнала операций. Сохраняйте его вместе с балансом кошелька и инвентарём;
        /// сам модуль ничего не пишет на диск.
        /// </summary>
        public ShopSnapshot CreateSnapshot()
        {
            var operations = new ShopOperationSnapshot[_operations.Count];

            for (int i = 0; i < _operations.Count; i++)
            {
                ShopOperation source = _operations[i];
                operations[i] = new ShopOperationSnapshot
                {
                    PurchaseId = source.PurchaseId,
                    ProductId = source.ProductId,
                    RequestedQuantity = source.RequestedQuantity,
                    Units = source.Units,
                    AmountRubles = source.Amount.Rubles,
                    State = (int)source.State,
                    DeclinedCode = (int)source.DeclinedCode
                };
            }

            return new ShopSnapshot
            {
                SchemaVersion = ShopSnapshot.CurrentSchemaVersion,
                Operations = operations
            };
        }

        /// <summary>
        /// Заменяет журнал содержимым снимка. Снимок проверяется целиком до применения:
        /// повреждённый не меняет ничего.
        /// </summary>
        public bool RestoreSnapshot(ShopSnapshot snapshot, out string error)
        {
            if (!ShopSnapshotValidator.Validate(snapshot, out error))
            {
                return false;
            }

            var restored = new List<ShopOperation>(snapshot.Operations.Length);
            var byId = new Dictionary<string, ShopOperation>(StringComparer.Ordinal);

            for (int i = 0; i < snapshot.Operations.Length; i++)
            {
                ShopOperationSnapshot source = snapshot.Operations[i];
                var operation = new ShopOperation(
                    source.PurchaseId,
                    source.ProductId,
                    source.RequestedQuantity,
                    source.Units,
                    Money.FromRubles(source.AmountRubles));

                operation.State = (ShopOperationState)source.State;
                operation.DeclinedCode = (EconomyResultCode)source.DeclinedCode;

                restored.Add(operation);
                byId.Add(operation.PurchaseId, operation);
            }

            _operations.Clear();
            _operations.AddRange(restored);
            _byPurchaseId.Clear();

            foreach (KeyValuePair<string, ShopOperation> pair in byId)
            {
                _byPurchaseId.Add(pair.Key, pair.Value);
            }

            error = null;
            return true;
        }

        // ---------------------------------------------------------------------
        // Повтор операции
        // ---------------------------------------------------------------------

        /// <summary>
        /// Ответ на повтор с тем же PurchaseId. Деньги и товар НЕ меняются.
        /// </summary>
        private PurchaseResult Replay(ShopOperation operation)
        {
            Product product = FindProductOrNull(operation.ProductId);
            EconomyResultCode code = operation.Code;

            if (code == EconomyResultCode.PendingOperation
                || code == EconomyResultCode.PendingRefund
                || code == EconomyResultCode.OutcomeUnknown)
            {
                // Незавершённая операция: ответ повторяет её состояние (и не мешает
                // восстановлению, которое выполнит следующий вызов).
                return PurchaseResult.FailureFromReplay(
                    code, product, operation.RequestedQuantity, operation.Amount, operation.PurchaseId);
            }

            if (code == EconomyResultCode.Success)
            {
                // Товар уже выдан ранее; повтор НЕ выдаёт его снова.
                return PurchaseResult.SuccessFromReplay(
                    product, operation.RequestedQuantity, operation.Units, operation.Amount, operation.PurchaseId);
            }

            return PurchaseResult.FailureFromReplay(
                code, product, operation.RequestedQuantity, operation.Amount, operation.PurchaseId);
        }

        // ---------------------------------------------------------------------
        // Адаптеры кошелька: идемпотентные и старые
        // ---------------------------------------------------------------------

        /// <summary>
        /// Списание с идентификатором, если адаптер это умеет.
        /// </summary>
        /// <returns>
        /// <c>true</c> — исход известен (<paramref name="spent"/> = списано или нет);
        /// <c>false</c> — исход неизвестен, запись должна остаться в журнале.
        /// </returns>
        private static bool TryDebit(IWalletAdapter wallet, ShopOperation operation, Money amount, out bool spent)
        {
            var idempotent = wallet as IIdempotentWalletAdapter;

            if (idempotent != null && operation != null)
            {
                bool ok;
                bool completed = SafeCall.Try(
                    () => idempotent.TrySpend(operation.DebitOperationId, amount), out ok);

                if (completed)
                {
                    spent = ok;

                    if (!spent)
                    {
                        // Отказ тоже сверяем с журналом: адаптер мог списать и вернуть false
                        // («потерянный ответ»). Сверка дешёвая, а цена ошибки — деньги игрока.
                        WalletOperationState declined = WalletOperationState.Unknown;

                        if (SafeCall.Try(
                                () => idempotent.TryGetOperationState(operation.DebitOperationId, out declined),
                                out _)
                            && declined.Status == WalletOperationStatus.Succeeded
                            && declined.Amount == amount)
                        {
                            spent = true;
                        }
                    }

                    return true;
                }

                // Исключение: спрашиваем журнал адаптера.
                WalletOperationState state = WalletOperationState.Unknown;

                if (SafeCall.Try(() => idempotent.TryGetOperationState(operation.DebitOperationId, out state), out _)
                    && state.Status == WalletOperationStatus.Succeeded)
                {
                    spent = true;
                    return true;
                }

                spent = false;
                return false;
            }

            // Старый контракт: идентификатора нет, идемпотентность обеспечить нельзя.
            bool legacyOk;
            bool legacyCompleted = SafeCall.Try(() => wallet.TrySpend(amount), out legacyOk);

            if (!legacyCompleted)
            {
                spent = false;
                return false;
            }

            spent = legacyOk;
            return true;
        }

        /// <summary>
        /// Выдача товара. Если инвентарь умеет идемпотентную выдачу, используется тот же
        /// идентификатор покупки: повтор после сбоя не добавит товар второй раз.
        /// </summary>
        private static bool TryGrant(
            IInventoryAdapter inventory,
            ShopOperation operation,
            Product product,
            int units,
            out bool unknown)
        {
            unknown = false;

            if (product == null)
            {
                return false;
            }

            var idempotent = inventory as IIdempotentInventoryAdapter;

            if (idempotent != null && operation != null)
            {
                bool ok;
                bool completed = SafeCall.Try(
                    () => idempotent.TryAdd(operation.DebitOperationId, product.InventoryItemId, units), out ok);

                if (completed)
                {
                    return ok;
                }

                // Исключение: спрашиваем журнал выдачи. «Выдано» — успех, «не выдано» — отказ,
                // «неизвестно» — исход не установлен (выдачу повторять нельзя).
                InventoryGrantStatus status = InventoryGrantStatus.Unknown;

                if (SafeCall.Try(() => idempotent.TryGetGrantState(operation.DebitOperationId, out status), out _))
                {
                    if (status == InventoryGrantStatus.Succeeded)
                    {
                        return true;
                    }

                    if (status == InventoryGrantStatus.Declined)
                    {
                        return false;
                    }
                }

                unknown = true;
                return false;
            }

            return Grant(inventory, product, units);
        }

        /// <summary>
        /// Проверка «выдача уже была» по журналу инвентаря.
        /// </summary>
        private static bool IsGrantConfirmed(IInventoryAdapter inventory, ShopOperation operation)
        {
            var idempotent = inventory as IIdempotentInventoryAdapter;

            if (idempotent == null || operation == null)
            {
                return false;
            }

            InventoryGrantStatus status = InventoryGrantStatus.Unknown;

            return SafeCall.Try(() => idempotent.TryGetGrantState(operation.DebitOperationId, out status), out _)
                   && status == InventoryGrantStatus.Succeeded;
        }

        /// <summary>
        /// Возврат с идентификатором, если адаптер это умеет.
        /// </summary>
        private static bool TryRefund(IWalletAdapter wallet, ShopOperation operation, Money amount, out bool refunded)
        {
            var idempotent = wallet as IIdempotentWalletAdapter;

            if (idempotent != null && operation != null)
            {
                bool ok;
                bool completed = SafeCall.Try(
                    () => idempotent.TryRefund(operation.RefundOperationId, amount), out ok);

                if (completed)
                {
                    refunded = ok;

                    if (!refunded)
                    {
                        // Та же сверка, что и при списании: отказ может быть потерянным успехом.
                        WalletOperationState declined = WalletOperationState.Unknown;

                        if (SafeCall.Try(
                                () => idempotent.TryGetOperationState(operation.RefundOperationId, out declined),
                                out _)
                            && declined.Status == WalletOperationStatus.Succeeded)
                        {
                            refunded = true;
                        }
                    }

                    return true;
                }

                WalletOperationState state = WalletOperationState.Unknown;

                if (SafeCall.Try(() => idempotent.TryGetOperationState(operation.RefundOperationId, out state), out _)
                    && state.Status == WalletOperationStatus.Succeeded)
                {
                    refunded = true;
                    return true;
                }

                refunded = false;
                return false;
            }

            refunded = TryLegacyRefund(wallet, amount);
            return true;
        }

        /// <summary>
        /// Вопрос «списано ли» по журналу адаптера. Если адаптер не умеет журнала — false.
        /// </summary>
        private static bool IsSpendConfirmed(
            IWalletAdapter wallet, string debitOperationId, Money amount, out bool debited)
        {
            debited = false;

            var idempotent = wallet as IIdempotentWalletAdapter;

            if (idempotent == null)
            {
                return false;
            }

            WalletOperationState state = WalletOperationState.Unknown;

            if (!SafeCall.Try(() => idempotent.TryGetOperationState(debitOperationId, out state), out _))
            {
                return false;
            }

            if (state.Status == WalletOperationStatus.NotFound)
            {
                // «Операции нет» — это ответ «не списывали», но только если адаптер
                // вообще ведёт журнал.
                return idempotent.IsDurable;
            }

            if (state.Status == WalletOperationStatus.Succeeded)
            {
                if (state.Kind != WalletOperationKind.Spend || state.Amount != amount)
                {
                    return false;
                }

                debited = true;
                return true;
            }

            return state.Status == WalletOperationStatus.Declined;
        }

        private static bool TryLegacyRefund(IWalletAdapter wallet, Money amount)
        {
            bool refunded;
            bool completed = SafeCall.Try(() => wallet.TryRefund(amount), out refunded);

            return completed && refunded;
        }

        // ---------------------------------------------------------------------
        // Журнал
        // ---------------------------------------------------------------------

        /// <summary>
        /// Рассылка уведомления. Исключение любого обработчика проглатывается:
        /// финансовая операция к этому моменту уже зафиксирована, и подписчик не должен
        /// превращать успешную покупку в «неизвестный исход».
        /// </summary>
        private void RaiseChanged(ShopOperation operation)
        {
            Action<ShopOperation> handlers = _changed;

            if (handlers == null || operation == null)
            {
                return;
            }

            Delegate[] list = handlers.GetInvocationList();

            for (int i = 0; i < list.Length; i++)
            {
                var handler = list[i] as Action<ShopOperation>;

                if (handler == null)
                {
                    continue;
                }

                try
                {
                    handler(operation);
                }
                catch (Exception)
                {
                    // Диагностику ведёт подписчик: он знает свой контекст.
                }
            }
        }

        private void Remember(ShopOperation operation)
        {
            _operations.Add(operation);

            if (!string.IsNullOrEmpty(operation.PurchaseId))
            {
                _byPurchaseId[operation.PurchaseId] = operation;
            }

            TrimFinished();
        }

        /// <summary>
        /// Вытесняет самые старые завершённые операции сверх лимита.
        /// Незавершённые не вытесняются никогда: это единственная память о списанных деньгах.
        /// </summary>
        private void TrimFinished()
        {
            // Вытесняем только завершённые, начиная с самых старых. Granted вытесняется
            // последним: именно он защищает от повторной выдачи товара по тому же ID.
            for (int i = 0; i < _operations.Count && _operations.Count > _rememberedOperations; )
            {
                ShopOperation candidate = _operations[i];

                if (candidate.State == ShopOperationState.Declined
                    || candidate.State == ShopOperationState.Refunded)
                {
                    _byPurchaseId.Remove(candidate.PurchaseId);
                    _operations.RemoveAt(i);
                    continue;
                }

                i++;
            }

            for (int i = 0; i < _operations.Count && _operations.Count > _rememberedOperations; )
            {
                ShopOperation candidate = _operations[i];

                if (candidate.State == ShopOperationState.Granted)
                {
                    _byPurchaseId.Remove(candidate.PurchaseId);
                    _operations.RemoveAt(i);
                    continue;
                }

                i++;
            }
        }

        private ShopOperation[] CollectPending()
        {
            var pending = new List<ShopOperation>();

            for (int i = 0; i < _operations.Count; i++)
            {
                if (_operations[i].IsPending)
                {
                    pending.Add(_operations[i]);
                }
            }

            return pending.ToArray();
        }

        private ShopOperation FindPending()
        {
            for (int i = 0; i < _operations.Count; i++)
            {
                if (_operations[i].IsPending)
                {
                    return _operations[i];
                }
            }

            return null;
        }

        private ShopOperation RefundOnlyRecord(Product product, int quantity, Money totalCost)
        {
            string id = "unidentified-" + (_operations.Count + 1);

            return new ShopOperation(id, product != null ? product.Id : "<none>", quantity, 0, totalCost);
        }

        private Product FindProductOrNull(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            try
            {
                Product product;
                return _catalog.TryGet(productId, out product) ? product : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private PurchaseResult Decline(
            ShopOperation operation,
            EconomyResultCode code,
            Product product,
            int quantity,
            Money totalCost,
            string message)
        {
            if (operation != null)
            {
                // Отказ до списания: запись закрывается, чтобы она не блокировала покупки,
                // но повтор с тем же ID вернёт именно этот отказ.
                operation.Amount = totalCost;
                operation.MoveTo(ShopOperationState.Declined, message);
                operation.DeclinedCode = code;
                RaiseChanged(operation);
            }

            return PurchaseResult.Failure(
                code, product, quantity, totalCost, operation != null ? operation.PurchaseId : null);
        }

        private static bool Grant(IInventoryAdapter inventory, Product product, int units)
        {
            if (inventory == null || product == null)
            {
                return false;
            }

            bool added;
            bool completed = SafeCall.Try(() => inventory.TryAdd(product.InventoryItemId, units), out added);

            return completed && added;
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
