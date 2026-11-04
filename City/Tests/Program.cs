// Life From Zero — City/Quarter module (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.Tests
//
// Точка входа для запуска тестов ВНЕ Unity:
//   dotnet run --project City/Tests/Console.csproj
// Внутри Unity этот файл не нужен и может быть исключён из компиляции.

using System;

namespace LifeFromZero.City.Quarter.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — City/Quarter module tests (ТЗ №023)");
            Console.WriteLine();

            TestSuite suite = QuarterTestSuite.CreateAll();
            int failed = MiniTestRunner.RunAndReport(suite);

            return failed == 0 ? 0 : 1;
        }
    }
}
