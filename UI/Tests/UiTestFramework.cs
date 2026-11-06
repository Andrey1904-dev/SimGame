// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests
//
// Минимальный раннер без сторонних пакетов: те же тесты прогоняются
// и вне Unity (dotnet / Roslyn), и через NUnit-обёртку в Unity Test Framework.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Tests
{
    public sealed class UiTestCase
    {
        public UiTestCase(string name, Action body)
        {
            Name = name;
            Body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public string Name { get; }

        public Action Body { get; }
    }

    public sealed class UiTestResult
    {
        public UiTestResult(string name, bool passed, string error)
        {
            Name = name;
            Passed = passed;
            Error = error;
        }

        public string Name { get; }

        public bool Passed { get; }

        public string Error { get; }
    }

    public sealed class UiTestSuite
    {
        private readonly List<UiTestCase> _cases = new List<UiTestCase>();

        public IReadOnlyList<UiTestCase> Cases => _cases;

        public UiTestSuite Add(string name, Action body)
        {
            _cases.Add(new UiTestCase(name, body));
            return this;
        }

        public IReadOnlyList<UiTestResult> RunAll()
        {
            var results = new List<UiTestResult>(_cases.Count);
            foreach (UiTestCase testCase in _cases)
            {
                try
                {
                    testCase.Body();
                    results.Add(new UiTestResult(testCase.Name, true, null));
                }
                catch (Exception exception)
                {
                    results.Add(new UiTestResult(testCase.Name, false, exception.GetType().Name + ": " + exception.Message));
                }
            }
            return results;
        }
    }

    public static class UiAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void False(bool condition, string message) => True(!condition, message);

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " (ожидалось «" + expected + "», получено «" + actual + "»)");
        }

        public static void NotEqual<T>(T unexpected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
                throw new InvalidOperationException(message + " (значение не должно быть «" + actual + "»)");
        }

        public static void Null(object value, string message)
        {
            if (value != null) throw new InvalidOperationException(message + " (значение не null)");
        }

        public static void NotNull(object value, string message)
        {
            if (value == null) throw new InvalidOperationException(message);
        }

        public static void Near(float expected, float actual, float tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException(message + " (ожидалось " + expected + " ± " + tolerance + ", получено " + actual + ")");
        }

        public static void Contains(string haystack, string needle, string message)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(message + " (в тексте нет «" + needle + "»: «" + haystack + "»)");
        }
    }

    public static class UiTestRunner
    {
        public static int RunAndReport(UiTestSuite suite)
        {
            IReadOnlyList<UiTestResult> results = suite.RunAll();
            int failed = 0;
            foreach (UiTestResult result in results)
            {
                if (result.Passed)
                {
                    Console.WriteLine("  PASS  " + result.Name);
                }
                else
                {
                    failed++;
                    Console.WriteLine("  FAIL  " + result.Name);
                    Console.WriteLine("        " + result.Error);
                }
            }
            Console.WriteLine();
            Console.WriteLine("Итого: " + (results.Count - failed) + "/" + results.Count + " PASS, " + failed + " FAIL");
            return failed;
        }
    }
}
