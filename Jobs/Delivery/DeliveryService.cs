// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery

using System;
using System.Collections.Generic;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Реализация курьерских заказов в памяти. Зависит только от абстракций:
    /// <see cref="IGameClockAdapter"/>, <see cref="IPayoutAdapter"/>, <see cref="ILocationProvider"/>.
    /// Ни собственного кошелька, ни таймера, ни системы перемещения внутри нет.
    /// </summary>
    /// <remarks>
    /// Неизвестный заказ всегда даёт <see cref="DeliveryResultCode.OrderNotFound"/> и статус
    /// с <c>OrderId = null</c>: «фантомного» состояния Available у несуществующего заказа нет.
    ///
    /// Атомарность выплаты: состояние заказа переводится в Completed только после успешного
    /// <see cref="IPayoutAdapter.TryCredit"/>. Если начисление вернуло false или упало,
    /// заказ остаётся в PickedUp, выплата не считается сделанной — повторный вызов Complete
    /// может начислить деньги позже. Флаг <see cref="DeliveryStatus.PayoutGranted"/> гарантирует,
    /// что при успехе начисление происходит ровно один раз.
    /// </remarks>
    public sealed class DeliveryService : IDeliveryService
    {
        private sealed class OrderEntry
        {
            public OrderEntry(DeliveryOrder order)
            {
                Order = order;
                State = DeliveryOrderState.Available;
            }

            public DeliveryOrder Order { get; }

            public DeliveryOrderState State { get; set; }

            public double? AcceptedAt { get; set; }

            public double? PickedUpAt { get; set; }

            public double? ClosedAt { get; set; }

            public bool PayoutGranted { get; set; }

            public bool IsActive
            {
                get { return State == DeliveryOrderState.Accepted || State == DeliveryOrderState.PickedUp; }
            }

            public DeliveryStatus ToStatus()
            {
                return new DeliveryStatus(
                    Order.Id,
                    State,
                    AcceptedAt,
                    PickedUpAt,
                    ClosedAt,
                    PayoutGranted);
            }
        }

        private readonly Dictionary<string, OrderEntry> _orders =
            new Dictionary<string, OrderEntry>(StringComparer.Ordinal);

        private readonly List<DeliveryOrder> _orderList = new List<DeliveryOrder>();

        private string _activeOrderId;

        public string ActiveOrderId => _activeOrderId;

        public IReadOnlyList<DeliveryOrder> Orders => _orderList.AsReadOnly();

        public DeliveryResult RegisterOrder(DeliveryOrder order)
        {
            if (order == null)
            {
                return DeliveryResult.Failure(DeliveryResultCode.InvalidArgument, NotFoundStatus(), double.NaN);
            }

            if (_orders.ContainsKey(order.Id))
            {
                return DeliveryResult.Failure(DeliveryResultCode.InvalidArgument, NotFoundStatus(), double.NaN);
            }

            var entry = new OrderEntry(order);
            _orders.Add(order.Id, entry);
            _orderList.Add(order);

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, double.NaN);
        }

        public DeliveryResult Accept(string orderId, IGameClockAdapter clock)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            // Просроченный (в том числе ещё не принятый) заказ нельзя принять — фиксируем провал.
            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            if (entry.State != DeliveryOrderState.Available)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            if (_activeOrderId != null)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AlreadyHasActiveOrder, entry.ToStatus(), now);
            }

            entry.State = DeliveryOrderState.Accepted;
            entry.AcceptedAt = now;
            _activeOrderId = entry.Order.Id;

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, now);
        }

        public DeliveryResult PickUp(string orderId, IGameClockAdapter clock, ILocationProvider location)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            if (location == null)
            {
                throw new DeliveryServiceException("Не передан провайдер местоположения (ILocationProvider).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            if (entry.State != DeliveryOrderState.Accepted)
            {
                // В том числе повторное получение груза и получение до принятия заказа.
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            string locationId;
            if (!TryReadLocation(location, out locationId))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, entry.ToStatus(), now);
            }

            if (!IsSamePoint(entry.Order.PickupPoint, locationId))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.WrongLocation, entry.ToStatus(), now);
            }

            entry.State = DeliveryOrderState.PickedUp;
            entry.PickedUpAt = now;

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, now);
        }

        public DeliveryResult Complete(
            string orderId,
            IGameClockAdapter clock,
            ILocationProvider location,
            IPayoutAdapter payout)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            if (location == null)
            {
                throw new DeliveryServiceException("Не передан провайдер местоположения (ILocationProvider).");
            }

            if (payout == null)
            {
                throw new DeliveryServiceException("Не передан адаптер начисления денег (IPayoutAdapter).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            // Завершить можно только заказ с полученным грузом. Повторное завершение
            // (заказ уже Completed) отсекается здесь же: выплата второй раз невозможна.
            if (entry.State != DeliveryOrderState.PickedUp)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            string locationId;
            if (!TryReadLocation(location, out locationId))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, entry.ToStatus(), now);
            }

            if (!IsSamePoint(entry.Order.DropOffPoint, locationId))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.WrongLocation, entry.ToStatus(), now);
            }

            // Страховка от двойной выплаты: если по какой-то причине флаг уже стоял — не платим снова.
            if (entry.PayoutGranted)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.InternalError, entry.ToStatus(), now);
            }

            long payment = entry.Order.PaymentRubles;
            bool paid;
            if (!SafeAdapter.Try(() => payout.TryCredit(payment), out paid))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, entry.ToStatus(), now);
            }

            if (!paid)
            {
                // Деньги не начислены, заказ остаётся в PickedUp и может быть завершён позже.
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, entry.ToStatus(), now);
            }

            entry.PayoutGranted = true;
            entry.State = DeliveryOrderState.Completed;
            entry.ClosedAt = now;
            ClearActiveIfMatches(entry.Order.Id);

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), payment, now);
        }

        public DeliveryResult Cancel(string orderId, IGameClockAdapter clock)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            if (entry.State == DeliveryOrderState.PickedUp)
            {
                // Груз уже у игрока: просто отменить заказ нельзя, иначе груз «испарится».
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            if (entry.State != DeliveryOrderState.Available && entry.State != DeliveryOrderState.Accepted)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            entry.State = DeliveryOrderState.Cancelled;
            entry.ClosedAt = now;
            ClearActiveIfMatches(entry.Order.Id);

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, now);
        }

        public DeliveryResult Refresh(string orderId, IGameClockAdapter clock)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, now);
        }

        public DeliveryResult Abandon(string orderId, IGameClockAdapter clock)
        {
            if (clock == null)
            {
                throw new DeliveryServiceException("Не передан адаптер игрового времени (IGameClockAdapter).");
            }

            double now;
            if (!TryReadClock(clock, out now))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.AdapterFailure, NotFoundStatus(), double.NaN);
            }

            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), now);
            }

            if (IsOpen(entry.State) && IsExpired(entry, now))
            {
                return Fail(entry, now);
            }

            if (entry.State != DeliveryOrderState.Accepted)
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            entry.State = DeliveryOrderState.Cancelled;
            entry.ClosedAt = now;
            ClearActiveIfMatches(entry.Order.Id);

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, now);
        }

        public DeliveryResult GetStatus(string orderId)
        {
            OrderEntry entry;
            if (!TryFind(orderId, out entry))
            {
                return DeliveryResult.Failure(
                    DeliveryResultCode.OrderNotFound, NotFoundStatus(), double.NaN);
            }

            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, double.NaN);
        }

        // ---------- helpers ----------

        private DeliveryResult Fail(OrderEntry entry, double now)
        {
            entry.State = DeliveryOrderState.Failed;
            entry.ClosedAt = now;
            ClearActiveIfMatches(entry.Order.Id);

            return DeliveryResult.Failure(DeliveryResultCode.DeadlineExpired, entry.ToStatus(), now);
        }

        private void ClearActiveIfMatches(string orderId)
        {
            if (string.Equals(_activeOrderId, orderId, StringComparison.Ordinal))
            {
                _activeOrderId = null;
            }
        }

        private bool TryFind(string orderId, out OrderEntry entry)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                entry = null;
                return false;
            }

            return _orders.TryGetValue(orderId, out entry);
        }

        /// <summary>Заказ ещё не закрыт: Available, Accepted или PickedUp.</summary>
        private static bool IsOpen(DeliveryOrderState state)
        {
            return state == DeliveryOrderState.Available
                   || state == DeliveryOrderState.Accepted
                   || state == DeliveryOrderState.PickedUp;
        }

        private static bool IsExpired(OrderEntry entry, double now)
        {
            return now > entry.Order.DeadlineGameMinutes;
        }

        private static bool IsSamePoint(DeliveryPoint point, string locationId)
        {
            return !string.IsNullOrEmpty(locationId)
                   && string.Equals(point.Id, locationId, StringComparison.Ordinal);
        }

        private static bool TryReadClock(IGameClockAdapter clock, out double now)
        {
            double value = 0d;
            bool ok = SafeAdapter.Try(() => { value = clock.NowMinutes; });

            // NaN и бесконечность считаются отказом адаптера: сравнения сроков с ними бессмысленны.
            if (!ok || double.IsNaN(value) || double.IsInfinity(value))
            {
                now = double.NaN;
                return false;
            }

            now = value;
            return true;
        }

        private static bool TryReadLocation(ILocationProvider location, out string locationId)
        {
            string value = null;
            bool ok = SafeAdapter.Try(() => { value = location.CurrentLocationId; });

            locationId = ok ? value : null;
            return ok;
        }

        /// <summary>Статус-заглушка «заказа нет»: состояния Available у него не бывает.</summary>
        private static DeliveryStatus NotFoundStatus()
        {
            return new DeliveryStatus(null, DeliveryOrderState.Available, null, null, null, false);
        }
    }
}
