// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode). Нужна только внутри Unity.
// Если Test Framework не подключён, файл можно удалить: сами тесты от NUnit не зависят.

#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace LifeFromZero.Debts.Tests
{
    /// <summary>Проводит все тест-кейсы модуля долгов через NUnit Test Runner.</summary>
    public sealed class DebtsNUnitTests
    {
        [Test]
        public void RunAllDebtsTests()
        {
            TestSuite suite = DebtTestSuite.Create();
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
