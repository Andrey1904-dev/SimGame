// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// ЕДИНСТВЕННАЯ точка подключения интерфейсов к игре.
//
// Что делает:
//   1. создаёт UiRouter + экраны (телефон, карта, HUD, пауза) ровно один раз;
//   2. находит в сцене существующие системы проекта и подключает их через
//      абстракции UiWorldAdapter/UiSnapshotSource — БЕЗ прямых ссылок UI на
//      экономику, доставку и телефон;
//   3. строит дорожный граф из QuarterPlan (District750) для маршрутов;
//   4. при смене сцены перепривязывает данные и снимает модальные окна.
//
// Чего НЕ делает (и это важно):
//   * не создаёт вторую систему паузы, времени, денег или доставки;
//   * не угадывает имена сцен и не грузит их;
//   * не включает себя сам: добавьте компонент на объект в стартовой сцене
//     (это делает Editor-установщик LifeFromZero.UI.Installer) или вызовите
//     UiBootstrap.Ensure() из существующего загрузчика игры.
//
// Если система не найдена, соответствующий блок данных показывает «Нет данных»,
// а в консоль уходит одно предупреждение с подсказкой, что именно подключить.

using System;
using System.Collections.Generic;
using LifeFromZero.City.Quarter;
using LifeFromZero.UI.Core;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Что бутстрапу удалось подключить — для отчёта и лога.</summary>
    public sealed class UiWiringReport
    {
        public readonly List<string> Connected = new List<string>();
        public readonly List<string> Missing = new List<string>();

        public bool IsComplete => Missing.Count == 0;

        public void Ok(string what) => Connected.Add(what);

        public void Miss(string what) => Missing.Add(what);

        public string Summary()
        {
            var text = new System.Text.StringBuilder();
            text.Append("UI подключено: ");
            text.Append(Connected.Count == 0 ? "ничего" : string.Join(", ", Connected.ToArray()));
            if (Missing.Count > 0)
            {
                text.Append("; НЕ подключено: ");
                text.Append(string.Join(", ", Missing.ToArray()));
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// Установщик интерфейсов. Живёт на объекте «LifeFromZero UI» в стартовой сцене.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class UiBootstrap : MonoBehaviour
    {
        [Header("Где искать данные (пусто — искать в сцене автоматически)")]
        [Tooltip("Компонент, реализующий UiWorldAdapter (позиция игрока, блокировка ввода, меню).")]
        [SerializeField] private UiWorldAdapter worldAdapter;

        [Tooltip("Компонент, реализующий UiSnapshotSource (часы, деньги, долги, статусы, цели).")]
        [SerializeField] private UiSnapshotSource snapshotSource;

        [Header("Системы проекта (для отчёта о подключении)")]
        [Tooltip("Объект с DeliveryService (Jobs.Delivery). Подключается через адаптер, не напрямую.")]
        [SerializeField] private MonoBehaviour deliverySystem;

        [Tooltip("Объект с телефоном (PhoneFacade). Подключается через адаптер.")]
        [SerializeField] private MonoBehaviour phoneSystem;

        [Header("Район и карта")]
        [Tooltip("План района District750. Пусто — берётся из District750Link/QuarterUnityAdapter в сцене.")]
        [SerializeField] private MonoBehaviour districtSource;

        [Tooltip("Шаг сетки на карте, метры.")]
        [SerializeField] private float mapGridStep = 100f;

        [Header("Поведение")]
        [Tooltip("Создать экраны, если их нет в сцене.")]
        [SerializeField] private bool createScreens = true;

        [Tooltip("Логировать отчёт о подключении при старте.")]
        [SerializeField] private bool logReport = true;

        private UiRouter _router;
        private MapScreen _map;

        /// <summary>Отчёт последней привязки.</summary>
        public UiWiringReport Report { get; private set; } = new UiWiringReport();

        /// <summary>Роутер интерфейсов (создаётся один на игру).</summary>
        public static UiRouter Router { get; private set; }

        /// <summary>Уже установлено в этой сцене.</summary>
        public static bool Installed => Router != null;

        private void Awake()
        {
            if (Router != null && Router != _router)
            {
                // Дубликат бутстрапа во второй сцене — не создаём вторые экраны.
                Debug.LogWarning("[UiBootstrap] UiRouter уже существует — дубликат отключён.");
                enabled = false;
                return;
            }
            Install();
        }

        private void OnDestroy()
        {
            if (_router != null && Router == _router) Router = null;
        }

        /// <summary>Точка входа для существующего загрузчика игры (если компонент не на сцене).</summary>
        public static UiRouter Ensure()
        {
            if (Router != null) return Router;

            UiBootstrap existing = FindAnyObjectByType<UiBootstrap>();
            if (existing != null) return existing._router;

            var go = new GameObject("LifeFromZero UI");
            UiBootstrap created = go.AddComponent<UiBootstrap>();
            created.Install();
            return Router;
        }

        // ------------------------------------------------------------------ установка

        private void Install()
        {
            Report = new UiWiringReport();
            _router = ResolveRouter();

            ResolveWorld();
            ResolveSnapshot();
            ResolvePauseOwner();
            ResolveDistrict();

            if (createScreens) CreateScreens();

            _router.Configure(worldAdapter, snapshotSource, Resolve<PhoneScreen>(), _map,
                Resolve<HudScreen>(), Resolve<PauseMenuScreen>());

            WirePhone();
            WireMap();

            Router = _router;
            if (logReport) Debug.Log("[UiBootstrap] " + Report.Summary());
        }

        private UiRouter ResolveRouter()
        {
            UiRouter router = UiRouter.Instance;
            if (router != null) return router;

            router = GetComponent<UiRouter>();
            if (router == null) router = GetComponentInChildren<UiRouter>(true);
            if (router == null) router = gameObject.AddComponent<UiRouter>();
            Report.Ok("UiRouter");
            return router;
        }

        /// <summary>
        /// В проекте уже есть City.Pause (PauseController + PauseView, клавиши Esc/P).
        /// Держать второе меню паузы нельзя: Esc откроет оба, а Time.timeScale будет
        /// останавливать и продолжать каждый по-своему. Поэтому приоритет у существующей
        /// паузы: наш экран паузы становится её видом (Render/Confirm), а действия
        /// меню уходят в её PauseIntegration.
        /// </summary>
        private void ResolvePauseOwner()
        {
            var bridge = FindAnyObjectByType<UiGameBridge>();
            if (bridge == null) bridge = worldAdapter as UiGameBridge;

            if (bridge != null && bridge.HasExistingPause)
            {
                _existingPause = bridge.ExistingPause;
                _router.FallbackTimeScalePause = false;
                // Экран паузы рисует данные существующей паузы, а не свои.
                if (snapshotSource == null)
                {
                    var adapter = bridge.GetComponent<PauseUiSnapshotSource>();
                    if (adapter != null)
                    {
                        snapshotSource = adapter;
                        Report.Ok("данные HUD/паузы из PauseDataSource игры");
                    }
                }
                // Клавиши Esc/P/M в проекте уже обрабатывает PauseKeyboardInput →
                // PauseController. Если читать их ещё и здесь, меню будут открываться
                // и закрываться одним нажатием одновременно.
                _router.ReadsKeyboard = false;
                Report.Ok("пауза игры (City.Pause): второй Time.timeScale и вторые клавиши отключены");
            }
            else if (bridge != null)
            {
                Report.Miss("City.Pause — меню паузы на своём Time.timeScale; " +
                            "клавиши Esc/P читает только UiRouter");
            }
            else
            {
                Report.Miss("UiGameBridge — адаптер мира не найден: клавиши P/M/Esc читает сам UiRouter, " +
                            "позиция игрока неизвестна");
            }
        }

        private void ResolveWorld()
        {
            if (worldAdapter == null) worldAdapter = FindAnyObjectByType<UiWorldAdapter>();
            if (worldAdapter != null) Report.Ok("адаптер мира (" + worldAdapter.GetType().Name + ")");
            else Report.Miss("UiWorldAdapter — позиция игрока, блокировка ввода, действия меню");
        }

        private void ResolveSnapshot()
        {
            if (snapshotSource == null) snapshotSource = FindAnyObjectByType<UiSnapshotSource>();
            if (snapshotSource != null) Report.Ok("источник данных HUD/паузы (" + snapshotSource.GetType().Name + ")");
            else Report.Miss("UiSnapshotSource — часы, деньги, долги, статусы, цели");
        }

        private void ResolveDistrict()
        {
            if (deliverySystem == null && phoneSystem == null) { /* необязательно */ }

            // План района берём там, где он уже сгенерирован: карта обязана рисовать
            // ТОТ ЖЕ город, что стоит в сцене, а не сгенерировать второй по сиду.
            //
            // Ссылка на QuarterUnityAdapter здесь намеренно НЕ ставится: UI не должен
            // зависеть от сборочной единицы адаптера города (иначе получится цикл
            // зависимостей). Поэтому план читается через контракт IQuarterPlanSource,
            // а готовый адаптер проекта подключается одним из двух способов:
            //   1) назначить в инспекторе поле districtSource на компонент-поставщик;
            //   2) отдать план кодом: UiBootstrap.SetDistrictPlan(plan).
            QuarterPlan plan = null;
            LifeFromZero.City.Quarter.IQuarterPlanSource source =
                districtSource as LifeFromZero.City.Quarter.IQuarterPlanSource;
            if (source != null) plan = source.Plan;
            if (plan == null) plan = _externalPlan;
            if (plan == null)
            {
                var sources = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                if (sources != null)
                {
                    for (int i = 0; i < sources.Length; i++)
                    {
                        var candidate = sources[i] as LifeFromZero.City.Quarter.IQuarterPlanSource;
                        if (candidate == null || candidate.Plan == null) continue;
                        plan = candidate.Plan;
                        break;
                    }
                }
            }

            if (plan == null)
            {
                Report.Miss("плана района (QuarterPlan) — карта покажет только сетку, маршрутов не будет");
                return;
            }

            QuarterPlanMapData mapData = QuarterPlanAdapter.Build(plan);
            var geometry = new QuarterMapGeometry(mapData);
            Report.Ok("геометрия карты (" + geometry.RoadCount + " дорог, " + geometry.PointCount + " точек)");

            RoadGraphData graph = mapData.Graph;
            if (graph != null && graph.NodeCount > 0)
            {
                _router.Navigation.SetGraph(graph);
                Report.Ok("дорожный граф маршрутов (" + graph.NodeCount + " узлов, " + graph.EdgeCount + " рёбер)");
            }
            else
            {
                Report.Miss("дорожного графа — маршрут не построить");
            }

            _mapGeometry = geometry;
            _plan = plan;
        }

        private QuarterMapGeometry _mapGeometry;
        private QuarterPlan _plan;
        private LifeFromZero.City.Pause.PauseIntegration _existingPause;
        private DeliveryServiceBridge _deliveryBridge;

        private static QuarterPlan _externalPlan;

        /// <summary>
        /// Передать план района из игры (вызывает интегратор проекта, например
        /// QuarterUnityAdapter после генерации). Без плана карта покажет только сетку.
        /// </summary>
        public static void SetDistrictPlan(QuarterPlan plan)
        {
            _externalPlan = plan;
            if (Router != null && Router.gameObject != null)
            {
                var bootstrap = FindAnyObjectByType<UiBootstrap>();
                if (bootstrap != null && bootstrap._router != null) bootstrap.ResolveDistrict();
            }
        }

        private void CreateScreens()
        {
            if (Resolve<HudScreen>() == null) Create<HudScreen>("HUD");
            if (Resolve<PhoneScreen>() == null) Create<PhoneScreen>("Phone");
            if (Resolve<MapScreen>() == null) Create<MapScreen>("Map");
            if (Resolve<PauseMenuScreen>() == null) Create<PauseMenuScreen>("Pause Menu");
        }

        private T Resolve<T>() where T : Component
        {
            T found = GetComponentInChildren<T>();
            if (found != null) return found;
            return FindAnyObjectByType<T>();
        }

        private T Create<T>(string name) where T : Component, new()
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            T component = go.AddComponent<T>();
            Report.Ok("экран " + name);
            return component;
        }

        private void WirePhone()
        {
            PhoneScreen phone = Resolve<PhoneScreen>();
            if (phone == null) return;

            _deliveryBridge = Resolve<DeliveryServiceBridge>();
            if (_deliveryBridge != null && !_deliveryBridge.Connected)
            {
                Report.Miss("DeliveryServiceHost — заказы: мост есть, но сервис доставки не найден");
            }
            else if (_deliveryBridge != null)
            {
                Report.Ok("система доставки (Jobs.Delivery) через DeliveryServiceBridge");
                if (!_deliveryBridge.CommandsReady)
                    Report.Miss("части игры для команд доставки: " + _deliveryBridge.CommandBlockReason);
            }

            // Телефон получает данные через делегаты. Конкретные источники подставляет
            // интегратор проекта (см. UI/CODEX_EXECUTE.md): UI не знает про DeliveryService
            // и PhoneFacade напрямую, чтобы не появилась вторая экономика.
            IPhoneDataSource provider = snapshotSource as IPhoneDataSource;
            if (provider == null) provider = _deliveryBridge;
            if (provider != null)
            {
                phone.DataProvider = provider.ReadPhone;
                Report.Ok("данные телефона");
            }
            else
            {
                Report.Miss("IPhoneDataSource — данные телефона (плитки, сообщения, заказы, банк)");
            }

            phone.ActionHandler = OnPhoneAction;
            phone.RouteHandler = OnPhoneRoute;
        }

        private void WireMap()
        {
            _map = Resolve<MapScreen>();
            if (_map == null) return;

            _map.Bind(_router.Navigation, worldAdapter);
            if (_mapGeometry != null)
            {
                _map.Geometry = _mapGeometry;
                _map.WorldSize = new Vector2(_mapGeometry.Size, _mapGeometry.Size);
                Report.Ok("карта привязана к району");
            }
            _map.GridStepMeters = mapGridStep;
        }

        // ------------------------------------------------------------------ телефон

        private string OnPhoneAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return null;

            if (action.StartsWith(PhoneActions.OpenApp, StringComparison.Ordinal))
            {
                string appId = action.Substring(PhoneActions.OpenApp.Length);
                return "Приложение «" + appId + "» не подключено к игре.";
            }

            string verb;
            string orderId;
            if (DeliveryPhoneRows.TryParse(action, out verb, out orderId))
            {
                IDeliveryUiBridge bridge = ResolveDeliveryBridge();
                if (bridge == null) return "Система заказов не подключена.";
                string outcome;
                if (bridge.TryRun(verb, orderId, out outcome))
                    return string.IsNullOrEmpty(outcome) ? null : outcome;
                return string.IsNullOrEmpty(outcome) ? "Действие с заказом не выполнено." : outcome;
            }

            IPhoneDataSource phoneData = snapshotSource as IPhoneDataSource;
            if (phoneData == null) phoneData = _deliveryBridge;
            if (phoneData != null)
            {
                string outcome;
                if (phoneData.TryRun(action, out outcome)) return outcome;
            }

            return null;
        }

        /// <summary>Мост к доставке: назначен в инспекторе → найден в сцене → нет.</summary>
        private IDeliveryUiBridge ResolveDeliveryBridge()
        {
            if (deliverySystem is IDeliveryUiBridge assigned) return assigned;
            return _deliveryBridge;
        }

        private string OnPhoneRoute(string orderId)
        {
            if (_plan == null) return "Район не загружен — маршрут недоступен.";
            if (string.IsNullOrEmpty(orderId)) return "Заказ не выбран.";

            IPhoneDataSource phoneData = snapshotSource as IPhoneDataSource;
            if (phoneData == null) phoneData = _deliveryBridge;
            if (phoneData == null) return "Данные телефона не подключены.";

            PhoneSnapshot snapshot = phoneData.ReadPhone();
            OrderView order = FindOrder(snapshot, orderId);
            if (order == null) return "Заказ не найден (возможно, уже закрыт).";

            string reason;
            if (order.CanRoute(out reason)) { }
            else return string.IsNullOrEmpty(reason) ? "Маршрут недоступен." : reason;

            Waypoint waypoint = _router.Navigation.Waypoints.SetOrder(orderId, order.DropOffPosition.Value,
                string.IsNullOrEmpty(order.DropOffTitle) ? "Заказ" : order.DropOffTitle);

            MapPoint player = PlayerPosition();
            NavigationOutcome result = _router.Navigation.NavigateTo(waypoint, player);
            if (!result.Ok) return result.Message;

            var hud = Resolve<HudScreen>();
            RoutePath built = _router.Navigation.ActiveRoute;
            if (hud != null && built != null)
                hud.ShowToast("Маршрут: " + RouteNavigator.FormatDistance(built.TotalDistance));
            return null;
        }

        private static OrderView FindOrder(PhoneSnapshot snapshot, string orderId)
        {
            if (snapshot == null || snapshot.Orders == null) return null;
            if (snapshot.Orders.Active != null && snapshot.Orders.Active.OrderId == orderId)
                return snapshot.Orders.Active;

            var available = snapshot.Orders.Available;
            if (available == null) return null;
            for (int i = 0; i < available.Count; i++)
            {
                if (available[i].OrderId == orderId) return available[i];
            }
            return null;
        }

        private MapPoint PlayerPosition()
        {
            Vector3 position;
            float heading;
            if (worldAdapter != null && worldAdapter.TryGetPlayerPose(out position, out heading))
                return new MapPoint(position.x, position.z);
            return new MapPoint(0f, 0f);
        }

        private void Update()
        {
            if (_router == null) return;

            // Навигация обновляется здесь, а не в каждом экране: маршрут один.
            if (_router.Navigation.ActiveRoute != null)
            {
                MapPoint player = PlayerPosition();
                _router.Navigation.Tick(player, Time.deltaTime);

                Vector3 position;
                float heading;
                if (worldAdapter != null && worldAdapter.TryGetPlayerPose(out position, out heading))
                {
                    // HUD получает свежие инструкции, пока роутер не в модальном окне.
                    if (!_router.IsModalOpen) _router.RefreshSnapshot();
                }
            }
        }
    }

    /// <summary>
    /// Контракт «телефон получает данные из игры». Реализует интегратор проекта:
    /// снимок собирается из PhoneFacade/DeliveryService/кошелька в PhoneSnapshot.
    /// </summary>
    public interface IPhoneDataSource
    {
        PhoneSnapshot ReadPhone();

        /// <summary>Выполнить действие телефона (кроме заказов и маршрута).</summary>
        bool TryRun(string action, out string message);
    }

    /// <summary>
    /// Контракт «действия заказа». Реализует интегратор: DeliveryService.Accept/PickUp/Complete.
    /// </summary>
    public interface IDeliveryUiBridge
    {
        /// <param name="verb">«order.accept», «order.pickup», «order.complete», …</param>
        bool TryRun(string verb, string orderId, out string message);
    }
}
