// Life From Zero — Delivery jobs module (ТЗ №021)
// Namespace: LifeFromZero.Jobs.Tests
//
// Единый набор тестов модуля: базовые сценарии заказов (ТЗ №014-Г.1 / №017)
// и регрессии финансовых контрактов (ТЗ №021).

namespace LifeFromZero.Jobs.Tests
{
    public static class DeliveryTestSuite
    {
        public static TestSuite CreateAll()
        {
            TestSuite suite = DeliveryServiceTests.CreateSuite();
            suite.AddRange(DeliveryFinancialSafetyTests.CreateSuite());
            return suite;
        }
    }
}
