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
    /// может начислить деньги позже. Флаг <see cref="DeliveryStatus.PayoutGranted"/> защищает только сохранённое состояние.
    /// Для защиты между начислением и сохранением нужен IIdempotentPayoutAdapter и атомарный кошелёк.
    ///
    /// Слот активного заказа (<see cref="ActiveOrderId"/>) — производная величина от состояний
    /// заказов, а не независимое поле: при приёме он сверяется с <see cref="FindActiveEntry"/>.
    /// Поэтому «зависший» слот не блокирует игру, а второй активный заказ, появившийся после
    /// ручной правки состояния или будущей загрузки сохранения, не теряется.
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

            // Порядок проверок важен: «не тот заказ» и «уже есть активный заказ» не должны
            // менять состояние запрошенного заказа. Раньше просрочка обрабатывалась первой,
            // и попытка взять второй заказ молча переводила Available-заказ в Failed —
            // то есть операция с побочным эффектом отвечала причиной, к нему не относящейся.
            if (entry.State != DeliveryOrderState.Available)
            {
                // В том числе повторное принятие того же заказа.
                return DeliveryResult.Failure(
                    DeliveryResultCode.InvalidState, entry.ToStatus(), now);
            }

            OrderEntry active = FindActiveEntry();

            if (active != null)
            {
                // Слот всегда выводится из фактических состояний: если _activeOrderId потерялся
                // (ручная правка состояния, будущая загрузка сохранения), он восстанавливается
                // здесь же, поэтому «зависший» слот не может заблокировать игру навсегда.
                _activeOrderId = active.Order.Id;
                return DeliveryResult.Failure(
                    DeliveryResultCode.AlreadyHasActiveOrder, entry.ToStatus(), now);
            }

            _activeOrderId = null;

            // Просроченный (в том числе ещё не принятый) заказ нельзя принять — фиксируем провал.
            if (IsExpired(entry, now))
            {
                return Fail(entry, now);
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
            var idempotent = payout as IIdempotentPayoutAdapter;
            // Legacy adapter remains supported, but cannot protect crash/retry.
            if (!SafeAdapter.Try(() => idempotent != null
                ? idempotent.TryCredit("delivery:" + entry.Order.Id, payment)
                : payout.TryCredit(payment), out paid))
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

            SyncActiveSlot();
            return new DeliveryResult(DeliveryResultCode.Success, entry.ToStatus(), 0L, double.NaN);
        }

        // ---------- сохранение / восстановление ----------

        /// <summary>
        /// Снимок всех заказов вместе с состоянием жизненного цикла.
        /// Нужен, чтобы после загрузки игры флаг <see cref="DeliveryStatus.PayoutGranted"/>
        /// не терялся: без него выполненный заказ снова стал бы Available
        /// и его можно было бы оплатить второй раз.
        /// </summary>
        public DeliverySnapshot CreateSnapshot()
        {
            var orders = new DeliveryOrderSnapshot[_orderList.Count];

            for (int i = 0; i < _orderList.Count; i++)
            {
                DeliveryOrder order = _orderList[i];
                OrderEntry entry = _orders[order.Id];

                orders[i] = new DeliveryOrderSnapshot
                {
                    Id = order.Id,
                    CargoTitle = order.CargoTitle,
                    PickupPointId = order.PickupPoint.Id,
                    PickupPointName = order.PickupPoint.DisplayName,
                    DropOffPointId = order.DropOffPoint.Id,
                    DropOffPointName = order.DropOffPoint.DisplayName,
                    PaymentRubles = order.PaymentRubles,
                    DeadlineGameMinutes = order.DeadlineGameMinutes,
                    State = (int)entry.State,
                    HasAcceptedAt = entry.AcceptedAt.HasValue,
                    AcceptedAtMinutes = entry.AcceptedAt ?? 0d,
                    HasPickedUpAt = entry.PickedUpAt.HasValue,
                    PickedUpAtMinutes = entry.PickedUpAt ?? 0d,
                    HasClosedAt = entry.ClosedAt.HasValue,
                    ClosedAtMinutes = entry.ClosedAt ?? 0d,
                    PayoutGranted = entry.PayoutGranted
                };
            }

            return new DeliverySnapshot
            {
                SchemaVersion = DeliverySnapshot.CurrentSchemaVersion,
                Orders = orders
            };
        }

        /// <summary>
        /// Заменяет состояние сервиса данными снимка. Снимок проверяется целиком
        /// до применения: повреждённый не меняет ничего. Восстановление допустимо
        /// только на «чистом» сервисе (до <see cref="RegisterOrder"/>) — иначе заказы,
        /// зарегистрированные кодом до загрузки, были бы потеряны.
        /// </summary>
        /// <param name="snapshot">Снимок.</param>
        /// <param name="error">Описание проблемы (null при успехе).</param>
        /// <returns><c>true</c> — состояние заменено.</returns>
        public bool RestoreSnapshot(DeliverySnapshot snapshot, out string error)
        {
            if (!DeliverySnapshotValidator.Validate(snapshot, out error))
            {
                return false;
            }

            if (_orderList.Count > 0)
            {
                error = "Восстановление допустимо только на пустом сервисе доставки.";
                return false;
            }

            // Собираем новое состояние в локальных структурах; рабочее не трогаем,
            // пока все записи не построены.
            var restored = new Dictionary<string, OrderEntry>(StringComparer.Ordinal);
            var restoredList = new List<DeliveryOrder>(snapshot.Orders.Length);

            for (int i = 0; i < snapshot.Orders.Length; i++)
            {
                DeliveryOrderSnapshot source = snapshot.Orders[i];
                DeliveryOrder order;

                try
                {
                    order = new DeliveryOrder(
                        source.Id,
                        source.CargoTitle,
                        new DeliveryPoint(source.PickupPointId, source.PickupPointName),
                        new DeliveryPoint(source.DropOffPointId, source.DropOffPointName),
                        source.PaymentRubles,
                        source.DeadlineGameMinutes);
                }
                catch (Exception exception)
                {
                    error = "Заказ " + source.Id + ": " + exception.Message;
                    return false;
                }

                var entry = new OrderEntry(order);
                entry.State = (DeliveryOrderState)source.State;
                entry.AcceptedAt = source.HasAcceptedAt ? (double?)source.AcceptedAtMinutes : null;
                entry.PickedUpAt = source.HasPickedUpAt ? (double?)source.PickedUpAtMinutes : null;
                entry.ClosedAt = source.HasClosedAt ? (double?)source.ClosedAtMinutes : null;
                entry.PayoutGranted = source.PayoutGranted;

                restored.Add(order.Id, entry);
                restoredList.Add(order);
            }

            _orders.Clear();
            foreach (KeyValuePair<string, OrderEntry> pair in restored)
            {
                _orders.Add(pair.Key, pair.Value);
            }

            _orderList.Clear();
            _orderList.AddRange(restoredList);

            // Слот выводится из фактических состояний, а не восстанавливается полем:
            // «зависший» слот после загрузки невозможен.
            _activeOrderId = null;
            SyncActiveSlot();

            error = null;
            return true;
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

        /// <summary>
        /// Первый по порядку регистрации заказ в состоянии Accepted/PickedUp.
        /// Используется для восстановления <see cref="_activeOrderId"/>: слот всегда
        /// выводится из фактических состояний, поэтому его нельзя «потерять» или «подделать».
        /// </summary>
        private OrderEntry FindActiveEntry()
        {
            for (int i = 0; i < _orderList.Count; i++)
            {
                OrderEntry candidate;

                if (_orders.TryGetValue(_orderList[i].Id, out candidate) && candidate.IsActive)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Приводит <see cref="_activeOrderId"/> в соответствие с фактическими состояниями.
        /// Возвращает идентификатор активного заказа (или null). Вызывается только из методов
        /// чтения вроде <see cref="GetStatus"/> — бизнес-операции делают эту сверку сами.
        /// </summary>
        private string SyncActiveSlot()
        {
            OrderEntry active = FindActiveEntry();
            _activeOrderId = active == null ? null : active.Order.Id;
            return _activeOrderId;
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

        /// <summary>
        /// Статус-заглушка «заказа нет». Состояние — <see cref="DeliveryOrderState.None"/>:
        /// раньше здесь стояло Available, и UI/адаптеры могли показать несуществующий
        /// заказ как доступный к принятию.
        /// </summary>
        private static DeliveryStatus NotFoundStatus()
        {
            return new DeliveryStatus(null, DeliveryOrderState.None, null, null, null, false);
        }
    }
}
