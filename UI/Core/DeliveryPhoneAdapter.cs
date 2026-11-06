// Life From Zero — UI/Core/DeliveryPhoneAdapter.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Core
//
// МОСТ «ЗАКАЗЫ ДОСТАВКИ → ТЕЛЕФОН». Чистая логика, без UnityEngine.
//
// Зачем отдельный файл: у игры уже есть Jobs.Delivery (IDeliveryService) со своей
// машиной состояний (Available → Accepted → PickedUp → Completed, оплата один раз).
// Телефону нужны только данные и команды. Дублировать правила доставки в UI нельзя,
// поэтому здесь:
//   * DeliveryPhoneRows.Build(...)   — превращает данные сервиса в строки телефона;
//   * DeliveryPhoneCommands.VerbOf   — переводит «order.accept:id» в verb/orderId;
//   * DeliveryPhoneCommands.Describe — объясняет ОТКАЗ человеческим языком.
// Ни одна цифра и ни одно состояние не выдумываются: чего сервис не сообщил,
// того в телефоне нет.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Core
{
    /// <summary>Заказ доставки в виде, нужном телефону (без зависимости от модуля доставки).</summary>
    public readonly struct DeliveryPhoneOrder
    {
        public DeliveryPhoneOrder(
            string orderId,
            string cargoTitle,
            string dropOffPointId,
            string dropOffTitle,
            long? paymentRubles,
            OrderStage stage,
            MapPoint? dropOffPosition,
            string deadlineLabel)
        {
            OrderId = orderId ?? string.Empty;
            CargoTitle = cargoTitle ?? string.Empty;
            DropOffPointId = dropOffPointId ?? string.Empty;
            DropOffTitle = dropOffTitle ?? string.Empty;
            PaymentRubles = paymentRubles;
            Stage = stage;
            DropOffPosition = dropOffPosition;
            DeadlineLabel = deadlineLabel ?? string.Empty;
        }

        public string OrderId { get; }
        public string CargoTitle { get; }
        public string DropOffPointId { get; }
        public string DropOffTitle { get; }
        public long? PaymentRubles { get; }
        public OrderStage Stage { get; }
        public MapPoint? DropOffPosition { get; }
        public string DeadlineLabel { get; }
    }

    /// <summary>Сборка строк телефона из заказов доставки.</summary>
    public static class DeliveryPhoneRows
    {
        /// <summary>Заказ → карточка телефона. Координаты переносятся как есть (или их нет).</summary>
        public static OrderView ToView(DeliveryPhoneOrder order, string address = null)
        {
            var view = new OrderView
            {
                OrderId = order.OrderId,
                CargoTitle = order.CargoTitle,
                DropOffTitle = string.IsNullOrEmpty(order.DropOffTitle) ? "Точка доставки" : order.DropOffTitle,
                Address = address ?? string.Empty,
                PaymentRubles = order.PaymentRubles,
                Stage = order.Stage,
                DeadlineLabel = order.DeadlineLabel,
                HasDropOffCoordinates = order.DropOffPosition.HasValue,
                DropOffPosition = order.DropOffPosition,
            };
            return view;
        }

        /// <summary>
        /// Заполняет раздел «Заказы». Активный заказ — ровно один (правило модуля доставки),
        /// остальные попадают в «доступные». Если заказов нет — раздел помечается
        /// причиной, а не заполняется примером.
        /// </summary>
        public static void Fill(OrdersView target, IReadOnlyList<DeliveryPhoneOrder> orders,
            string unavailableReason = null)
        {
            if (target == null) return;

            target.Active = null;
            var available = new List<OrderView>(orders == null ? 0 : orders.Count);
            if (orders != null)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    DeliveryPhoneOrder order = orders[i];
                    if (order.Stage == OrderStage.Accepted || order.Stage == OrderStage.PickedUp)
                    {
                        if (target.Active == null) target.Active = ToView(order);
                        continue;
                    }
                    if (order.Stage == OrderStage.Available) available.Add(ToView(order));
                }
            }

            target.Available = available;
            target.UnavailableReason = unavailableReason ?? string.Empty;
        }

        /// <summary>
        /// Переводит действие телефона «order.&lt;verb&gt;:&lt;id&gt;» в пару verb/orderId.
        /// Возвращает false для не-заказных действий, чтобы вызывающий не гадал.
        /// </summary>
        public static bool TryParse(string action, out string verb, out string orderId)
        {
            verb = string.Empty;
            orderId = string.Empty;
            if (string.IsNullOrEmpty(action) || !action.StartsWith("order.", StringComparison.Ordinal)) return false;

            int colon = action.IndexOf(':');
            string head = colon > 0 ? action.Substring(0, colon) : action;
            verb = head.Substring("order.".Length);
            orderId = colon > 0 && colon + 1 < action.Length ? action.Substring(colon + 1) : string.Empty;
            return verb.Length > 0;
        }
    }

    /// <summary>Человеческие тексты для отказов сервиса доставки.</summary>
    public static class DeliveryPhoneCommands
    {
        public const string Accept = "accept";
        public const string PickUp = "pickup";
        public const string Complete = "complete";
        public const string Cancel = "cancel";
        public const string Abandon = "abandon";

        /// <summary>
        /// Объяснение отказа по коду сервиса. Коды намеренно принимаются строками:
        /// UI не должен зависеть от перечисления модуля доставки (иначе любое
        /// переименование в Jobs.Delivery ломает интерфейс), а неизвестный код
        /// всё равно доводится до игрока, а не глотается.
        /// </summary>
        public static string Describe(string failureCode, string verb)
        {
            switch (failureCode)
            {
                case "OrderNotFound":
                    return "Заказ не найден — возможно, он уже закрыт.";
                case "InvalidState":
                    return verb == Complete
                        ? "Сначала заберите груз в точке получения."
                        : "Так с этим заказом сейчас поступить нельзя.";
                case "AlreadyHasActiveOrder":
                    return "Сначала закройте текущий заказ.";
                case "WrongLocation":
                    return verb == PickUp
                        ? "Груз выдают только в точке получения."
                        : "Вы не в точке доставки.";
                case "DeadlineExpired":
                    return "Срок заказа истёк.";
                case "InvalidArgument":
                    return "Заказ передан некорректно.";
                case "AdapterFailure":
                    return "Игровое время или кошелёк недоступны — попробуйте позже.";
                default:
                    return string.IsNullOrEmpty(failureCode)
                        ? "Не удалось выполнить действие с заказом."
                        : "Не удалось выполнить действие: " + failureCode;
            }
        }

        /// <summary>Заголовок кнопки для шага заказа.</summary>
        public static string VerbLabel(string verb)
        {
            switch (verb)
            {
                case Accept: return "Принять";
                case PickUp: return "Забрать";
                case Complete: return "Доставить";
                case Cancel: return "Отменить";
                case Abandon: return "Отказаться";
                default: return verb ?? string.Empty;
            }
        }
    }
}

namespace LifeFromZero.UI.Core
{
    /// <summary>
    /// Плитки рабочего стола телефона. Единственное место, где решается, какие
    /// приложения «включены»: включено только то, что реально умеет работать,
    /// остальное показано неактивным (а не спрятано молча).
    /// </summary>
    public static class PhoneAppCatalog
    {
        public static IReadOnlyList<PhoneAppTile> Build(bool ordersAvailable) => new[]
        {
            new PhoneAppTile(PhoneAppIds.Messages, "Сообщения", 0, true, "messages"),
            new PhoneAppTile(PhoneAppIds.Calls, "Звонки", 0, true, "calls"),
            new PhoneAppTile(PhoneAppIds.Contacts, "Контакты", 0, true, "contacts"),
            new PhoneAppTile(PhoneAppIds.Bank, "Банк", 0, true, "bank"),
            new PhoneAppTile(PhoneAppIds.Orders, "Заказы", 0, ordersAvailable, "orders"),
            new PhoneAppTile(PhoneAppIds.Tasks, "Задания", 0, true, "tasks"),
            new PhoneAppTile(PhoneAppIds.Ads, "Объявления", 0, false, "ads"),
            new PhoneAppTile(PhoneAppIds.Education, "Учёба", 0, false, "education"),
            new PhoneAppTile(PhoneAppIds.Gallery, "Галерея", 0, false, "gallery"),
            new PhoneAppTile(PhoneAppIds.Settings, "Настройки", 0, true, "settings"),
        };
    }
}
