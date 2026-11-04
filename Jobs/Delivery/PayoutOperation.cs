// Life From Zero — Delivery jobs module (ТЗ №021)
// Namespace: LifeFromZero.Jobs.Delivery
//
// Идентификатор и проверка выплаты курьеру.
//
// БЫЛО (и чем это плохо): идентификатор выплаты склеивался прямо в Complete —
// "delivery:" + order.Id. Если заказ удаляли и регистрировали заново с тем же Id
// (или сохранение откатывали), новый заказ получал ТОТ ЖЕ идентификатор, и журнал
// кошелька справедливо отвечал «уже выплачено»: курьер не получал денег за новый заказ.
//
// СТАЛО: у заказа есть собственный PayOperationId, который выдаётся при регистрации
// и сохраняется в снимке. Он начинается с "delivery:" (общее пространство имён
// финансовых операций) и содержит последовательность сервиса, поэтому:
//   * повтор Complete по тому же заказу всегда использует ОДИН И ТОТ ЖЕ ID;
//   * перерегистрированный заказ получает НОВЫЙ ID и выплачивается отдельно.

using System;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>Сборка и проверка идентификаторов выплат доставки.</summary>
    public static class PayoutOperation
    {
        /// <summary>Префикс пространства имён выплат доставки.</summary>
        public const string Prefix = "delivery:";

        /// <summary>
        /// Максимальная длина идентификатора операции. Ограничение общее для всех модулей:
        /// адаптер кошелька использует ID как ключ журнала.
        /// </summary>
        public const int MaxOperationIdLength = 128;

        /// <summary>
        /// Собирает идентификатор выплаты из идентификатора заказа и номера операции.
        /// Номер операции делает идентификатор уникальным для повторно зарегистрированного
        /// заказа с тем же Id.
        /// </summary>
        public static string ForOrder(string orderId, long sequence)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("Идентификатор заказа не должен быть пустым.", nameof(orderId));
            }

            if (sequence < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Номер операции начинается с 1.");
            }

            return Prefix + orderId + "#" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>true, если идентификатор принадлежит пространству имён доставки.</summary>
        public static bool IsPayoutOperationId(string operationId)
        {
            return !string.IsNullOrEmpty(operationId)
                   && operationId.StartsWith(Prefix, StringComparison.Ordinal)
                   && operationId.Length > Prefix.Length
                   && operationId.Length <= MaxOperationIdLength;
        }

        /// <summary>
        /// Проверяет, что адаптер начисления действительно подтвердил выплату по этому
        /// идентификатору. Нужно, чтобы «вернул true» не принималось на веру, когда адаптер
        /// умеет отвечать по журналу.
        /// </summary>
        public static bool IsCreditConfirmed(IIdempotentPayoutAdapter adapter, string operationId)
        {
            if (adapter == null || string.IsNullOrEmpty(operationId))
            {
                return false;
            }

            bool credited = false;

            return SafeAdapter.Try(() => adapter.WasCredited(operationId, out credited), out bool answered)
                   && answered
                   && credited;
        }
    }
}
