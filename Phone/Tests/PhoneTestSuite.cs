// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Сборка всех тестов модуля телефона.</summary>
    public static class PhoneTestSuite
    {
        public static TestSuite Create()
        {
            var suite = new TestSuite();

            PhoneAppRegistryTests.Register(suite);
            PhoneMessageTests.Register(suite);
            PhoneContactTests.Register(suite);
            PhoneNotificationTests.Register(suite);
            PhoneIntegrationTests.Register(suite);

            return suite;
        }
    }
}
