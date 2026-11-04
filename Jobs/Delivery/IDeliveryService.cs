// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery

using System.Collections.Generic;

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Сервис курьерских заказов. Заменяет отсутствующее в проекте «рабочее место курьера»:
    /// хранит заказы в памяти, следит за состояниями и вызывает адаптеры времени/денег/локации.
    /// Singleton-ом не является: экземпляр создаётся там, где нужен.
    /// </summary>
    /// <remarks>
    /// Правила (ТЗ №014-Г.1):
    /// <list type="bullet">
    /// <item>Только один активный заказ у игрока одновременно.</item>
    /// <item>Груз можно получить только после принятия заказа и только в точке получения.</item>
    /// <item>Завершение — только после получения груза и только в точке доставки.</item>
    /// <item>Деньги начисляются ровно один раз и только при успешном завершении.</item>
    /// <item>Просроченный заказ проваливается без выплаты.</item>
    /// <item>Повторные завершение/получение/выплата и неверная последовательность отклоняются кодом.</item>
    /// </list>
    /// </remarks>
    public interface IDeliveryService
    {
        /// <summary>Добавляет заказ в общий список доступных. null и дубликаты отклоняются.</summary>
        DeliveryResult RegisterOrder(DeliveryOrder order);

        /// <summary>
        /// Принимает доступный заказ. Если есть незакрытый активный заказ — отказ.
        /// Местоположение не требуется: принимать заказ можно откуда угодно.
        /// </summary>
        DeliveryResult Accept(string orderId, IGameClockAdapter clock);

        /// <summary>Получает груз: только принятый заказ и только в точке получения и в срок.</summary>
        DeliveryResult PickUp(string orderId, IGameClockAdapter clock, ILocationProvider location);

        /// <summary>Завершает заказ: только после получения груза, в точке доставки и в срок. Начисляет оплату один раз.</summary>
        DeliveryResult Complete(string orderId, IGameClockAdapter clock, ILocationProvider location, IPayoutAdapter payout);

        /// <summary>Отменяет заказ: доступный, принятый или полученный. После груза отмена запрещена.</summary>
        DeliveryResult Cancel(string orderId, IGameClockAdapter clock);

        /// <summary>
        /// Обновляет состояние активного заказа по таймеру: если срок истёк, заказ переходит в Failed.
        /// Ничего не начисляет и не требует адаптера локации.
        /// </summary>
        DeliveryResult Refresh(string orderId, IGameClockAdapter clock);

        /// <summary>
        /// Отказывается от активного заказа (игрок бросил работу). Возможен только до получения груза,
        /// чтобы груз не «пропадал». После получения используйте <see cref="Complete"/> или дождитесь просрочки.
        /// </summary>
        DeliveryResult Abandon(string orderId, IGameClockAdapter clock);

        /// <summary>Текущее состояние заказа. Для несуществующего заказа возвращает OrderNotFound.</summary>
        DeliveryResult GetStatus(string orderId);

        /// <summary>Идентификатор активного заказа (Accepted/PickedUp) или null.</summary>
        string ActiveOrderId { get; }

        /// <summary>Все известные заказы (доступные к принятию).</summary>
        IReadOnlyList<DeliveryOrder> Orders { get; }
    }
}
