// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Tests
//
// Единый набор тестов модуля: базовые тесты магазина (ТЗ №014-В.1 / №015 / №017)
// и регрессии финансовых контрактов (ТЗ №021). Одна точка сборки нужна, чтобы
// вне Unity, в NUnit-обёртке и в консольном прогоне запускался ОДИН И ТОТ ЖЕ набор.

namespace LifeFromZero.Economy.Tests
{
    public static class EconomyTestSuite
    {
        public static TestSuite CreateAll()
        {
            TestSuite suite = ShopServiceTests.CreateSuite();
            suite.AddRange(ShopFinancialSafetyTests.CreateSuite());
            return suite;
        }
    }
}
