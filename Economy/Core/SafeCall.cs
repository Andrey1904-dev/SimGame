// Life From Zero — Economy module (ТЗ №014-В.1)
// Namespace: LifeFromZero.Economy.Core

using System;

namespace LifeFromZero.Economy.Core
{
    /// <summary>
    /// Безопасный вызов адаптеров. Нужен, чтобы отличать «адаптер вернул false»
    /// (штатный бизнес-отказ) от «адаптер упал» (внутренняя ошибка): во втором случае
    /// магазин обязан выполнить компенсацию и вернуть код, а не выбросить исключение в UI.
    /// </summary>
    public static class SafeCall
    {
        /// <summary>Выполняет действие. <c>true</c>, если исключений не было.</summary>
        public static bool Try(Action action)
        {
            if (action == null)
            {
                return false;
            }

            try
            {
                action();
                return true;
            }
            catch (Exception)
            {
                // Исключение намеренно проглатывается: диагностика — задача адаптера
                // (он знает свой контекст и умеет логировать). Магазин сообщает кодом.
                return false;
            }
        }

        /// <summary>
        /// Вызывает функцию, возвращающую признак успеха.
        /// </summary>
        /// <param name="func">Вызываемая функция.</param>
        /// <param name="result">Значение функции или <c>false</c>, если она упала.</param>
        /// <returns><c>true</c>, если функция была вызвана и не выбросила исключение.</returns>
        public static bool Try(Func<bool> func, out bool result)
        {
            result = false;
            if (func == null)
            {
                return false;
            }

            try
            {
                result = func();
                return true;
            }
            catch (Exception)
            {
                result = false;
                return false;
            }
        }
    }
}
