// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery

using System;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Безопасный вызов адаптеров: исключение адаптера превращается в «отказ»,
    /// а не долетает до UI. Модуль при этом возвращает код
    /// <see cref="DeliveryResultCode.AdapterFailure"/>.
    /// </summary>
    public static class SafeAdapter
    {
        /// <summary>Выполняет действие. <c>true</c>, если исключения не было.</summary>
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
                // Диагностику ведёт адаптер: он знает свой контекст и умеет логировать.
                return false;
            }
        }

        /// <summary>Вызывает функцию, возвращающую признак успеха.</summary>
        /// <param name="func">Вызываемая функция.</param>
        /// <param name="result">Результат функции или <c>false</c>, если было исключение.</param>
        /// <returns><c>true</c>, если функция вызвана без исключения.</returns>
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
