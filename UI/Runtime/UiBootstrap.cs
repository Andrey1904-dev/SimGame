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

        /// <summary>
        /// Информационные строки: «так и должно быть» (например, в сцене квартиры
        /// района нет). В отличие от <see cref="Missing"/>, это не дефект настройки
        /// и не делает <see cref="IsComplete"/> ложным.
        /// </summary>
        public readonly List<string> Notes = new List<string>();

        public bool IsComplete => Missing.Count == 0;

        public void Ok(string what) => Connected.Add(what);

        public void Miss(string what) => Missing.Add(what);

        public void Note(string what) => Notes.Add(what);

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
            if (Notes.Count > 0)
            {
                text.Append("; норма: ");
                text.Append(string.Join(", ", Notes.ToArray()));
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
        private GameObject _modalGuard;
        private bool _releaseModalGuardInLateUpdate;
        private bool _hadActiveRouteLastFrame;
        private LifeFromZero.City.Quarter.INotifyQuarterPlanSource _hookedPlanSource;
        private LifeFromZero.City.Pause.StandalonePauseIntegration _hookedStandalonePause;

        /// <summary>Источник данных телефона, выбранный в <see cref="WirePhone"/> (один для данных и действий).</summary>
        private IPhoneDataSource _phoneSource;

        /// <summary>Установка уже выполнена (повторный вызов Install ничего не дублирует).</summary>
        private bool _installed;

        private float _nextDistrictRetry;
        private int _districtRetryCount;

        private const float DistrictRetryInterval = 1f;
        private const int DistrictRetryLimit = 20;

        /// <summary>Активный экземпляр бутстрапа UI.</summary>
        public static UiBootstrap Instance { get; private set; }

        /// <summary>Текущий активный план района (null вне сцены района или до завершения генерации).</summary>
        public QuarterPlan CurrentDistrictPlan => _plan;

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
            Instance = this;
            Install();
        }

        private void Start()
        {
            // CityBootstrap строит район в своём Start(): если в Awake план ещё был null,
            // подхватываем его здесь (и параллельно держим подписку на PlanChanged).
            if (_router != null && _plan == null)
            {
                ResolveDistrict();
                WireMap();
            }
            ResolvePauseOwner();
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookPlanSource();
            UnhookStandalonePause();
            if (_router != null)
            {
                _router.ModalOpenChanged -= OnRouterModalOpenChanged;
                if (Router == _router) Router = null;
            }
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (_router == null) return;

            _releaseModalGuardInLateUpdate = false;
            if (_modalGuard != null) _modalGuard.SetActive(false);

            // ВАЖНО: ссылки на объекты уничтоженной сцены Unity уже считает «пустыми»
            // (== null). Присваивание null убирает мёртвую ссылку из поля, после чего
            // ResolveWorld/ResolveSnapshot ищут адаптеры в НОВОЙ сцене. Живые ссылки
            // (DontDestroyOnLoad) не трогаем: sceneLoaded вызывается и сразу после
            // Awake первой сцены.
            worldAdapter = LiveReference(worldAdapter);
            snapshotSource = LiveReference(snapshotSource);
            deliverySystem = LiveReference(deliverySystem);
            phoneSystem = LiveReference(phoneSystem);
            _deliveryBridge = LiveReference(_deliveryBridge);

            // Телефон: провайдер старой сцены мог быть уничтожен вместе с ней.
            UnityEngine.Object phoneObject = _phoneSource as UnityEngine.Object;
            if ((object)phoneObject != null && phoneObject == null) _phoneSource = null;

            if (districtSource == null)
            {
                districtSource = null;
                UnhookPlanSource();
            }

            var gameBridge = worldAdapter as UiGameBridge;
            if (gameBridge != null) gameBridge.ResolveReferences();

            var pauseSnapshot = snapshotSource as PauseUiSnapshotSource;
            if (pauseSnapshot != null) pauseSnapshot.RefreshSource();

            if (_deliveryBridge != null) _deliveryBridge.ResetSceneCache();

            _existingPause = null;
            _plan = null;
            _mapGeometry = null;
            _router.Navigation.SetGraph(null);

            // В новой сцене район может появиться позже генерации — счётчик попыток
            // начинается заново (иначе в District750 после Apartment67 он уже исчерпан).
            _districtRetryCount = 0;
            _nextDistrictRetry = 0f;

            Report = new UiWiringReport();
            ResolveWorld();
            ResolveSnapshot();
            ResolvePauseOwner();
            ResolveDistrict();
            if (createScreens) CreateScreens();
            WirePhone();
            WireMap();

            // Роутер хранит ссылки на адаптеры: без Configure он остался бы с объектами
            // уничтоженной сцены и не увидел бы новую игру.
            _router.Configure(worldAdapter, snapshotSource, Resolve<PhoneScreen>(), _map,
                Resolve<HudScreen>(), Resolve<PauseMenuScreen>());

            if (logReport) Debug.Log("[UiBootstrap] после смены сцены (" + scene.name + "): " + Report.Summary());
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

        /// <summary>
        /// Живая ссылка на объект Unity или null. Уничтоженный объект сравнивается
        /// с null, но поле продолжает хранить мёртвую ссылку — её надо убрать,
        /// иначе следующий <c>as IInterface</c> вернёт интерфейс к уничтоженному
        /// объекту и обращение к нему даст MissingReferenceException.
        /// </summary>
        private static T LiveReference<T>(T target) where T : UnityEngine.Object
        {
            return target == null ? null : target;
        }

        private void Install()
        {
            // Повторный вызов (например, Ensure() сразу после AddComponent, когда
            // Awake уже выполнил установку) не должен второй раз создавать экраны,
            // перепривязывать мосты и затирать отчёт.
            if (_installed) return;
            _installed = true;

            Report = new UiWiringReport();
            EnsureModalGuard();
            _router = ResolveRouter();
            _router.ModalOpenChanged -= OnRouterModalOpenChanged;
            _router.ModalOpenChanged += OnRouterModalOpenChanged;

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

            // Смена сцен (Apartment67 ↔ District750) приходит сюда: план района и
            // мосты переустанавливаются, а не остаются от старой сцены.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void EnsureModalGuard()
        {
            if (_modalGuard != null) return;
            Transform existing = transform.Find("UI Modal Guard");
            if (existing != null) _modalGuard = existing.gameObject;
            else
            {
                _modalGuard = new GameObject("UI Modal Guard");
                _modalGuard.transform.SetParent(transform, false);
            }
            _modalGuard.SetActive(false);
        }

        private void OnRouterModalOpenChanged(bool isOpen)
        {
            EnsureModalGuard();
            if (isOpen)
            {
                _releaseModalGuardInLateUpdate = false;
                _modalGuard.SetActive(true);
            }
            else
            {
                // Держим ModalGuard активным до конца текущего кадра (LateUpdate),
                // чтобы закрытие телефона/карты по Esc в Update(-50) не открывало
                // PauseKeyboardInput в Update(0) того же самого кадра.
                _releaseModalGuardInLateUpdate = true;
            }
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
            var bridge = worldAdapter as UiGameBridge;
            if (bridge == null) bridge = FindAnyObjectByType<UiGameBridge>();
            if (bridge != null) bridge.ResolveReferences();

            if (bridge != null && bridge.HasExistingPause)
            {
                _existingPause = bridge.ExistingPause;
                _router.FallbackTimeScalePause = false;
                _router.ExternalPauseOwner = true;
                _router.ReadsKeyboard = true; // P и M остаются активными у UiRouter; Esc в игре уступается City.Pause
                _router.IsExternalPauseOpen = () => bridge != null && bridge.IsExistingPauseOpen;
                _router.ReopenExternalPause = () =>
                {
                    var controller = bridge != null ? bridge.ExistingPauseController : null;
                    if (controller != null) controller.Open();
                };

                var standalone = _existingPause as LifeFromZero.City.Pause.StandalonePauseIntegration;
                if (standalone != _hookedStandalonePause)
                {
                    UnhookStandalonePause();
                    if (standalone != null)
                    {
                        EnsureModalGuard();
                        standalone.RegisterCompetingModalRoot(_modalGuard);
                        standalone.SetRuntimeAction(LifeFromZero.City.Pause.PauseAction.Phone,
                            OpenPhoneFromExternalPause, "[P]");
                        standalone.SetRuntimeAction(LifeFromZero.City.Pause.PauseAction.Map,
                            OpenMapFromExternalPause, "[M]");
                        _hookedStandalonePause = standalone;
                    }
                }

                // Экран паузы/HUD рисует данные существующей паузы, а не свои.
                if (snapshotSource == null)
                {
                    var adapter = bridge.GetComponent<PauseUiSnapshotSource>();
                    if (adapter != null)
                    {
                        snapshotSource = adapter;
                        Report.Ok("данные HUD/паузы из PauseDataSource игры");
                    }
                }
                Report.Ok("пауза игры (City.Pause): Esc в игре у City.Pause, P/M и модальные окна у UiRouter");
            }
            else if (bridge != null)
            {
                UnhookStandalonePause();
                _router.ExternalPauseOwner = false;
                _router.FallbackTimeScalePause = true;
                _router.IsExternalPauseOpen = null;
                _router.ReopenExternalPause = null;
                Report.Miss("City.Pause — меню паузы на своём Time.timeScale; " +
                            "клавиши Esc/P/M читает сам UiRouter");
            }
            else
            {
                UnhookStandalonePause();
                _router.ExternalPauseOwner = false;
                _router.FallbackTimeScalePause = true;
                _router.IsExternalPauseOpen = null;
                _router.ReopenExternalPause = null;
                Report.Miss("UiGameBridge — адаптер мира не найден: клавиши P/M/Esc читает сам UiRouter, " +
                            "позиция игрока неизвестна");
            }
        }

        private void UnhookStandalonePause()
        {
            if (_hookedStandalonePause == null) return;
            if (_modalGuard != null) _hookedStandalonePause.UnregisterCompetingModalRoot(_modalGuard);
            _hookedStandalonePause.SetRuntimeAction(LifeFromZero.City.Pause.PauseAction.Phone, null);
            _hookedStandalonePause.SetRuntimeAction(LifeFromZero.City.Pause.PauseAction.Map, null);
            _hookedStandalonePause = null;
        }

        private void OpenPhoneFromExternalPause()
        {
            var bridge = worldAdapter as UiGameBridge ?? FindAnyObjectByType<UiGameBridge>();
            var controller = bridge != null ? bridge.ExistingPauseController : null;
            if (controller != null && controller.IsOpen) controller.Close();
            if (_router != null) _router.OpenFromExternalPause(UiIntent.OpenPhoneFromPause);
        }

        private void OpenMapFromExternalPause()
        {
            var bridge = worldAdapter as UiGameBridge ?? FindAnyObjectByType<UiGameBridge>();
            var controller = bridge != null ? bridge.ExistingPauseController : null;
            if (controller != null && controller.IsOpen) controller.Close();
            if (_router != null) _router.OpenFromExternalPause(UiIntent.OpenMapFromPause);
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
            if (source == null)
            {
                var sources = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                if (sources != null)
                {
                    MonoBehaviour fallbackCandidate = null;
                    for (int i = 0; i < sources.Length; i++)
                    {
                        var candidate = sources[i] as LifeFromZero.City.Quarter.IQuarterPlanSource;
                        if (candidate == null) continue;
                        if (fallbackCandidate == null) fallbackCandidate = sources[i];
                        if (candidate.Plan != null)
                        {
                            fallbackCandidate = sources[i];
                            break;
                        }
                    }
                    if (fallbackCandidate != null)
                    {
                        districtSource = fallbackCandidate;
                        source = fallbackCandidate as LifeFromZero.City.Quarter.IQuarterPlanSource;
                    }
                }
            }

            HookPlanSource(source as LifeFromZero.City.Quarter.INotifyQuarterPlanSource);

            if (source != null) plan = source.Plan;
            if (plan == null) plan = _externalPlan;

            // Три состояния вместо одного «план не подключён» (ТЗ №045):
            //   * в квартире №67 района нет вообще — это НОРМА, пишем Note, а не Miss;
            //   * в District750 источник есть, но генерация ещё идёт — ждём PlanChanged
            //     и пробуем повторно (генератор может строить район в своём Start,
            //     а порядок выполнения у UiBootstrap = -100);
            //   * план есть — подключаем.
            DistrictPlanState state = DistrictPlanDiagnostics.Evaluate(source != null, plan != null);
            string sourceName = source != null && districtSource != null ? districtSource.GetType().Name : null;

            if (plan == null)
            {
                if (DistrictPlanDiagnostics.IsProblem(state)) Report.Miss(DistrictPlanDiagnostics.Message(state, sourceName));
                else Report.Note(DistrictPlanDiagnostics.Message(state, sourceName));
                return;
            }

            ApplyDistrictPlan(plan, true);
            Report.Ok(DistrictPlanDiagnostics.Message(state, sourceName));
            _districtRetryCount = 0;
        }

        /// <summary>
        /// Повторная попытка получить план района. Нужна, когда район строит другой
        /// компонент в своём Start/Awake и он не реализует INotifyQuarterPlanSource
        /// (или появляется в сцене позже бутстрапа). Ограничено, чтобы не работать вечно.
        /// </summary>
        private void RetryDistrictIfNeeded()
        {
            if (_plan != null) return;
            if (_districtRetryCount >= DistrictRetryLimit) return;

            float now = Time.unscaledTime;
            if (now < _nextDistrictRetry) return;

            _nextDistrictRetry = now + DistrictRetryInterval;
            _districtRetryCount++;

            ResolveDistrict();
            if (_plan != null)
            {
                WireMap();
                if (_map != null && _map.isActiveAndEnabled) _map.Refresh(true);
                if (logReport)
                    Debug.Log("[UiBootstrap] План района подключён повторной попыткой (" +
                              _districtRetryCount + " из " + DistrictRetryLimit + ").");
            }
            else if (_districtRetryCount == DistrictRetryLimit && logReport)
            {
                Debug.LogWarning("[UiBootstrap] Район так и не появился за " + DistrictRetryLimit +
                                 " попыток: карта покажет только сетку. Если это District750 — " +
                                 "проверьте CityBootstrap/QuarterUnityAdapter и districtSource.");
            }
        }

        private void HookPlanSource(LifeFromZero.City.Quarter.INotifyQuarterPlanSource notifySource)
        {
            if (ReferenceEquals(_hookedPlanSource, notifySource)) return;
            UnhookPlanSource();
            if (notifySource == null) return;
            notifySource.PlanChanged += OnDistrictPlanChanged;
            _hookedPlanSource = notifySource;
        }

        private void UnhookPlanSource()
        {
            if (_hookedPlanSource != null) _hookedPlanSource.PlanChanged -= OnDistrictPlanChanged;
            _hookedPlanSource = null;
        }

        private void OnDistrictPlanChanged(QuarterPlan plan)
        {
            if (_router == null) return;
            if (plan == null)
            {
                _plan = null;
                _mapGeometry = null;
                _router.Navigation.SetGraph(null);
                if (_map != null)
                {
                    _map.Geometry = null;
                    if (_map.isActiveAndEnabled) _map.Refresh();
                }
                return;
            }

            ApplyDistrictPlan(plan, false);
            WireMap();
            if (_map != null && _map.isActiveAndEnabled) _map.Refresh();
            if (logReport)
                Debug.Log("[UiBootstrap] План района получен (" + plan.LayoutId + ", seed=" + plan.Seed + ").");
        }

        private void ApplyDistrictPlan(QuarterPlan plan, bool recordReport)
        {
            if (plan == null) return;

            QuarterPlanMapData mapData = QuarterPlanAdapter.Build(plan);
            var geometry = new QuarterMapGeometry(mapData);
            if (recordReport)
                Report.Ok("геометрия карты (" + geometry.RoadCount + " дорог, " + geometry.PointCount + " точек)");

            RoadGraphData graph = mapData.Graph;
            if (graph != null && graph.NodeCount > 0)
            {
                _router.Navigation.SetGraph(graph);
                if (recordReport)
                    Report.Ok("дорожный граф маршрутов (" + graph.NodeCount + " узлов, " + graph.EdgeCount + " рёбер)");
            }
            else if (recordReport)
            {
                Report.Miss("дорожного графа — маршрут не построить");
            }

            // Постоянные ориентиры района на карте: Квартира №67 и ПВЗ.
            if (plan.PlayerHome != null)
            {
                _router.Navigation.Waypoints.SetHome(
                    new MapPoint(plan.PlayerHome.EntranceDoor.X, plan.PlayerHome.EntranceDoor.Z),
                    "Квартира №67");
            }
            if (plan.PickupPoint != null)
            {
                _router.Navigation.Waypoints.SetPickup(
                    new MapPoint(plan.PickupPoint.InteractionPoint.X, plan.PickupPoint.InteractionPoint.Z),
                    "ПВЗ");
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

            // Телефон получает данные через делегаты. UI не знает про DeliveryService
            // и PhoneFacade напрямую (иначе появилась бы вторая экономика), поэтому
            // источник ищется по КОНТРАКТУ IPhoneDataSource. До ТЗ №045 здесь были
            // только «UiSnapshotSource» и «DeliveryServiceBridge», из-за чего готовый
            // PhoneFacadeUiBridge в сцене не находился, а поле phoneSystem не читалось
            // вообще — телефон открывался с пустым рабочим столом.
            IPhoneDataSource assigned = phoneSystem as IPhoneDataSource;
            IPhoneDataSource sceneBridge = ResolveScenePhoneSource();
            IPhoneDataSource snapshotBridge = snapshotSource as IPhoneDataSource;

            PhoneSourceChoice choice = PhoneSourceResolver.Choose(
                assigned != null,
                _deliveryBridge != null,
                sceneBridge != null,
                snapshotBridge != null);

            IPhoneDataSource provider = null;
            switch (choice)
            {
                case PhoneSourceChoice.PhoneSystemField:
                    provider = assigned;
                    break;
                case PhoneSourceChoice.DeliveryBridge:
                    // Мост доставки умеет объединять заказы с внешним телефонным мостом
                    // (сообщения, контакты, задачи). Передаём его, чтобы разделы телефона
                    // не пропали только потому, что выбран мост доставки.
                    if (PhoneSourceResolver.NeedsExternalBridge(choice) && sceneBridge != null)
                        _deliveryBridge.UseExternalPhoneDataSource(sceneBridge);
                    provider = _deliveryBridge;
                    break;
                case PhoneSourceChoice.ScenePhoneBridge:
                    provider = sceneBridge;
                    break;
                case PhoneSourceChoice.SnapshotSource:
                    provider = snapshotBridge;
                    break;
            }

            _phoneSource = provider;
            if (provider != null)
            {
                phone.DataProvider = provider.ReadPhone;
                Report.Ok("данные телефона (" + PhoneSourceResolver.Describe(choice) + ")");
            }
            else
            {
                phone.DataProvider = null;
                Report.Miss("IPhoneDataSource — данные телефона (плитки, сообщения, заказы, банк): " +
                            "назначьте phoneSystem в UiBootstrap или добавьте PhoneFacadeUiBridge/DeliveryServiceBridge в сцену");
                Debug.LogWarning("[UiBootstrap] Телефон не подключён к данным: рабочий стол покажет " +
                                 "неактивные плитки. Нужен компонент с IPhoneDataSource " +
                                 "(PhoneFacadeUiBridge из UnityIntegrationKit или DeliveryServiceBridge).");
            }

            phone.ActionHandler = OnPhoneAction;
            phone.RouteHandler = OnPhoneRoute;
        }

        /// <summary>
        /// Компонент сцены, реализующий <see cref="IPhoneDataSource"/> и не являющийся
        /// мостом доставки (его вызывает сам бутстрап отдельно). Так находится
        /// PhoneFacadeUiBridge из UnityIntegrationKit: UI ссылается на интерфейс,
        /// а не на сборку интеграции, поэтому цикла зависимостей нет.
        /// </summary>
        private IPhoneDataSource ResolveScenePhoneSource()
        {
            // Явно назначенное поле (PhoneSystemField) и UiSnapshotSource (SnapshotSource)
            // проверяет сам PhoneSourceResolver — здесь ищем ТОЛЬКО «прочий» мост сцены,
            // например PhoneFacadeUiBridge из UnityIntegrationKit.
            if (phoneSystem is IPhoneDataSource) return null;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            if (behaviours == null) return null;

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour candidate = behaviours[i];
                if (candidate == null || ReferenceEquals(candidate, this)) continue;
                if (ReferenceEquals(candidate, phoneSystem)) continue;
                if (ReferenceEquals(candidate, snapshotSource)) continue;
                if (_deliveryBridge != null && ReferenceEquals(candidate, _deliveryBridge)) continue;

                var source = candidate as IPhoneDataSource;
                if (source != null) return source;
            }
            return null;
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

                // Сначала спрашиваем подключённый источник данных: PhoneFacade умеет
                // отвечать на часть разделов. Раньше ответ «не подключено» выдавался
                // ДО обращения к мосту, и раздел, который был готов, объявлялся чужим.
                IPhoneDataSource data = CurrentPhoneSource();
                string handled;
                if (data != null && data.TryRun(action, out handled)) return handled;

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
                {
                    if (verb == DeliveryPhoneCommands.Complete ||
                        verb == DeliveryPhoneCommands.Cancel ||
                        verb == DeliveryPhoneCommands.Abandon)
                    {
                        _router.Navigation.OnOrderClosed(orderId);
                    }
                    else if (verb == DeliveryPhoneCommands.PickUp &&
                             _router.Navigation.ActiveRoute != null &&
                             _router.Navigation.ActiveRoute.DestinationId == WaypointService.OrderIdPrefix + orderId)
                    {
                        // Груз забран в ПВЗ: автоматически переключаем активный маршрут на адрес вручения.
                        OnPhoneRoute(orderId);
                    }

                    if (snapshotSource != null) snapshotSource.NotifyChanged();
                    else _router.RefreshSnapshot();

                    return string.IsNullOrEmpty(outcome) ? null : outcome;
                }
                return string.IsNullOrEmpty(outcome) ? "Действие с заказом не выполнено." : outcome;
            }

            IPhoneDataSource phoneData = CurrentPhoneSource();
            if (phoneData != null)
            {
                string outcome;
                if (phoneData.TryRun(action, out outcome)) return outcome;
            }

            return null;
        }

        /// <summary>
        /// Источник данных телефона, выбранный в <see cref="WirePhone"/>. Один и тот же
        /// для чтения снимка и для действий: раньше «Заказы» и «маршрут» искали мост
        /// заново и теряли PhoneFacade-мост, объявляя телефон неподключённым.
        /// </summary>
        private IPhoneDataSource CurrentPhoneSource()
        {
            if (_phoneSource != null && IsAlive(_phoneSource)) return _phoneSource;

            _phoneSource = phoneSystem as IPhoneDataSource ?? _deliveryBridge;
            return _phoneSource;
        }

        private static bool IsAlive(IPhoneDataSource source)
        {
            if (source == null) return false;
            UnityEngine.Object unityObject = source as UnityEngine.Object;
            // Не Unity-объект — считаем живым; уничтоженный Unity-объект сравнивается с null.
            return (object)unityObject == null || unityObject != null;
        }

        /// <summary>Мост к доставке: назначен в инспекторе → найден в сцене → нет.</summary>
        private IDeliveryUiBridge ResolveDeliveryBridge()
        {
            if (deliverySystem is IDeliveryUiBridge assigned) return assigned;
            return _deliveryBridge;
        }

        private string OnPhoneRoute(string orderId)
        {
            if (_plan == null)
            {
                ResolveDistrict();
                WireMap();
            }
            if (_plan == null) return "Район не загружен — маршрут недоступен.";
            if (string.IsNullOrEmpty(orderId)) return "Заказ не выбран.";

            IPhoneDataSource phoneData = CurrentPhoneSource();
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

            // Район мог появиться после Start (генерация в другой сцене/позже этого
            // компонента). Дешёвая проверка не чаще раза в секунду и не вечно.
            RetryDistrictIfNeeded();

            bool hasRoute = _router.Navigation.ActiveRoute != null;
            if (hasRoute)
            {
                MapPoint player = PlayerPosition();
                _router.Navigation.Tick(player, Time.deltaTime);

                if (!_router.IsModalOpen)
                {
                    _router.RefreshSnapshot();
                    var hud = Resolve<HudScreen>();
                    if (hud != null && hud.isActiveAndEnabled)
                        hud.Render(_router.Snapshot, _router.Navigation);
                }
                _hadActiveRouteLastFrame = _router.Navigation.ActiveRoute != null;
            }
            else if (_hadActiveRouteLastFrame)
            {
                _hadActiveRouteLastFrame = false;
                _router.RefreshSnapshot();
                var hud = Resolve<HudScreen>();
                if (hud != null && hud.isActiveAndEnabled)
                    hud.Render(_router.Snapshot, _router.Navigation);
            }
        }

        private void LateUpdate()
        {
            if (_releaseModalGuardInLateUpdate)
            {
                _releaseModalGuardInLateUpdate = false;
                if (_modalGuard != null && (_router == null || !_router.IsModalOpen))
                    _modalGuard.SetActive(false);
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
