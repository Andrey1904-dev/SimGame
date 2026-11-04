// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode).
// Если Test Framework не подключён, файл можно удалить: остальные тесты от NUnit не зависят.

#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace LifeFromZero.Jobs.Tests
{
    /// <summary>Прогоняет все тест-кейсы модуля доставки через NUnit Test Runner.</summary>
    public sealed class JobsNUnitTests
    {
        [Test]
        public void RunAllDeliveryTests()
        {
            TestSuite suite = DeliveryServiceTests.CreateSuite();
            var failures = new List<string>();

            foreach (TestRunResult result in suite.RunAll())
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
