// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Тесты создания долгов, кредиторов и базовых платежей.

using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtServiceTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("Создание: успешный долг и событие DebtCreated", CreateDebtSuccess);
            suite.Add("Создание: процентная надбавка включается в основной долг", CreateDebtWithInterest);
            suite.Add("Создание: повторный ID долга отклонён", CreateDuplicateDebt);
            suite.Add("Создание: незарегистрированный кредитор отклонён", CreateUnknownCreditor);
            suite.Add("Создание: нулевая основная сумма отклонена", CreateZeroPrincipal);
            suite.Add("Создание: отрицательная основная сумма отклонена", CreateNegativePrincipal);
            suite.Add("Создание: отрицательная ставка отклонена", CreateNegativeRate);
            suite.Add("Создание: переполнение процента отклонено", CreateInterestOverflow);
            suite.Add("Создание: переполнение долг+максимум штрафов отклонено", CreatePenaltyCapOverflow);
            suite.Add("Создание: переполнение срок+льгота отклонено", CreateDueGraceOverflow);
            suite.Add("Кредиторы: несколько кредиторов и их долги", MultipleCreditors);
            suite.Add("Кредиторы: повторная регистрация отклонена", DuplicateCreditor);
            suite.Add("Фабрика: стартовый долг 38400 и настраиваемые поля", StartingFactory);
            suite.Add("Платёж: частичное погашение уменьшает основной долг", PartialPayment);
            suite.Add("Платёж: полное погашение закрывает долг", FullPaymentCloses);
            suite.Add("Платёж: повтор paymentId не списывает дважды", DuplicatePaymentId);
            suite.Add("Платёж: отрицательная сумма отклонена", NegativePayment);
            suite.Add("Платёж: нулевая сумма отклонена", ZeroPayment);
            suite.Add("Платёж: недостаток средств отклонён", InsufficientFunds);
            suite.Add("Платёж: переплата отклонена", Overpayment);
            suite.Add("Платёж: неизвестный долг отклонён", UnknownDebt);
            suite.Add("Платёж: закрытый долг отклонён", ClosedDebtPayment);
            suite.Add("Платёж: отказ кошелька (false) не меняет долг", WalletReturnsFalse);
            suite.Add("Платёж: исключение чтения баланса — AdapterError", BalanceReadThrows);
            suite.Add("Платёж: пустой paymentId отклонён", EmptyPaymentId);
            suite.Add("Платёж: история записывает распределение суммы", PaymentHistoryParts);
            suite.Add("Платёж: выплаченная сумма накапливается", PaidTotalAccumulates);
            suite.Add("Чтение: суммарная задолженность по открытым долгам", TotalRemainingRead);
            suite.Add("Чтение: неизвестный долг не найден", ReadUnknownDebt);

            return suite;
        }

        private static void CreateDebtSuccess()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(1270L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink);

            MiniTestRunner.IsTrue(service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view), "долг создан");
            MiniTestRunner.AreEqual(38400L, view.PrincipalOriginal, "основной долг");
            MiniTestRunner.AreEqual(38400L, view.TotalRemaining, "общая задолженность");
            MiniTestRunner.AreEqual(0L, view.PaidTotal, "выплачено");
            MiniTestRunner.AreEqual(DebtStatus.Current, view.Status, "стадия");
            MiniTestRunner.AreEqual(DebtTestEnv.CreditorId, view.CreditorId, "кредитор");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.DebtCreated), "событие DebtCreated");
        }

        private static void CreateDebtWithInterest()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, principal: 1000L, rateBasisPoints: 1500);

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            // 1000 + 15% = 1150.
            MiniTestRunner.AreEqual(1150L, view.PrincipalOriginal, "основной долг с процентом");
            MiniTestRunner.AreEqual(1150L, view.TotalRemaining, "общая задолженность с процентом");
        }

        private static void CreateDuplicateDebt()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = DebtTestEnv.DebtId,
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 100L,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.DuplicateDebt, service.CreateDebt(config), "повторный ID");
        }

        private static void CreateUnknownCreditor()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "d",
                CreditorId = "ghost",
                PrincipalRubles = 100L,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.CreditorNotFound, service.CreateDebt(config), "нет кредитора");
        }

        private static void CreateZeroPrincipal()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "zero",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 0L,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument, service.CreateDebt(config), "нулевая сумма");
        }

        private static void CreateNegativePrincipal()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "neg",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = -5L,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument, service.CreateDebt(config), "отрицательная сумма");
        }

        private static void CreateNegativeRate()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "rate",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 100L,
                InterestRateBasisPoints = -1,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument, service.CreateDebt(config), "отрицательная ставка");
        }

        private static void CreateInterestOverflow()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "overflow",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = long.MaxValue,
                InterestRateBasisPoints = 10000,
                DueAtMinutes = 10L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.Overflow, service.CreateDebt(config), "переполнение процента");
        }

        private static void CreatePenaltyCapOverflow()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "cap",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 100L,
                DueAtMinutes = 10L,
                Penalties = PenaltyPolicy.FixedPerPeriod(1440L, 10L, long.MaxValue)
            };

            DebtTestEnv.ExpectCode(DebtResultCode.Overflow, service.CreateDebt(config), "переполнение долг+максимум");
        }

        private static void CreateDueGraceOverflow()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            var config = new DebtConfiguration
            {
                DebtId = "due",
                CreditorId = DebtTestEnv.CreditorId,
                PrincipalRubles = 100L,
                DueAtMinutes = long.MaxValue,
                GracePeriodMinutes = 1L
            };

            DebtTestEnv.ExpectCode(DebtResultCode.Overflow, service.CreateDebt(config), "переполнение срок+льгота");
        }

        private static void MultipleCreditors()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(service.RegisterCreditor(new Creditor("bank", "Банк")), "банк");
            DebtTestEnv.ExpectSuccess(service.RegisterCreditor(new Creditor("friend", "Друг")), "друг");

            DebtTestEnv.ExpectSuccess(service.CreateDebt(new DebtConfiguration
            {
                DebtId = "d1", CreditorId = "bank", PrincipalRubles = 100L, DueAtMinutes = 10L
            }), "долг банка");
            DebtTestEnv.ExpectSuccess(service.CreateDebt(new DebtConfiguration
            {
                DebtId = "d2", CreditorId = "friend", PrincipalRubles = 200L, DueAtMinutes = 20L
            }), "долг друга");

            service.TryGetDebt("d1", out DebtView v1);
            service.TryGetDebt("d2", out DebtView v2);
            MiniTestRunner.AreEqual("Банк", v1.CreditorName, "имя кредитора 1");
            MiniTestRunner.AreEqual("Друг", v2.CreditorName, "имя кредитора 2");
            MiniTestRunner.AreEqual(2, service.GetDebts().Count, "число долгов");
        }

        private static void DuplicateCreditor()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(service.RegisterCreditor(new Creditor("bank", "Банк")), "первый");
            DebtTestEnv.ExpectCode(DebtResultCode.DuplicateCreditor,
                service.RegisterCreditor(new Creditor("bank", "Другой")), "повтор");
        }

        private static void StartingFactory()
        {
            StartingDebtScenario scenario = StartingDebtFactory.Create(
                creditorId: "unknown",
                creditorName: "Неизвестный кредитор",
                dueAtMinutes: 43200L,
                interestRateBasisPoints: 0,
                gracePeriodMinutes: 1440L);

            MiniTestRunner.AreEqual(38400L, scenario.Debt.PrincipalRubles, "стартовая сумма");
            MiniTestRunner.AreEqual("Неизвестный кредитор", scenario.Creditor.Name, "имя кредитора");
            MiniTestRunner.AreEqual(43200L, scenario.Debt.DueAtMinutes, "срок");

            var clock = new FakeClock();
            var wallet = new FakeWallet(1270L);
            var service = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(service.RegisterCreditor(scenario.Creditor), "кредитор сценария");
            DebtTestEnv.ExpectSuccess(service.CreateDebt(scenario.Debt), "долг сценария");
            service.TryGetDebt(scenario.Debt.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(38400L, view.TotalRemaining, "стартовый долг в сервисе");
        }

        private static void PartialPayment()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(1270L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtPaymentResult result = service.TryPay(DebtTestEnv.DebtId, 1000L, "p1");
            DebtTestEnv.ExpectSuccess(result, "частичный платёж");
            MiniTestRunner.AreEqual(1000L, result.PaidRubles, "оплачено");
            MiniTestRunner.AreEqual(1000L, result.PaidPrincipalRubles, "в основной долг");
            MiniTestRunner.AreEqual(0L, result.PaidPenaltiesRubles, "в штрафы");
            MiniTestRunner.AreEqual(37400L, result.RemainingRubles, "остаток");
            MiniTestRunner.AreEqual(270L, wallet.Balance, "баланс кошелька");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.PrincipalRemaining, "остаток основного");
            MiniTestRunner.AreEqual(DebtStatus.Current, view.Status, "долг не закрыт");
        }

        private static void FullPaymentCloses()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            var sink = new RecordingEventSink();
            DebtService service = DebtTestEnv.CreateService(clock, wallet, sink);

            DebtPaymentResult result = service.TryPay(DebtTestEnv.DebtId, 38400L, "full");
            DebtTestEnv.ExpectSuccess(result, "полный платёж");
            MiniTestRunner.AreEqual(0L, result.RemainingRubles, "остаток ноль");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(DebtStatus.Closed, view.Status, "долг закрыт");
            MiniTestRunner.AreEqual(0L, view.TotalRemaining, "задолженность ноль");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.DebtClosed), "событие DebtClosed");
            MiniTestRunner.AreEqual(1, sink.CountOf(DebtEventType.PaymentCompleted), "событие PaymentCompleted");
        }

        private static void DuplicatePaymentId()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "same"), "первый");

            // ТЗ №021: повтор идемпотентен и возвращает ПОЛНЫЙ прежний результат
            // (суммы и остаток), а не отказ — иначе UI показывал бы неверный остаток.
            DebtPaymentResult second = service.TryPay(DebtTestEnv.DebtId, 1000L, "same");
            DebtTestEnv.ExpectCode(DebtResultCode.Success, second, "повтор paymentId — успех без списания");
            MiniTestRunner.AreEqual(1000L, second.PaidRubles, "возвращена зачтённая сумма");
            MiniTestRunner.AreEqual(37400L, second.RemainingRubles, "возвращён актуальный остаток");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "списано только один раз");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "один вызов списания");
        }

        private static void NegativePayment()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument,
                service.TryPay(DebtTestEnv.DebtId, -100L, "neg"), "отрицательный платёж");
            MiniTestRunner.AreEqual(5000L, wallet.Balance, "баланс не изменён");
        }

        private static void ZeroPayment()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument,
                service.TryPay(DebtTestEnv.DebtId, 0L, "zero"), "нулевой платёж");
        }

        private static void InsufficientFunds()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(1270L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.InsufficientFunds,
                service.TryPay(DebtTestEnv.DebtId, 5000L, "poor"), "недостаток средств");
            MiniTestRunner.AreEqual(1270L, wallet.Balance, "баланс не изменён");
            MiniTestRunner.AreEqual(0, wallet.DebitCalls, "списания не было");
        }

        private static void Overpayment()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(999999L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.Overpayment,
                service.TryPay(DebtTestEnv.DebtId, 40000L, "over"), "переплата");
            MiniTestRunner.AreEqual(999999L, wallet.Balance, "баланс не изменён");
        }

        private static void UnknownDebt()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.DebtNotFound,
                service.TryPay("ghost", 100L, "x"), "неизвестный долг");
        }

        private static void ClosedDebtPayment()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(999999L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 38400L, "full"), "закрытие");

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidState,
                service.TryPay(DebtTestEnv.DebtId, 100L, "after"), "платёж после закрытия");
        }

        private static void WalletReturnsFalse()
        {
            var clock = new FakeClock();
            // Кошелёк сообщает баланс 5000, но списание отклоняет (false), не меняя баланс.
            var rejecting = new RejectingWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, rejecting, null);

            DebtTestEnv.ExpectCode(DebtResultCode.WalletFailure,
                service.TryPay(DebtTestEnv.DebtId, 1000L, "reject"), "отказ кошелька");
            MiniTestRunner.AreEqual(5000L, rejecting.Balance, "баланс не изменён");
            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(38400L, view.TotalRemaining, "долг не изменён");
        }

        private static void BalanceReadThrows()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L) { ThrowOnBalance = true };
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.AdapterError,
                service.TryPay(DebtTestEnv.DebtId, 1000L, "x"), "исключение баланса");
            MiniTestRunner.AreEqual(0, wallet.DebitCalls, "списания не было");
        }

        private static void EmptyPaymentId()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectCode(DebtResultCode.InvalidArgument,
                service.TryPay(DebtTestEnv.DebtId, 1000L, "  "), "пустой paymentId");
        }

        private static void PaymentHistoryParts()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            var penalties = PenaltyPolicy.FixedPerPeriod(1440L, 500L, 10000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null, due: 100L, penalties: penalties);

            // Просрочка на 1 период → штраф 500.
            clock.Minutes = 100L + 1440L;
            DebtTestEnv.ExpectSuccess(service.UpdateTime(), "начисление штрафа");

            // Платёж 1500: сначала штрафы 500, затем основной 1000.
            DebtPaymentResult result = service.TryPay(DebtTestEnv.DebtId, 1500L, "mix");
            DebtTestEnv.ExpectSuccess(result, "смешанный платёж");
            MiniTestRunner.AreEqual(500L, result.PaidPenaltiesRubles, "погашено штрафов");
            MiniTestRunner.AreEqual(1000L, result.PaidPrincipalRubles, "погашено основного");

            var history = service.GetPaymentHistory(DebtTestEnv.DebtId);
            MiniTestRunner.AreEqual(1, history.Count, "одна запись истории");
            MiniTestRunner.AreEqual("mix", history[0].PaymentId, "paymentId в истории");
            MiniTestRunner.AreEqual(500L, history[0].PenaltiesPartRubles, "штрафы в истории");
            MiniTestRunner.AreEqual(1000L, history[0].PrincipalPartRubles, "основной в истории");
        }

        private static void PaidTotalAccumulates()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(50000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "a"), "платёж a");
            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 2000L, "b"), "платёж b");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(3000L, view.PaidTotal, "накоплено выплачено");
            MiniTestRunner.AreEqual(35400L, view.TotalRemaining, "остаток");
        }

        private static void TotalRemainingRead()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            var service = new DebtService(clock, wallet, null);
            service.RegisterCreditor(new Creditor("c", "К"));
            service.CreateDebt(new DebtConfiguration { DebtId = "a", CreditorId = "c", PrincipalRubles = 100L, DueAtMinutes = 10L });
            service.CreateDebt(new DebtConfiguration { DebtId = "b", CreditorId = "c", PrincipalRubles = 200L, DueAtMinutes = 10L });

            MiniTestRunner.IsTrue(service.TryGetTotalRemainingRubles(out long total), "сумма читается");
            MiniTestRunner.AreEqual(300L, total, "суммарная задолженность");
        }

        private static void ReadUnknownDebt()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(0L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            MiniTestRunner.IsFalse(service.TryGetDebt("ghost", out DebtView view), "неизвестный долг");
            MiniTestRunner.IsNull(view, "представление null");
            MiniTestRunner.AreEqual(0, service.GetPaymentHistory("ghost").Count, "пустая история");
        }

        /// <summary>Кошелёк, который всегда отклоняет списание (false), не меняя баланс.</summary>
        private sealed class RejectingWallet : IDebtWalletAdapter
        {
            public long Balance { get; }

            public RejectingWallet(long balance)
            {
                Balance = balance;
            }

            public bool TryGetBalance(out long rubles)
            {
                rubles = Balance;
                return true;
            }

            public bool TryDebit(string paymentId, long rubles)
            {
                return false;
            }

            public bool TryRefund(string paymentId, long rubles)
            {
                return false;
            }

            public bool TryWasDebited(string paymentId, out bool debited)
            {
                debited = false;
                return true;
            }
        }
    }
}
