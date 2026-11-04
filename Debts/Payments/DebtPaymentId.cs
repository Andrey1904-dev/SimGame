// Life From Zero — Debts module (ТЗ №021)
// Namespace: LifeFromZero.Debts.Payments
//
// Пространство идентификаторов платежей по долгам и проверка финансовых контрактов.
//
// ЗАЧЕМ. paymentId попадает в журнал кошелька вместе с идентификаторами других систем.
// Если магазин, доставка и долги пользуются общим журналом, их идентификаторы не должны
// совпадать. Префикс "debt:" делает это явным; сервис принимает и «сырые» ID
// (совместимость), но предпочитает префиксованные.

using System;

namespace LifeFromZero.Debts.Payments
{
    /// <summary>Сборка и проверка идентификаторов платежей по долгам.</summary>
    public static class DebtPaymentId
    {
        /// <summary>Префикс пространства имён платежей по долгам.</summary>
        public const string Prefix = "debt:";

        /// <summary>Максимальная длина идентификатора платежа (без учёта префикса).</summary>
        public const int MaxPaymentIdLength = 64;

        /// <summary>Собирает идентификатор платежа: "debt:" + ключ операции.</summary>
        public static string ForPayment(string key)
        {
            if (!IsValidKey(key))
            {
                throw new ArgumentException(
                    "Некорректный ключ платежа: пустой, слишком длинный или содержит пробелы.",
                    nameof(key));
            }

            return Prefix + key;
        }

        /// <summary>
        /// Проверяет идентификатор платежа, который задаёт вызывающая сторона.
        /// Допускается как префиксованный ("debt:pay-1"), так и «сырой» ("pay-1").
        /// </summary>
        public static bool IsValidPaymentId(string paymentId)
        {
            if (string.IsNullOrWhiteSpace(paymentId) || paymentId.Length > Prefix.Length + MaxPaymentIdLength)
            {
                return false;
            }

            if (paymentId.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return IsValidKey(paymentId.Substring(Prefix.Length));
            }

            return IsValidKey(paymentId) && !paymentId.StartsWith("shop:", StringComparison.Ordinal)
                   && !paymentId.StartsWith("shop-refund:", StringComparison.Ordinal)
                   && !paymentId.StartsWith("delivery:", StringComparison.Ordinal);
        }

        /// <summary>true, если идентификатор уже отделён префиксом долгов.</summary>
        public static bool IsNamespaced(string paymentId)
        {
            return !string.IsNullOrEmpty(paymentId) && paymentId.StartsWith(Prefix, StringComparison.Ordinal);
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > MaxPaymentIdLength || key[0] == ':')
            {
                return false;
            }

            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];

                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
