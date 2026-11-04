// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Shop
//
// Явные состояния финансовой операции магазина и её запись в журнале.
//
// ЗАЧЕМ. Раньше исход покупки существовал только как код возврата одного вызова:
// если процесс падал (или адаптер бросал исключение) между списанием и выдачей товара,
// восстановить «где мы остановились» было нечем. Теперь каждая покупка — запись
// журнала с идентификатором и состоянием, которое двигается строго вперёд:
//
//   Reserved → Debited → Granted  (успех; деньги списаны, товар выдан)
//      │          │
//      │          └→ RefundPending → Refunded   (товар не выдан, деньги возвращены)
//      │                          → RefundFailed (возврат не удался, будет повторён)
//      └→ Declined                             (деньги не тронуты, операция закрыта)
//
//   Debited и RefundFailed — «незавершённые» состояния: они блокируют новые покупки
//   (код PendingOperation), пока операция не будет доиграна или компенсирована.

using System;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>
    /// Состояние покупки. Значения фиксированы числами: они попадают в снимок
    /// и не должны меняться при доработках (иначе старые сохранения «поедут»).
    /// </summary>
    public enum ShopOperationState
    {
        /// <summary>Операция ещё не начиналась / запись отсутствует.</summary>
        None = 0,

        /// <summary>Заявка зарезервирована, деньги ещё не списаны.</summary>
        Reserved = 1,

        /// <summary>Деньги списаны, товар ещё не выдан.</summary>
        Debited = 2,

        /// <summary>Товар выдан, деньги списаны — операция завершена успешно.</summary>
        Granted = 3,

        /// <summary>Нужно вернуть деньги (товар не выдан), возврат ещё не подтверждён.</summary>
        RefundPending = 4,

        /// <summary>Деньги возвращены — операция закрыта без товара.</summary>
        Refunded = 5,

        /// <summary>Возврат не удался, деньги игрока ещё у адаптера. Повторяется.</summary>
        RefundFailed = 6,

        /// <summary>Операция отклонена до списания: деньги и товар не тронуты.</summary>
        Declined = 7
    }

    /// <summary>
    /// Неизменяемая запись журнала операций магазина.
    /// Хранит ровно то, что нужно для восстановления: идентификаторы, количество,
    /// суммы и состояние. Итоговый код ответа (<see cref="EconomyResultCode"/>) НЕ хранится:
    /// он выводится из состояния, чтобы не было двух источников правды.
    /// </summary>
    public sealed class ShopOperation
    {
        /// <summary>Идентификатор покупки, заданный вызывающей стороной.</summary>
        public string PurchaseId { get; }

        /// <summary>Идентификатор операции списания: "shop:" + PurchaseId.</summary>
        public string DebitOperationId { get; }

        /// <summary>Идентификатор операции возврата: "shop-refund:" + PurchaseId.</summary>
        public string RefundOperationId { get; }

        public string ProductId { get; }

        /// <summary>Сколько покупок (упаковок) запрошено.</summary>
        public int RequestedQuantity { get; }

        /// <summary>Сколько единиц предмета должно быть выдано.</summary>
        public int Units { get; }

        /// <summary>Стоимость покупки (заполняется, когда её удалось посчитать).</summary>
        public Money Amount { get; internal set; }

        public ShopOperationState State { get; internal set; }

        /// <summary>Код отказа, если операция закрыта отказом (<see cref="ShopOperationState.Declined"/>).</summary>
        public EconomyResultCode DeclinedCode { get; internal set; }

        /// <summary>Причина отказа для лога (не для UI).</summary>
        public string Message { get; internal set; }

        internal ShopOperation(
            string purchaseId,
            string productId,
            int requestedQuantity,
            int units,
            Money amount)
        {
            PurchaseId = purchaseId;
            DebitOperationId = FinancialOperationId.ForPurchase(purchaseId);
            RefundOperationId = FinancialOperationId.ForRefund(purchaseId);
            ProductId = productId;
            RequestedQuantity = requestedQuantity;
            Units = units;
            Amount = amount;
            State = ShopOperationState.Reserved;
        }

        /// <summary>
        /// Операция завершена и ничего не ждёт: товар выдан, деньги возвращены
        /// или покупка отклонена до списания.
        /// </summary>
        public bool IsTerminal
        {
            get
            {
                return State == ShopOperationState.Granted
                       || State == ShopOperationState.Refunded
                       || State == ShopOperationState.Declined;
            }
        }

        /// <summary>
        /// Операция «висит»: деньги списаны, но итог не зафиксирован. Пока такие записи
        /// есть, новые покупки не начинаются.
        /// </summary>
        public bool IsPending
        {
            get
            {
                // Reserved входит сюда намеренно: запись создаётся непосредственно перед
                // списанием, поэтому «Reserved» = «исход списания ещё не установлен».
                return State == ShopOperationState.Reserved
                       || State == ShopOperationState.Debited
                       || State == ShopOperationState.RefundPending
                       || State == ShopOperationState.RefundFailed;
            }
        }

        /// <summary>
        /// Деньги списаны (подтверждено журналом кошелька или самим фактом успешного вызова).
        /// </summary>
        public bool IsDebited
        {
            get
            {
                return State == ShopOperationState.Debited
                       || State == ShopOperationState.Granted
                       || State == ShopOperationState.RefundPending
                       || State == ShopOperationState.RefundFailed
                       || State == ShopOperationState.Refunded;
            }
        }

        /// <summary>Код исхода, соответствующий текущему состоянию.</summary>
        public EconomyResultCode Code
        {
            get
            {
                switch (State)
                {
                    case ShopOperationState.Granted:
                        return EconomyResultCode.Success;

                    case ShopOperationState.Declined:
                        return DeclinedCode;

                    case ShopOperationState.Refunded:
                        return EconomyResultCode.InventoryRejected;

                    case ShopOperationState.RefundPending:
                    case ShopOperationState.RefundFailed:
                        return EconomyResultCode.PendingRefund;

                    case ShopOperationState.Debited:
                        return EconomyResultCode.PendingOperation;

                    case ShopOperationState.Reserved:
                        return EconomyResultCode.OutcomeUnknown;

                    default:
                        return EconomyResultCode.InternalError;
                }
            }
        }

        internal void MoveTo(ShopOperationState state, string message)
        {
            State = state;
            Message = message;
        }

        public override string ToString()
        {
            return "ShopOperation(" + PurchaseId + ", " + State + ", " + ProductId
                   + " x" + RequestedQuantity + ", " + Amount + ")";
        }
    }
}
