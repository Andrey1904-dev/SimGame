// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Tests
//
// Точка входа для запуска тестов ВНЕ Unity:
//   dotnet run --project Jobs/Tests/Console.csproj
// Внутри Unity этот файл не нужен.

using System;

namespace LifeFromZero.Jobs.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — Delivery jobs tests (ТЗ №014-Г.1)");
            Console.WriteLine();

            TestSuite suite = DeliveryTestSuite.CreateAll();
            int failed = MiniTestRunner.RunAndReport(suite);

            return failed == 0 ? 0 : 1;
        }
    }
}
