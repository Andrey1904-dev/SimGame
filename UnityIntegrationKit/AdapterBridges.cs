// Life From Zero — Unity Integration Kit (ТЗ №021)
// Namespace: LifeFromZero.UnityIntegrationKit
//
// ГОТОВЫЕ (не зависящие от Unity) части адаптеров к существующим компонентам игры.
//
// ЧТО ЗДЕСЬ ЕСТЬ И ЧЕГО ЗДЕСЬ НЕТ.
//   * Здесь только логика, которую можно проверить без Unity: нормализация сумм,
//     защита от повторного входа, бухгалтерия операций, разбор идентификаторов.
//   * Здесь НЕТ обращений к MoneyWallet/FoodInventory/HUD: их сигнатуры неизвестны
//     (NEEDS_UNITY_INSPECTION). Codex реализует МИНИМАЛЬНЫЙ набор методов из
//     "// CODEX:"-блоков ниже и передаёт их сюда делегатами.
//   * Ни один файл не ссылается на UnityEngine — можно компилировать и в тестах.

using System;
using System.Collections.Generic;
using LifeFromZero.Economy.Core;
using LifeFromZero.Economy.Shop;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Payments;

namespace LifeFromZero.UnityIntegrationKit
{
    // =========================================================================
    // 1. MoneyWallet → LifeFromZero.Economy.Core.IWalletAdapter / IIdempotentWalletAdapter
    // =========================================================================
    //
    // CODEX: реализовать в существующем MoneyWallet ровно эти четыре операции
    //        (имена — предложение, суть обязательна):
    //          long Balance { get; }                       // чтение без побочных эффектов
    //          bool TrySpend(long rubles);                 // атомарно, при false баланс не изменён
    //          bool TryCredit(long rubles);                // начисление (возврат/выплата)
    //          bool TrySpend(string operationId, long rubles);   // + журнал, если нужен Режим B
    //          bool TryGetOperation(string operationId, out int kind, out long rubles, out int status);
    //
    //        ВАЖНО: если MoneyWallet хранит int, приведение делать ЗДЕСЬ (WalletBridge),
    //        с проверкой диапазона, а не в MoneyWallet.

    /// <summary>
    /// Готовый мост «MoneyWallet → IWalletAdapter»: единственное место, где сумма
    /// переводится из <see cref="Money"/> в рубли и проверяется диапазон.
    /// </summary>
    /// <remarks>
    /// Делегаты — это и есть те минимальные методы, которые Codex пробрасывает
    /// из настоящего MoneyWallet. Никаких предположений о его API здесь не делается.
    /// </remarks>
    public sealed class WalletBridge : IWalletAdapter
    {
        private readonly Func<long> _readBalance;
        private readonly Func<long, bool> _spend;
        private readonly Func<long, bool> _credit;

        public WalletBridge(Func<long> readBalance, Func<long, bool> spend, Func<long, bool> credit)
        {
            _readBalance = readBalance ?? throw new ArgumentNullException(nameof(readBalance));
            _spend = spend ?? throw new ArgumentNullException(nameof(spend));
            _credit = credit ?? throw new ArgumentNullException(nameof(credit));
        }

        /// <summary>
        /// true, если реализация кошелька вернула сумму, которую нельзя представить
        /// как <see cref="Money"/> (отрицательная). Тогда адаптер отдаёт 0 и запоминает
        /// проблему: <see cref="LastReadFailed"/> = true.
        /// </summary>
        public bool LastReadFailed { get; private set; }

        public Money Balance
        {
            get
            {
                long rubles = _readBalance();
                Money money;

                if (!Money.TryFromRubles(rubles, out money))
                {
                    LastReadFailed = true;
                    return Money.Zero;
                }

                LastReadFailed = false;
                return money;
            }
        }

        public bool TrySpend(Money amount)
        {
            return _spend(amount.Rubles);
        }

        public bool TryRefund(Money amount)
        {
            return _credit(amount.Rubles);
        }
    }

    /// <summary>
    /// Готовый мост «MoneyWallet с журналом → IIdempotentWalletAdapter» (Режим B).
    /// Codex передаёт сюда операцию списания/начисления с идентификатором и запрос журнала.
    /// </summary>
    public sealed class JournalWalletBridge : IIdempotentWalletAdapter
    {
        private readonly WalletBridge _legacy;
        private readonly Func<string, long, bool> _spendWithId;
        private readonly Func<string, long, bool> _creditWithId;
        private readonly Func<string, WalletOperationState> _query;

        public JournalWalletBridge(
            Func<long> readBalance,
            Func<long, bool> spend,
            Func<long, bool> credit,
            Func<string, long, bool> spendWithId,
            Func<string, long, bool> creditWithId,
            Func<string, WalletOperationState> query,
            bool isDurable)
        {
            _legacy = new WalletBridge(readBalance, spend, credit);
            _spendWithId = spendWithId ?? throw new ArgumentNullException(nameof(spendWithId));
            _creditWithId = creditWithId ?? throw new ArgumentNullException(nameof(creditWithId));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            IsDurable = isDurable;
        }

        /// <summary>
        /// true ТОЛЬКО если журнал реально лежит в сохранении. Не ставьте true «на всякий
        /// случай»: по этому флагу модуль решает, можно ли объявить Режим B.
        /// </summary>
        public bool IsDurable { get; }

        public Money Balance => _legacy.Balance;

        public bool TrySpend(Money amount) => _legacy.TrySpend(amount);

        public bool TryRefund(Money amount) => _legacy.TryRefund(amount);

        public bool TrySpend(string operationId, Money amount)
        {
            return !string.IsNullOrEmpty(operationId) && _spendWithId(operationId, amount.Rubles);
        }

        public bool TryRefund(string operationId, Money amount)
        {
            return !string.IsNullOrEmpty(operationId) && _creditWithId(operationId, amount.Rubles);
        }

        public bool TryGetOperationState(string operationId, out WalletOperationState result)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                result = WalletOperationState.Unknown;
                return false;
            }

            result = _query(operationId);
            return result.Status != WalletOperationStatus.Unknown;
        }
    }

    // =========================================================================
    // 2. FoodInventory → LifeFromZero.Economy.Core.IInventoryAdapter
    // =========================================================================
    //
    // CODEX: реализовать в существующем FoodInventory ровно эти операции:
    //          bool CanAccept(string itemId, int quantity);   // БЕЗ изменения состояния
    //          bool TryAdd(string itemId, int quantity);      // всё или ничего
    //          bool TryAdd(string operationId, string itemId, int quantity); // журнал (Режим B)
    //          bool TryGetGrant(string operationId, out int status);
    //
    //        Если FoodInventory не умеет проверять вместимость без мутации —
    //        правило вместимости пишется ЗДЕСЬ, в CanApprove, по данным инвентаря.

    /// <summary>Мост «FoodInventory → IInventoryAdapter».</summary>
    public sealed class InventoryBridge : IInventoryAdapter
    {
        private readonly Func<string, int, bool> _canAccept;
        private readonly Func<string, int, bool> _add;

        public InventoryBridge(Func<string, int, bool> canAccept, Func<string, int, bool> add)
        {
            _canAccept = canAccept ?? throw new ArgumentNullException(nameof(canAccept));
            _add = add ?? throw new ArgumentNullException(nameof(add));
        }

        public bool CanApprove(string itemId, int quantity)
        {
            return !string.IsNullOrEmpty(itemId) && quantity > 0 && _canAccept(itemId, quantity);
        }

        public bool TryAdd(string itemId, int quantity)
        {
            return !string.IsNullOrEmpty(itemId) && quantity > 0 && _add(itemId, quantity);
        }
    }

    /// <summary>Мост «FoodInventory с журналом выдачи → IIdempotentInventoryAdapter» (Режим B).</summary>
    public sealed class JournalInventoryBridge : IIdempotentInventoryAdapter
    {
        private readonly InventoryBridge _legacy;
        private readonly Func<string, string, int, bool> _addWithId;
        private readonly Func<string, InventoryGrantStatus> _query;

        public JournalInventoryBridge(
            Func<string, int, bool> canAccept,
            Func<string, int, bool> add,
            Func<string, string, int, bool> addWithId,
            Func<string, InventoryGrantStatus> query,
            bool isDurable)
        {
            _legacy = new InventoryBridge(canAccept, add);
            _addWithId = addWithId ?? throw new ArgumentNullException(nameof(addWithId));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            IsDurable = isDurable;
        }

        /// <summary>true только если журнал выдачи реально сохраняется.</summary>
        public bool IsDurable { get; }

        public bool CanApprove(string itemId, int quantity) => _legacy.CanApprove(itemId, quantity);

        public bool TryAdd(string itemId, int quantity) => _legacy.TryAdd(itemId, quantity);

        public bool TryAdd(string operationId, string itemId, int quantity)
        {
            return !string.IsNullOrEmpty(operationId) && _addWithId(operationId, itemId, quantity);
        }

        public bool TryGetGrantState(string operationId, out InventoryGrantStatus result)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                result = InventoryGrantStatus.Unknown;
                return false;
            }

            result = _query(operationId);
            return result != InventoryGrantStatus.Unknown;
        }
    }

    // =========================================================================
    // 3. Покупки и возвраты Economy
    // =========================================================================

    /// <summary>
    /// Готовая обёртка магазина: сама выдаёт идентификаторы покупок и не даёт UI
    /// случайно вызвать покупку без идентификатора (тогда повтор стал бы новой покупкой).
    /// </summary>
    public sealed class ShopCheckout
    {
        private readonly ShopService _shop;
        private readonly Func<string> _newPurchaseId;

        /// <param name="shop">Сервис магазина (один на игру).</param>
        /// <param name="newPurchaseId">
        /// Генератор идентификатора. По умолчанию — GUID без дефисов. Codex может передать
        /// детерминированный счётчик, если так удобнее для сохранений.
        /// </param>
        public ShopCheckout(ShopService shop, Func<string> newPurchaseId = null)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _newPurchaseId = newPurchaseId ?? (() => Guid.NewGuid().ToString("N"));
        }

        /// <summary>Сервис магазина (для снимка/восстановления).</summary>
        public ShopService Service => _shop;

        /// <summary>Начинает новую покупку: идентификатор создаётся один раз.</summary>
        public PurchaseResult Buy(IWalletAdapter wallet, IInventoryAdapter inventory, string productId, int quantity)
        {
            return _shop.Purchase(wallet, inventory, new PurchaseRequest(productId, quantity), _newPurchaseId());
        }

        /// <summary>
        /// Повторяет покупку с тем же идентификатором (обрыв, повторный клик).
        /// Деньги и товар не меняются.
        /// </summary>
        public PurchaseResult Retry(IWalletAdapter wallet, IInventoryAdapter inventory, PurchaseRequest request, string purchaseId)
        {
            return _shop.Purchase(wallet, inventory, request, purchaseId);
        }

        /// <summary>Доводит или компенсирует незавершённые операции.</summary>
        public bool Recover(IWalletAdapter wallet, IInventoryAdapter inventory)
        {
            return _shop.RetryPendingOperations(wallet, inventory);
        }
    }

    // =========================================================================
    // 4. Выплата Delivery
    // =========================================================================
    //
    // CODEX: реализовать в существующем MoneyWallet ровно эти операции:
    //          bool TryCredit(string operationId, long rubles);                  // идемпотентно
    //          bool WasCredited(string operationId, out bool credited);          // по журналу
    //        Журнал обязан переживать перезапуск и быть изолирован по линии сохранения.
    //        Идентификатор НЕ собирайте из orderId вручную: используйте
    //        DeliveryService.GetPayoutOperationId(orderId) или DeliveryResult.PayoutOperationId.

    /// <summary>Мост «MoneyWallet → IIdempotentPayoutAdapter» для выплат курьеру.</summary>
    public sealed class PayoutBridge : IIdempotentPayoutAdapter
    {
        private readonly Func<string, long, bool> _credit;
        private readonly Func<string, bool?> _wasCredited;

        /// <param name="credit">Идемпотентное начисление по operationId.</param>
        /// <param name="wasCredited">Запрос журнала: true — начислено, false — нет, null — журнал недоступен.</param>
        public PayoutBridge(Func<string, long, bool> credit, Func<string, bool?> wasCredited)
        {
            _credit = credit ?? throw new ArgumentNullException(nameof(credit));
            _wasCredited = wasCredited ?? throw new ArgumentNullException(nameof(wasCredited));
        }

        public bool TryCredit(long rubles)
        {
            // Legacy-путь сознательно не начисляет: без идентификатора повтор не защищён,
            // и такой вызов — ошибка интеграции, а не штатная ситуация.
            return false;
        }

        public bool TryCredit(string operationId, long rubles)
        {
            return !string.IsNullOrEmpty(operationId) && rubles > 0L && _credit(operationId, rubles);
        }

        public bool WasCredited(string operationId, out bool credited)
        {
            credited = false;

            if (string.IsNullOrEmpty(operationId))
            {
                return false;
            }

            bool? answer = _wasCredited(operationId);

            if (!answer.HasValue)
            {
                return false;
            }

            credited = answer.Value;
            return true;
        }
    }

    /// <summary>
    /// Готовый помощник выплаты: не даёт собрать идентификатор заново и проверяет,
    /// что выплата ровно одна.
    /// </summary>
    public static class DeliveryPayout
    {
        /// <summary>
        /// Завершает заказ и возвращает результат. Идентификатор выплаты берётся из сервиса
        /// (он же попадёт в кошелёк), поэтому повтор Complete идемпотентен.
        /// </summary>
        public static DeliveryResult CompleteAndPay(
            DeliveryService service,
            string orderId,
            IGameClockAdapter clock,
            ILocationProvider location,
            IPayoutAdapter payout)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return service.Complete(orderId, clock, location, payout);
        }

        /// <summary>Идентификатор выплаты, который будет использован (для логов/тестов).</summary>
        public static string PayoutIdOf(DeliveryService service, string orderId)
        {
            return service == null ? null : service.GetPayoutOperationId(orderId);
        }
    }

    // =========================================================================
    // 5. Платежи Debts
    // =========================================================================
    //
    // CODEX: реализовать в существующем MoneyWallet ровно эти операции:
    //          bool TryGetBalance(out long rubles);
    //          bool TryDebit(string paymentId, long rubles);        // идемпотентно, атомарно
    //          bool TryRefund(string paymentId, long rubles);       // идемпотентно
    //          bool TryWasDebited(string paymentId, out bool debited); // по durable-журналу
    //        Идентификаторы платежей передавайте через DebtPaymentId.ForPayment(...) —
    //        тогда они не пересекутся с "shop:" и "delivery:".

    /// <summary>Мост «MoneyWallet → IDebtWalletAdapter» для платежей по долгам.</summary>
    public sealed class DebtWalletBridge : IDebtWalletAdapter
    {
        private readonly Func<long> _readBalance;
        private readonly Func<string, long, bool> _debit;
        private readonly Func<string, long, bool> _refund;
        private readonly Func<string, bool?> _wasDebited;

        /// <param name="readBalance">
        /// Чтение баланса. Исключение трактуется как «баланс недоступен» и превращается
        /// в <c>false</c>: сервис долгов тогда не начнёт платёж.
        /// </param>
        /// <param name="debit">Идемпотентное списание по paymentId.</param>
        /// <param name="refund">Идемпотентный возврат по paymentId.</param>
        /// <param name="wasDebited">Запрос durable-журнала: true/false, null — журнал недоступен.</param>
        public DebtWalletBridge(
            Func<long> readBalance,
            Func<string, long, bool> debit,
            Func<string, long, bool> refund,
            Func<string, bool?> wasDebited)
        {
            _readBalance = readBalance ?? throw new ArgumentNullException(nameof(readBalance));
            _debit = debit ?? throw new ArgumentNullException(nameof(debit));
            _refund = refund ?? throw new ArgumentNullException(nameof(refund));
            _wasDebited = wasDebited ?? throw new ArgumentNullException(nameof(wasDebited));
        }

        public bool TryGetBalance(out long rubles)
        {
            rubles = 0L;

            try
            {
                rubles = _readBalance();
                return rubles >= 0L;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryDebit(string paymentId, long rubles)
        {
            return !string.IsNullOrEmpty(paymentId) && rubles > 0L && _debit(paymentId, rubles);
        }

        public bool TryRefund(string paymentId, long rubles)
        {
            return !string.IsNullOrEmpty(paymentId) && rubles > 0L && _refund(paymentId, rubles);
        }

        public bool TryWasDebited(string paymentId, out bool debited)
        {
            debited = false;

            if (string.IsNullOrEmpty(paymentId))
            {
                return false;
            }

            bool? answer = _wasDebited(paymentId);

            if (!answer.HasValue)
            {
                return false;
            }

            debited = answer.Value;
            return true;
        }
    }

    /// <summary>Готовый помощник платежа: идентификатор и повтор безопасны.</summary>
    public static class DebtPayment
    {
        /// <summary>
        /// Платёж по долгу с идентификатором из пространства имён долгов.
        /// Повтор с тем же ключом не списывает деньги второй раз.
        /// </summary>
        public static DebtPaymentResult Pay(DebtService service, string debtId, long amountRubles, string paymentKey)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return service.TryPay(new DebtPaymentRequest(debtId, amountRubles, DebtPaymentId.ForPayment(paymentKey)));
        }
    }
}
