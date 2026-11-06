// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests

namespace LifeFromZero.UI.Tests
{
    /// <summary>Собирает все наборы тестов UI-слоя.</summary>
    public static class UiTestSuiteFactory
    {
        public static UiTestSuite Create()
        {
            UiTestSuite suite = UiCoreTests.Create();
            // Регрессии по следам реального Play Mode (ТЗ №045).
            UiRegression045Tests.AddTo(suite);
            return suite;
        }
    }
}
