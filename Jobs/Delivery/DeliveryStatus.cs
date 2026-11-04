// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery

using System;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Снимок состояния заказа на момент вызова. Возвращается сервисом вместе
    /// с кодом результата, чтобы UI не запрашивал состояние повторно.
    /// </summary>
    public readonly struct DeliveryStatus
    {
        /// <summary>Идентификатор заказа (null, если заказ не найден).</summary>
        public string OrderId { get; }

        /// <summary>Текущее состояние заказа.</summary>
        public DeliveryOrderState State { get; }

        /// <summary>Игровое время, когда заказ был принят (минуты); null — ещё не принимался.</summary>
        public double? AcceptedAtMinutes { get; }

        /// <summary>Игровое время, когда груз был получен; null — ещё не получен.</summary>
        public double? PickedUpAtMinutes { get; }

        /// <summary>Игровое время, когда заказ завершился (Completed/Failed/Cancelled); null — ещё активен.</summary>
        public double? ClosedAtMinutes { get; }

        /// <summary>Начислена ли выплата (ровно один раз при успешном завершении).</summary>
        public bool PayoutGranted { get; }

        public DeliveryStatus(
            string orderId,
            DeliveryOrderState state,
            double? acceptedAtMinutes,
            double? pickedUpAtMinutes,
            double? closedAtMinutes,
            bool payoutGranted)
        {
            OrderId = orderId;
            State = state;
            AcceptedAtMinutes = acceptedAtMinutes;
            PickedUpAtMinutes = pickedUpAtMinutes;
            ClosedAtMinutes = closedAtMinutes;
            PayoutGranted = payoutGranted;
        }

        public override string ToString()
        {
            return "DeliveryStatus(" + (OrderId ?? "<none>") + ", " + State + ", payout=" + PayoutGranted + ")";
        }
    }

    /// <summary>
    /// Результат операции над заказом. Текстов уведомлений здесь нет: UI сам
    /// подбирает текст по <see cref="Code"/>.
    /// </summary>
    public readonly struct DeliveryResult
    {
        /// <summary>Машиночитаемый код исхода.</summary>
        public DeliveryResultCode Code { get; }

        /// <summary>Состояние заказа после операции (или до неё, если операция отклонена).</summary>
        public DeliveryStatus Status { get; }

        /// <summary>Сумма, фактически начисленная этой операцией. 0, если выплаты не было.</summary>
        public long PaidRubles { get; }

        /// <summary>Успешна ли операция.</summary>
        public bool IsSuccess => Code == DeliveryResultCode.Success;

        /// <summary>Игровое время, зафиксированное при операции (NaN, если время недоступно).</summary>
        public double NowMinutes { get; }

        public DeliveryResult(
            DeliveryResultCode code,
            DeliveryStatus status,
            long paidRubles,
            double nowMinutes)
        {
            Code = code;
            Status = status;
            PaidRubles = paidRubles;
            NowMinutes = nowMinutes;
        }

        /// <summary>Создаёт результат с явным торговым статусом-заглушкой для несуществующего заказа.</summary>
        public static DeliveryResult Failure(DeliveryResultCode code, DeliveryStatus status, double nowMinutes)
        {
            return new DeliveryResult(code, status, 0L, nowMinutes);
        }

        public override string ToString()
        {
            return "DeliveryResult(" + Code + ", " + Status + ", paid=" + PaidRubles + ")";
        }
    }

    /// <summary>
    /// Ошибка интеграции: сервис вызван с нарушением контракта (null-адаптер, null-заказ).
    /// Это не бизнес-отказ — бизнес-отказы возвращаются кодом в <see cref="DeliveryResult"/>.
    /// </summary>
    public sealed class DeliveryServiceException : Exception
    {
        public DeliveryServiceException(string message) : base(message)
        {
        }

        public DeliveryServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
