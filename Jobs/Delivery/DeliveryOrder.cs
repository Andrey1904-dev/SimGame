// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery
//
// Состояния и переходы (единственный источник правды — DeliveryService):
//   Available --Accept--> Accepted
//   Accepted  --PickUp(в точке получения, до срока)--> PickedUp
//   PickedUp  --Complete(в точке доставки, до срока)--> Completed (+ оплата один раз)
//   Available/Accepted/PickedUp --Cancel--> Cancelled
//   Accepted/PickedUp --(срок истёк)--> Failed (без выплаты)
//   Заказ без активного игрока не переходит в Failed сам по себе: просрочка
//   фиксируется в момент обращения (Complete/PickUp/Cancel/Refresh/Abandon).

using System;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Неизменяемая модель курьерского заказа: ID, название груза, точки, оплата и срок.
    /// Оплата хранится целыми рублями (long), срок — абсолютным игровым временем в минутах.
    /// </summary>
    public sealed class DeliveryOrder
    {
        /// <summary>Уникальный идентификатор заказа.</summary>
        public string Id { get; }

        /// <summary>Название груза (для UI/логов).</summary>
        public string CargoTitle { get; }

        /// <summary>Где получить груз.</summary>
        public DeliveryPoint PickupPoint { get; }

        /// <summary>Куда доставить груз.</summary>
        public DeliveryPoint DropOffPoint { get; }

        /// <summary>Оплата в целых рублях. Больше нуля.</summary>
        public long PaymentRubles { get; }

        /// <summary>Срок выполнения в игровых минутах (абсолютное время GameClock-эквивалента).</summary>
        public long DeadlineGameMinutes { get; }

        public DeliveryOrder(
            string id,
            string cargoTitle,
            DeliveryPoint pickupPoint,
            DeliveryPoint dropOffPoint,
            long paymentRubles,
            long deadlineGameMinutes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Идентификатор заказа не должен быть пустым.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(cargoTitle))
            {
                throw new ArgumentException("Название груза не должно быть пустым.", nameof(cargoTitle));
            }

            if (paymentRubles <= 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(paymentRubles), paymentRubles, "Оплата должна быть больше нуля.");
            }

            if (deadlineGameMinutes < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deadlineGameMinutes), deadlineGameMinutes, "Срок не может быть отрицательным.");
            }

            Id = id;
            CargoTitle = cargoTitle;
            PickupPoint = pickupPoint;
            DropOffPoint = dropOffPoint;
            PaymentRubles = paymentRubles;
            DeadlineGameMinutes = deadlineGameMinutes;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
