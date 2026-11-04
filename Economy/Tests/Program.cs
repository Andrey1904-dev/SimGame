// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Tests
//
// Точка входа для запуска тестов ВНЕ Unity:
//   dotnet run --project Economy/Tests/Console.csproj
//   (или собрать эти .cs файлы вместе с Economy/Core и Economy/Shop в любой C#-проект).
// Внутри Unity этот файл не нужен и может быть исключён из компиляции.

using System;

namespace LifeFromZero.Economy.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — Economy module tests (ТЗ №014-В.1)");
            Console.WriteLine();

            TestSuite suite = EconomyTestSuite.CreateAll();
            int failed = MiniTestRunner.RunAndReport(suite);

            return failed == 0 ? 0 : 1;
        }
    }
}
