// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Сценарии потери/дублирования денег и безопасного восстановления незавершённых
// операций: сбои адаптера, «зависшие» платежи, компенсация, восстановление из снимка.

using System.Collections.Generic;
using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Persistence;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtPaymentSafetyTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Сбой: исключение до списания — деньги целы, повтор работает", DebitThrowsBeforeMove);
            suite.Add("Сбой: списал и бросил исключение — платёж завершён восстановлением", DebitThenThrowRecovered);
            suite.Add("Сбой: списал и вернул false — платёж завершён по журналу", DebitThenFalseRecovered);
            suite.Add("Сбой: журнал недоступен — платёж ждёт, новые заблокированы", UnknownDebitBlocks);
            suite.Add("Сбой: ожидание разрешения блокирует и не дублирует списание", PendingBlocksNoDoubleDebit);
            suite.Add("Восстановление: DebitConfirmed из снимка зачитывается без повторного списания", RestoreDebitConfirmed);
            suite.Add("Восстановление: DebitPending из снимка разрешается по журналу адаптера", RestoreDebitPending);
            suite.Add("Восстановление: DebitPending без списания отменяется, paymentId освобождается", RestoreDebitPendingCancelled);
            suite.Add("Восстановление: RefundPending возвращает деньги, долг не меняется", RestoreRefundPending);
            suite.Add("Восстановление: отказ возврата повторяется до успеха", RefundRetried);
            suite.Add("Восстановление: повторное восстановление не зачитывает дважды", RecoveryIdempotent);
            suite.Add("Защита: повтор paymentId после загрузки отклонён", DuplicateAfterRestore);
            suite.Add("Защита: серия платежей со сбоями сохраняет итоговую сумму", SeriesWithFailuresConsistent);
            suite.Add("Защита: компенсация не создаёт деньги из воздуха", RefundOnlyForRealDebit);

            // Регрессии ТЗ №016.2 — реально найденные дефекты реентрантности.
            suite.Add("Регрессия: реентрантное начисление штрафа во время списания не теряется", ReentrantPenaltiesNotLost);
            suite.Add("Регрессия: снимок из обработчика DebtClosed валиден", SnapshotInsideClosedEventValid);
            suite.Add("Регрессия: вложенное восстановление не отменяет операцию в полёте", ReentrantRecoveryKeepsJournal);
            suite.Add("Регрессия: вложенный платёж во время списания не ломает буфер", NestedDebitWindowKeepsBothOperations);

            return suite;
        }

        /// <summary>
        /// Дефект: распределение платежа считалось ДО вызова адаптера, а записывалось
        /// после. Если адаптер реентрантно начислял штрафы (UpdateTime), они затирались.
        /// </summary>
        private static void ReentrantPenaltiesNotLost()
        {
            var clock = new FakeClock();
            var inner = new FakeWallet(20000L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 500L, 100000L);
            DebtService service = null;

            var wallet = new ReentrantWallet(inner, () =>
            {
                // Адаптер во время списания продвигает время и обновляет долги.
                clock.Minutes = 250L;
                service.UpdateTime();
            });

            service = DebtTestEnv.CreateService(clock, wallet, null,
                principal: 10000L, due: 0L, penalties: penalties);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "платёж при реентрантности");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            // За время списания начислено 3 периода по 500 = 1500.
            MiniTestRunner.AreEqual(1500L, view.PenaltiesAccrued, "начисленные штрафы сохранены");
            // Платёж 1000 ушёл в штрафы (порядок: штрафы, затем основной долг).
            MiniTestRunner.AreEqual(500L, view.PenaltiesRemaining, "остаток штрафов корректен");
            MiniTestRunner.AreEqual(10000L, view.PrincipalRemaining, "основной долг не тронут");
            MiniTestRunner.AreEqual(1000L, view.PaidTotal, "выплачено корректно");

            // Финансовый инвариант и валидность снимка после реентрантного платежа.
            DebtSnapshot snapshot = service.CreateSnapshot();
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(snapshot, out string err),
                "снимок после реентрантного платежа валиден: " + err);
        }

        /// <summary>
        /// Дефект: журнал освобождался ПОСЛЕ рассылки DebtClosed/CollectionStageChanged.
        /// Обработчик, вызывавший CreateSnapshot(), получал снимок, где paymentId есть
        /// и в истории, и в журнале — валидатор такой снимок отклонял (сейв ломался).
        /// </summary>
        private static void SnapshotInsideClosedEventValid()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            DebtService service = null;
            var sink = new SnapshotOnEventSink(() => service);
            service = DebtTestEnv.CreateService(clock, wallet, sink);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 38400L, "full"), "закрывающий платёж");

            MiniTestRunner.IsNotNull(sink.Captured, "снимок из обработчика получен");
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(sink.Captured, out string err),
                "снимок внутри события валиден: " + err);
            MiniTestRunner.AreEqual(0, sink.Captured.PendingOperations.Length, "журнал уже освобождён");
            MiniTestRunner.AreEqual(1, sink.Captured.Payments.Length, "платёж уже в истории");
        }

        /// <summary>
        /// Дефект: вложенный ResolvePending во время списания опрашивал кошелёк,
        /// который ещё не списал деньги, и отменял журнал текущей операции —
        /// защита от краха между списанием и зачётом пропадала.
        /// </summary>
        private static void ReentrantRecoveryKeepsJournal()
        {
            var clock = new FakeClock();
            var inner = new FakeWallet(20000L);
            DebtService service = null;
            int observedPending = -1;

            var wallet = new ReentrantWallet(inner, () =>
            {
                service.RetryPendingRecovery();
                observedPending = service.PendingOperationCount;
            });

            service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "платёж");

            MiniTestRunner.AreEqual(1, observedPending, "операция в полёте не отменена вложенным восстановлением");
            MiniTestRunner.AreEqual(0, service.PendingOperationCount, "после зачёта журнал пуст");
            MiniTestRunner.AreEqual(1, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "зачёт один раз");
            MiniTestRunner.AreEqual(19000L, inner.Balance, "списано один раз");
        }

        /// <summary>
        /// Дефект: «окно списания» было bool-флагом. Вложенный платёж (например,
        /// адаптер кошелька, который сам начинает платёж по долгу) выходил из
        /// try/finally и закрывал флаг внешней операции — её журнал терял защиту.
        /// Теперь глубина вложенных списаний считается в счётчике.
        /// </summary>
        private static void NestedDebitWindowKeepsBothOperations()
        {
            var clock = new FakeClock();
            var inner = new FakeWallet(1000L);
            DebtService service = null;

            var wallet = new ReentrantWallet(inner, () =>
            {
                // Вложенный платёж: выполняется внутри списания внешнюю операцию.
                service.TryPay(DebtTestEnv.DebtId, 100L, "p2");
            });

            service = DebtTestEnv.CreateService(clock, wallet, null, principal: 1000L);

            // Внешний платёж: во время списания идёт вложенный платёж p2.
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 500L, "p1"), "внешний платёж");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(2, service.GetPaymentHistory(DebtTestEnv.DebtId).Count,
                "оба платежа зачтены ровно один раз");
            MiniTestRunner.AreEqual(600L, view.PaidTotal, "выплачено за два платежа");
            MiniTestRunner.AreEqual(400L, view.PrincipalRemaining, "остаток основного долга");
            MiniTestRunner.AreEqual(400L, inner.Balance, "списано ровно один раз");
            MiniTestRunner.AreEqual(0, service.PendingOperationCount, "журнал незавершённых пуст");
        }

        /// <summary>Приёмник, снимающий снимок состояния прямо в обработчике события.</summary>
        private sealed class SnapshotOnEventSink : IDebtEventSink
        {
            private readonly System.Func<DebtService> _service;

            public DebtSnapshot Captured { get; private set; }

            public SnapshotOnEventSink(System.Func<DebtService> service)
            {
                _service = service;
            }

            public void Publish(DebtEvent debtEvent)
            {
                if (debtEvent.Type == DebtEventType.DebtClosed)
                {
                    Captured = _service().CreateSnapshot();
                }
            }
        }

        private static void DebitThrowsBeforeMove()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { ThrowOnDebit = true };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError,
                service.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "сбой до списания");
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "деньги не тронуты");
            MiniTestRunner.AreEqual(0, service.PendingOperationCount, "журнал пуст");

            wallet.ThrowOnDebit = false;
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "p2"), "повтор с новым id");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "списано один раз");
        }

        private static void DebitThenThrowRecovered()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { DebitThenThrow = true };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            // Деньги списаны, адаптер бросил исключение; сервис уточняет по журналу и зачитывает.
            DebtPaymentResult result = service.TryPay(DebtTestEnv.DebtId, 1000L, "p1");
            DebtTestEnv.ExpectSuccess(result, "платёж завершён несмотря на исключение");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "списано ровно один раз");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "один вызов списания");
            MiniTestRunner.AreEqual(0, service.PendingOperationCount, "журнал пуст");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "долг уменьшился");
        }

        private static void DebitThenFalseRecovered()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { DebitThenReturnFalse = true };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtPaymentResult result = service.TryPay(DebtTestEnv.DebtId, 1000L, "p1");
            DebtTestEnv.ExpectSuccess(result, "«лгущий» адаптер: списание подтверждено журналом");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "списано один раз");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "долг уменьшился");
        }

        private static void UnknownDebitBlocks()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { DebitThenThrow = true, QueryFailuresRemaining = 10 };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            // Списал, бросил, журнал недоступен → операция ждёт.
            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError,
                service.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "исход неизвестен");
            MiniTestRunner.AreEqual(1, service.PendingOperationCount, "операция в журнале");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "деньги фактически списаны");

            // Новый платёж заблокирован.
            DebtTestEnv.ExpectCode(DebtResultCode.PendingRecovery,
                service.TryPay(DebtTestEnv.DebtId, 500L, "p2"), "новые платежи заблокированы");

            // Журнал ожил — восстановление завершает первый платёж.
            wallet.QueryFailuresRemaining = 0;
            wallet.DebitThenThrow = false;
            MiniTestRunner.IsTrue(service.RetryPendingRecovery(), "восстановление завершилось");
            MiniTestRunner.AreEqual(0, service.PendingOperationCount, "журнал пуст");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "первый платёж зачтён");
            MiniTestRunner.AreEqual(1, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "одна запись истории");
        }

        private static void PendingBlocksNoDoubleDebit()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { DebitThenThrow = true, QueryFailuresRemaining = 10 };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            service.TryPay(DebtTestEnv.DebtId, 1000L, "p1");
            // Несколько попыток recovery с недоступным журналом — списание не повторяется.
            service.RetryPendingRecovery();
            service.RetryPendingRecovery();
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "списание не дублируется");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "баланс стабилен");
            MiniTestRunner.AreEqual(1, service.PendingOperationCount, "операция всё ещё ждёт");
        }

        private static void RestoreDebitConfirmed()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            // Имитация краха после списания, но до зачёта: деньги уже ушли.
            wallet.TryDebit("crash", 1000L);
            DebtSnapshot snapshot = SnapshotWithPending(source, "crash", 1000L, phase: 1);

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление снимка");
            MiniTestRunner.AreEqual(1, restored.PendingOperationCount, "незавершённая операция загружена");

            MiniTestRunner.IsTrue(restored.RetryPendingRecovery(), "зачёт подтверждённого списания");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "повторного списания не было");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "баланс корректен");
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "долг уменьшился");
            MiniTestRunner.AreEqual(1, restored.GetPaymentHistory(DebtTestEnv.DebtId).Count, "платёж в истории");
        }

        private static void RestoreDebitPending()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            wallet.TryDebit("crash", 1000L);
            DebtSnapshot snapshot = SnapshotWithPending(source, "crash", 1000L, phase: 0);

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");
            MiniTestRunner.IsTrue(restored.RetryPendingRecovery(), "разрешение по журналу адаптера");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "без повторного списания");
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "платёж зачтён");
        }

        private static void RestoreDebitPendingCancelled()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            // Списания НЕ было (деньги целы), операция DebitPending.
            DebtSnapshot snapshot = SnapshotWithPending(source, "ghost", 1000L, phase: 0);

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");
            MiniTestRunner.IsTrue(restored.RetryPendingRecovery(), "операция отменена");
            MiniTestRunner.AreEqual(0, restored.PendingOperationCount, "журнал пуст");
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "деньги не тронуты");
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(38400L, view.TotalRemaining, "долг не изменён");

            // paymentId освобождён — им можно воспользоваться.
            DebtTestEnv.ExpectSuccess(restored.TryPay(DebtTestEnv.DebtId, 1000L, "ghost"), "повторное использование id");
        }

        private static void RestoreRefundPending()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            wallet.TryDebit("bad", 1000L);
            DebtSnapshot snapshot = SnapshotWithPending(source, "bad", 1000L, phase: 2);

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");
            MiniTestRunner.IsTrue(restored.RetryPendingRecovery(), "компенсация выполнена");
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "деньги возвращены");
            MiniTestRunner.AreEqual(0, restored.PendingOperationCount, "журнал пуст");
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(38400L, view.TotalRemaining, "долг не изменён");
            MiniTestRunner.AreEqual(0, restored.GetPaymentHistory(DebtTestEnv.DebtId).Count, "платёж не зачтён");
        }

        private static void RefundRetried()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { RefundFailuresRemaining = 1 };
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            wallet.TryDebit("bad", 1000L);
            DebtSnapshot snapshot = SnapshotWithPending(source, "bad", 1000L, phase: 2);

            var restored = new DebtService(clock, wallet, null);
            restored.RestoreSnapshot(snapshot);

            MiniTestRunner.IsFalse(restored.RetryPendingRecovery(), "первый возврат не удался");
            MiniTestRunner.AreEqual(1, restored.PendingOperationCount, "операция ждёт");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "деньги ещё не возвращены");

            MiniTestRunner.IsTrue(restored.RetryPendingRecovery(), "повторный возврат успешен");
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "деньги возвращены один раз");
        }

        private static void RecoveryIdempotent()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            wallet.TryDebit("crash", 1000L);
            DebtSnapshot snapshot = SnapshotWithPending(source, "crash", 1000L, phase: 1);

            var restored = new DebtService(clock, wallet, null);
            restored.RestoreSnapshot(snapshot);
            restored.RetryPendingRecovery();
            restored.RetryPendingRecovery();
            restored.RetryPendingRecovery();

            MiniTestRunner.AreEqual(1, restored.GetPaymentHistory(DebtTestEnv.DebtId).Count, "зачёт ровно один раз");
            restored.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "остаток корректен");
            MiniTestRunner.AreEqual(1000L, view.PaidTotal, "выплачено один раз");
        }

        private static void DuplicateAfterRestore()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(source.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "платёж до сохранения");

            DebtSnapshot snapshot = source.CreateSnapshot();
            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");

            DebtTestEnv.ExpectCode(DebtResultCode.DuplicatePayment,
                restored.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "повтор paymentId после загрузки");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "повторного списания нет");
        }

        private static void SeriesWithFailuresConsistent()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(10000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "a"), "платёж a");

            // Сбой на платеже b: списал и бросил, журнал сразу доступен.
            wallet.DebitThenThrow = true;
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 2000L, "b"), "платёж b восстановлен");
            wallet.DebitThenThrow = false;

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 3000L, "c"), "платёж c");

            MiniTestRunner.AreEqual(4000L, wallet.Balance, "итоговый баланс");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(6000L, view.PaidTotal, "итого выплачено");
            MiniTestRunner.AreEqual(32400L, view.TotalRemaining, "итоговый остаток");
            MiniTestRunner.AreEqual(3, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "три записи истории");
        }

        private static void RefundOnlyForRealDebit()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService source = DebtTestEnv.CreateService(clock, wallet, null);

            // RefundPending, но списания НЕ было: возврат не должен создать деньги.
            DebtSnapshot snapshot = SnapshotWithPending(source, "never", 1000L, phase: 2);
            var restored = new DebtService(clock, wallet, null);
            restored.RestoreSnapshot(snapshot);

            // FakeWallet.TryRefund вернёт false (нет списания) — операция останется ждать,
            // баланс не увеличится. Это защита от «возврата» несписанных денег.
            restored.RetryPendingRecovery();
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "деньги из воздуха не появились");
        }

        // --- помощник ---

        private static DebtSnapshot SnapshotWithPending(DebtService service, string paymentId, long amount, int phase)
        {
            DebtSnapshot snapshot = service.CreateSnapshot();
            var list = new List<PendingOperationSnapshot>(snapshot.PendingOperations);
            list.Add(new PendingOperationSnapshot
            {
                PaymentId = paymentId,
                DebtId = DebtTestEnv.DebtId,
                AmountRubles = amount,
                Phase = phase,
                CreatedAtMinutes = 0L
            });
            snapshot.PendingOperations = list.ToArray();
            return snapshot;
        }
    }
}
