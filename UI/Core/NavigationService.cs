// Life From Zero — UI/Core (ТЗ №042 §6, §7, §7.5)
// Namespace: LifeFromZero.UI.Core
//
// Метки и ЕДИНОЕ состояние навигации.
//
// Главное требование (§7.5): карта и HUD работают с ОДНИМ объектом маршрута.
// Здесь этот объект и живёт — <see cref="NavigationService.ActiveRoute"/>.
// Ни карта, ни HUD не считают путь самостоятельно.
//
// Модуль без UnityEngine: прогоняется вне Unity.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace LifeFromZero.UI.Core
{
    /// <summary>Тип метки.</summary>
    public enum WaypointKind
    {
        /// <summary>Ручная метка игрока (клик по карте).</summary>
        Manual = 0,

        /// <summary>Метка активного курьерского заказа.</summary>
        Order = 1,

        /// <summary>Дом игрока (квартира №67) — важный объект, не удаляется.</summary>
        Home = 2,

        /// <summary>Пункт выдачи заказов (ПВЗ).</summary>
        Pickup = 3,
    }

    /// <summary>Метка на карте.</summary>
    public sealed class Waypoint
    {
        public Waypoint(string id, WaypointKind kind, MapPoint position, string label, string orderId)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Идентификатор метки не должен быть пустым.", nameof(id));

            Id = id;
            Kind = kind;
            Position = position;
            Label = label ?? string.Empty;
            OrderId = orderId ?? string.Empty;
        }

        public string Id { get; }

        public WaypointKind Kind { get; }

        public MapPoint Position { get; private set; }

        public string Label { get; private set; }

        /// <summary>Идентификатор заказа для метки заказа; пусто для ручной.</summary>
        public string OrderId { get; }

        /// <summary>Метку можно двигать и удалять вручную.</summary>
        public bool IsEditable => Kind == WaypointKind.Manual || Kind == WaypointKind.Order;

        internal void MoveTo(MapPoint position, string label)
        {
            Position = position;
            if (!string.IsNullOrWhiteSpace(label)) Label = label;
        }

        public override string ToString() => Id + " [" + Kind + "] " + Label + " " + Position;
    }

    /// <summary>
    /// Хранилище меток. Живёт в пределах игровой сессии и переживает открытие/закрытие
    /// окон и переходы между сценами (сериализуется через <see cref="Snapshot"/>).
    /// </summary>
    public sealed class WaypointService
    {
        public const string ManualId = "waypoint.manual";
        public const string HomeId = "waypoint.home";
        public const string PickupId = "waypoint.pickup";
        public const string OrderIdPrefix = "waypoint.order.";

        private readonly List<Waypoint> _waypoints = new List<Waypoint>();
        private int _manualCounter;

        /// <summary>Все метки в порядке добавления.</summary>
        public IReadOnlyList<Waypoint> Waypoints => _waypoints;

        /// <summary>Активная цель (по ней строится маршрут). Может быть null.</summary>
        public Waypoint ActiveTarget { get; private set; }

        /// <summary>Создаёт/перемещает ручную метку. Одна ручная метка на игрока (ТЗ §6.1).</summary>
        public Waypoint SetManual(MapPoint position, string label = null)
        {
            Waypoint existing = Find(ManualId);
            if (existing != null)
            {
                existing.MoveTo(position, label ?? "Метка игрока");
                return existing;
            }

            _manualCounter++;
            var created = new Waypoint(ManualId, WaypointKind.Manual, position,
                label ?? "Метка игрока", string.Empty);
            _waypoints.Add(created);
            return created;
        }

        /// <summary>Удаляет ручную метку. Метки заказа и дом так не удаляются.</summary>
        public bool RemoveManual()
        {
            Waypoint manual = Find(ManualId);
            if (manual == null) return false;
            _waypoints.Remove(manual);
            if (ReferenceEquals(ActiveTarget, manual)) ActiveTarget = null;
            return true;
        }

        public bool MoveManual(MapPoint position)
        {
            Waypoint manual = Find(ManualId);
            if (manual == null) return false;
            manual.MoveTo(position, manual.Label);
            return true;
        }

        /// <summary>Метка заказа (ТЗ §6.2): собственный тип и идентификатор заказа.</summary>
        public Waypoint SetOrder(string orderId, MapPoint position, string label)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return null;
            string id = OrderIdPrefix + orderId;
            Waypoint existing = Find(id);
            if (existing != null)
            {
                existing.MoveTo(position, label);
                return existing;
            }

            var created = new Waypoint(id, WaypointKind.Order, position, label, orderId);
            _waypoints.Add(created);
            return created;
        }

        /// <summary>Удаляет метку конкретного заказа (завершение/отмена). Ручная метка остаётся.</summary>
        public bool RemoveOrder(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return false;
            Waypoint order = Find(OrderIdPrefix + orderId);
            if (order == null) return false;
            _waypoints.Remove(order);
            if (ReferenceEquals(ActiveTarget, order)) ActiveTarget = null;
            return true;
        }

        /// <summary>Дом игрока — постоянная метка, создаётся один раз.</summary>
        public Waypoint SetHome(MapPoint position, string label = "Квартира №67")
        {
            Waypoint existing = Find(HomeId);
            if (existing != null)
            {
                existing.MoveTo(position, label);
                return existing;
            }

            var created = new Waypoint(HomeId, WaypointKind.Home, position, label, string.Empty);
            _waypoints.Add(created);
            return created;
        }

        public Waypoint SetPickup(MapPoint position, string label = "ПВЗ")
        {
            Waypoint existing = Find(PickupId);
            if (existing != null)
            {
                existing.MoveTo(position, label);
                return existing;
            }

            var created = new Waypoint(PickupId, WaypointKind.Pickup, position, label, string.Empty);
            _waypoints.Add(created);
            return created;
        }

        public Waypoint Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < _waypoints.Count; i++)
                if (string.Equals(_waypoints[i].Id, id, StringComparison.Ordinal))
                    return _waypoints[i];
            return null;
        }

        public Waypoint FindOrder(string orderId) =>
            string.IsNullOrWhiteSpace(orderId) ? null : Find(OrderIdPrefix + orderId);

        /// <summary>Сделать метку активной целью.</summary>
        public bool SetActive(string id)
        {
            Waypoint waypoint = Find(id);
            if (waypoint == null) return false;
            ActiveTarget = waypoint;
            return true;
        }

        public void ClearActive() => ActiveTarget = null;

        /// <summary>Удаляет все метки заказов, кроме активного заказа (если он ещё идёт).</summary>
        public int ClearOrdersExcept(string orderId)
        {
            int removed = 0;
            for (int i = _waypoints.Count - 1; i >= 0; i--)
            {
                Waypoint waypoint = _waypoints[i];
                if (waypoint.Kind != WaypointKind.Order) continue;
                if (!string.IsNullOrEmpty(orderId) &&
                    string.Equals(waypoint.OrderId, orderId, StringComparison.Ordinal)) continue;
                _waypoints.RemoveAt(i);
                if (ReferenceEquals(ActiveTarget, waypoint)) ActiveTarget = null;
                removed++;
            }
            return removed;
        }

        /// <summary>Снимок меток: переживает смену сцены в пределах сессии (ТЗ §6.2).</summary>
        public WaypointSnapshot Snapshot()
        {
            var items = new WaypointState[_waypoints.Count];
            for (int i = 0; i < _waypoints.Count; i++)
            {
                Waypoint w = _waypoints[i];
                items[i] = new WaypointState
                {
                    Id = w.Id,
                    Kind = (int)w.Kind,
                    X = w.Position.X,
                    Z = w.Position.Z,
                    Label = w.Label,
                    OrderId = w.OrderId,
                };
            }
            return new WaypointSnapshot
            {
                Items = items,
                ActiveId = ActiveTarget != null ? ActiveTarget.Id : string.Empty,
                ManualCounter = _manualCounter,
            };
        }

        /// <summary>Восстанавливает метки из снимка. Битые элементы пропускаются.</summary>
        public void Restore(WaypointSnapshot snapshot)
        {
            _waypoints.Clear();
            ActiveTarget = null;
            if (snapshot == null || snapshot.Items == null) return;

            for (int i = 0; i < snapshot.Items.Length; i++)
            {
                WaypointState item = snapshot.Items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.Id)) continue;
                if (float.IsNaN(item.X) || float.IsNaN(item.Z)) continue;
                var waypoint = new Waypoint(item.Id, (WaypointKind)item.Kind,
                    new MapPoint(item.X, item.Z), item.Label, item.OrderId);
                _waypoints.Add(waypoint);
            }

            _manualCounter = Math.Max(0, snapshot.ManualCounter);
            if (!string.IsNullOrEmpty(snapshot.ActiveId)) SetActive(snapshot.ActiveId);
        }

        public void Clear()
        {
            _waypoints.Clear();
            ActiveTarget = null;
            _manualCounter = 0;
        }
    }

    /// <summary>Состояние одной метки для снимка.</summary>
    [Serializable]
    public sealed class WaypointState
    {
        public string Id;
        public int Kind;
        public float X;
        public float Z;
        public string Label;
        public string OrderId;
    }

    /// <summary>Снимок меток.</summary>
    [Serializable]
    public sealed class WaypointSnapshot
    {
        public WaypointState[] Items = Array.Empty<WaypointState>();
        public string ActiveId = string.Empty;
        public int ManualCounter;
    }

    /// <summary>Итог построения маршрута для UI (карта, HUD, телефон).</summary>
    public readonly struct NavigationOutcome
    {
        public NavigationOutcome(bool ok, string message, RouteFailure failure)
        {
            Ok = ok;
            Message = message ?? string.Empty;
            Failure = failure;
        }

        public bool Ok { get; }

        public string Message { get; }

        public RouteFailure Failure { get; }

        public static NavigationOutcome Success(RoutePath path) =>
            new NavigationOutcome(true, "Маршрут построен: " + RouteNavigator.FormatDistance(path.TotalDistance), RouteFailure.None);

        public static NavigationOutcome Error(string message, RouteFailure failure) =>
            new NavigationOutcome(false, message, failure);
    }

    /// <summary>
    /// Единый сервис навигации: держит активный маршрут, метки и правила перестроения.
    /// </summary>
    /// <remarks>
    /// Перестроение не выполняется каждый кадр (§7.4): <see cref="Tick"/> срабатывает
    /// только когда игрок отклонился сильнее порога, изменилась цель или истёк кулдаун.
    /// </remarks>
    public sealed class NavigationService
    {
        private readonly RoutePlanner _planner = new RoutePlanner();
        private float _rebuildCooldown;
        private float _lastRebuildDistance = float.NaN;

        public NavigationService(WaypointService waypoints = null)
        {
            Waypoints = waypoints ?? new WaypointService();
        }

        public WaypointService Waypoints { get; }

        public RoadGraphData Graph { get; private set; }

        /// <summary>Единственный активный маршрут. Его читают и карта, и HUD.</summary>
        public RoutePath ActiveRoute { get; private set; }

        /// <summary>Последнее сообщение для игрока (успех/причина отказа).</summary>
        public NavigationOutcome LastOutcome { get; private set; } = new NavigationOutcome(false, string.Empty, RouteFailure.None);

        /// <summary>Сколько раз маршрут перестраивался — для диагностики и тестов.</summary>
        public int RebuildCount { get; private set; }

        /// <summary>Минимальный интервал между автоматическими перестроениями, секунды.</summary>
        public float RebuildCooldownSeconds { get; set; } = 1.5f;

        public bool HasRoute => ActiveRoute != null;

        public event Action<RoutePath> RouteChanged;

        public void SetGraph(RoadGraphData graph)
        {
            Graph = graph;
            ClearRoute("сеть обновлена");
        }

        /// <summary>
        /// Строит маршрут к метке и делает её активной целью. Возвращает результат
        /// с понятной причиной отказа (кнопка в телефоне показывает её игроку).
        /// </summary>
        public NavigationOutcome NavigateTo(string waypointId, MapPoint from)
        {
            Waypoint target = Waypoints.Find(waypointId);
            if (target == null)
            {
                LastOutcome = NavigationOutcome.Error("Цель не найдена.", RouteFailure.InvalidPoint);
                return LastOutcome;
            }
            return NavigateTo(target, from);
        }

        public NavigationOutcome NavigateTo(Waypoint target, MapPoint from)
        {
            if (target == null)
            {
                LastOutcome = NavigationOutcome.Error("Цель не найдена.", RouteFailure.InvalidPoint);
                return LastOutcome;
            }

            if (Graph == null || Graph.NodeCount == 0)
            {
                LastOutcome = NavigationOutcome.Error(
                    "Дорожная сеть района не загружена — маршрут недоступен.", RouteFailure.NoGraph);
                return LastOutcome;
            }

            RouteResult result = _planner.Build(Graph, from, target.Position, target.Id, target.Label);
            if (!result.Success)
            {
                LastOutcome = NavigationOutcome.Error(result.Message, result.Failure);
                return LastOutcome;
            }

            Waypoints.SetActive(target.Id);
            ActiveRoute = result.Path;
            _lastRebuildDistance = RouteNavigator.RemainingDistance(ActiveRoute, from);
            _rebuildCooldown = RebuildCooldownSeconds;
            RouteChanged?.Invoke(ActiveRoute);
            LastOutcome = NavigationOutcome.Success(ActiveRoute);
            return LastOutcome;
        }

        /// <summary>Проверка «можно ли строить маршрут к заказу» для кнопки в телефоне.</summary>
        public bool CanNavigateTo(Waypoint target, MapPoint from, out string reason)
        {
            reason = string.Empty;
            if (target == null)
            {
                reason = "Координаты точки доставки недоступны.";
                return false;
            }
            if (Graph == null || Graph.NodeCount == 0)
            {
                reason = "Дорожная сеть района не загружена.";
                return false;
            }
            if (!float.IsNaN(target.Position.X) && !float.IsNaN(target.Position.Z))
            {
                List<int> network = Graph.LargestComponent();
                int nearest = Graph.FindNearestNode(target.Position, _planner.DestinationSnapRadius, network);
                if (nearest < 0)
                {
                    reason = "Точка доставки не связана с дорогами.";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Обновление навигации. Вызывать из Update игрового контроллера.
        /// Пересчёт выполняется только при отклонении от маршрута и не чаще кулдауна.
        /// </summary>
        public void Tick(MapPoint position, float deltaTime)
        {
            if (_rebuildCooldown > 0f) _rebuildCooldown -= Math.Max(0f, deltaTime);
            if (ActiveRoute == null) return;

            if (RouteNavigator.IsArrived(ActiveRoute, position))
            {
                // Навигация корректно завершается у цели: маршрут снимается, метка заказа — нет.
                RoutePath finished = ActiveRoute;
                ActiveRoute = null;
                LastOutcome = new NavigationOutcome(true, "Вы на месте: " + finished.DestinationLabel, RouteFailure.None);
                RouteChanged?.Invoke(null);
                return;
            }

            if (_rebuildCooldown > 0f) return;
            if (!RouteNavigator.ShouldRebuild(ActiveRoute, position)) return;

            Waypoint target = Waypoints.Find(ActiveRoute.DestinationId);
            if (target == null) return;

            RouteResult rebuilt = _planner.Build(Graph, position, target.Position, target.Id, target.Label);
            if (!rebuilt.Success) return;
            ActiveRoute = rebuilt.Path;
            RebuildCount++;
            _rebuildCooldown = RebuildCooldownSeconds;
            RouteChanged?.Invoke(ActiveRoute);
        }

        /// <summary>Текущая инструкция для HUD. Считается из того же маршрута, что видит карта.</summary>
        public NavigationCue Cue(MapPoint position, float headingDegrees) =>
            RouteNavigator.Cue(ActiveRoute, position, headingDegrees);

        /// <summary>Снять маршрут (завершение заказа, отмена, смена сцены).</summary>
        public void ClearRoute(string reason = "")
        {
            if (ActiveRoute == null) return;
            ActiveRoute = null;
            if (!string.IsNullOrEmpty(reason))
                LastOutcome = new NavigationOutcome(true, reason, RouteFailure.None);
            RouteChanged?.Invoke(null);
        }

        /// <summary>Заказ завершён/отменён: удаляем маршрут и метку заказа, ручная метка остаётся.</summary>
        public void OnOrderClosed(string orderId)
        {
            if (!string.IsNullOrEmpty(orderId) &&
                ActiveRoute != null &&
                string.Equals(ActiveRoute.DestinationId, WaypointService.OrderIdPrefix + orderId, StringComparison.Ordinal))
            {
                ClearRoute("Заказ закрыт — маршрут снят.");
            }
            Waypoints.RemoveOrder(orderId);
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "Navigation(route={0}, rebuilds={1})",
                ActiveRoute == null ? "нет" : ActiveRoute.DestinationLabel, RebuildCount);
    }
}
