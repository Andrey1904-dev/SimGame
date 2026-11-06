# Игровые сценарии: цепочки вызовов и места отказа

Каждый сценарий описан как «что вызывается → что должно произойти → где
ломается». Пометка **[UNITY]** = проверить можно только в Unity; в репозитории
для этого нет сцены.

---

## 1. Запуск игры → стартовая сцена → появление персонажа

**Цепочка (как задумано):**
```
Unity загружает стартовую сцену проекта (её имени в репозитории нет)
 ├── CityBootstrap.Awake/Start → GenerateOnStart → Generate()
 │     ├── DistrictMapGenerator.Generate(seed=1904)  → QuarterPlan
 │     ├── QuarterUnityAdapter.Build(plan, transform) → GameObject-ы + коллайдеры
 │     └── PlanBuilt?.Invoke(plan)
 │           ├── PlayerHomeAnchor → точки player.home.door / .exit
 │           └── (PR #14) UiBootstrap.SetDistrictPlan(plan) / чтение IQuarterPlanSource
 ├── PauseController.Awake → PauseView.Create(...) → DontDestroyOnLoad
 └── (PR #14) UiBootstrap.Awake → Install() → экраны + мосты
```
**Что нужно:** сцена с `CityBootstrap`, `PauseController`, камерой, персонажем.
**Место отказа:**
* персонаж в репозитории **не создаётся ничем** — нет ни контроллера игрока, ни
  префаба; их должен дать Unity-проект;
* ~~**P0:** `QuarterUnityAdapter.Build()` обнуляет `_plan`~~ **исправлено `786c7fc`**:
  план теперь фиксируется после очистки (`QuarterPlanSlot.BeginBuild`). Было так:
  `PlanBuilt` приходил,
  но `adapter.Plan` уже `null`; если UI читает план из адаптера (а не из события),
  карта остаётся пустой;
* **[UNITY]** коллайдеры: `AddColliders = true` создаёт их, но слой/маска не
  заданы нигде — персонаж может проваливаться.

---

## 2. Квартира №67 → улица → возвращение

**Цепочка:**
```
Игрок у двери → существующая система взаимодействия проекта (UnityEvent)
  → District750Link.OpenDistrict()      [из сцены квартиры]
      SceneManager.LoadScene(districtSceneName, loadMode)
  → в районе: PlayerHomeAnchor.OpenApartment()
      SceneManager.LoadScene(apartmentSceneName)
```
Точки: `player.home.door = (205; 636.4)`, `player.home.exit = (205; 638.4)`,
дом `bld-n3`, проверено `district_layout_check.py`.

**Место отказа:**
* имена сцен — пустые строки по умолчанию (`returnSceneName = ""`,
  `apartmentSceneName` задаётся вручную). Пока их не задали, кнопка двери ничего
  не делает — в логе `Debug.LogWarning`;
* **[UNITY]** ни одна из сцен в репозитории не существует;
* возврат из района в квартиру не проверялся: `UiRouter` переживает смену сцены,
  но перепривязка мостов в PR #14 — новый код, ни разу не исполнявшийся.

---

## 3. Телефон P → приложение «Заказы» → принятие заказа

**Цепочка (PR #14 + Jobs.Delivery):**
```
UiRouter.Update → ReadKeyboard → P → Request(UiInputAction.Phone)
  → RequestIntent(UiIntent.Phone) → UiStateMachine.Apply → ApplyState
     → phone.SetVisible(true), hud.SetVisibility(Muted), EnterModal()
  → PhoneScreen.Refresh() → DataProvider() (IPhoneDataSource.ReadPhone)
       → DeliveryServiceBridge.ReadPhone()
            → _service.Orders (IDeliveryService)
            → TryGetDeliveryPosition(...) → QuarterPlanAdapter → QuarterPlan
  → игрок нажимает «Принять» → PhoneActions.OrderAccept + orderId
  → PhoneScreen → ActionHandler → UiBootstrap.OnPhoneAction
       → DeliveryPhoneRows.TryParse → verb="accept"
       → DeliveryServiceBridge.TryRun(verb, id)
            → IDeliveryService.Accept(id, clock)
```
**Место отказа:**
1. `DeliveryServiceBridge` находит сервис только через `DeliveryServiceHost` —
   наследника в репозитории **нет**; без него телефон покажет «Система доставки
   не подключена» (честно, но игра не играбельна);
2. заказы никто не регистрирует (`RegisterOrder` только в тестах) → список пуст;
3. `clock/payout/location` адаптеры не реализованы → `CommandsReady == false`,
   команда вернёт «Игровое время не подключено»;
4. Id приложений: телефон UI ищет `orders`, модуль `Phone` заводит `jobs` —
   если подключить модуль, плитка «Заказы» не активируется (P2).

---

## 4. Заказ → маршрут → карта M → HUD

**Цепочка:**
```
PhoneScreen → RouteHandler → UiBootstrap.OnPhoneRoute(orderId)
  → Navigation.Waypoints.SetOrder(orderId, order.DropOffPosition, title)
  → NavigationService.NavigateTo(waypoint, playerPosition)
       → RoutePlanner.Build(graph, start, dest, ...)
            → SnapToNetwork (ребро, радиус 45 м; цель — 90 м)
            → Dijkstra по RoadGraphData (82 ребра)
            → RouteGeometryBuilder → RoutePath (шаги + подход пешком)
  → получен RoutePath → MapScreen.SetRoute → MapCanvasGraphic.Redraw
  → UiRouter.RefreshSnapshot → HUD: метка, расстояние, манёвр
```
**Место отказа:**
* граф берётся из `QuarterPlanAdapter.Build(plan)`; до исправления P0 план был пуст →
  `graph.NodeCount == 0` → «дорожного графа — маршрут не построить»;
* `OnPhoneRoute` отказывает, если `DropOffPosition` нет (нет плана) — правильное
  поведение, но означает, что без плана маршрутов нет вообще;
* **[UNITY]** `MapCanvasGraphic` — один меш на `VertexHelper`; в реальном
  Canvas не проверялся (переполнение `ushort` вершин возможно при зуме).

---

## 5. Получение заказа → доставка → начисление денег

**Цепочка:**
```
PhoneScreen «Забрать» → verb="pickup"
  → IDeliveryService.PickUp(id, clock, locationProvider)
       locationProvider.CurrentLocationId должен совпасть с order.PickUpPoint.Id
       иначе WrongLocation
PhoneScreen «Доставить» → verb="complete"
  → IDeliveryService.Complete(id, clock, location, payout)
       → IPayoutAdapter.TryCredit(operationId, rubles)
       → PayoutGranted = true, PaidRubles = N
  → сообщение игроку: «Заказ закрыт. Оплата: N ₽.»
```
**Место отказа:**
* `ILocationProvider` не реализован — сверка точки невозможна;
* `IPayoutAdapter` не реализован; `PayoutBridge.TryCredit(long)` (legacy)
  **всегда возвращает `false`** — если интегратор возьмёт его, деньги не начислятся
  никогда;
* связь `DeliveryPoint.Id` (`"bakery"`) ↔ `DeliveryPointInfo.Id` (`drop-bld-n3-1`)
  отсутствует: заказ нельзя «привязать» к дому.

---

## 6. Esc → пауза → продолжение

**Цепочка (вариант A — без PR #14):**
```
PauseKeyboardInput.Update (если useStandaloneKeyboard = true)
  → PauseController.HandleEscape() → Open()
       PauseIntegration.AcquireGameplayLock() → отключить ввод
       Time.timeScale = 0; Cursor.visible = true
  → PauseView.Render(snapshot, integration)
Повторный Esc → Close() → вернуть timeScale/cursor, Dispose блокировки
```
**Цепочка (вариант B — с PR #14):**
```
UiBootstrap находит PauseIntegration → router.ReadsKeyboard = false,
router.FallbackTimeScalePause = false
Esc обрабатывает только PauseKeyboardInput → PauseController
Кнопки меню в нашем экране → UiGameBridge.ExecuteMenuAction → integration.Execute(...)
```
**Место отказа:**
* `PauseKeyboardInput.useStandaloneKeyboard` по умолчанию **false** → в варианте A
  пауза не открывается вообще, пока не включат вручную;
* если в проекте есть СВОЙ обработчик Esc и оставить `useStandaloneKeyboard=true`,
  меню откроется дважды (в PR #14 это учтено, в `main` — нет);
* **[UNITY]** `PauseView` строит 721 строку uGUI кодом: раскладка не проверена.

---

## 7. Пауза → настройки → изменение параметров

**Цепочка (PR #14):**
```
PauseMenuScreen кнопка «Настройки» → Execute(action="settings")
  → UiGameBridge.ExecuteMenuAction("settings")
      → PauseIntegration.Execute(PauseAction.Settings)  [если пауза игры есть]
      → иначе UiMenuActions.TryExecute("settings")
          → UiSettings.Volume/Fullscreen → AudioListener.volume / Screen.fullScreen
```
**Место отказа:**
* в `main` кнопка уходит в `PauseIntegration.Execute`, а реализация —
  `StandalonePauseIntegration`, у которой действия выполняются только если в
  инспекторе привязаны `UnityEvent`-ы. Без привязок — «нажал и ничего»;
* в PR #14: `UiSettings` пишет `PlayerPrefs` без версионирования ключей.

---

## 8. Смена сцены → корректное состояние UI

**Цепочка (PR #14):**
```
SceneManager.sceneLoaded
  ├── UiRouter.OnSceneLoaded → если модальное окно открыто: Reset + ApplyState(Gameplay)
  │                            ReleaseAllLocks(); RebindSceneServices()
  └── UiBootstrap.OnSceneLoaded → сброс ссылок → ResolveWorld/Snapshot/Pause/District
                                   → CreateScreens → WirePhone/WireMap
                                   → router.Configure(новые ссылки)
```
**Место отказа:**
* порядок двух обработчиков не задан: `UiRouter` и `UiBootstrap` подписываются в
  разное время, и `RebindSceneServices` может сработать **до** того, как новая
  сцена создала свои адаптеры → `world == null` до следующего кадра;
* **[UNITY]** `UiRouter` в `DontDestroyOnLoad`, экраны — дети бутстрапа. Если
  бутстрап живёт в сцене (а установщик кладёт его именно в сцену), при загрузке
  новой сцены экраны уничтожаются и создаются заново — ссылки у роутера
  обновляются в `OnSceneLoaded`, но кадр между уничтожением и созданием может
  дать `MissingReferenceException` в `HudScreen.Render`.

---

## 9. Сохранение → загрузка → восстановление

**Цепочка в репозитории — ОТСУТСТВУЕТ.** Есть только «снимки» модулей:
```
Economy: ShopSnapshot (+Validator)          — снимок магазина
Jobs:    DeliverySnapshot (+Validator)      — снимок заказов
Debts:   DebtSnapshot (+Validator)          — снимок долгов
Phone:   PhoneAppsSnapshot, PhoneNotificationsSnapshot,
         PhoneMessagesSnapshot, PhoneContactsSnapshot
```
**Чего нет:** ни `SaveService`, ни записи на диск, ни оркестратора, который
соберёт снимки в один файл, ни миграций версий, ни обработки
`OutcomeUnknown` (операция в полёте на момент сохранения).

**Место отказа:** «сохранить игру» в меню паузы **нечем выполнить** — в `main`
это `PauseIntegration` с `UnityEvent`, в PR #14 — `UiMenuActions`, у которой
исполнитель (`IUiGameAction`) должен написать интегратор. Кнопка честно неактивна.

**Рекомендация:** см. `ROADMAP.md`, шаг 4 — «оркестратор сохранений» — это
единственный крупный модуль, которого в проекте нет вовсе.
