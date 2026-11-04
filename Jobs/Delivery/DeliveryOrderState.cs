// Life From Zero — Delivery jobs module (ТЗ №014-Г.1)
// Namespace: LifeFromZero.Jobs.Delivery

namespace LifeFromZero.Jobs.Delivery
{
    /// <summary>
    /// Состояние курьерского заказа. Переходы описаны в <see cref="DeliveryOrder"/>.
    /// </summary>
    public enum DeliveryOrderState
    {
        /// <summary>Заказ доступен, но ещё не принят.</summary>
        Available = 0,

        /// <summary>Заказ принят игроком; груз ещё не получен.</summary>
        Accepted = 1,

        /// <summary>Груз получен; можно везти к точке доставки.</summary>
        PickedUp = 2,

        /// <summary>Заказ выполнен успешно, оплата начислена один раз.</summary>
        Completed = 3,

        /// <summary>Заказ провален (например, истёк срок). Оплата не начисляется.</summary>
        Failed = 4,

        /// <summary>Заказ отменён игроком (или системой до получения груза). Оплата не начисляется.</summary>
        Cancelled = 5,

        /// <summary>
        /// Заказа не существует: значение только для статуса-заглушки в результатах
        /// <see cref="DeliveryResultCode.OrderNotFound"/>. У реального заказа такого
        /// состояния не бывает, поэтому UI не должен рисовать «доступный заказ»
        /// по ответу «не найдено».
        /// </summary>
        None = 6
    }

    /// <summary>Код исхода операции над заказом. Тексты уведомлений сюда не входят.</summary>
    public enum DeliveryResultCode
    {
        /// <summary>Операция выполнена.</summary>
        Success = 0,

        /// <summary>Заказ с таким идентификатором не найден.</summary>
        OrderNotFound = 1,

        /// <summary>Действие невозможно в текущем состоянии заказа.</summary>
        InvalidState = 2,

        /// <summary>Игрок уже занят другим активным заказом.</summary>
        AlreadyHasActiveOrder = 3,

        /// <summary>Игрок находится не в той точке.</summary>
        WrongLocation = 4,

        /// <summary>Срок выполнения заказа истёк.</summary>
        DeadlineExpired = 5,

        /// <summary>Некорректные входные данные (пустой id, отрицательная оплата, плохой срок и т. п.).</summary>
        InvalidArgument = 6,

        /// <summary>Адаптер (время, кошелёк, местоположение) отказал или выбросил исключение.</summary>
        AdapterFailure = 7,

        /// <summary>Внутренняя ошибка модуля или несогласованное состояние.</summary>
        InternalError = 8
    }
}
