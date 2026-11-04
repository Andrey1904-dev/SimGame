// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Минималистичный раннер тестов без NUnit/xUnit: в ТЗ запрещены сторонние пакеты,
// а Unity Test Framework подключается Codex на его стороне.
// Как запустить:
//   1) Unity: Window → General → Test Runner → EditMode, если Codex обернёт тесты в NUnit
//      (см. Economy/Tests/README.md — там же вариант с UniTest-обёрткой).
//   2) Вне Unity (dotnet/линковка): класс Program с Main вызывает MiniTestRunner.RunAll().
//
// Раннер не прячет ошибки: исключение в тесте считается провалом теста.

using System;
using System.Collections.Generic;

namespace LifeFromZero.City.Quarter.Tests
{
    /// <summary>Один тест-кейс: имя + тело.</summary>
    public sealed class TestCase
    {
        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public string Name { get; }

        public Action Body { get; }
    }

    /// <summary>Результат прогона одного теста.</summary>
    public sealed class TestRunResult
    {
        public TestRunResult(string name, bool passed, string error)
        {
            Name = name;
            Passed = passed;
            Error = error;
        }

        public string Name { get; }

        public bool Passed { get; }

        public string Error { get; }
    }

    /// <summary>Список тестов и прогон с человекочитаемым отчётом.</summary>
    public sealed class TestSuite
    {
        private readonly List<TestCase> _cases = new List<TestCase>();

        public IReadOnlyList<TestCase> Cases => _cases;

        public TestSuite Add(string name, Action body)
        {
            _cases.Add(new TestCase(name, body));
            return this;
        }

        /// <summary>Добавляет все кейсы другого набора (для объединения файлов тестов).</summary>
        public TestSuite AddRange(TestSuite other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            _cases.AddRange(other.Cases);
            return this;
        }

        public IReadOnlyList<TestRunResult> RunAll()
        {
            var results = new List<TestRunResult>(_cases.Count);

            foreach (TestCase testCase in _cases)
            {
                try
                {
                    testCase.Body();
                    results.Add(new TestRunResult(testCase.Name, true, null));
                }
                catch (Exception exception)
                {
                    results.Add(new TestRunResult(testCase.Name, false, exception.GetType().Name + ": " + exception.Message));
                }
            }

            return results;
        }
    }

    /// <summary>Утилиты проверок с понятными сообщениями.</summary>
    public static class MiniTestRunner
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Ожидалось true: " + message);
            }
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new InvalidOperationException("Ожидалось false: " + message);
            }
        }

        public static void AreEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + " (ожидалось: " + expected + ", фактически: " + actual + ")");
            }
        }

        public static void AreNotEqual<T>(T unexpected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
            {
                throw new InvalidOperationException(
                    message + " (не ожидалось значение: " + unexpected + ")");
            }
        }

        /// <summary>Прогоняет набор и печатает отчёт. Возвращает количество провалов.</summary>
        public static int RunAndReport(TestSuite suite)
        {
            if (suite == null)
            {
                throw new ArgumentNullException(nameof(suite));
            }

            IReadOnlyList<TestRunResult> results = suite.RunAll();
            int failed = 0;

            foreach (TestRunResult result in results)
            {
                if (result.Passed)
                {
                    Console.WriteLine("[PASS] " + result.Name);
                }
                else
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + result.Name + " — " + result.Error);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Всего: " + results.Count + ", провалено: " + failed);
            return failed;
        }
    }
}
