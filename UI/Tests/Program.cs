// Life From Zero — UI tests (ТЗ №042)
// Namespace: LifeFromZero.UI.Tests
//
// Запуск вне Unity:
//   dotnet run --project UI/Tests/Console.csproj

using System;

namespace LifeFromZero.UI.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — UI core tests (ТЗ №042)");
            Console.WriteLine();
            UiTestSuite suite = UiTestSuiteFactory.Create();
            int failed = UiTestRunner.RunAndReport(suite);
            return failed == 0 ? 0 : 1;
        }
    }
}
