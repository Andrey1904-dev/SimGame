// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Core
//
// Единое пространство идентификаторов финансовых операций.
//
// ЗАЧЕМ. До ТЗ №021 у покупки и компенсации не было идентификатора: кошелёк не мог
// отличить повторный вызов от новой операции, а сохранённый журнал не мог сопоставить
// «висящий» возврат с конкретной покупкой. Идентификатор операции — единственное, что
// позволяет адаптеру кошелька ответить на вопрос «эти деньги уже списаны?» после сбоя.
//
// ФОРМАТ. Одна покупка порождает ДВЕ операции:
//   * списание  — "shop:" + purchaseId;
//   * возврат   — "shop-refund:" + purchaseId;
// Возврат всегда выводится из той же purchaseId, поэтому повторная компенсация
// не может получить новый идентификатор и «вернуть» деньги дважды.
//
// Идентификаторы разных модулей не пересекаются по префиксу:
//   "shop:" / "shop-refund:" — Economy, "delivery:" — выплаты Delivery, "debt:" — платежи Debts.
// (см. FINANCIAL_OPERATION_IDS.md в корне репозитория).

using System;

namespace LifeFromZero.Economy.Core
{
    /// <summary>Вид финансовой операции магазина, восстановленный по идентификатору.</summary>
    public enum FinancialOperationKind
    {
        /// <summary>Идентификатор не принадлежит магазину или не разбирается.</summary>
        Unknown = 0,

        /// <summary>Списание денег за покупку.</summary>
        Purchase = 1,

        /// <summary>Компенсация (возврат) по покупке.</summary>
        Refund = 2
    }

    /// <summary>
    /// Разбор и сборка идентификаторов финансовых операций магазина.
    /// Только чистые функции: ни состояния, ни времени, ни случайности.
    /// </summary>
    public static class FinancialOperationId
    {
        /// <summary>Префикс операции списания за покупку.</summary>
        public const string PurchasePrefix = "shop:";

        /// <summary>Префикс операции возврата по покупке.</summary>
        public const string RefundPrefix = "shop-refund:";

        /// <summary>
        /// Максимальная длина идентификатора покупки (без префикса). Ограничение защищает
        /// адаптер кошелька от значений, которые не влезают в его хранилище ключей.
        /// </summary>
        public const int MaxPurchaseIdLength = 64;

        /// <summary>Префиксы, зарезервированные другими модулями: их нельзя использовать внутри.</summary>
        private static readonly string[] ForeignPrefixes = { "delivery:", "debt:" };

        /// <summary>
        /// Проверяет идентификатор покупки, который задаёт вызывающая сторона.
        /// Требования: непустой, без пробелов, не длиннее <see cref="MaxPurchaseIdLength"/>
        /// и без ':' в начале (двоеточие — разделитель пространства имён модуля).
        /// </summary>
        public static bool IsValidPurchaseId(string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                return false;
            }

            if (purchaseId.Length > MaxPurchaseIdLength)
            {
                return false;
            }

            if (purchaseId[0] == ':')
            {
                return false;
            }

            for (int i = 0; i < purchaseId.Length; i++)
            {
                char c = purchaseId[i];

                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// true, если идентификатор уже занят другим модулем ("delivery:", "debt:").
        /// Нужно, чтобы один журнал кошелька мог безопасно обслуживать все системы.
        /// </summary>
        public static bool IsForeignOperationId(string operationId)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                return false;
            }

            for (int i = 0; i < ForeignPrefixes.Length; i++)
            {
                if (operationId.StartsWith(ForeignPrefixes[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Идентификатор списания за покупку: "shop:" + purchaseId.</summary>
        /// <exception cref="ArgumentException">Если purchaseId некорректен или чужой.</exception>
        public static string ForPurchase(string purchaseId)
        {
            EnsureUsablePurchaseId(purchaseId);
            return PurchasePrefix + purchaseId;
        }

        /// <summary>Идентификатор возврата по покупке: "shop-refund:" + purchaseId.</summary>
        /// <exception cref="ArgumentException">Если purchaseId некорректен или чужой.</exception>
        public static string ForRefund(string purchaseId)
        {
            EnsureUsablePurchaseId(purchaseId);
            return RefundPrefix + purchaseId;
        }

        /// <summary>
        /// Разбирает идентификатор операции магазина.
        /// </summary>
        /// <param name="operationId">Идентификатор.</param>
        /// <param name="kind">Вид операции.</param>
        /// <returns><c>true</c>, если идентификатор принадлежит магазину и разобран.</returns>
        public static bool TryClassify(string operationId, out FinancialOperationKind kind)
        {
            kind = FinancialOperationKind.Unknown;

            if (string.IsNullOrEmpty(operationId))
            {
                return false;
            }

            // Сначала возврат: его префикс длиннее и начинается так же, как префикс покупки
            // после "shop-", поэтому порядок проверок важен только для читаемости.
            if (operationId.StartsWith(RefundPrefix, StringComparison.Ordinal))
            {
                if (!IsValidPurchaseId(operationId.Substring(RefundPrefix.Length)))
                {
                    return false;
                }

                kind = FinancialOperationKind.Refund;
                return true;
            }

            if (operationId.StartsWith(PurchasePrefix, StringComparison.Ordinal))
            {
                if (!IsValidPurchaseId(operationId.Substring(PurchasePrefix.Length)))
                {
                    return false;
                }

                kind = FinancialOperationKind.Purchase;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Возвращает идентификатор покупки, вшитый в идентификатор операции магазина.
        /// Для чужих идентификаторов возвращает <c>null</c>.
        /// </summary>
        public static string ExtractPurchaseId(string operationId)
        {
            if (string.IsNullOrEmpty(operationId))
            {
                return null;
            }

            if (operationId.StartsWith(RefundPrefix, StringComparison.Ordinal))
            {
                string refundPurchaseId = operationId.Substring(RefundPrefix.Length);
                return IsValidPurchaseId(refundPurchaseId) ? refundPurchaseId : null;
            }

            if (operationId.StartsWith(PurchasePrefix, StringComparison.Ordinal))
            {
                string purchasePurchaseId = operationId.Substring(PurchasePrefix.Length);
                return IsValidPurchaseId(purchasePurchaseId) ? purchasePurchaseId : null;
            }

            return null;
        }

        private static void EnsureUsablePurchaseId(string purchaseId)
        {
            if (!IsValidPurchaseId(purchaseId))
            {
                throw new ArgumentException(
                    "Некорректный идентификатор покупки: пустой, слишком длинный или содержит пробелы.",
                    nameof(purchaseId));
            }

            if (IsForeignOperationId(purchaseId))
            {
                throw new ArgumentException(
                    "Идентификатор покупки начинается с префикса другого модуля: " + purchaseId,
                    nameof(purchaseId));
            }
        }
    }
}
