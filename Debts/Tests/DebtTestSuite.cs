// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Единый набор тестов модуля: создание/платежи, время/штрафы/стадии,
// финансовая безопасность и восстановление, сохранение/загрузка,
// финансовые контракты ТЗ №021 (идентификаторы, идемпотентность, компенсация).

namespace LifeFromZero.Debts.Tests
{
    public static class DebtTestSuite
    {
        public static TestSuite Create()
        {
            var suite = new TestSuite();
            suite.AddRange(DebtServiceTests.CreateSuite());
            suite.AddRange(DebtPenaltyTests.CreateSuite());
            suite.AddRange(DebtPaymentSafetyTests.CreateSuite());
            suite.AddRange(DebtPersistenceTests.CreateSuite());
            suite.AddRange(DebtFinancialContractTests.CreateSuite());
            return suite;
        }
    }
}
