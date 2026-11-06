// Life From Zero — UI/Runtime/DeliveryServiceBridge.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// ПОДКЛЮЧЕНИЕ ТЕЛЕФОНА К СУЩЕСТВУЮЩЕЙ СИСТЕМЕ ДОСТАВКИ (Jobs.Delivery).
//
// Что делает: реализует IPhoneDataSource и IDeliveryUiBridge поверх настоящего
// IDeliveryService — телефон получает список заказов, координаты точки доставки
// и отправляет команды (принять / забрать / доставить / отказаться).
//
// Чего НЕ делает (и почему):
//   * не создаёт DeliveryService сам — владелец сервиса в сцене назначается в
//     инспекторе. UI не имеет права заводить вторую доставку;
//   * не реализует IGameClockAdapter/IPayoutAdapter/ILocationProvider — это части
//     игры (время, кошелёк, местоположение). Если они не назначены, команда
//     вернёт понятную причину, а не «успех»;
//   * не считает деньги, сроки и рейтинг. Кошелёк и банк в телефоне заполняются
//     только если передан источник кошелька/снимка.

using System;
using System.Collections.Generic;
using UnityEngine;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.UI.Core;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Мост: телефон и карта получают данные из настоящего DeliveryService.</summary>
    [DefaultExecutionOrder(-45)]
    public sealed class DeliveryServiceBridge : MonoBehaviour, IPhoneDataSource, IDeliveryUiBridge
    {
        [Header("Система доставки (обязательно)")]
        [Tooltip("Компонент, отдающий IDeliveryService. Пусто — ищется в сцене.")]
        [SerializeField] private MonoBehaviour deliveryServiceSource;

        [Header("Части игры (обязательны для команд)")]
        [Tooltip("Игровое время: сроки и переходы состояний.")]
        [SerializeField] private MonoBehaviour clockAdapter;

        [Tooltip("Кошелёк: выплата за выполненный заказ.")]
        [SerializeField] private MonoBehaviour payoutAdapter;

        [Tooltip("Местоположение игрока: проверка «я в точке получения / доставки».")]
        [SerializeField] private MonoBehaviour locationProvider;

        [Header("Данные для экрана телефона")]
        [Tooltip("Кошелёк/банк для раздела «Банк». Необязательно.")]
        [SerializeField] private MonoBehaviour walletSource;

        [Tooltip("План района: нужен для координат точек доставки на карте.")]
        [SerializeField] private MonoBehaviour districtSource;

        private IDeliveryService _service;
        private IGameClockAdapter _clock;
        private IPayoutAdapter _payout;
        private ILocationProvider _location;

        /// <summary>Сервис доставки подключён и готов отдавать данные.</summary>
        public bool Connected => _service != null;

        /// <summary>Можно ли выполнять команды (нужны все три части игры).</summary>
        public bool CommandsReady => _service != null && _clock != null && _payout != null && _location != null;

        /// <summary>Причина, по которой команды недоступны (null — всё в порядке).</summary>
        public string CommandBlockReason
        {
            get
            {
                if (_service == null) return "Система доставки не подключена.";
                if (_clock == null) return "Игровое время не подключено.";
                if (_payout == null) return "Кошелёк не подключён.";
                if (_location == null) return "Местоположение игрока не подключено.";
                return null;
            }
        }

        /// <summary>Сервис доставки (для карты/HUD).</summary>
        public IDeliveryService Service
        {
            get
            {
                Resolve();
                return _service;
            }
        }

        private void Awake() => Resolve();

        /// <summary>Найти части игры в сцене, если не назначены в инспекторе.</summary>
        public void Resolve()
        {
            if (_service == null && deliveryServiceSource != null)
                _service = deliveryServiceSource as IDeliveryService;
            if (_service == null)
            {
                var holders = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                for (int i = 0; i < holders.Length; i++)
                {
                    // DeliveryService — обычный класс, а не компонент: ищем компонент,
                    // который его ОТДАЁТ (наследник DeliveryServiceHost).
                    var host = holders[i] as DeliveryServiceHost;
                    if (host == null) continue;
                    _service = host.Service;
                    if (_service != null) break;
                }
            }

            if (_clock == null) _clock = clockAdapter as IGameClockAdapter;
            if (_payout == null) _payout = payoutAdapter as IPayoutAdapter;
            if (_location == null) _location = locationProvider as ILocationProvider;
        }

        // ------------------------------------------------------------------ IPhoneDataSource

        public PhoneSnapshot ReadPhone()
        {
            Resolve();
            var snapshot = new PhoneSnapshot { Apps = PhoneAppCatalog.Build(_service != null) };

            if (_service == null)
            {
                snapshot.Orders = new OrdersView
                {
                    UnavailableReason = "Система доставки не подключена — заказов нет.",
                };
                return snapshot;
            }

            snapshot.Orders = BuildOrders();
            snapshot.Bank = BuildBank(snapshot.Orders);
            snapshot.UnreadMessages = 0;
            snapshot.UnreadNotifications = 0;
            return snapshot;
        }

        private OrdersView BuildOrders()
        {
            var orders = new List<DeliveryPhoneOrder>();
            var known = _service.Orders;
            if (known != null)
            {
                for (int i = 0; i < known.Count; i++)
                {
                    DeliveryOrder order = known[i];
                    if (order == null) continue;
                    orders.Add(ToPhoneOrder(order, StateOf(order.Id)));
                }
            }

            // Активный заказ может быть не в списке «доступных» — добавим его отдельно.
            string activeId = _service.ActiveOrderId;
            if (!string.IsNullOrEmpty(activeId))
            {
                bool present = false;
                for (int i = 0; i < orders.Count; i++)
                {
                    if (orders[i].OrderId == activeId) { present = true; break; }
                }
                if (!present)
                {
                    DeliveryResult status = _service.GetStatus(activeId);
                    if (status.IsSuccess)
                    {
                        // Данных заказа нет — но состояние есть. Показываем честно минимум.
                        orders.Add(new DeliveryPhoneOrder(activeId, "Активный заказ", string.Empty,
                            "Активный заказ", null, ToStage(status.Status.State), null, string.Empty));
                    }
                }
            }

            var view = new OrdersView();
            DeliveryPhoneRows.Fill(view, orders);
            view.CourierShiftActive = !string.IsNullOrEmpty(_service.ActiveOrderId);
            return view;
        }

        private DeliveryPhoneOrder ToPhoneOrder(DeliveryOrder order, DeliveryOrderState state)
        {
            MapPoint? position = null;
            MapPoint point;
            if (TryGetDeliveryPosition(order.DropOffPoint.Id, out point)) position = point;

            long? payment = order.PaymentRubles > 0 ? order.PaymentRubles : (long?)null;
            return new DeliveryPhoneOrder(
                order.Id,
                order.CargoTitle,
                order.DropOffPoint.Id,
                order.DropOffPoint.DisplayName,
                payment,
                ToStage(state),
                position,
                string.Empty);
        }

        private DeliveryOrderState StateOf(string orderId)
        {
            DeliveryResult status = _service.GetStatus(orderId);
            return status.IsSuccess ? status.Status.State : DeliveryOrderState.Available;
        }

        /// <summary>
        /// Координаты точки доставки берутся из плана района по её Id. Если план не
        /// подключён — координат нет, и маршрут честно откажет (см. OrderView.CanRoute).
        /// </summary>
        public bool TryGetDeliveryPosition(string deliveryPointId, out MapPoint position)
        {
            position = new MapPoint(0f, 0f);
            if (string.IsNullOrEmpty(deliveryPointId)) return false;

            LifeFromZero.City.Quarter.QuarterPlan plan = null;
            var source = districtSource as LifeFromZero.City.Quarter.IQuarterPlanSource;
            if (source != null) plan = source.Plan;
            if (plan == null) return false;

            return QuarterPlanAdapter.TryGetDeliveryPosition(plan, deliveryPointId, out position);
        }

        private BankView BuildBank(OrdersView orders)
        {
            var bank = new BankView();
            if (walletSource is UiSnapshotSource wallet)
            {
                var snapshot = new UiSnapshot();
                wallet.ReadInto(snapshot);
                bank.Balance = snapshot.Finance.Money;
                bank.Debt = snapshot.Finance.Debt;
                bank.DailyIncome = snapshot.Finance.DailyIncome;
            }
            if (orders != null) bank.OperationsAvailable = false; // журнала операций у UI нет
            return bank;
        }

        // ------------------------------------------------------------------ IDeliveryUiBridge

        /// <summary>Телефон: действие вида «app.messages» или «order.accept:id».</summary>
        bool IPhoneDataSource.TryRun(string action, out string message)
        {
            string verb;
            string orderId;
            if (DeliveryPhoneRows.TryParse(action, out verb, out orderId))
                return TryRun(verb, orderId, out message);

            message = "Раздел «" + action + "» пока не подключён к игре.";
            return false;
        }

        public bool TryRun(string verb, string orderId, out string message)
        {
            message = null;
            Resolve();

            if (_service == null) { message = "Система доставки не подключена."; return false; }
            if (string.IsNullOrEmpty(orderId)) { message = "Заказ не выбран."; return false; }

            string blocked = CommandBlockReason;
            if (blocked != null) { message = blocked; return false; }

            DeliveryResult result;
            switch (verb)
            {
                case DeliveryPhoneCommands.Accept:
                    result = _service.Accept(orderId, _clock);
                    break;
                case DeliveryPhoneCommands.PickUp:
                    result = _service.PickUp(orderId, _clock, _location);
                    break;
                case DeliveryPhoneCommands.Complete:
                    result = _service.Complete(orderId, _clock, _location, _payout);
                    break;
                case DeliveryPhoneCommands.Cancel:
                    result = _service.Cancel(orderId, _clock);
                    break;
                case DeliveryPhoneCommands.Abandon:
                    result = _service.Abandon(orderId, _clock);
                    break;
                default:
                    message = "Неизвестное действие с заказом: " + verb;
                    return false;
            }

            if (result.IsSuccess)
            {
                message = result.PaidRubles > 0
                    ? "Заказ закрыт. Оплата: " + UiFormat.Rubles(result.PaidRubles) + "."
                    : null;
                return true;
            }

            message = DeliveryPhoneCommands.Describe(result.Code.ToString(), verb);
            return false;
        }

        internal static OrderStage ToStage(DeliveryOrderState state)
        {
            switch (state)
            {
                case DeliveryOrderState.Available: return OrderStage.Available;
                case DeliveryOrderState.Accepted: return OrderStage.Accepted;
                case DeliveryOrderState.PickedUp: return OrderStage.PickedUp;
                case DeliveryOrderState.Completed: return OrderStage.Completed;
                case DeliveryOrderState.Failed: return OrderStage.Failed;
                case DeliveryOrderState.Cancelled: return OrderStage.Cancelled;
                default: return OrderStage.None;
            }
        }
    }


}
