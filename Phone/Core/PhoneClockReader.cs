// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core

using System;

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Чтение игрового времени через адаптер с защитой от исключений и мусорных значений.
    /// </summary>
    public static class PhoneClockReader
    {
        /// <summary>
        /// Возвращает время или <see cref="GameMinutes.Unknown"/>, если адаптер не передан,
        /// упал, вернул NaN/бесконечность или отрицательное значение.
        /// </summary>
        public static GameMinutes Read(IPhoneClock clock)
        {
            if (clock == null)
            {
                return GameMinutes.Unknown;
            }

            double value;
            try
            {
                value = clock.NowMinutes;
            }
            catch (Exception)
            {
                // Внешняя система времени не должна ронять телефон.
                return GameMinutes.Unknown;
            }

            GameMinutes parsed;
            return GameMinutes.TryFromMinutes(value, out parsed) ? parsed : GameMinutes.Unknown;
        }
    }
}
