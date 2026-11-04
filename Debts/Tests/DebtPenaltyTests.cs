// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Тесты игрового времени, льготного периода, штрафов, стадий взыскания и событий.

using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Persistence;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtPenaltyTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Время: до срока долг Current, событий нет", BeforeDue);
            suite.Add("Время: PaymentDue одноразовое на границе срока", PaymentDueOnce);
            suite.Add("Время: граница льготного периода", GraceBoundary);
            suite.Add("Время: DebtOverdue одноразовое", OverdueOnce);
            suite.Add("Штраф: один раз за период, повтор не начисляет", PenaltyOncePerPeriod);
            suite.Add("Штраф: граница расчётного периода", PenaltyPeriodBoundary);
            suite.Add("Штраф: скачок времени начисляет пропущенные периоды", PenaltyTimeJump);
            suite.Add("Штраф: максимум за всё время ограничивает начисление", PenaltyCap);
            suite.Add("Штраф: отключённые штрафы не начисляются", PenaltiesDisabled);
            suite.Add("Штраф: огромный скачок времени без переполнения", PenaltyHugeJump);
            suite.Add("Штраф: процент считается от остатка основного долга", PenaltyRateOnRemaining);
            suite.Add("Штраф: начисление продолжается после частичного погашения", PenaltyContinuesAfterPartialPayment);
            suite.Add("Штраф: событие PenaltyApplied несёт сумму начисления", PenaltyEventAmount);
            suite.Add("Стадии: переход Overdue → Reminder → Collection → LegalAction", StageTransitions);
            suite.Add("Стадии: повторное обновление не дублирует события стадий", StageNoDuplicate);
            suite.Add("Стадии: закрытый долг не начисляет и не меняет стадию", ClosedDebtSkipped);
            suite.Add("Время: уменьшение времени — AdapterError без изменений", ClockRegression);
            suite.Add("Время: исключение часов — AdapterError", ClockThrows);
            suite.Add("Время: повторный UpdateTime в тот же момент идемпотентен", UpdateIdempotent);
            suite.Add("Время: сбой обработчика событий не ломает начисление", SinkThrowsDoesNotBreak);

            // Регрессионные тесты аудита ТЗ №017.
            suite.Add("Регрессия: снимок внутри PenaltyApplied валиден", SnapshotInsidePenaltyEventValid);
            suite.Add("Регрессия: restore-по-снимку внутри списания отклонён", RestoreInsideDebitWindowRejected);

            return suite;
        }

        private static void BeforeDue()
        {
            var clock = new FakeClock { Minutes = 500L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L);

            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "обновление до срока");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(DebtStatus.Current, view.Status, "стадия Current");
            MiniTestRunner.AreEqual(0, sink.CountOf(DebtEventType.PaymentDue), "нет события срока");
            MiniTestRunner.AreEqual(0, sink.CountOf(DebtEventType.DebtOverdue), "нет просрочки");
        }

        private static void PaymentDueOnce()
        {
            var clock = new FakeClock { Minutes = 1000L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L);

            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "первое обновление");
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "повторное обновление");
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "третье обновление");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.PaymentDue), "PaymentDue ровно один раз");
        }

        private static void GraceBoundary()
        {
            var clock = new FakeClock { Minutes = 1500L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            // due=1000, grace=500 → просрочка строго после 1500.
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L, grace: 500L);

            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "ровно на границе");
            MiniTestRunner.AreEqual(0, sink.CountOf(DebtEventType.DebtOverdue), "на границе просрочки нет");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView atBoundary);
            MiniTestRunner.AreEqual(DebtStatus.Current, atBoundary.Status, "на границе Current");

            clock.Minutes = 1501L;
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "на минуту позже");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.DebtOverdue), "просрочка началась");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView after);
            MiniTestRunner.AreEqual(DebtStatus.Overdue, after.Status, "стадия Overdue");
        }

        private static void OverdueOnce()
        {
            var clock = new FakeClock { Minutes = 1001L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L);

            service.UpdateTime();
            clock.Minutes = 2000L;
            service.UpdateTime();
            service.UpdateTime();
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.DebtOverdue), "DebtOverdue один раз");
        }

        private static void PenaltyOncePerPeriod()
        {
            var clock = new FakeClock { Minutes = 101L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            var penalties = PenaltyPolicy.FixedPerPeriod(1440L, 500L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 100L, penalties: penalties);

            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView first);
            MiniTestRunner.AreEqual(500L, first.PenaltiesRemaining, "штраф за первый период");

            // Тот же момент и тот же период — повторного начисления нет.
            service.UpdateTime();
            clock.Minutes = 100L + 1440L; // всё ещё период 1
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView again);
            MiniTestRunner.AreEqual(500L, again.PenaltiesRemaining, "повтор не начислил");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.PenaltyApplied), "одно событие штрафа");
        }

        private static void PenaltyPeriodBoundary()
        {
            var clock = new FakeClock { Minutes = 100L };
            var wallet = new FakeWallet(0L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L, penalties: penalties);

            service.UpdateTime(); // overdue=100 → период 1
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView p1);
            MiniTestRunner.AreEqual(50L, p1.PenaltiesAccrued, "период 1");

            clock.Minutes = 101L; // overdue=101 → период 2
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView p2);
            MiniTestRunner.AreEqual(100L, p2.PenaltiesAccrued, "период 2");
        }

        private static void PenaltyTimeJump()
        {
            var clock = new FakeClock { Minutes = 250L };
            var wallet = new FakeWallet(0L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L, penalties: penalties);

            // overdue=250 → период 3 → три штрафа по 50.
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(150L, view.PenaltiesAccrued, "три пропущенных периода");
            MiniTestRunner.AreEqual(150L, view.PenaltiesRemaining, "остаток штрафов");
        }

        private static void PenaltyCap()
        {
            var clock = new FakeClock { Minutes = 1000L };
            var wallet = new FakeWallet(0L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 120L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L, penalties: penalties);

            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView capped);
            MiniTestRunner.AreEqual(120L, capped.PenaltiesAccrued, "начисление ограничено максимумом");

            clock.Minutes = 5000L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView still);
            MiniTestRunner.AreEqual(120L, still.PenaltiesAccrued, "максимум не превышен повторно");
        }

        private static void PenaltiesDisabled()
        {
            var clock = new FakeClock { Minutes = 100000L };
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L, penalties: PenaltyPolicy.Disabled);

            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(0L, view.PenaltiesAccrued, "штрафов нет");
            MiniTestRunner.IsFalse(view.PenaltiesEnabled, "штрафы отключены");
        }

        private static void PenaltyHugeJump()
        {
            var clock = new FakeClock { Minutes = long.MaxValue };
            var wallet = new FakeWallet(0L);
            var penalties = PenaltyPolicy.FixedPerPeriod(1440L, 500L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L, penalties: penalties);

            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "огромный скачок");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(100000L, view.PenaltiesAccrued, "начислено до максимума без переполнения");
            MiniTestRunner.AreEqual(138400L, view.TotalRemaining, "общая задолженность корректна");
        }

        private static void PenaltyRateOnRemaining()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            // 10% за период от остатка основного долга.
            var penalties = new PenaltyPolicy(true, 100L, 0L, 1000, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, principal: 10000L, due: 0L, penalties: penalties);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 5000L, "half"), "погашена половина");
            clock.Minutes = 1L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            // 10% от остатка 5000 = 500, а не от исходных 10000.
            MiniTestRunner.AreEqual(500L, view.PenaltiesRemaining, "процент от остатка");
        }

        private static void PenaltyContinuesAfterPartialPayment()
        {
            var clock = new FakeClock { Minutes = 1L };
            var wallet = new FakeWallet(1000L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, principal: 1000L, due: 0L, penalties: penalties);

            service.UpdateTime(); // период 1: штраф 50

            // Порядок погашения — сначала штрафы, затем основной долг.
            // Платёж 500 = штрафы 50 + основной 450 → основной остаток 550.
            DebtPaymentResult payment = service.TryPay(DebtTestEnv.DebtId, 500L, "partial");
            DebtTestEnv.ExpectSuccess(payment, "частичное погашение");
            MiniTestRunner.AreEqual(50L, payment.PaidPenaltiesRubles, "штрафы погашены первыми");
            MiniTestRunner.AreEqual(450L, payment.PaidPrincipalRubles, "остальное в основной долг");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView mid);
            MiniTestRunner.AreEqual(550L, mid.PrincipalRemaining, "остаток основного долга");
            MiniTestRunner.AreEqual(0L, mid.PenaltiesRemaining, "штрафы погашены");
            MiniTestRunner.AreNotEqual(DebtStatus.Closed, mid.Status, "долг ещё открыт");

            clock.Minutes = 101L; // период 2
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView after);
            MiniTestRunner.AreEqual(100L, after.PenaltiesAccrued, "фиксированный штраф продолжает начисляться");
            MiniTestRunner.AreEqual(50L, after.PenaltiesRemaining, "новый штраф не погашен");
        }

        private static void PenaltyEventAmount()
        {
            var clock = new FakeClock { Minutes = 250L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 0L, penalties: penalties);

            service.UpdateTime();
            DebtEvent evt = sink.LastOf(DebtEventType.PenaltyApplied);
            MiniTestRunner.IsNotNull(evt, "событие штрафа есть");
            MiniTestRunner.AreEqual(150L, evt.AmountRubles, "сумма начисления в событии");
            MiniTestRunner.AreEqual(250L, evt.AtMinutes, "временная метка");
        }

        private static void StageTransitions()
        {
            var clock = new FakeClock { Minutes = 50L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            var thresholds = new CollectionThresholds(100L, 200L, 300L);
            var service = new DebtService(clock, wallet, sink);
            service.RegisterCreditor(new Creditor(DebtTestEnv.CreditorId, "К"));
            service.CreateDebt(new DebtConfiguration
            {
                DebtId = DebtTestEnv.DebtId,
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 1000L,
                DueAtMinutes = 0L,
                Collection = thresholds
            });

            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView s1);
            MiniTestRunner.AreEqual(DebtStatus.Overdue, s1.Status, "Overdue");

            clock.Minutes = 100L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView s2);
            MiniTestRunner.AreEqual(DebtStatus.Reminder, s2.Status, "Reminder");

            clock.Minutes = 200L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView s3);
            MiniTestRunner.AreEqual(DebtStatus.Collection, s3.Status, "Collection");

            clock.Minutes = 300L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView s4);
            MiniTestRunner.AreEqual(DebtStatus.LegalAction, s4.Status, "LegalAction");

            // Current→Overdue→Reminder→Collection→LegalAction = 4 события стадии.
            MiniTestRunner.AreEqual(4, sink.CountOf(DebtEventType.CollectionStageChanged), "число смен стадии");
            DebtEvent last = sink.LastOf(DebtEventType.CollectionStageChanged);
            MiniTestRunner.AreEqual(DebtStatus.Collection, last.PreviousStatus, "предыдущая стадия в событии");
            MiniTestRunner.AreEqual(DebtStatus.LegalAction, last.Status, "новая стадия в событии");
        }

        private static void StageNoDuplicate()
        {
            var clock = new FakeClock { Minutes = 150L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 0L);

            service.UpdateTime();
            service.UpdateTime();
            service.UpdateTime();
            // Current→Overdue один раз (пороги по умолчанию: Reminder через 3 суток).
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.CollectionStageChanged), "смена стадии без дублей");
        }

        private static void ClosedDebtSkipped()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            var sink = new RecordingEventSink();
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 50L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 0L, penalties: penalties);
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 38400L, "full"), "закрытие");

            int eventsBefore = sink.Events.Count;
            clock.Minutes = 100000L;
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(DebtStatus.Closed, view.Status, "стадия осталась Closed");
            MiniTestRunner.AreEqual(0L, view.PenaltiesAccrued, "штрафы закрытому долгу не начисляются");
            MiniTestRunner.AreEqual(eventsBefore, sink.Events.Count, "новых событий нет");
        }

        private static void ClockRegression()
        {
            var clock = new FakeClock { Minutes = 100L };
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L);
            service.UpdateTime();

            clock.Minutes = 50L;
            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError, service.UpdateTime(), "время уменьшилось");
            MiniTestRunner.AreEqual(100L, service.LastProcessedMinutes, "время не откатилось");
        }

        private static void ClockThrows()
        {
            var clock = new FakeClock { Minutes = 100L, Throw = true };
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 0L);

            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError, service.UpdateTime(), "часы бросили исключение");
        }

        private static void UpdateIdempotent()
        {
            var clock = new FakeClock { Minutes = 5000L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink();
            var penalties = PenaltyPolicy.FixedPerPeriod(1440L, 500L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L, penalties: penalties);

            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView first);
            int eventCount = sink.Events.Count;

            service.UpdateTime();
            service.UpdateTime();
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView second);
            MiniTestRunner.AreEqual(first.PenaltiesAccrued, second.PenaltiesAccrued, "штрафы не изменились");
            MiniTestRunner.AreEqual(first.TotalRemaining, second.TotalRemaining, "остаток не изменился");
            MiniTestRunner.AreEqual(eventCount, sink.Events.Count, "события не продублировались");
        }

        private static void SinkThrowsDoesNotBreak()
        {
            var clock = new FakeClock { Minutes = 5000L };
            var wallet = new FakeWallet(0L);
            var sink = new RecordingEventSink { Throw = true };
            var penalties = PenaltyPolicy.FixedPerPeriod(1440L, 500L, 100000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L, penalties: penalties);

            // Обработчик бросает исключение, но начисление и состояние должны быть корректны.
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "обновление со сбойным обработчиком");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.IsTrue(view.PenaltiesAccrued > 0L, "штраф начислен несмотря на сбой обработчика");
        }

        /// <summary>
        /// Дефект: LastPenaltyPeriodIndex продвигался ПОСЛЕ рассылки PenaltyApplied.
        /// Обработчик, вызывавший CreateSnapshot() в событии, получал снимок, где
        /// штрафы начислены, но индекс периода ещё стоит на предыдущем значении —
        /// при загрузке такой снимок начислил бы штраф за тот же период повторно.
        /// </summary>
        private static void SnapshotInsidePenaltyEventValid()
        {
            var clock = new FakeClock { Minutes = 1000L };
            var wallet = new FakeWallet(0L);
            var penalties = PenaltyPolicy.FixedPerPeriod(100L, 500L, 100000L);
            DebtService service = null;
            var sink = new SnapshotOnPenaltySink(() => service);
            service = DebtTestEnv.CreateService(clock, wallet, sink, due: 1000L, penalties: penalties);

            // 1000 → 2500: 1500 минут просрочки = 15 периодов по 100 минут.
            clock.Minutes = 2500L;
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "начисление штрафов");

            MiniTestRunner.IsNotNull(sink.Captured, "снимок из обработчика PenaltyApplied получен");
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(sink.Captured, out string err),
                "снимок внутри PenaltyApplied валиден: " + err);

            var debt = sink.Captured.Debts[0];
            MiniTestRunner.AreEqual(15L, debt.LastPenaltyPeriodIndex, "индекс периода согласован с начислением");
            MiniTestRunner.AreEqual(7500L, debt.PenaltiesAccrued, "штрафы начислены полностью");
        }

        /// <summary>
        /// Дефект: реентрантное RestoreSnapshot внутри списания (адаптер кошелька
        /// или обработчик событий, который накатывал снимок) заменял рабочее
        /// состояние сервиса, пока выполняется платеж. Теперь restore внутри
        /// «окна списания» отклоняется кодом.
        /// </summary>
        private static void RestoreInsideDebitWindowRejected()
        {
            var clock = new FakeClock { Minutes = 1000L };
            var wallet = new FakeWallet(10000L);

            // Снимок, который «внешняя система» попытается накатить реентрантно.
            var outside = DebtTestEnv.CreateService(clock, wallet, null);
            DebtSnapshot snapshot = outside.CreateSnapshot();

            DebtService service = null;
            var restoreSink = new RestoringSink(() => service, snapshot);

            // WalletAdapter: при списании вызывает restore на том же сервисе.
            var reentrant = new ReentrantWallet(wallet, () => restoreSink.TryRestore());

            service = DebtTestEnv.CreateService(clock, reentrant, restoreSink);
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "p1"), "платёж");

            MiniTestRunner.IsTrue(restoreSink.RestoreRejected,
                "restore внутри выполняющегося платежа отклонён");
            MiniTestRunner.AreEqual(1, restoreSink.RestoreAttempts, "попытка restore была");

            // Рабочее состояние не сломали: платёж зачтён ровно один раз.
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "долг уменьшен");
            MiniTestRunner.AreEqual(1000L, view.PaidTotal, "платёж зачтён один раз");
            MiniTestRunner.AreEqual(9000L, wallet.Balance, "списано один раз");

            var after = service.CreateSnapshot();
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(after, out string err),
                "снимок сервиса после платежа валиден: " + err);
        }

        /// <summary>Приёмник, который в PenaltyApplied делает снимок и подтверждает его.</summary>
        private sealed class SnapshotOnPenaltySink : IDebtEventSink
        {
            private readonly System.Func<DebtService> _service;

            public DebtSnapshot Captured { get; private set; }

            public SnapshotOnPenaltySink(System.Func<DebtService> service)
            {
                _service = service;
            }

            public void Publish(DebtEvent debtEvent)
            {
                if (debtEvent.Type == DebtEventType.PenaltyApplied)
                {
                    Captured = _service().CreateSnapshot();
                }
            }
        }

        /// <summary>
        /// Приёмник, который выполняет restore по заранее снятому снимку —
        /// можно вызвать как из события, так и из адаптера кошелька.
        /// </summary>
        private sealed class RestoringSink : IDebtEventSink
        {
            private readonly System.Func<DebtService> _service;
            private readonly DebtSnapshot _snapshot;

            public RestoringSink(System.Func<DebtService> service, DebtSnapshot snapshot)
            {
                _service = service;
                _snapshot = snapshot;
            }

            public DebtSnapshot Captured { get; private set; }

            public bool RestoreRejected { get; private set; }

            public int RestoreAttempts { get; private set; }

            public void TryRestore()
            {
                DebtResult restore = _service().RestoreSnapshot(_snapshot);
                RestoreAttempts++;
                RestoreRejected = !restore.IsSuccess;
            }

            public void Publish(DebtEvent debtEvent)
            {
                if (debtEvent.Type == DebtEventType.PenaltyApplied)
                {
                    TryRestore();
                    Captured = _service().CreateSnapshot();
                }
            }
        }
    }
}
