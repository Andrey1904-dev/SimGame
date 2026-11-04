// Life From Zero — Phone core (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Core

using System;

namespace LifeFromZero.Phone.Core
{
    /// <summary>
    /// Проверки строковых идентификаторов телефона в одном месте.
    /// Все ID в модуле — непустые строки без ведущих/замыкающих пробелов.
    /// </summary>
    public static class PhoneId
    {
        /// <summary>
        /// Корректен ли идентификатор: не null, не пустой и не из одних пробелов.
        /// </summary>
        public static bool IsValid(string id)
        {
            return !string.IsNullOrWhiteSpace(id);
        }

        /// <summary>
        /// Сравнивает идентификаторы без учёта культуры, с учётом регистра.
        /// </summary>
        public static bool AreSame(string left, string right)
        {
            return IsValid(left) && IsValid(right) && string.Equals(left, right, StringComparison.Ordinal);
        }
    }
}
