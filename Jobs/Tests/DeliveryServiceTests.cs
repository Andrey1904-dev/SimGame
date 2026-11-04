// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Tests
//
// Покрытие ТЗ:
//  1. Успешное выполнение (Available → Accepted → PickedUp → Completed, выплата один раз).
//  2. Просрочка (Failed без выплаты) — на получении, завершении и по Refresh.
//  3. Повторная выплата (повторный Complete, повторный PickUp, повторный Accept).
//  4. Неверная точка (получение и завершение).
//  5. Отмена (Available, Accepted, запрет после получения груза).
//  6. Исключения/отказы адаптеров (время, локация, начисление).
//  7. Один активный заказ, некорректные данные, регистрация заказов.

using System;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Jobs.Tests.Fakes;

namespace LifeFromZero.Jobs.Tests
{
    /// <summary>Тесты сервиса курьерских заказов.</summary>
    public static class DeliveryServiceTests
    {
        // Вспомогательные «точки» для тестовых данных (id совпадают с DeliveryPoint.Id).
        private const string Bakery = "bakery";
        private const string Office = "office_12";
        private const string Cafe = "cafe";

        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Доставка: успешное выполнение с выплатой", HappyPath);
            suite.Add("Доставка: выплата начисляется ровно один раз", PayoutOnlyOnce);
            suite.Add("Доставка: просрочка до получения груза", ExpiredBeforePickUp);
            suite.Add("Доставка: просрочка после получения груза", ExpiredAfterPickUp);
            suite.Add("Доставка: просроченный заказ нельзя принять", AcceptExpiredOrder);
            suite.Add("Доставка: Refresh проваливает просроченный заказ", RefreshExpiresOrder);
            suite.Add("Доставка: отмена не маскирует просрочку", CancelExpiredOrder);
            suite.Add("Доставка: повторное завершение отклоняется без выплаты", RepeatedComplete);
            suite.Add("Доставка: повторное получение груза отклоняется", RepeatedPickUp);
            suite.Add("Доставка: повторное принятие заказа отклоняется", RepeatedAccept);
            suite.Add("Доставка: неудачная попытка взять второй заказ не меняет его состояние", SecondAcceptAttemptDoesNotMutateRequestedOrder);
            suite.Add("Доставка: неверная точка получения", WrongPickupLocation);
            suite.Add("Доставка: неверная точка доставки", WrongDropOffLocation);
            suite.Add("Доставка: завершение до получения груза", CompleteBeforePickUp);
            suite.Add("Доставка: получение груза без принятия заказа", PickUpWithoutAccept);
            suite.Add("Доставка: отмена доступного заказа", CancelAvailable);
            suite.Add("Доставка: отмена принятого заказа освобождает слот", CancelAccepted);
            suite.Add("Доставка: отмена после получения груза запрещена", CancelAfterPickUp);
            suite.Add("Доставка: отказ от заказа до получения груза", AbandonAccepted);
            suite.Add("Доставка: второй активный заказ не принимается", SecondActiveOrderRejected);
            suite.Add("Доставка: после завершения можно взять новый заказ", NewOrderAfterCompletion);
            suite.Add("Доставка: несуществующий заказ", UnknownOrder);
            suite.Add("Доставка: исключение адаптера времени", ClockThrows);
            suite.Add("Доставка: NaN от адаптера времени", ClockReturnsNaN);
            suite.Add("Доставка: исключение провайдера локации", LocationThrows);
            suite.Add("Доставка: отказ начисления оставляет заказ активным", PayoutRejected);
            suite.Add("Доставка: исключение начисления не теряет заказ", PayoutThrows);
            suite.Add("Доставка: повторное завершение после неудачной выплаты проходит", RetryAfterPayoutFailure);
            suite.Add("Доставка: просрочка после неудачной выплаты не платит", PayeeRejectsAfterClockShift);
            suite.Add("Доставка: регистрация заказа и дубликаты", RegisterOrder);
            suite.Add("Доставка: некорректные данные заказа", InvalidOrderData);
            suite.Add("Доставка: без адаптера времени — DeliveryServiceException", ServiceRequiresAdapters);

            // Регрессионные тесты аудита ТЗ №015 (найденные дефекты и границы времени).
            suite.Add("Доставка: второй активный заказ не маскирует просрочку", ActiveOrderDoesNotMaskExpiry);
            suite.Add("Доставка: зависший слот не блокирует новые заказы", StaleActiveSlotDoesNotLockPlayer);
            suite.Add("Доставка: скачок времени на огромную величину", HugeTimeJump);
            suite.Add("Доставка: ровно в срок — успех, на минуту позже — провал", DeadlineBoundary);
            suite.Add("Доставка: повторная отмена не меняет состояние", RepeatedCancel);
            suite.Add("Доставка: Abandon после получения груза и до принятия", AbandonOnlyWhileAccepted);
            suite.Add("Доставка: Refresh после завершения не платит повторно", RefreshAfterCompletion);
            suite.Add("Доставка: выплата ровно один раз при многократных операциях", PayoutOnceUnderMixedCalls);

            // Регрессионные тесты аудита ТЗ №017 (сохранение состояния заказов).
            suite.Add("Снимок доставки: флаг выплаты переживает сохранение — повторной оплаты нет", SnapshotKeepsPayoutFlag);
            suite.Add("Снимок доставки: активный заказ и слот восстанавливаются", SnapshotRestoresActiveOrder);
            suite.Add("Снимок доставки: повреждённый снимок отклонён и не меняет состояние", SnapshotRejectsCorrupt);
            suite.Add("Снимок доставки: восстановление поверх зарегистрированных заказов отклонено", SnapshotRejectsNonEmpty);
            suite.Add("Статус: несуществующий заказ не выглядит доступным", NotFoundIsNotAvailable);

            return suite;
        }

        // ---------- вспомогательные ----------

        private static DeliveryOrder NewOrder(
            string id = "order-1",
            string cargo = "Хлеб",
            long payment = 150L,
            long deadline = 600L)
        {
            return new DeliveryOrder(
                id,
                cargo,
                new DeliveryPoint(Bakery, "Пекарня"),
                new DeliveryPoint(Office, "Офис 12"),
                payment,
                deadline);
        }

        private static IDeliveryService NewServiceWithOrder(out DeliveryOrder order, long deadline = 600L, long payment = 150L)
        {
            var service = new DeliveryService();
            order = NewOrder(payment: payment, deadline: deadline);
            service.RegisterOrder(order);
            return service;
        }

        /// <summary>Доводит заказ до состояния PickedUp.</summary>
        private static IDeliveryService ToPickedUp(
            out DeliveryOrder order,
            out FakeClock clock,
            out FakeLedger ledger,
            out FakeLocation location,
            long deadline = 600L,
            long payment = 150L)
        {
            IDeliveryService service = NewServiceWithOrder(out order, deadline, payment);
            clock = new FakeClock(0d);
            ledger = new FakeLedger();
            location = new FakeLocation(Bakery);

            service.Accept(order.Id, clock);
            service.PickUp(order.Id, clock, location);

            location.MoveTo(Office);
            return service;
        }

        // ---------- 1. Успешное выполнение ----------

        private static void HappyPath()
        {
            var service = new DeliveryService();
            DeliveryOrder order = NewOrder();
            MiniTestRunner.IsTrue(service.RegisterOrder(order).IsSuccess, "заказ зарегистрирован");

            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery);

            DeliveryResult accepted = service.Accept(order.Id, clock);
            MiniTestRunner.IsTrue(accepted.IsSuccess, "заказ принят");
            MiniTestRunner.AreEqual(DeliveryOrderState.Accepted, accepted.Status.State, "состояние Accepted");
            MiniTestRunner.AreEqual(order.Id, service.ActiveOrderId, "активный заказ зафиксирован");

            DeliveryResult pickedUp = service.PickUp(order.Id, clock, location);
            MiniTestRunner.IsTrue(pickedUp.IsSuccess, "груз получен");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, pickedUp.Status.State, "состояние PickedUp");

            location.MoveTo(Office);
            clock.Advance(30d);

            DeliveryResult completed = service.Complete(order.Id, clock, location, ledger);
            MiniTestRunner.IsTrue(completed.IsSuccess, "заказ завершён");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, completed.Status.State, "состояние Completed");
            MiniTestRunner.AreEqual(150L, completed.PaidRubles, "выплата в результате");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "деньги начислены");
            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "начисление вызвано один раз");
            MiniTestRunner.IsTrue(completed.Status.PayoutGranted, "флаг выплаты установлен");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        private static void PayoutOnlyOnce()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);

            DeliveryResult first = service.Complete(order.Id, clock, location, ledger);
            DeliveryResult second = service.Complete(order.Id, clock, location, ledger);
            DeliveryResult third = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.IsTrue(first.IsSuccess, "первое завершение успешно");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, second.Code, "второе завершение отклонено");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, third.Code, "третье завершение отклонено");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "выплата ровно одна");
            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "начисление вызвано один раз");
            MiniTestRunner.AreEqual(0L, second.PaidRubles, "по отказу ничего не начислено");
        }

        // ---------- 2. Просрочка ----------

        private static void ExpiredBeforePickUp()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order, deadline: 60L);
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery);

            service.Accept(order.Id, clock);
            clock.Advance(61d);

            DeliveryResult pickedUp = service.PickUp(order.Id, clock, location);
            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, pickedUp.Code, "получение после срока отклонено");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, pickedUp.Status.State, "заказ провален");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");

            DeliveryResult completed = service.Complete(order.Id, clock, location, ledger);
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, completed.Code, "завершить проваленный заказ нельзя");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
        }

        private static void ExpiredAfterPickUp()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location, deadline: 60L);
            clock.Advance(61d);

            DeliveryResult completed = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, completed.Code, "завершение после срока отклонено");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, completed.Status.State, "заказ провален");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплаты за просрочку нет");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        private static void AcceptExpiredOrder()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order, deadline: 10L);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);

            clock.Advance(11d);
            DeliveryResult result = service.Accept(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, result.Code, "просроченный заказ не принимается");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, result.Status.State, "заказ сразу провален");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот не занят");
        }

        private static void CancelExpiredOrder()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order, deadline: 20L);
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery);
            service.Accept(order.Id, clock);

            clock.Advance(21d);
            DeliveryResult result = service.Cancel(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, result.Code, "просрочку нельзя замаскировать отменой");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, result.Status.State, "заказ провален");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплаты нет");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        private static void RefreshExpiresOrder()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order, deadline: 30L);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);

            service.Accept(order.Id, clock);

            DeliveryResult alive = service.Refresh(order.Id, clock);
            MiniTestRunner.IsTrue(alive.IsSuccess, "до срока заказ жив");
            MiniTestRunner.AreEqual(DeliveryOrderState.Accepted, alive.Status.State, "состояние не изменилось");

            clock.Advance(31d);
            DeliveryResult expired = service.Refresh(order.Id, clock);
            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, expired.Code, "просрочка зафиксирована");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, expired.Status.State, "заказ провален");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        // ---------- 3. Повторные операции ----------

        private static void RepeatedComplete()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            service.Complete(order.Id, clock, location, ledger);

            for (int i = 0; i < 5; i++)
            {
                DeliveryResult again = service.Complete(order.Id, clock, location, ledger);
                MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, again.Code, "повтор №" + (i + 1) + " отклонён");
            }

            MiniTestRunner.AreEqual(150L, ledger.Credited, "итоговая выплата не выросла");
            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "начисление ровно один раз");
        }

        private static void RepeatedPickUp()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);
            service.Accept(order.Id, clock);

            DeliveryResult first = service.PickUp(order.Id, clock, location);
            DeliveryResult second = service.PickUp(order.Id, clock, location);

            MiniTestRunner.IsTrue(first.IsSuccess, "первое получение успешно");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, second.Code, "повторное получение отклонено");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, second.Status.State, "состояние не сброшено");
        }

        private static void RepeatedAccept()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);

            DeliveryResult first = service.Accept(order.Id, clock);
            DeliveryResult second = service.Accept(order.Id, clock);

            MiniTestRunner.IsTrue(first.IsSuccess, "первое принятие успешно");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, second.Code, "повторное принятие отклонено");

            // Аудит ТЗ №015: ForbidAcceptingSecondOrder — попытка взять другой заказ при активном
            // первом не должна менять состояние запрошенного заказа. Если заказ уже был
            // Accepted, проверка чужого активного заказа не должна подменять код InvalidState.
            DeliveryOrder other = NewOrder("order-2", payment: 100L);
            service.RegisterOrder(other);
            MiniTestRunner.AreEqual(
                DeliveryResultCode.InvalidState,
                service.Accept(order.Id, clock).Code,
                "повторное принятие при активном заказе даёт InvalidState, а не AlreadyHasActiveOrder");
        }

        /// <summary>
        /// Регрессия ТЗ №015 (сценарий ForbidAcceptingSecondOrder): попытка взять второй заказ
        /// при активном первом не должна ни сжигать запрошенный Available-заказ,
        /// ни подменять причину отказа.
        /// </summary>
        private static void SecondAcceptAttemptDoesNotMutateRequestedOrder()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder first, deadline: 500L);
            var clock = new FakeClock(0d);
            DeliveryOrder second = NewOrder("order-2", payment: 100L, deadline: 10L);
            service.RegisterOrder(second);

            MiniTestRunner.IsTrue(service.Accept(first.Id, clock).IsSuccess, "первый заказ принят");

            clock.Advance(11d); // срок второго заказа истёк
            DeliveryResult attempt = service.Accept(second.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.AlreadyHasActiveOrder, attempt.Code, "отказ по активному заказу");
            MiniTestRunner.AreEqual(
                DeliveryOrderState.Available, attempt.Status.State,
                "запрошенный заказ не переведён в Failed побочным эффектом");
        }


        // ---------- 4. Неверная точка ----------

        private static void WrongPickupLocation()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Cafe);
            service.Accept(order.Id, clock);

            DeliveryResult result = service.PickUp(order.Id, clock, location);

            MiniTestRunner.AreEqual(DeliveryResultCode.WrongLocation, result.Code, "получение не в той точке");
            MiniTestRunner.AreEqual(DeliveryOrderState.Accepted, result.Status.State, "состояние не изменилось");
        }

        private static void WrongDropOffLocation()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            location.MoveTo(Cafe);

            DeliveryResult result = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.WrongLocation, result.Code, "завершение не в той точке");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, result.Status.State, "заказ остался в PickedUp");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплаты не было");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
        }

        // ---------- 5. Неверная последовательность ----------

        private static void CompleteBeforePickUp()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Office);
            service.Accept(order.Id, clock);

            DeliveryResult result = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, result.Code, "завершение без груза отклонено");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплаты не было");
        }

        private static void PickUpWithoutAccept()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);

            DeliveryResult result = service.PickUp(order.Id, clock, location);

            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, result.Code, "получение без принятия отклонено");
            MiniTestRunner.AreEqual(DeliveryOrderState.Available, result.Status.State, "заказ остался Available");
        }

        // ---------- 6. Отмена ----------

        private static void CancelAvailable()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);

            DeliveryResult result = service.Cancel(order.Id, clock);

            MiniTestRunner.IsTrue(result.IsSuccess, "доступный заказ отменяется");
            MiniTestRunner.AreEqual(DeliveryOrderState.Cancelled, result.Status.State, "состояние Cancelled");

            DeliveryResult acceptAfter = service.Accept(order.Id, clock);
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, acceptAfter.Code, "отменённый заказ нельзя принять");
        }

        private static void CancelAccepted()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);
            service.Accept(order.Id, clock);

            DeliveryResult result = service.Cancel(order.Id, clock);

            MiniTestRunner.IsTrue(result.IsSuccess, "принятый заказ отменяется");
            MiniTestRunner.AreEqual(DeliveryOrderState.Cancelled, result.Status.State, "состояние Cancelled");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");

            DeliveryOrder second = NewOrder("order-2", payment: 200L);
            service.RegisterOrder(second);
            DeliveryResult accept = service.Accept(second.Id, clock);
            MiniTestRunner.IsTrue(accept.IsSuccess, "после отмены можно взять новый заказ");
        }

        private static void CancelAfterPickUp()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);

            DeliveryResult result = service.Cancel(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, result.Code, "после груза отмена запрещена");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, result.Status.State, "груз остаётся у игрока");
            MiniTestRunner.AreEqual(order.Id, service.ActiveOrderId, "слот не освобождён");

            DeliveryResult completed = service.Complete(order.Id, clock, location, ledger);
            MiniTestRunner.IsTrue(completed.IsSuccess, "после неудачной отмены заказ можно завершить");
        }

        private static void AbandonAccepted()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);
            service.Accept(order.Id, clock);

            DeliveryResult result = service.Abandon(order.Id, clock);

            MiniTestRunner.IsTrue(result.IsSuccess, "отказ до получения груза разрешён");
            MiniTestRunner.AreEqual(DeliveryOrderState.Cancelled, result.Status.State, "состояние Cancelled");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        // ---------- 7. Один активный заказ ----------

        private static void SecondActiveOrderRejected()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder first);
            service.RegisterOrder(NewOrder("order-2", payment: 100L));

            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);

            MiniTestRunner.IsTrue(service.Accept(first.Id, clock).IsSuccess, "первый заказ принят");
            DeliveryResult second = service.Accept("order-2", clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.AlreadyHasActiveOrder, second.Code, "второй активный заказ отклонён");
            MiniTestRunner.AreEqual(first.Id, service.ActiveOrderId, "активным остался первый");
        }

        private static void NewOrderAfterCompletion()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            service.Complete(order.Id, clock, location, ledger);

            DeliveryOrder second = NewOrder("order-2", payment: 90L, deadline: 900L);
            service.RegisterOrder(second);

            DeliveryResult accepted = service.Accept(second.Id, clock);
            MiniTestRunner.IsTrue(accepted.IsSuccess, "после завершения можно принять новый заказ");
            MiniTestRunner.AreEqual(second.Id, service.ActiveOrderId, "активен новый заказ");
        }

        private static void UnknownOrder()
        {
            IDeliveryService service = new DeliveryService();
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery);

            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.GetStatus("nope").Code, "GetStatus неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Accept("nope", clock).Code, "Accept неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.PickUp("nope", clock, location).Code, "PickUp неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Complete("nope", clock, location, ledger).Code, "Complete неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Cancel("nope", clock).Code, "Cancel неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Refresh("nope", clock).Code, "Refresh неизвестного");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Accept(null, clock).Code, "null-идентификатор");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
        }

        // ---------- 8. Исключения адаптеров ----------

        private static void ClockThrows()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d) { ThrowOnRead = true };
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery);

            DeliveryResult result = service.Accept(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, result.Code, "сбой времени => AdapterFailure");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "заказ не был принят");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
        }

        private static void ClockReturnsNaN()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(double.NaN);
            var location = new FakeLocation(Bakery);

            DeliveryResult result = service.Accept(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, result.Code, "NaN считается сбоем адаптера");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "заказ не был принят");
        }

        private static void LocationThrows()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();
            var location = new FakeLocation(Bakery) { ThrowOnRead = true };
            service.Accept(order.Id, clock);

            DeliveryResult pickUp = service.PickUp(order.Id, clock, location);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, pickUp.Code, "сбой локации => AdapterFailure");
            MiniTestRunner.AreEqual(DeliveryOrderState.Accepted, pickUp.Status.State, "состояние не изменилось");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
        }

        private static void PayoutRejected()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            ledger.CreditOverride = false;

            DeliveryResult result = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, result.Code, "отказ начисления => AdapterFailure");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, result.Status.State, "заказ остался активным");
            MiniTestRunner.IsFalse(result.Status.PayoutGranted, "выплата не зафиксирована");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
            MiniTestRunner.AreEqual(order.Id, service.ActiveOrderId, "слот не освобождён");
        }

        private static void PayoutThrows()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            ledger.ThrowOnCredit = true;

            DeliveryResult result = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, result.Code, "исключение начисления => AdapterFailure");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, result.Status.State, "заказ не потерян");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
        }

        private static void PayeeRejectsAfterClockShift()
        {
            // Начисление отклонено, затем время ушло за срок: повторная попытка уже не платит,
            // а фиксирует просрочку — «долг» за выполненную работу не возникает задним числом.
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location, deadline: 60L);
            ledger.CreditOverride = false;
            DeliveryResult rejected = service.Complete(order.Id, clock, location, ledger);
            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, rejected.Code, "первая попытка отклонена");

            ledger.CreditOverride = null;
            clock.Advance(61d);
            DeliveryResult expired = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, expired.Code, "после срока — просрочка");
            MiniTestRunner.AreEqual(DeliveryOrderState.Failed, expired.Status.State, "заказ провален");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплаты нет");
        }

        private static void RetryAfterPayoutFailure()
        {
            IDeliveryService service = ToPickedUp(out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);
            ledger.CreditOverride = false;
            service.Complete(order.Id, clock, location, ledger);

            ledger.CreditOverride = null;
            DeliveryResult retry = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.IsTrue(retry.IsSuccess, "повторное завершение после сбоя проходит");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "выплата начислена один раз");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, retry.Status.State, "состояние Completed");
        }

        // ---------- 9. Регистрация и валидация ----------

        private static void RegisterOrder()
        {
            var service = new DeliveryService();
            DeliveryOrder order = NewOrder();

            MiniTestRunner.IsTrue(service.RegisterOrder(order).IsSuccess, "первая регистрация успешна");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidArgument, service.RegisterOrder(order).Code, "дубликат id отклонён");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidArgument, service.RegisterOrder(null).Code, "null-заказ отклонён");
            MiniTestRunner.AreEqual(1, service.Orders.Count, "в списке один заказ");
            MiniTestRunner.AreEqual(DeliveryOrderState.Available, service.GetStatus(order.Id).Status.State, "новый заказ Available");
        }

        private static void InvalidOrderData()
        {
            MiniTestRunner.IsTrue(Throws<ArgumentException>(() => new DeliveryOrder("", "Груз", new DeliveryPoint(Bakery, null), new DeliveryPoint(Office, null), 100L, 60L)),
                "пустой id заказа отклонён");
            MiniTestRunner.IsTrue(Throws<ArgumentException>(() => new DeliveryOrder("id", " ", new DeliveryPoint(Bakery, null), new DeliveryPoint(Office, null), 100L, 60L)),
                "пустое название груза отклонено");
            MiniTestRunner.IsTrue(Throws<ArgumentOutOfRangeException>(() => new DeliveryOrder("id", "Груз", new DeliveryPoint(Bakery, null), new DeliveryPoint(Office, null), 0L, 60L)),
                "нулевая оплата отклонена");
            MiniTestRunner.IsTrue(Throws<ArgumentOutOfRangeException>(() => new DeliveryOrder("id", "Груз", new DeliveryPoint(Bakery, null), new DeliveryPoint(Office, null), -5L, 60L)),
                "отрицательная оплата отклонена");
            MiniTestRunner.IsTrue(Throws<ArgumentOutOfRangeException>(() => new DeliveryOrder("id", "Груз", new DeliveryPoint(Bakery, null), new DeliveryPoint(Office, null), 100L, -1L)),
                "отрицательный срок отклонён");
            MiniTestRunner.IsTrue(Throws<ArgumentException>(() => new DeliveryPoint("  ", "Точка")),
                "пустой id точки отклонён");
        }

        private static void ServiceRequiresAdapters()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var location = new FakeLocation(Bakery);
            var ledger = new FakeLedger();

            MiniTestRunner.IsTrue(Throws<DeliveryServiceException>(() => service.Accept(order.Id, null)),
                "без часов Accept бросает DeliveryServiceException");
            MiniTestRunner.IsTrue(Throws<DeliveryServiceException>(() => service.PickUp(order.Id, new FakeClock(0d), null)),
                "без локации PickUp бросает DeliveryServiceException");
            MiniTestRunner.IsTrue(Throws<DeliveryServiceException>(() => service.Complete(order.Id, new FakeClock(0d), location, null)),
                "без начисления Complete бросает DeliveryServiceException");
            MiniTestRunner.IsTrue(Throws<DeliveryServiceException>(() => service.Cancel(order.Id, null)),
                "без часов Cancel бросает DeliveryServiceException");
        }

        // ---------- Регрессия аудита ТЗ №015 ----------

        /// <summary>
        /// Дефект: просроченный Available-заказ при уже имеющемся активном заказе возвращал
        /// DeadlineExpired и переводил заказ в Failed — то есть чужой активный заказ маскировал
        /// причину отказа («уже есть заказ»), а правило «один активный заказ» обходилось
        /// побочным эффектом. Теперь приоритет у AlreadyHasActiveOrder, состояние не меняется.
        /// </summary>
        private static void ActiveOrderDoesNotMaskExpiry()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder first, deadline: 500L);
            var clock = new FakeClock(0d);
            var second = new DeliveryOrder(
                "order-2", "Лапша", new DeliveryPoint(Bakery, "Пекарня"),
                new DeliveryPoint(Office, "Офис"), 90L, 10L);
            service.RegisterOrder(second);

            MiniTestRunner.IsTrue(service.Accept(first.Id, clock).IsSuccess, "первый заказ принят");
            clock.Advance(11d); // срок второго заказа истёк, срок первого — нет

            DeliveryResult result = service.Accept(second.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.AlreadyHasActiveOrder, result.Code, "причина отказа — активный заказ");
            MiniTestRunner.AreEqual(DeliveryOrderState.Available, result.Status.State, "состояние заказа не изменено");
            MiniTestRunner.IsTrue(
                service.GetStatus("order-2").Status.ClosedAtMinutes == null,
                "заказ не закрыт побочным эффектом");
            MiniTestRunner.AreEqual(first.Id, service.ActiveOrderId, "активный заказ прежний");
        }

        /// <summary>
        /// Дефект: слот активного заказа хранился отдельным полем и мог «зависнуть» на закрытом
        /// заказе — тогда Accept навсегда отвечал AlreadyHasActiveOrder, и играть было нельзя.
        /// Слот теперь выводится из фактических состояний заказов.
        /// Имитация такого состояния — подмена приватного поля через рефлексию
        /// (публичного API для повреждения слота нет и не должно быть).
        /// </summary>
        private static void StaleActiveSlotDoesNotLockPlayer()
        {
            var service = new DeliveryService();
            var clock = new FakeClock(0d);
            service.RegisterOrder(NewOrder("order-1"));
            service.RegisterOrder(NewOrder("order-2"));

            MiniTestRunner.IsTrue(service.Accept("order-1", clock).IsSuccess, "заказ принят");
            MiniTestRunner.IsTrue(ForceActiveSlot(service, "order-2"), "слот подменён на закрытый заказ");
            MiniTestRunner.IsTrue(service.Cancel("order-1", clock).IsSuccess, "первый заказ отменён");

            DeliveryResult result = service.Accept("order-2", clock);

            MiniTestRunner.IsTrue(result.IsSuccess, "зависший слот не блокирует выдачу нового заказа");
            MiniTestRunner.AreEqual("order-2", service.ActiveOrderId, "слот указывает на новый заказ");
        }

        private static bool ForceActiveSlot(DeliveryService service, string orderId)
        {
            var field = typeof(DeliveryService).GetField(
                "_activeOrderId",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field == null)
            {
                return false;
            }

            field.SetValue(service, orderId);
            return true;
        }

        /// <summary>Скачок времени (сон, ускорение, рассинхрон часов) не должен платить за просрочку.</summary>
        private static void HugeTimeJump()
        {
            IDeliveryService service = ToPickedUp(
                out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);

            clock.NowMinutes = 1e18d;
            DeliveryResult completed = service.Complete(order.Id, clock, location, ledger);

            MiniTestRunner.AreEqual(DeliveryResultCode.DeadlineExpired, completed.Code, "гигантский скачок = просрочка");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "денег не начислено");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        /// <summary>Граница срока: now == deadline — ещё успех, теперь &gt; deadline — провал.</summary>
        private static void DeadlineBoundary()
        {
            IDeliveryService exact = ToPickedUp(
                out DeliveryOrder first, out FakeClock exactClock, out FakeLedger exactLedger,
                out FakeLocation exactLocation, deadline: 100L);

            exactClock.NowMinutes = 100d;
            MiniTestRunner.IsTrue(
                exact.Complete(first.Id, exactClock, exactLocation, exactLedger).IsSuccess,
                "ровно в срок доставка разрешена");

            IDeliveryService late = ToPickedUp(
                out DeliveryOrder second, out FakeClock lateClock, out FakeLedger lateLedger,
                out FakeLocation lateLocation, deadline: 100L);

            lateClock.NowMinutes = 100.001d;
            MiniTestRunner.AreEqual(
                DeliveryResultCode.DeadlineExpired,
                late.Complete(second.Id, lateClock, lateLocation, lateLedger).Code,
                "через мгновение после срока — провал");
        }

        private static void RepeatedCancel()
        {
            IDeliveryService service = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();

            MiniTestRunner.IsTrue(service.Cancel(order.Id, clock).IsSuccess, "первая отмена успешна");
            DeliveryResult second = service.Cancel(order.Id, clock);
            DeliveryResult third = service.Cancel(order.Id, clock);

            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, second.Code, "вторая отмена отклонена");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, third.Code, "третья отмена отклонена");
            MiniTestRunner.AreEqual(DeliveryOrderState.Cancelled, third.Status.State, "состояние не изменилось");
            MiniTestRunner.AreEqual(0L, ledger.Credited, "выплат не было");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
        }

        private static void AbandonOnlyWhileAccepted()
        {
            IDeliveryService available = NewServiceWithOrder(out DeliveryOrder order);
            var clock = new FakeClock(0d);

            MiniTestRunner.AreEqual(
                DeliveryResultCode.InvalidState, available.Abandon(order.Id, clock).Code,
                "нельзя бросить непринятый заказ");

            IDeliveryService picked = ToPickedUp(
                out DeliveryOrder pickedOrder, out FakeClock pickedClock,
                out FakeLedger ledger, out FakeLocation location);

            MiniTestRunner.AreEqual(
                DeliveryResultCode.InvalidState, picked.Abandon(pickedOrder.Id, pickedClock).Code,
                "нельзя бросить заказ с грузом");
            MiniTestRunner.AreEqual(pickedOrder.Id, picked.ActiveOrderId, "заказ остался активным");
        }

        private static void RefreshAfterCompletion()
        {
            IDeliveryService service = ToPickedUp(
                out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);

            MiniTestRunner.IsTrue(service.Complete(order.Id, clock, location, ledger).IsSuccess, "заказ завершён");
            clock.Advance(10_000d);

            DeliveryResult refreshed = service.Refresh(order.Id, clock);

            MiniTestRunner.IsTrue(refreshed.IsSuccess, "Refresh закрытого заказа ничего не ломает");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, refreshed.Status.State, "состояние осталось Completed");
            MiniTestRunner.IsTrue(refreshed.Status.PayoutGranted, "флаг выплаты сохранён");
            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "повторной выплаты не было");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "сумма не изменилась");
        }

        private static void PayoutOnceUnderMixedCalls()
        {
            IDeliveryService service = ToPickedUp(
                out DeliveryOrder order, out FakeClock clock, out FakeLedger ledger, out FakeLocation location);

            for (int i = 0; i < 5; i++)
            {
                service.Refresh(order.Id, clock);
                service.GetStatus(order.Id);
                service.Complete(order.Id, clock, location, ledger);
                service.PickUp(order.Id, clock, location);
                service.Cancel(order.Id, clock);
            }

            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "выплата ровно одна");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "сумма ровно одна");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, service.GetStatus(order.Id).Status.State, "заказ завершён");
            MiniTestRunner.IsTrue(service.ActiveOrderId == null, "слот освобождён");
        }

        // ---------- ТЗ №017: сохранение состояния заказов ----------

        /// <summary>
        /// Дефект: состояние заказов не сохранялось, поэтому после загрузки игры
        /// выполненный заказ снова становился Available и оплачивался второй раз.
        /// </summary>
        private static void SnapshotKeepsPayoutFlag()
        {
            var service = new DeliveryService();
            DeliveryOrder order = NewOrder();
            service.RegisterOrder(order);

            var clock = new FakeClock(0d);
            var location = new FakeLocation(Bakery);
            var ledger = new FakeLedger();

            service.Accept(order.Id, clock);
            service.PickUp(order.Id, clock, location);
            location.MoveTo(Office);
            MiniTestRunner.IsTrue(service.Complete(order.Id, clock, location, ledger).IsSuccess, "заказ выполнен");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "оплата получена один раз");

            DeliverySnapshot snapshot = service.CreateSnapshot();

            var loaded = new DeliveryService();
            string error;
            MiniTestRunner.IsTrue(loaded.RestoreSnapshot(snapshot, out error), "снимок восстановлен: " + error);

            DeliveryResult status = loaded.GetStatus(order.Id);
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, status.Status.State, "состояние сохранено");
            MiniTestRunner.IsTrue(status.Status.PayoutGranted, "флаг выплаты сохранён");

            // Повторные операции над загруженным заказом не платят второй раз.
            DeliveryResult repeat = loaded.Complete(order.Id, clock, location, ledger);
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, repeat.Code, "повторное завершение отклонено");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, loaded.Accept(order.Id, clock).Code, "повторное принятие отклонено");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "оплата не продублирована после загрузки");
            MiniTestRunner.AreEqual(1, ledger.CreditCalls, "начисление вызвано один раз");
        }

        private static void SnapshotRestoresActiveOrder()
        {
            var service = new DeliveryService();
            DeliveryOrder active = NewOrder(id: "order-active");
            DeliveryOrder done = NewOrder(id: "order-done");
            service.RegisterOrder(active);
            service.RegisterOrder(done);

            var clock = new FakeClock(5d);
            var location = new FakeLocation(Bakery);
            var ledger = new FakeLedger();

            service.Accept(active.Id, clock);
            service.PickUp(active.Id, clock, location);

            DeliverySnapshot snapshot = service.CreateSnapshot();

            var loaded = new DeliveryService();
            string error;
            MiniTestRunner.IsTrue(loaded.RestoreSnapshot(snapshot, out error), "снимок восстановлен: " + error);

            MiniTestRunner.AreEqual("order-active", loaded.ActiveOrderId, "активный заказ восстановлен");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, loaded.GetStatus("order-active").Status.State, "груз уже получен");
            MiniTestRunner.AreEqual(DeliveryOrderState.Available, loaded.GetStatus("order-done").Status.State, "второй заказ доступен");

            // Слот занят: второй заказ не принимается, но и не портится.
            DeliveryResult attempt = loaded.Accept("order-done", clock);
            MiniTestRunner.AreEqual(DeliveryResultCode.AlreadyHasActiveOrder, attempt.Code, "слот восстановлен корректно");

            // Работу можно довести до конца и получить оплату один раз.
            location.MoveTo(Office);
            MiniTestRunner.IsTrue(loaded.Complete("order-active", clock, location, ledger).IsSuccess, "заказ завершён после загрузки");
            MiniTestRunner.AreEqual(150L, ledger.Credited, "оплата начислена один раз");
        }

        private static void SnapshotRejectsCorrupt()
        {
            // Источник снимков — отдельный сервис с одним заказом; проверяем,
            // что именно валидатор отклоняет снимок, а не проверка «сервис не пуст».
            var source = new DeliveryService();
            source.RegisterOrder(NewOrder());

            var target = new DeliveryService();
            string error;

            MiniTestRunner.IsFalse(target.RestoreSnapshot(null, out error), "null-снимок отклонён");

            var wrongVersion = source.CreateSnapshot();
            wrongVersion.SchemaVersion = 99;
            MiniTestRunner.IsFalse(target.RestoreSnapshot(wrongVersion, out error), "неверная версия схемы отклонена");

            // «Завершён без выплаты» — противоречивое состояние: снимок не применяется.
            var contradictory = source.CreateSnapshot();
            contradictory.Orders[0].State = (int)DeliveryOrderState.Completed;
            contradictory.Orders[0].PayoutGranted = false;
            MiniTestRunner.IsFalse(target.RestoreSnapshot(contradictory, out error), "противоречивый снимок отклонён");

            // Два активных заказа тоже невозможны.
            var twoActive = source.CreateSnapshot();
            twoActive.Orders[0].State = (int)DeliveryOrderState.Accepted;
            twoActive.Orders[0].HasAcceptedAt = true;
            twoActive.Orders[0].AcceptedAtMinutes = 1d;
            var second = new DeliveryOrderSnapshot
            {
                Id = "order-2",
                CargoTitle = "Груз",
                PickupPointId = Bakery,
                DropOffPointId = Office,
                PaymentRubles = 100L,
                DeadlineGameMinutes = 100L,
                State = (int)DeliveryOrderState.PickedUp,
                HasAcceptedAt = true,
                AcceptedAtMinutes = 1d,
                HasPickedUpAt = true,
                PickedUpAtMinutes = 2d
            };
            twoActive.Orders = new[] { twoActive.Orders[0], second };
            MiniTestRunner.IsFalse(target.RestoreSnapshot(twoActive, out error), "два активных заказа отклонены");

            // Ни один отклонённый снимок не изменил целевой сервис.
            MiniTestRunner.AreEqual(0, target.Orders.Count, "целевой сервис не тронут");
            MiniTestRunner.IsTrue(target.ActiveOrderId == null, "слот не занят");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, target.GetStatus("order-1").Code, "заказов нет");
        }

        private static void SnapshotRejectsNonEmpty()
        {
            var service = new DeliveryService();
            service.RegisterOrder(NewOrder());

            DeliverySnapshot snapshot = new DeliveryService().CreateSnapshot();
            string error;

            MiniTestRunner.IsFalse(service.RestoreSnapshot(snapshot, out error), "восстановление поверх заказов отклонено");
            MiniTestRunner.IsTrue(error != null, "причина сообщена");
            MiniTestRunner.AreEqual(1, service.Orders.Count, "заказы на месте");
        }

        private static void NotFoundIsNotAvailable()
        {
            IDeliveryService service = new DeliveryService();
            var clock = new FakeClock(0d);
            var ledger = new FakeLedger();

            DeliveryResult status = service.GetStatus("nope");

            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, status.Code, "код OrderNotFound");
            MiniTestRunner.AreEqual(DeliveryOrderState.None, status.Status.State, "состояние None, а не Available");
            MiniTestRunner.IsTrue(status.Status.OrderId == null, "нет идентификатора заказа");
            MiniTestRunner.IsFalse(status.Status.PayoutGranted, "выплаты нет");
            MiniTestRunner.AreEqual(0, ledger.CreditCalls, "начисление не вызывалось");
            MiniTestRunner.AreEqual(DeliveryResultCode.OrderNotFound, service.Complete("nope", clock, new FakeLocation(Bakery), ledger).Code, "завершение неизвестного");
        }

        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
        }
    }
}
