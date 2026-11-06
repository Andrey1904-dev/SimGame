// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests

namespace LifeFromZero.UI.Tests
{
    /// <summary>Собирает все наборы тестов UI-слоя.</summary>
    public static class UiTestSuiteFactory
    {
        public static UiTestSuite Create() => UiCoreTests.Create();
    }
}
