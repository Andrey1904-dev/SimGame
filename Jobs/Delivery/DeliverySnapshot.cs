// Life From Zero — Delivery jobs module (ТЗ №017: сохранение состояния заказов)
// Namespace: LifeFromZero.Jobs.Delivery
//
// Модуль по-прежнему не пишет на диск и не сериализует сам: он принимает уже
// разобранный снимок, проверяет его целиком и только потом заменяет состояние.
// Формат файла (JsonUtility / System.Text.Json / бинарный) выбирает Codex.
//
// ЗАЧЕМ ЭТО НУЖНО. Без снимка состояния заказов сохранение игры теряло флаг
// PayoutGranted: после загрузки выполненный заказ снова становился Available,
// его можно было принять повторно и получить оплату второй раз. Модуль долгов
// такую защиту уже имел, модуль доставки — нет.

using System;
using System.Collections.Generic;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Снимок одного заказа: неизменяемое описание (DeliveryOrder) плюс состояние
    /// жизненного цикла (state, метки времени, флаг выплаты).
    /// Плоские поля без вложенных коллекций — совместимо с Unity JsonUtility.
    /// </summary>
    [Serializable]
    public sealed class DeliveryOrderSnapshot
    {
        // --- Описание заказа (DeliveryOrder) ---
        public string Id;
        public string CargoTitle;
        public string PickupPointId;
        public string PickupPointName;
        public string DropOffPointId;
        public string DropOffPointName;
        public long PaymentRubles;
        public long DeadlineGameMinutes;

        // --- Состояние жизненного цикла ---
        /// <summary>DeliveryOrderState как int.</summary>
        public int State;

        /// <summary>Была ли метка принятия (double? в DeliveryStatus сериализовать нельзя).</summary>
        public bool HasAcceptedAt;
        public double AcceptedAtMinutes;

        public bool HasPickedUpAt;
        public double PickedUpAtMinutes;

        public bool HasClosedAt;
        public double ClosedAtMinutes;

        /// <summary>Оплата начислена (ровно один раз при успешном завершении).</summary>
        public bool PayoutGranted;
    }

    /// <summary>Полный снимок состояния модуля доставки.</summary>
    [Serializable]
    public sealed class DeliverySnapshot
    {
        /// <summary>Текущая версия схемы снимка.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        /// <summary>Заказы в порядке регистрации.</summary>
        public DeliveryOrderSnapshot[] Orders = new DeliveryOrderSnapshot[0];
    }

    /// <summary>
    /// Полная проверка снимка ДО применения: ссылки, уникальность ID, диапазоны,
    /// согласованность состояния с метками времени и с флагом выплаты.
    /// </summary>
    public static class DeliverySnapshotValidator
    {
        /// <summary>Проверяет снимок. false — снимок повреждён, описание в <paramref name="error"/>.</summary>
        public static bool Validate(DeliverySnapshot snapshot, out string error)
        {
            error = null;

            if (snapshot == null)
            {
                error = "Снимок доставки не передан (null).";
                return false;
            }

            if (snapshot.SchemaVersion != DeliverySnapshot.CurrentSchemaVersion)
            {
                error = "Неподдерживаемая версия схемы снимка доставки: " + snapshot.SchemaVersion;
                return false;
            }

            if (snapshot.Orders == null)
            {
                error = "Массив заказов в снимке равен null.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            int activeCount = 0;

            for (int i = 0; i < snapshot.Orders.Length; i++)
            {
                DeliveryOrderSnapshot order = snapshot.Orders[i];

                if (order == null || string.IsNullOrWhiteSpace(order.Id))
                {
                    error = "Заказ #" + i + ": пустой идентификатор.";
                    return false;
                }

                if (!ids.Add(order.Id))
                {
                    error = "Дублирующийся идентификатор заказа: " + order.Id;
                    return false;
                }

                if (!ValidateDescription(order, out error))
                {
                    return false;
                }

                if (!ValidateLifecycle(order, out error))
                {
                    return false;
                }

                if (IsActiveState(order.State))
                {
                    activeCount++;
                }
            }

            // Правило модуля: у игрока не больше одного активного заказа.
            // Снимок с двумя активными заказами не мог быть создан сервисом,
            // поэтому он считается повреждённым, а не «исправляется» молча.
            if (activeCount > 1)
            {
                error = "В снимке " + activeCount + " активных заказов; допустим максимум один.";
                return false;
            }

            return true;
        }

        private static bool ValidateDescription(DeliveryOrderSnapshot order, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(order.CargoTitle))
            {
                error = "Заказ " + order.Id + ": пустое название груза.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(order.PickupPointId)
                || string.IsNullOrWhiteSpace(order.DropOffPointId))
            {
                error = "Заказ " + order.Id + ": пустая точка получения или доставки.";
                return false;
            }

            if (order.PaymentRubles <= 0L)
            {
                error = "Заказ " + order.Id + ": оплата должна быть больше нуля.";
                return false;
            }

            if (order.DeadlineGameMinutes < 0L)
            {
                error = "Заказ " + order.Id + ": отрицательный срок.";
                return false;
            }

            return true;
        }

        private static bool ValidateLifecycle(DeliveryOrderSnapshot order, out string error)
        {
            error = null;

            if (order.State < (int)DeliveryOrderState.Available
                || order.State > (int)DeliveryOrderState.Cancelled)
            {
                error = "Заказ " + order.Id + ": неизвестное состояние " + order.State;
                return false;
            }

            if (!ValidateStamp(order.HasAcceptedAt, order.AcceptedAtMinutes, "принятия", order.Id, out error)
                || !ValidateStamp(order.HasPickedUpAt, order.PickedUpAtMinutes, "получения груза", order.Id, out error)
                || !ValidateStamp(order.HasClosedAt, order.ClosedAtMinutes, "закрытия", order.Id, out error))
            {
                return false;
            }

            bool completed = order.State == (int)DeliveryOrderState.Completed;
            bool terminal = completed
                || order.State == (int)DeliveryOrderState.Failed
                || order.State == (int)DeliveryOrderState.Cancelled;

            // Completed выставляется только после успешного начисления,
            // поэтому «завершён без выплаты» — противоречивый снимок.
            if (order.PayoutGranted != completed)
            {
                error = "Заказ " + order.Id + ": флаг выплаты не согласован с состоянием.";
                return false;
            }

            if (terminal != order.HasClosedAt)
            {
                error = "Заказ " + order.Id + ": метка закрытия не согласована с состоянием.";
                return false;
            }

            // PickedUp/Completed не могли наступить без принятия и получения груза.
            bool pickedUpOrLater = order.State == (int)DeliveryOrderState.PickedUp || completed;
            if (pickedUpOrLater && (!order.HasAcceptedAt || !order.HasPickedUpAt))
            {
                error = "Заказ " + order.Id + ": нет меток принятия/получения при состоянии " + order.State;
                return false;
            }

            if (order.State == (int)DeliveryOrderState.Accepted && order.HasPickedUpAt)
            {
                error = "Заказ " + order.Id + ": метка получения груза при состоянии Accepted.";
                return false;
            }

            return true;
        }

        private static bool ValidateStamp(
            bool has, double value, string name, string orderId, out string error)
        {
            error = null;

            if (!has)
            {
                return true;
            }

            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                error = "Заказ " + orderId + ": некорректная метка времени " + name + ".";
                return false;
            }

            return true;
        }

        private static bool IsActiveState(int state)
        {
            return state == (int)DeliveryOrderState.Accepted
                   || state == (int)DeliveryOrderState.PickedUp;
        }
    }
}
