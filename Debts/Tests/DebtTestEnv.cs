// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Общие помощники для тестов: сборка сервиса с одним долгом и кредиторами.

using System;
using LifeFromZero.Debts.Core;
using LifeFromZero.Debts.Events;
using LifeFromZero.Debts.Payments;
using LifeFromZero.Debts.Penalties;
using LifeFromZero.Debts.Tests.Fakes;

namespace LifeFromZero.Debts.Tests
{
    public static class DebtTestEnv
    {
        public const string CreditorId = "creditor-1";
        public const string DebtId = "debt-1";

        /// <summary>Сервис с одним зарегистрированным кредитором и одним долгом.</summary>
        public static DebtService CreateService(
            FakeClock clock,
            IDebtWalletAdapter wallet,
            IDebtEventSink sink,
            long principal = 38400L,
            long due = 1000L,
            long grace = 0L,
            PenaltyPolicy penalties = null,
            int rateBasisPoints = 0)
        {
            var service = new DebtService(clock, wallet, sink);

            DebtResult creditorResult = service.RegisterCreditor(new Creditor(CreditorId, "Кредитор"));
            if (!creditorResult.IsSuccess)
            {
                throw new InvalidOperationException("Не удалось зарегистрировать кредитора: " + creditorResult);
            }

            var config = new DebtConfiguration
            {
                DebtId = DebtId,
                CreditorId = CreditorId,
                Title = "Долг",
                PrincipalRubles = principal,
                DueAtMinutes = due,
                GracePeriodMinutes = grace,
                InterestRateBasisPoints = rateBasisPoints,
                Penalties = penalties
            };

            DebtResult debtResult = service.CreateDebt(config);
            if (!debtResult.IsSuccess)
            {
                throw new InvalidOperationException("Не удалось создать долг: " + debtResult);
            }

            return service;
        }

        public static void ExpectSuccess(DebtResult result, string context)
        {
            MiniTestRunner.IsTrue(result.IsSuccess, context + ": ожидался успех, получено " + result);
        }

        public static void ExpectSuccess(DebtPaymentResult result, string context)
        {
            MiniTestRunner.IsTrue(result.IsSuccess, context + ": ожидался успех, получено " + result);
        }

        public static void ExpectCode(DebtResultCode expected, DebtResult result, string context)
        {
            MiniTestRunner.AreEqual(expected, result.Code, context);
        }

        public static void ExpectCode(DebtResultCode expected, DebtPaymentResult result, string context)
        {
            MiniTestRunner.AreEqual(expected, result.Code, context);
        }
    }
}
