// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Core
// Unity 6000.3 / C# 9. Без Unity API и сторонних пакетов.

namespace LifeFromZero.Debts.Core
{
    /// <summary>
    /// Целочисленная денежная арифметика в рублях (<see cref="long"/>).
    /// Все операции проверяют переполнение и отрицательные значения.
    /// Типы с плавающей точкой для денег запрещены условиями ТЗ.
    /// </summary>
    public static class DebtMath
    {
        /// <summary>Сложение неотрицательных сумм с защитой от переполнения.</summary>
        public static bool TryAdd(long a, long b, out long result)
        {
            result = 0L;
            if (a < 0L || b < 0L)
            {
                return false;
            }

            if (a > long.MaxValue - b)
            {
                return false;
            }

            result = a + b;
            return true;
        }

        /// <summary>Вычитание b из a при a &gt;= b &gt;= 0.</summary>
        public static bool TrySubtract(long a, long b, out long result)
        {
            result = 0L;
            if (a < 0L || b < 0L || b > a)
            {
                return false;
            }

            result = a - b;
            return true;
        }

        /// <summary>Умножение неотрицательных значений с защитой от переполнения.</summary>
        public static bool TryMultiply(long a, long b, out long result)
        {
            result = 0L;
            if (a < 0L || b < 0L)
            {
                return false;
            }

            if (a == 0L || b == 0L)
            {
                return true;
            }

            if (a > long.MaxValue / b)
            {
                return false;
            }

            result = a * b;
            return true;
        }

        /// <summary>
        /// Процент в базисных пунктах: 100 б.п. = 1%.
        /// result = value * basisPoints / 10000, округление вниз, без переполнения.
        /// </summary>
        public static bool TryPercent(long value, int basisPoints, out long result)
        {
            result = 0L;
            if (value < 0L || basisPoints < 0)
            {
                return false;
            }

            if (value == 0L || basisPoints == 0)
            {
                return true;
            }

            // Умножаем с проверкой; деление afterwards уже безопасно.
            if (value > long.MaxValue / basisPoints)
            {
                return false;
            }

            result = value * basisPoints / 10000L;
            return true;
        }
    }
}
