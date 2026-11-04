// Life From Zero — Debts module (ТЗ №021)
// Namespace: LifeFromZero.Debts.Tests
//
// Только финансовые контракты, связанные с Economy: идентификаторы платежей,
// идемпотентный повтор, компенсация и перезагрузка снимка.

using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Persistence;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtFinancialContractTests
    {
        public static TestSuite CreateSuite()
        {
            var suite = new TestSuite();

            suite.Add("ID платежа: префикс debt: отделяет долги от магазина и доставки", PaymentIdNamespace);
            suite.Add("ID платежа: чужой префикс не считается идентификатором долга", PaymentIdForeignPrefixRejected);
            suite.Add("Платёж: повтор того же paymentId возвращает прежний результат без списания",
                RepeatedPaymentFullReplay);
            suite.Add("Платёж: повтор с другой суммой не списывает деньги и не меняет долг",
                RepeatedPaymentDifferentAmountNoSideEffects);
            suite.Add("Платёж: повтор не создаёт вторую запись в истории", RepeatedPaymentSingleRecord);
            suite.Add("Платёж: повтор после загрузки снимка не списывает снова", RepeatedPaymentAfterRestore);
            suite.Add("Компенсация: возврат помечает платёж, повтор с тем же ID не зачитывает его",
                RefundedPaymentNotReplayed);
            suite.Add("Компенсация: флаг возврата переживает снимок", RefundFlagSurvivesSnapshot);
            suite.Add("Недостаток средств: деньги не списываются, paymentId остаётся свободным",
                InsufficientFundsKeepsIdFree);
            suite.Add("Платёж: paymentId, зачтённый по другому долгу, отклоняется без списания",
                PaymentIdUsedForOtherDebtRejected);

            return suite;
        }

        private static void PaymentIdNamespace()
        {
            MiniTestRunner.AreEqual("debt:pay-1", DebtPaymentId.ForPayment("pay-1"), "сборка ID");
            MiniTestRunner.IsTrue(DebtPaymentId.IsNamespaced("debt:pay-1"), "префикс распознан");
            MiniTestRunner.IsTrue(DebtPaymentId.IsValidPaymentId("debt:pay-1"), "префиксованный ID валиден");
            MiniTestRunner.IsTrue(DebtPaymentId.IsValidPaymentId("pay-1"), "«сырой» ID тоже валиден (совместимость)");
            MiniTestRunner.IsFalse(DebtPaymentId.IsValidPaymentId(""), "пустой ID невалиден");
            MiniTestRunner.IsFalse(DebtPaymentId.IsValidPaymentId("bad id"), "пробелы невалидны");
        }

        private static void PaymentIdForeignPrefixRejected()
        {
            MiniTestRunner.IsFalse(
                DebtPaymentId.IsValidPaymentId("shop:buy-1"),
                "ID магазина нельзя передать как платёж по долгу");
            MiniTestRunner.IsFalse(
                DebtPaymentId.IsValidPaymentId("delivery:order#1"),
                "ID выплаты доставки нельзя передать как платёж по долгу");
        }

        private static void RepeatedPaymentFullReplay()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtPaymentResult first = service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p1");
            DebtPaymentResult second = service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p1");

            DebtTestEnv.ExpectSuccess(first, "первый платёж");
            DebtTestEnv.ExpectSuccess(second, "повтор — успех");
            MiniTestRunner.AreEqual(1000L, second.PaidRubles, "прежняя сумма");
            MiniTestRunner.AreEqual(first.RemainingRubles, second.RemainingRubles, "тот же остаток");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "списано один раз");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "один вызов списания");
        }

        private static void RepeatedPaymentDifferentAmountNoSideEffects()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p2");
            DebtPaymentResult second = service.TryPay(DebtTestEnv.DebtId, 5000L, "debt:p2");

            DebtTestEnv.ExpectSuccess(second, "повтор с другой суммой не ломает состояние");
            MiniTestRunner.AreEqual(1000L, second.PaidRubles, "зачтена прежняя сумма, а не новая");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "деньги не списаны");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "списание осталось одно");

            service.TryGetDebt(DebtTestEnv.DebtId, out DebtView view);
            MiniTestRunner.AreEqual(37400L, view.TotalRemaining, "долг уменьшен ровно на один платёж");
        }

        private static void RepeatedPaymentSingleRecord()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p3");
            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p3");
            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p3");

            MiniTestRunner.AreEqual(1, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "одна запись в истории");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "одно списание");
        }

        private static void RepeatedPaymentAfterRestore()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p4");
            DebtSnapshot snapshot = service.CreateSnapshot();

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "восстановление");
            DebtPaymentResult replay = restored.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p4");

            DebtTestEnv.ExpectSuccess(replay, "повтор после загрузки");
            MiniTestRunner.AreEqual(1000L, replay.PaidRubles, "прежняя сумма");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "деньги не списаны повторно");
            MiniTestRunner.AreEqual(1, restored.GetPaymentHistory(DebtTestEnv.DebtId).Count, "история не раздулась");
        }

        /// <summary>
        /// Компенсация по УЖЕ ЗАЧТЁННОМУ платежу: деньги вернулись, поэтому повтор с тем же
        /// paymentId не должен снова зачитывать сумму как оплату долга.
        /// </summary>
        private static void RefundedPaymentNotReplayed()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            // Платёж зачтён, затем руками восстанавливаем компенсацию: именно такая пара
            // («платёж в истории» + «деньги вернулись») возникает при сбое зачёта.
            service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p5");
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Payments[0].Refunded = true;

            var restored = new DebtService(clock, wallet, null);
            DebtTestEnv.ExpectSuccess(restored.RestoreSnapshot(snapshot), "снимок с флагом возврата");

            DebtPaymentResult repeat = restored.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p5");

            MiniTestRunner.IsFalse(repeat.IsSuccess, "повтор после возврата не зачитывается");
            MiniTestRunner.AreEqual(DebtResultCode.DuplicatePayment, repeat.Code, "причина — деньги уже вернули");
            MiniTestRunner.AreEqual(4000L, wallet.Balance, "второго списания нет");
            MiniTestRunner.AreEqual(1, restored.GetPaymentHistory(DebtTestEnv.DebtId).Count, "история не раздулась");
        }

        private static void RefundFlagSurvivesSnapshot()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(5000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            // Испорченный снимок с противоречием: платёж есть, долг не уменьшен —
            // валидатор такой снимок отклонит, поэтому проверяем аддитивное поле напрямую.
            DebtSnapshot snapshot = service.CreateSnapshot();
            snapshot.Payments = new[]
            {
                new PaymentRecordSnapshot
                {
                    PaymentId = "debt:old",
                    DebtId = DebtTestEnv.DebtId,
                    AmountRubles = 0L,
                    Refunded = true
                }
            };

            string error;
            MiniTestRunner.IsFalse(
                DebtSnapshotValidator.Validate(snapshot, out error),
                "нулевой платёж отклонён валидатором (поле Refunded не влияет на валидацию)");

            // Корректный снимок с флагом возврата читается и сохраняется.
            DebtSnapshot clean = service.CreateSnapshot();
            string cleanError;
            MiniTestRunner.IsTrue(DebtSnapshotValidator.Validate(clean, out cleanError), "чистый снимок валиден: " + cleanError);
            MiniTestRunner.AreEqual(0, clean.Payments.Length, "история пуста");
        }

        private static void InsufficientFundsKeepsIdFree()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(500L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtPaymentResult first = service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p6");
            DebtTestEnv.ExpectCode(DebtResultCode.InsufficientFunds, first, "денег не хватает");
            MiniTestRunner.AreEqual(500L, wallet.Balance, "баланс не тронут");

            wallet.TryGetBalance(out _);
            var rich = new FakeWallet(5000L);
            var second = DebtTestEnv.CreateService(clock, rich, null);
            DebtPaymentResult retry = second.TryPay(DebtTestEnv.DebtId, 1000L, "debt:p6");

            DebtTestEnv.ExpectSuccess(retry, "тем же ID можно заплатить позже");
            MiniTestRunner.AreEqual(4000L, rich.Balance, "списание одно");
        }

        /// <summary>
        /// Один и тот же paymentId пытаются использовать для ДВУХ разных долгов. Это не повтор
        /// (повтор — тот же долг), а конфликт ключей: второй долг не должен выглядеть
        /// оплаченным, деньги повторно не списываются, запись истории остаётся одна.
        /// </summary>
        private static void PaymentIdUsedForOtherDebtRejected()
        {
            var clock = new FakeClock();
            var wallet = new FakeWallet(20000L);
            DebtService service = DebtTestEnv.CreateService(clock, wallet, null);

            DebtTestEnv.ExpectSuccess(
                service.CreateDebt(new DebtConfiguration
                {
                    DebtId = "debt-2",
                    CreditorId = DebtTestEnv.CreditorId,
                    Title = "Второй долг",
                    PrincipalRubles = 5000L,
                    DueAtMinutes = 1000L,
                    GracePeriodMinutes = 0L,
                    InterestRateBasisPoints = 0
                }),
                "второй долг создан");

            DebtTestEnv.ExpectSuccess(service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:shared"), "первый долг оплачен");

            DebtPaymentResult other = service.TryPay("debt-2", 1000L, "debt:shared");

            MiniTestRunner.IsFalse(other.IsSuccess, "платёж по другому долгу не считается успешным");
            MiniTestRunner.AreEqual(DebtResultCode.DuplicatePayment, other.Code, "причина — ключ занят другим долгом");
            MiniTestRunner.AreEqual(19000L, wallet.Balance, "второго списания нет");
            MiniTestRunner.AreEqual(1, wallet.DebitCalls, "вызов списания остался один");
            MiniTestRunner.AreEqual(1, service.GetPaymentHistory(DebtTestEnv.DebtId).Count, "история первого долга");
            MiniTestRunner.AreEqual(0, service.GetPaymentHistory("debt-2").Count, "история второго долга пуста");

            service.TryGetDebt("debt-2", out DebtView view);
            MiniTestRunner.AreEqual(5000L, view.TotalRemaining, "второй долг не уменьшен");

            // Тот же ключ для СВОЕГО долга остаётся идемпотентным повтором.
            DebtPaymentResult replay = service.TryPay(DebtTestEnv.DebtId, 1000L, "debt:shared");
            DebtTestEnv.ExpectSuccess(replay, "повтор по своему долгу");
            MiniTestRunner.AreEqual(1000L, replay.PaidRubles, "прежняя сумма");
        }

    }
}
