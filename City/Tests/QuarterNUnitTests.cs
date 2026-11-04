// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode). Нужна только внутри Unity.
// Сами тест-кейсы не ссылаются на NUnit, поэтому модуль свободен от сторонних пакетов.
// В Unity НЕ проверялось (проекта в репозитории нет) — см. City/README.md.

#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace LifeFromZero.City.Quarter.Tests
{
    /// <summary>Проводит все тест-кейсы модуля через NUnit Test Runner.</summary>
    public sealed class QuarterNUnitTests
    {
        [Test]
        public void RunAllQuarterTests()
        {
            TestSuite suite = QuarterTestSuite.CreateAll();
            var failures = new System.Collections.Generic.List<string>();

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
