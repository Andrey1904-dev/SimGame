// Life From Zero — Economy module (ТЗ №021)
// Namespace: LifeFromZero.Economy.Shop
//
// Полная проверка снимка журнала ДО применения. Возвращает false и описание первой
// ошибки; рабочее состояние сервиса при этом не меняется.

using System.Collections.Generic;
using LifeFromZero.Economy.Core;

namespace LifeFromZero.Economy.Shop
{
    /// <summary>Валидатор снимка журнала операций магазина.</summary>
    public static class ShopSnapshotValidator
    {
        /// <summary>Проверяет снимок. false — снимок повреждён, описание в <paramref name="error"/>.</summary>
        public static bool Validate(ShopSnapshot snapshot, out string error)
        {
            error = null;

            if (snapshot == null)
            {
                error = "Снимок магазина не передан (null).";
                return false;
            }

            if (snapshot.SchemaVersion != ShopSnapshot.CurrentSchemaVersion)
            {
                error = "Неподдерживаемая версия схемы снимка магазина: " + snapshot.SchemaVersion;
                return false;
            }

            if (snapshot.Operations == null)
            {
                error = "Массив операций в снимке равен null.";
                return false;
            }

            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            int pendingCount = 0;

            for (int i = 0; i < snapshot.Operations.Length; i++)
            {
                ShopOperationSnapshot operation = snapshot.Operations[i];

                if (operation == null)
                {
                    error = "Операция #" + i + ": пустая запись.";
                    return false;
                }

                if (!FinancialOperationId.IsValidPurchaseId(operation.PurchaseId))
                {
                    error = "Операция #" + i + ": некорректный PurchaseId.";
                    return false;
                }

                if (!ids.Add(operation.PurchaseId))
                {
                    error = "Дублирующийся PurchaseId: " + operation.PurchaseId;
                    return false;
                }

                if (operation.State < (int)ShopOperationState.Reserved
                    || operation.State > (int)ShopOperationState.Declined)
                {
                    error = "Операция " + operation.PurchaseId + ": неизвестное состояние " + operation.State;
                    return false;
                }

                ShopOperationState state = (ShopOperationState)operation.State;

                if (state == ShopOperationState.Declined)
                {
                    // Отказ фиксируется до списания: товара в нём нет, а сумма может быть
                    // посчитанной (недостаточно средств) или нулевой (плохой запрос).
                    if (operation.AmountRubles < 0L)
                    {
                        error = "Операция " + operation.PurchaseId + ": отказ с ненулевой суммой.";
                        return false;
                    }

                    if (operation.DeclinedCode < 0
                        || operation.DeclinedCode > (int)EconomyResultCode.OutcomeUnknown)
                    {
                        error = "Операция " + operation.PurchaseId + ": неизвестный код отказа " + operation.DeclinedCode;
                        return false;
                    }

                    continue;
                }

                if (string.IsNullOrEmpty(operation.ProductId))
                {
                    error = "Операция " + operation.PurchaseId + ": пустой ProductId.";
                    return false;
                }

                if (operation.RequestedQuantity <= 0 || operation.Units <= 0)
                {
                    error = "Операция " + operation.PurchaseId + ": некорректное количество.";
                    return false;
                }

                if (operation.AmountRubles < 0L)
                {
                    error = "Операция " + operation.PurchaseId + ": отрицательная сумма.";
                    return false;
                }

                // Пока деньги не списаны (Reserved), суммы может не быть; дальше — обязана.
                if (state != ShopOperationState.Reserved && operation.AmountRubles == 0L)
                {
                    error = "Операция " + operation.PurchaseId + ": нулевая сумма при списании.";
                    return false;
                }

                if (state == ShopOperationState.Debited
                    || state == ShopOperationState.RefundPending
                    || state == ShopOperationState.RefundFailed)
                {
                    pendingCount++;
                }
            }

            // Правило сервиса: одновременно «висит» не больше одной покупки.
            // Снимок с двумя незавершёнными операциями сервис создать не мог
            // (новая покупка не начинается, пока есть незавершённая), поэтому
            // он считается повреждённым, а не «исправляется» молча.
            if (pendingCount > 1)
            {
                error = "В снимке " + pendingCount + " незавершённых операций; допустима максимум одна.";
                return false;
            }

            return true;
        }
    }
}
