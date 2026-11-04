// Life From Zero — Delivery jobs module (ТЗ №021)
// Namespace: LifeFromZero.Jobs.Tests
//
// Регрессионные тесты финансовых контрактов доставки: уникальные идентификаторы
// выплат, повторная выплата курьеру, неоднозначный исход начисления и перезагрузка снимка.

using System;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Jobs.Tests.Fakes;

namespace LifeFromZero.Jobs.Tests
{
    public static class DeliveryFinancialSafetyTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Выплата: идентификатор операции уникален и стабилен для заказа", PayoutIdStable);
            suite.Add("Выплата: разные заказы получают разные идентификаторы операций", PayoutIdUniquePerRegistration);
            suite.Add("Выплата: повтор Complete не начисляет деньги второй раз", RepeatedCompleteNoDoublePay);
            suite.Add("Выплата: исключение после начисления восстанавливается по журналу", CreditThrowsAfterRecovered);
            suite.Add("Выплата: false после начисления восстанавливается по журналу", CreditFalseAfterRecovered);
            suite.Add("Выплата: «true» без начисления не принимается на веру", LyingsSuccessRejected);
            suite.Add("Выплата: повтор после загрузки снимка не начисляет второй раз", PayoutAfterRestoreNoDouble);
            suite.Add("Выплата: перезагрузка снимка повторно не платит выполненный заказ", ReloadCompletedSnapshotNoDouble);
            suite.Add("Выплата: ID выплаты переживает снимок и не конфликтует с новыми заказами",
                PayoutIdSurvivesSnapshotWithoutCollision);
            suite.Add("Выплата: legacy-адаптер по-прежнему работает (без гарантий повтора)", LegacyPayoutStillWorks);

            return suite;
        }

        private static DeliveryOrder Order(string id, long payment)
        {
            return new DeliveryOrder(
                id,
                "Груз " + id,
                new DeliveryPoint("pickup", "Склад"),
                new DeliveryPoint("drop", "Клиент"),
                payment,
                100L);
        }

        private static DeliveryService PickedUpOrder(string id, long payment, FakeClock clock, FakeLocation location)
        {
            var service = new DeliveryService();
            service.RegisterOrder(Order(id, payment));
            service.Accept(id, clock);
            location.CurrentLocationId = "pickup";
            service.PickUp(id, clock, location);
            location.CurrentLocationId = "drop";
            return service;
        }

        // ---------- 1. Идентификаторы ----------

        private static void PayoutIdStable()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            string before = service.GetPayoutOperationId("order-1");
            MiniTestRunner.AreEqual("delivery:order-1#1", before, "ID выплаты — order id + номер");

            DeliveryResult result = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(result.IsSuccess, "заказ завершён");
            MiniTestRunner.AreEqual(before, result.PayoutOperationId, "в результате — тот же ID");
            MiniTestRunner.AreEqual(100L, payouts.Total, "начислено 100 ₽");
        }

        /// <summary>
        /// Два разных заказа в одной сессии получают разные идентификаторы выплат.
        /// Уникальность МЕЖДУ линиями сохранения — обязанность адаптера кошелька
        /// (см. контракт <see cref="IIdempotentPayoutAdapter"/>): сервис не знает, одна ли
        /// это игра. Внутри одной линии (сервис + снимок) идентификаторы не повторяются.
        /// </summary>
        private static void PayoutIdUniquePerRegistration()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = new DeliveryService();

            service.RegisterOrder(Order("order-1", 100L));
            service.RegisterOrder(Order("order-2", 100L));

            string first = service.GetPayoutOperationId("order-1");
            string second = service.GetPayoutOperationId("order-2");

            MiniTestRunner.IsTrue(first != second, "идентификаторы различаются: " + first + " / " + second);
            MiniTestRunner.IsTrue(PayoutOperation.IsPayoutOperationId(first), "ID в пространстве имён доставки");
            MiniTestRunner.IsTrue(PayoutOperation.IsPayoutOperationId(second), "второй ID тоже");
            MiniTestRunner.AreEqual(0L, payouts.Total, "регистрация денег не двигает");
        }

        // ---------- 2. Повторная выплата ----------

        private static void RepeatedCompleteNoDoublePay()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliveryResult first = service.Complete("order-1", clock, location, payouts);
            DeliveryResult second = service.Complete("order-1", clock, location, payouts);
            DeliveryResult third = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(first.IsSuccess, "первое завершение успешно");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, second.Code, "повтор отклонён состоянием");
            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, third.Code, "и третий раз тоже");
            MiniTestRunner.AreEqual(100L, payouts.Total, "начислено ровно один раз");
            MiniTestRunner.AreEqual(1, payouts.CreditCalls, "вызов начисления был один");
        }

        private static void CreditThrowsAfterRecovered()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts { ThrowAfterCredit = true };
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliveryResult result = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(result.IsSuccess, "исход восстановлен по журналу: деньги начислены");
            MiniTestRunner.AreEqual(100L, payouts.Total, "начислено один раз");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, result.Status.State, "заказ завершён");
        }

        private static void CreditFalseAfterRecovered()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts { FalseAfterCredit = true };
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliveryResult result = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(result.IsSuccess, "«потерянный ответ» не считается отказом");
            MiniTestRunner.AreEqual(100L, payouts.Total, "начислено один раз");
        }

        private static void LyingsSuccessRejected()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts { LieSuccess = true };
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliveryResult result = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.AreEqual(DeliveryResultCode.AdapterFailure, result.Code, "начисление не подтверждено журналом");
            MiniTestRunner.AreEqual(DeliveryOrderState.PickedUp, result.Status.State, "заказ не считается завершённым");
            MiniTestRunner.AreEqual(0L, payouts.Total, "денег нет");
        }

        private static void PayoutAfterRestoreNoDouble()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliverySnapshot snapshot = service.CreateSnapshot();
            MiniTestRunner.IsTrue(service.Complete("order-1", clock, location, payouts).IsSuccess, "выплата в первой сессии");
            MiniTestRunner.AreEqual(100L, payouts.Total, "начислено");

            // Загрузка снимка, сделанного ДО выплаты: заказ снова PickedUp.
            var restored = new DeliveryService();
            string error;
            MiniTestRunner.IsTrue(restored.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);

            DeliveryResult retry = restored.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(retry.IsSuccess, "повторное завершение проходит");
            MiniTestRunner.AreEqual(100L, payouts.Total, "деньги не начислены второй раз");
            MiniTestRunner.AreEqual(
                service.GetPayoutOperationId("order-1") ?? "delivery:order-1#1",
                restored.GetPayoutOperationId("order-1"),
                "идентификатор выплаты совпадает с исходным");
        }

        private static void ReloadCompletedSnapshotNoDouble()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            service.Complete("order-1", clock, location, payouts);
            DeliverySnapshot snapshot = service.CreateSnapshot();

            var restored = new DeliveryService();
            string error;
            MiniTestRunner.IsTrue(restored.RestoreSnapshot(snapshot, out error), "снимок загружен: " + error);

            DeliveryResult again = restored.Complete("order-1", clock, location, payouts);
            DeliveryResult status = restored.GetStatus("order-1");

            MiniTestRunner.AreEqual(DeliveryResultCode.InvalidState, again.Code, "повтор после загрузки отклонён");
            MiniTestRunner.AreEqual(DeliveryOrderState.Completed, status.Status.State, "заказ остался выполненным");
            MiniTestRunner.AreEqual(100L, payouts.Total, "выплата одна");
        }

        private static void PayoutIdSurvivesSnapshotWithoutCollision()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new JournalPayouts();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliverySnapshot snapshot = service.CreateSnapshot();

            var restored = new DeliveryService();
            string error;
            MiniTestRunner.IsTrue(restored.RestoreSnapshot(snapshot, out error), "снимок загружен");

            // Завершаем первый заказ после загрузки: идентификатор выплаты не изменился.
            MiniTestRunner.AreEqual(
                "delivery:order-1#1",
                restored.GetPayoutOperationId("order-1"),
                "ID выплаты сохранён снимком");
            MiniTestRunner.IsTrue(restored.Complete("order-1", clock, location, payouts).IsSuccess, "первый заказ оплачен");

            // Новый заказ после загрузки не должен получить тот же идентификатор выплаты.
            restored.RegisterOrder(Order("order-2", 70L));
            string firstId = restored.GetPayoutOperationId("order-1");
            string secondId = restored.GetPayoutOperationId("order-2");

            MiniTestRunner.IsTrue(firstId != secondId, "идентификаторы выплат различаются: " + firstId + " / " + secondId);

            MiniTestRunner.IsTrue(restored.Accept("order-2", clock).IsSuccess, "второй заказ принят");
            location.CurrentLocationId = "pickup";
            MiniTestRunner.IsTrue(restored.PickUp("order-2", clock, location).IsSuccess, "груз получен");
            location.CurrentLocationId = "drop";

            MiniTestRunner.IsTrue(restored.Complete("order-2", clock, location, payouts).IsSuccess, "второй заказ оплачен");
            MiniTestRunner.AreEqual(170L, payouts.Total, "обе выплаты прошли");
        }

        private static void LegacyPayoutStillWorks()
        {
            var clock = new FakeClock(1d);
            var location = new FakeLocation("drop");
            var payouts = new FakeLedger();
            DeliveryService service = PickedUpOrder("order-1", 100L, clock, location);

            DeliveryResult result = service.Complete("order-1", clock, location, payouts);

            MiniTestRunner.IsTrue(result.IsSuccess, "старый адаптер работает");
            MiniTestRunner.AreEqual(100L, payouts.Credited, "начислено один раз");
            MiniTestRunner.IsTrue(result.PayoutOperationId != null, "идентификатор операции всё равно известен");
        }
    }
}
