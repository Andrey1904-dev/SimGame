// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Тесты сохранения/восстановления: круговой прогон, повреждённые снимки,
// атомарность восстановления, сохранение защиты от повторов после загрузки.

using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Persistence;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtPersistenceTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Снимок: круговой прогон сохраняет долги, суммы и историю", RoundTrip);
            suite.Add("Снимок: после загрузки штраф не начисляется повторно за тот же период", NoPenaltyReaccrual);
            suite.Add("Снимок: одноразовые события не дублируются после загрузки", NoEventReplay);
            suite.Add("Снимок: последнее время сохраняется (защита от отката часов)", LastNowPreserved);
            suite.Add("Снимок: пустой сервис даёт валидный пустой снимок", EmptySnapshotValid);
            suite.Add("Снимок: восстановление пустого снимка очищает состояние", RestoreEmptyClears);
            suite.Add("Валидация: null снимок отклонён", NullSnapshot);
            suite.Add("Валидация: неверная версия схемы отклонена", WrongVersion);
            suite.Add("Валидация: null массив отклонён", NullArray);
            suite.Add("Валидация: дубликат ID долга отклонён", DuplicateDebtId);
            suite.Add("Валидация: дубликат ID кредитора отклонён", DuplicateCreditorId);
            suite.Add("Валидация: отрицательная сумма отклонена", NegativeAmount);
            suite.Add("Валидация: платёж ссылается на несуществующий долг", PaymentMissingDebt);
            suite.Add("Валидация: нарушен инвариант выплаченной суммы", PaidTotalInvariant);
            suite.Add("Валидация: закрытый долг с ненулевым остатком", ClosedWithRemaining);
            suite.Add("Валидация: части платежа не равны сумме", PaymentPartsMismatch);
            suite.Add("Валидация: дубликат paymentId в истории", DuplicatePaymentIdHistory);
            suite.Add("Валидация: незавершённая операция больше задолженности", PendingTooLarge);
            suite.Add("Атомарность: повреждённый снимок не меняет рабочее состояние", CorruptLeavesStateIntact);

            // Регрессия ТЗ №017: валидатор отклонял снимки, которые сервис создаёт сам.
            suite.Add("Валидация: RefundPending больше остатка — снимок остаётся загружаемым", RefundPendingAboveRemaining);

            return suite;
        }

        private static DebtService PopulatedService(FakeClock clock, FakeWallet wallet, IDebtEventSink sink)
        {
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 0L, penalties: penalties);
            clock.Minutes = 250L;
            service.UpdateTime(); // штрафы 150, стадия Overdue
            service.TryPay(DebtTestEnv.DebtId, 200L, "p1"); // штрафы 150 + основной 50
            return service;
        }

        private static void RoundTrip()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = PopulatedService(clock, wallet, null);
            DebtSnapshot snapshot = source.CreateSnapshot();

            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(snapshot, out string err), "снимок валиден: " + err);

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");

            source.TryGetDebt(DebtTestEnv.DebtId, out DebtView a);
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView b);
            MiniTestRunner.AreEqual(a.PrincipalRemaining, b.PrincipalRemaining, "остаток основного");
            MiniTestRunner.AreEqual(a.PenaltiesRemaining, b.PenaltiesRemaining, "остаток штрафов");
            MiniTestRunner.AreEqual(a.PenaltiesAccrued, b.PenaltiesAccrued, "начислено штрафов");
            MiniTestRunner.AreEqual(a.PaidTotal, b.PaidTotal, "выплачено");
            MiniTestRunner.AreEqual(a.TotalRemaining, b.TotalRemaining, "общая задолженность");
            MiniTestRunner.AreEqual(a.Status, b.Status, "стадия");
            MiniTestRunner.AreEqual(a.CreditorName, b.CreditorName, "имя кредитора");
            MiniTestRunner.AreEqual(
                source.GetPaymentHistory(DebtTestEnv.DebtId).Count,
                restored.GetPaymentHistory(DebtTestEnv.DebtId).Count,
                "история платежей");
        }

        private static void NoPenaltyReaccrual()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = PopulatedService(clock, wallet, null);
            DebtSnapshot snapshot = source.CreateSnapshot();

            var sink = new RecordingEventSink();
            var restored = new DebtService(clock, wallet, sink);
            restored.RestoreSnapshot(snapshot);

            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView before);
            restored.UpdateTime(); // тот же момент времени
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView after);
            MiniTestRunner.AreEqual(before.PenaltiesAccrued, after.PenaltiesAccrued, "повторного начисления нет");
            MiniTestRunner.AreEqual(0, sink.CountOf(DebtEventType.PenaltyApplied), "нет события штрафа");
        }

        private static void NoEventReplay()
        {
            var clock = new FakeClock { Minutes = 1500L };
            var wallet = new FakeWallet(0L);
            var sourceSink = new RecordingEventSink();
            DebtService source = DebtTestEnv.CreateService(clock, wallet, sourceSink, due: 1000L);
            source.UpdateTime(); // PaymentDue + DebtOverdue
            MiniTestRunner.AreEqual(1, sourceSink.CountOf(DebtEventType.PaymentDue), "срок в источнике");
            MiniTestRunner.AreEqual(1, sourceSink.CountOf(DebtEventType.DebtOverdue), "просрочка в источнике");

            DebtSnapshot snapshot = source.CreateSnapshot();
            var restoredSink = new RecordingEventSink();
            var restored = new DebtService(clock, wallet, restoredSink);
            restored.RestoreSnapshot(snapshot);
            restored.UpdateTime();

            MiniTestRunner.AreEqual(0, restoredSink.CountOf(DebtEventType.PaymentDue), "срок не продублирован");
            MiniTestRunner.AreEqual(0, restoredSink.CountOf(DebtEventType.DebtOverdue), "просрочка не продублирована");
        }

        private static void LastNowPreserved()
        {
            var clock = new FakeClock { Minutes = 5000L };
            var wallet = new FakeWallet(0L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null, due: 0L);
            source.UpdateTime();
            DebtSnapshot snapshot = source.CreateSnapshot();

            var restored = new DebtService(clock, wallet, null);
            restored.RestoreSnapshot(snapshot);
            MiniTestRunner.AreEqual(5000L, restored.LastProcessedMinutes, "последнее время сохранено");

            clock.Minutes = 4000L;
            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError, restored.UpdateTime(), "откат часов после загрузки");
        }

        private static void EmptySnapshotValid()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(snapshot, out string err), "пустой снимок валиден: " + err);
            MiniTestRunner.AreEqual(0, snapshot.Debts.Length, "долгов нет");
        }

        private static void RestoreEmptyClears()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            MiniTestRunner.AreEqual(1, service.GetDebts().Count, "долг есть до восстановления");

            DebtTestEnv.ExpectSuccess(service.RestoreSnapshot(new DebtSnapshot()), "пустой снимок");
            MiniTestRunner.AreEqual(0, service.GetDebts().Count, "состояние очищено");
        }

        private static void NullSnapshot()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(null), "null снимок");
        }

        private static void WrongVersion()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.SchemaVersion = 999;

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "неверная версия");
        }

        private static void NullArray()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Debts = null;

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "null массив");
        }

        private static void DuplicateDebtId()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();

            var copy = new DebtEntrySnapshot
            {
                Id = snapshot.Debts[0].Id,
                CreditorId = snapshot.Debts[0].CreditorId,
                PrincipalOriginal = 100L,
                PrincipalRemaining = 100L,
                DueAtMinutes = 10L,
                ClosedAtMinutes = -1L,
                ReminderAfterMinutes = 1L,
                CollectionAfterMinutes = 2L,
                LegalActionAfterMinutes = 3L
            };
            snapshot.Debts = new[] { snapshot.Debts[0], copy };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "дубликат долга");
        }

        private static void DuplicateCreditorId()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Creditors = new[] { snapshot.Creditors[0], snapshot.Creditors[0] };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "дубликат кредитора");
        }

        private static void NegativeAmount()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Debts[0].PrincipalRemaining = -1L;

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "отрицательная сумма");
        }

        private static void PaymentMissingDebt()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 100L, "p");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Payments[0].DebtId = "ghost";

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "платёж без долга");
        }

        private static void PaidTotalInvariant()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 100L, "p");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Debts[0].PaidTotal = 999L; // не согласовано с остатком и историей

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "инвариант выплаченной суммы");
        }

        private static void ClosedWithRemaining()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 38400L, "full");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Debts[0].PrincipalRemaining = 100L; // закрыт, но остаток ненулевой

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "закрыт с остатком");
        }

        private static void PaymentPartsMismatch()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 100L, "p");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Payments[0].PrincipalPartRubles = 50L; // 50 != 100

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "части не равны сумме");
        }

        private static void DuplicatePaymentIdHistory()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 100L, "p");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Payments = new[] { snapshot.Payments[0], snapshot.Payments[0] };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "дубликат paymentId");
        }

        private static void PendingTooLarge()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.PendingOperations = new[]
            {
                new PendingOperationSnapshot
                {
                    PaymentId = "big",
                    DebtId = DebtTestEnv.DebtId,
                    AmountRubles = 999999L,
                    Phase = 0,
                    CreatedAtMinutes = 0L
                }
            };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(snapshot), "операция больше задолженности");
        }

        private static void CorruptLeavesStateIntact()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            service.TryPay(DebtTestEnv.DebtId, 1000L, "p1");

            DebtSnapshot corrupt = service.CreateSnapshot();
            corrupt.Debts[0].PrincipalRemaining = -5L;

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidSnapshot, service.RestoreSnapshot(corrupt), "повреждённый снимок");

            // Рабочее состояние не изменилось.
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "остаток прежний");
            MiniTestRunner.AreEqual(1000L, view.PaidTotal, "выплачено прежнее");
            MiniTestRunner.AreEqual(1, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "история прежняя");
        }

        /// <summary>
        /// Дефект: проверка «сумма незавершённой операции не больше задолженности»
        /// применялась ко всем фазам. Но в фазах DebitConfirmed/RefundPending деньги
        /// уже списаны, а долг за это время мог уменьшиться — сервис создавал снимок,
        /// который его же валидатор отклонял, и сохранение становилось незагружаемым
        /// вместе с единственной записью о деньгах, которые нужно вернуть.
        /// </summary>
        private static void RefundPendingAboveRemaining()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.PendingOperations = new[]
            {
                new PendingOperationSnapshot
                {
                    PaymentId = "refund-me",
                    DebtId = DebtTestEnv.DebtId,
                    AmountRubles = 50000L, // больше остатка (38 400)
                    Phase = 2,             // RefundPending: деньги уже списаны
                    CreatedAtMinutes = 0L
                }
            };

            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(snapshot, out string error),
                "снимок с RefundPending больше остатка валиден: " + error);
            DebtTestEnv.ExpectSuccess(service.RestoreSnapshot(snapshot), "восстановление");
            MiniTestRunner.AreEqual(1, service.PendingOperationCount, "запись о возврате сохранена");

            // Сбой возврата не теряет запись: деньги всё ещё можно вернуть позже.
            wallet.ThrowOnRefund = true;
            MiniTestRunner.IsFalse(service.RetryPendingRecovery(), "возврат при сбое не выполнен");
            MiniTestRunner.AreEqual(1, service.PendingOperationCount, "запись о деньгах не потеряна");

            // Повторное сохранение по-прежнему валидно и сохраняет фазу и сумму.
            DebtSnapshot again = service.CreateSnapshot();
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(again, out string repeatedError),
                "повторный снимок валиден: " + repeatedError);
            MiniTestRunner.AreEqual(1, again.PendingOperations.Length, "запись в снимке одна");
            MiniTestRunner.AreEqual(50000L, again.PendingOperations[0].AmountRubles, "сумма сохранена");
            MiniTestRunner.AreEqual(2, again.PendingOperations[0].Phase, "фаза RefundPending сохранена");
        }
    }
}
