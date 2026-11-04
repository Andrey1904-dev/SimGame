// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Tests
//
// Точка входа для запуска тестов ВНЕ Unity:
//   dotnet run --project Debts/Tests/Console.csproj
// Внутри Unity этот файл не используется (исключён из asmdef-сборки тестов Editor).

using System;

namespace LifeFromZero.Debts.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — Debts module tests (ТЗ №016)");
            Console.WriteLine();

            TestSuite suite = DebtTestSuite.Create();
            int failed = MiniTestRunner.RunAndReport(suite);

            return failed == 0 ? 0 : 1;
        }
    }
}
