// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Tests
//
// NUnit-обёртка для Unity Test Framework (EditMode). Нужна только внутри Unity.
// Если Unity Test Framework ещё не подключён к проекту, Codex может удалить этот файл:
// тесты в Economy/Tests/ShopServiceTests.cs от него не зависят.
//
// Почему отдельный файл: сами тест-кейсы написаны обычными методами и не ссылаются
// на NUnit, поэтому модуль остаётся свободным от сторонних пакетов.

#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace LifeFromZero.Economy.Tests
{
    /// <summary>Проводит все тест-кейсы модуля через NUnit Test Runner.</summary>
    public sealed class EconomyNUnitTests
    {
        [Test]
        public void RunAllEconomyTests()
        {
            TestSuite suite = ShopServiceTests.CreateSuite();
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
