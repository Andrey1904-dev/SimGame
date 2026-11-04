// Life From Zero — Phone tests (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Tests
//
// Запуск вне Unity:
//   cd Phone/Tests && dotnet run --project Console.csproj

using System;

namespace LifeFromZero.Phone.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Life From Zero — Phone core tests (ТЗ №014-Т.1)");
            Console.WriteLine();

            TestSuite suite = PhoneTestSuite.Create();
            int failed = MiniTestRunner.RunAndReport(suite);

            return failed == 0 ? 0 : 1;
        }
    }
}
