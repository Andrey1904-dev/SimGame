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
        [Tooltip("Кошелёк/банк и часы для раздела «Банк» и шапки телефона. Пусто — ищется UiSnapshotSource.")]
        [SerializeField] private MonoBehaviour walletSource;

        [Tooltip("Дополнительный источник данных телефона (сообщения, звонки, задачи из PhoneFacade). Необязательно.")]
        [SerializeField] private MonoBehaviour externalPhoneDataSource;

        [Tooltip("План района: нужен для координат точек доставки на карте. Пусто — берётся у UiBootstrap/сцены.")]
        [SerializeField] private MonoBehaviour districtSource;

        private IDeliveryService _service;
        private IGameClockAdapter _clock;
        private IPayoutAdapter _payout;
        private ILocationProvider _location;

        /// <summary>Сервис доставки подключён и готов отдавать данные.</summary>
        public bool Connected
        {
            get
            {
                Resolve();
                return IsAlive(_service);
            }
        }

        /// <summary>Можно ли выполнять все команды доставки (нужны все три части игры).</summary>
        public bool CommandsReady
        {
            get
            {
                Resolve();
                return IsAlive(_service) && IsAlive(_clock) && IsAlive(_payout) && IsAlive(_location);
            }
        }

        /// <summary>Причина, по которой полный цикл команд недоступен (null — всё в порядке).</summary>
        public string CommandBlockReason
        {
            get
            {
                Resolve();
                if (!IsAlive(_service)) return "Система доставки не подключена.";
                if (!IsAlive(_clock)) return "Игровое время не подключено.";
                if (!IsAlive(_payout)) return "Кошелёк не подключён.";
                if (!IsAlive(_location)) return "Местоположение игрока не подключено.";
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

        private static bool IsAlive(object target)
        {
            if (target == null) return false;
            var unityObj = target as UnityEngine.Object;
            if ((object)unityObj != null && unityObj == null) return false;
            var configurable = target as IConfiguredDeliveryAdapter;
            return configurable == null || configurable.IsConfigured;
        }

        /// <summary>
        /// Передать телефонный мост (например PhoneFacadeUiBridge), чтобы телефон получал
        /// и заказы доставки, и сообщения/контакты/задачи из PhoneFacade.
        /// </summary>
        /// <remarks>
        /// ТЗ №045: UiBootstrap выбирает источником данных телефона этот мост (он умеет
        /// объединять источники), но раньше внешний мост можно было задать только в
        /// инспекторе — при автоподключении разделы телефона терялись.
        /// </remarks>
        public void UseExternalPhoneDataSource(IPhoneDataSource source)
        {
            if (ReferenceEquals(source, this)) return;
            externalPhoneDataSource = source as MonoBehaviour;
        }

        /// <summary>Сбросить закэшированные ссылки сцены перед повторным разрешением.</summary>
        public void ResetSceneCache()
        {
            if (!IsAlive(_service)) _service = null;
            if (!IsAlive(_clock)) _clock = null;
            if (!IsAlive(_payout)) _payout = null;
            if (!IsAlive(_location)) _location = null;
            Resolve();
        }

        /// <summary>Найти части игры в сцене, если не назначены в инспекторе.</summary>
        public void Resolve()
        {
            if (!IsAlive(_service)) _service = null;
            if (!IsAlive(_clock)) _clock = null;
            if (!IsAlive(_payout)) _payout = null;
            if (!IsAlive(_location)) _location = null;

            if (_service == null && deliveryServiceSource != null)
            {
                var assignedDistrictHost = deliveryServiceSource as DistrictDeliveryServiceHost;
                if (assignedDistrictHost != null)
                {
                    _service = assignedDistrictHost.HasConfiguredService ? assignedDistrictHost.Service : null;
                }
                else
                {
                    var assignedHost = deliveryServiceSource as DeliveryServiceHost;
                    _service = assignedHost != null ? assignedHost.Service : (deliveryServiceSource as IDeliveryService);
                }
            }

            MonoBehaviour[] holders = null;
            if (_service == null || _clock == null || _payout == null || _location == null ||
                walletSource == null || districtSource == null)
            {
                holders = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            }

            if (_service == null && holders != null)
            {
                for (int i = 0; i < holders.Length; i++)
                {
                    MonoBehaviour candidate = holders[i];
                    if (candidate == null || ReferenceEquals(candidate, this)) continue;
                    var districtHost = candidate as DistrictDeliveryServiceHost;
                    if (districtHost != null)
                    {
                        if (districtHost.HasConfiguredService && districtHost.Service != null)
                        {
                            _service = districtHost.Service;
                            break;
                        }
                        continue;
                    }
                    var host = candidate as DeliveryServiceHost;
                    if (host != null && host.Service != null) { _service = host.Service; break; }
                    var direct = candidate as IDeliveryService;
                    if (direct != null) { _service = direct; break; }
                }
            }

            if (_clock == null && clockAdapter != null) _clock = clockAdapter as IGameClockAdapter;
            if (_payout == null && payoutAdapter != null) _payout = payoutAdapter as IPayoutAdapter;
            if (_location == null && locationProvider != null) _location = locationProvider as ILocationProvider;

            if (holders != null)
            {
                for (int i = 0; i < holders.Length; i++)
                {
                    MonoBehaviour candidate = holders[i];
                    if (candidate == null || ReferenceEquals(candidate, this)) continue;
                    if (_clock == null && candidate is IGameClockAdapter c) _clock = c;
                    if (_payout == null && candidate is IPayoutAdapter p) _payout = p;
                    if (_location == null && candidate is ILocationProvider l) _location = l;
                    if (walletSource == null && candidate is UiSnapshotSource) walletSource = candidate;
                    if (districtSource == null && candidate is LifeFromZero.City.Quarter.IQuarterPlanSource)
                        districtSource = candidate;
                }
            }
        }

        // ------------------------------------------------------------------ IPhoneDataSource

        public PhoneSnapshot ReadPhone()
        {
            Resolve();

            PhoneSnapshot snapshot = null;
            if (externalPhoneDataSource != null && !ReferenceEquals(externalPhoneDataSource, this))
            {
                var ext = externalPhoneDataSource as IPhoneDataSource;
                if (ext != null) snapshot = ext.ReadPhone();
            }

            if (snapshot == null)
            {
                snapshot = new PhoneSnapshot { Apps = PhoneAppCatalog.Build(_service != null) };
            }

            if (walletSource is UiSnapshotSource uiSource)
            {
                var uiSnap = new UiSnapshot();
                uiSource.ReadInto(uiSnap);
                if (string.IsNullOrEmpty(snapshot.LockTime) || snapshot.LockTime == UiFormat.Dash)
                    snapshot.LockTime = uiSnap.Clock.TimeLabel;
                if (string.IsNullOrEmpty(snapshot.LockDate) || snapshot.LockDate == UiFormat.Dash)
                    snapshot.LockDate = uiSnap.Clock.DayLabel;
            }

            if (_service == null)
            {
                snapshot.Orders = new OrdersView
                {
                    UnavailableReason = "Система доставки не подключена — заказов нет.",
                };
                if (snapshot.Bank == null) snapshot.Bank = BuildBank(snapshot.Orders);
                return snapshot;
            }

            snapshot.Orders = BuildOrders();
            snapshot.Bank = BuildBank(snapshot.Orders);
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
            // Пока груз ещё не забран в ПВЗ (Accepted), курьеру сначала нужен маршрут
            // к точке получения (PickupPoint), а после PickUp — к точке вручения (DropOffPoint).
            string routePointId = state == DeliveryOrderState.Accepted
                ? order.PickupPoint.Id
                : order.DropOffPoint.Id;
            string routeTitle = state == DeliveryOrderState.Accepted
                ? "Забрать: " + order.PickupPoint.DisplayName + " → " + order.DropOffPoint.DisplayName
                : order.DropOffPoint.DisplayName;

            MapPoint? position = null;
            MapPoint point;
            if (TryGetDeliveryPosition(routePointId, out point))
                position = point;
            else if (state == DeliveryOrderState.Accepted && TryGetDeliveryPosition(order.DropOffPoint.Id, out point))
                position = point;

            string deadlineLabel = string.Empty;
            if (order.DeadlineGameMinutes > 0 && IsAlive(_clock))
            {
                try
                {
                    long remaining = order.DeadlineGameMinutes - (long)Math.Floor(_clock.NowMinutes);
                    deadlineLabel = remaining >= 0 ? ("осталось " + remaining + " мин") : "просрочен";
                }
                catch
                {
                    deadlineLabel = string.Empty;
                }
            }

            long? payment = order.PaymentRubles > 0 ? order.PaymentRubles : (long?)null;
            return new DeliveryPhoneOrder(
                order.Id,
                order.CargoTitle,
                routePointId,
                routeTitle,
                payment,
                ToStage(state),
                position,
                deadlineLabel);
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
            if (plan == null && UiBootstrap.Instance != null) plan = UiBootstrap.Instance.CurrentDistrictPlan;
            if (plan == null)
            {
                Resolve();
                source = districtSource as LifeFromZero.City.Quarter.IQuarterPlanSource;
                if (source != null) plan = source.Plan;
            }
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

            if (externalPhoneDataSource != null && !ReferenceEquals(externalPhoneDataSource, this))
            {
                var ext = externalPhoneDataSource as IPhoneDataSource;
                if (ext != null) return ext.TryRun(action, out message);
            }

            message = "Раздел «" + action + "» пока не подключён к игре.";
            return false;
        }

        private string CheckBlockReasonForVerb(string verb)
        {
            if (!IsAlive(_service)) return "Система доставки не подключена.";
            if (!IsAlive(_clock)) return "Игровое время не подключено.";
            if (verb == DeliveryPhoneCommands.PickUp && !IsAlive(_location))
                return "Местоположение игрока не подключено.";
            if (verb == DeliveryPhoneCommands.Complete)
            {
                if (!IsAlive(_location)) return "Местоположение игрока не подключено.";
                if (!IsAlive(_payout)) return "Кошелёк не подключён.";
            }
            return null;
        }

        public bool TryRun(string verb, string orderId, out string message)
        {
            message = null;
            Resolve();

            if (!IsAlive(_service)) { message = "Система доставки не подключена."; return false; }
            if (string.IsNullOrEmpty(orderId)) { message = "Заказ не выбран."; return false; }

            string blocked = CheckBlockReasonForVerb(verb);
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
