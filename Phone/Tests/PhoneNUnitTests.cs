// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode).
// Если Test Framework не подключён — файл можно удалить: остальные тесты от NUnit не зависят.

#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace LifeFromZero.Phone.Tests
{
    /// <summary>Прогоняет все тест-кейсы модуля телефона через NUnit Test Runner.</summary>
    public sealed class PhoneNUnitTests
    {
        [Test]
        public void RunAllPhoneTests()
        {
            TestSuite suite = PhoneTestSuite.Create();
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
