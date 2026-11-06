// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode). UI/Core не зависит от UnityEngine,
// поэтому тесты идут в EditMode и не требуют сцены.

#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace LifeFromZero.UI.Tests
{
    public sealed class UiNUnitTests
    {
        [Test]
        public void RunAllUiCoreTests()
        {
            UiTestSuite suite = UiTestSuiteFactory.Create();
            var failures = new List<string>();

            foreach (UiTestResult result in suite.RunAll())
            {
                if (!result.Passed)
                {
                    failures.Add(result.Name + " — " + result.Error);
                }
            }

            if (failures.Count > 0)
            {
                Assert.Fail(
                    "Провалено " + failures.Count + " из " + suite.Cases.Count + " тестов:" +
                    System.Environment.NewLine + string.Join(System.Environment.NewLine, failures));
            }
        }
    }
}
#endif
