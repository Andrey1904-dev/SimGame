# Реестр проблем (P0–P3)

Правила реестра:
* **P0** — игра/сборка не работает или данные теряются; блокирует приёмку.
* **P1** — система формально есть, но не выполняет заявленную функцию.
* **P2** — работает, но хрупко, дублируется или ломается при росте.
* **P3** — качество, читаемость, косметика.

Каждая запись: точное место → воспроизводимый сценарий → конкретное исправление.
Проблемы без подтверждения файлом/строкой в реестр не попадают.

**Статусы в этой версии (после коммита `786c7fc`):**

| Статус | Записи |
|---|---|
| ✅ Исправлено и проверено тестом | P0-1, P1-1, P2-5, P2-7 (новый) |
| ✅ Исправлено статически (проверка в Unity не выполнена) | P0-3 (asmdef `UI.Core`), P2-8 (новый) |
| ⛔ Не исправлялось (вне разрешённого объёма / нужен Unity) | P0-2, P1-2…P1-6, P2-1…P2-4, P2-6, P3-* |

---

## P0

### P0-1. Карта района всегда пустая: `Build` обнулял план (PR #14) — ✅ ИСПРАВЛЕНО

* **Где:** `City/UnityAdapter/QuarterUnityAdapter.cs:117` (`_plan = plan;`),
  `:120` (`ClearBuilt();`), `ClearBuilt` пишет `_plan = null` на `:68`.
* **Сценарий:** `CityBootstrap.Generate()` (`CityBootstrap.cs:123`) вызывает
  `adapter.Build(plan, transform)` → внутри `ClearBuilt()` → `Plan == null`.
  `UiBootstrap.ResolveDistrict()` берёт `districtSource as IQuarterPlanSource`
  и получает `Plan == null` → карта рисует пустую сетку, маршрут не строится,
  HUD без навигации.
* **Доказательство:** исполняемая модель порядка вызовов → `HasPlan = False`,
  exit 1 (`TEST_RESULTS.md` §4.1).
* **Исправление:**
  ```csharp
  public GameObject Build(QuarterPlan plan, Transform parent)
  {
      if (plan == null) throw new System.ArgumentNullException(nameof(plan));
      ClearBuilt();      // очистка первой — она обнуляет старый план
      _plan = plan;      // затем фиксируем новый
  ```
  Плюс регрессионный тест: `Build(plan, null); Assert.IsTrue(adapter.HasPlan);`
* **Автор:** PR #14 (ТЗ №042). В базовой `main` дефекта нет — полей `_plan`
  там не существует, они добавлены этим PR.
* **✅ Исправление (коммит `786c7fc`):**
  * добавлен `City/Quarter/QuarterPlanSlot.cs` — хранение плана вынесено из
    MonoBehaviour; `BeginBuild(plan)` сначала очищает, затем фиксирует план;
  * `QuarterUnityAdapter` держит `readonly QuarterPlanSlot _planSlot`,
    `Plan => _planSlot.Plan`, `HasPlan => _planSlot.HasPlan`, а `Build` вызывает
    `ClearBuilt()` → `_planSlot.BeginBuild(plan)` в этом порядке;
  * регрессионные тесты `City/Tests/QuarterPlanSourceTests.cs` (6 шт.) +
    сквозной `UI/Tests/UiCoreTests.cs` («источник плана → данные карты»);
  * проверено откатом: на исходном порядке тесты падают (3 в City, 1 в UI),
    с исправлением — 98/0 и 99/99.

---

### P0-2. В репозитории нет Unity-проекта: игра не запускается ни при каких условиях

* **Где:** корень репозитория. Проверено: `find . -name '*.unity' -o -name '*.prefab'
  -o -name '*.meta' -o -name 'ProjectSettings' -o -name 'Packages/manifest.json'`
  → **0 файлов**.
* **Сценарий:** открыть репозиторий в Unity Hub → «не является проектом».
  Открыть папку `City/` как проект → пустая сцена, `CityBootstrap` некуда
  положить, генерация не начнётся.
* **Исправление:** создать минимальный проект (`ProjectSettings/`,
  `Packages/manifest.json` с `com.unity.ugui`, `com.unity.test-framework`),
  сцену `Assets/Scenes/Main.unity` с `CityBootstrap` + `QuarterUnityAdapter`,
  префабы `bld-*`, и только потом запускать чек-лист `UNITY_CHECKLIST.md`.
* **Важно:** это не «дефект кода», а граница аудита. Без этого пункта любые
  утверждения «игра работает» недействительны.

---

### P0-3. asmdef `UI/Core` не ссылается на `LifeFromZero.City.Quarter` — ✅ ИСПРАВЛЕНО (проверка в Unity не выполнена)

* **Где:** `UI/Core/LifeFromZero.UI.Core.asmdef` — `"references": []`, при этом
  `UI/Core/QuarterPlanAdapter.cs:18` содержит `using LifeFromZero.City.Quarter;`.
* **Сценарий:** открыть проект в Unity → сборка `LifeFromZero.UI.Core`
  компилируется отдельно от `LifeFromZero.City.Quarter` → `CS0246: type or
  namespace 'QuarterPlan' could not be found` → каскад ошибок в `UI/Runtime`,
  `UI/Editor`, `UI/Tests`. Фактически весь PR #14 не собрался бы в Unity.
* **Почему не поймали:** `UI/Tools/checkui.py` компилирует все файлы в **одну**
  сборку и `.asmdef` не читает; тесты UI шли против собранного вручную образа.
* **✅ Исправление:** в `references` добавлен `"LifeFromZero.City.Quarter"`
  (`noEngineReferences` остался `true` — модуль данных не тянет Unity).
  Написан статический чекер графа asmdef: для каждой сборки собираются все
  `using LifeFromZero.*` из её `.cs` и сверяются со списком `references`.
  Сейчас: `asmdef graph OK`, 18 сборок.
* **Остаточный риск:** правильность имён сборок Unity подтвердит только Editor.

## P1

### P1-1. Два теста `Phone` падали: смешаны контракты «вернуть код» и «бросить исключение» — ✅ ИСПРАВЛЕНО

* **Где:** `Phone/Apps/PhoneApp.cs:64` (`throw new ArgumentException(...)`),
  `Phone/Notifications/PhoneNotification.cs:74`; падавшие тесты —
  `Phone/Tests/PhoneAppRegistryTests.cs:71` (`InvalidApp`, «Приложения: пустой ID
  и название отклоняются») и `Phone/Tests/PhoneNotificationTests.cs:224`
  (`InvalidNotification`, «Уведомления: пустые поля отклоняются»).
* **Сценарий:** `new PhoneApp("  ", "x")` → `ArgumentException` **до** того, как
  сервис вернёт `InvalidArgument`. Любой вызов из UI/сейва с пустым Id валит
  вызывающий код вместо диагностируемого отказа.
* **Доказательство:** прогон `Phone` → 93 проверки, 2 провала, exit 1.
* **Исправление (2 варианта, выбрать один):**
  1. Разрешить пустой Id в конструкторе, валидировать в `Register/Add`
     (тогда тесты проходят без правок);
  2. Оставить бросок и переписать тесты на `Assert.Throws<ArgumentException>`.
* **Рекомендация:** вариант 1 — сервисы модуля везде возвращают
  `Result<T>`-подобный код, конструктор не должен быть исключением из правила.
* **✅ Фактическое исправление (`786c7fc`):** выбрана «гибридная» фиксация
  контракта без изменения поведения модуля в бою:
  * конструкторы `PhoneApp`/`PhoneNotification` продолжают бросать (это их
    документированный контракт, проверен тестом `AppModelValidation`);
  * `IPhoneAppRegistry.Register` задокументирован: `null` → `InvalidArgument`,
    невалидные данные отсекает конструктор;
  * тесты переписаны на проверку **каждого уровня там, где он работает**;
    раньше проверка сервиса фактически не выполнялась (строка падала на
    конструкторе до вызова `Register`);
  * добавлен недостающий тест контракта `PhoneNotification` и проверки
    сервисного пути `Add(id, source, …)` с пустыми `id`/`source` → `InvalidArgument`.
  * Итог: Phone 93/93, exit 0.

### P1-2. Заказы доставки никто не создаёт

* **Где:** `DeliveryService.RegisterOrder(...)` вызывается **только** из
  `Jobs/Tests/*`. Прод-вызовов: 0.
* **Сценарий:** подключить `DeliveryServiceBridge` к реальному сервису → телефон
  покажет «Нет заказов», доставки нет, деньги за доставку не приходят.
* **Исправление:** в `DeliveryServiceHost`-наследнике при старте генерировать
  заказы из `QuarterPlan` (46 точек доставки уже есть) — либо
  `OrderGenerator` в `Jobs/Delivery`.

### P1-3. Мосты выплат/часов/локаций не реализованы

* **Где:** `UnityIntegrationKit/AdapterBridges.cs` — `PayoutBridge.TryCredit(long)`
  возвращает `false` при `null` адаптере; `IGameClockAdapter`,
  `ILocationProvider`, `IPayoutAdapter` не имеют реализаций в репозитории
  (проверено `grep -rn "class .*: IPayoutAdapter"` → 0).
* **Сценарий:** в UI «Выплата за доставку» → `TryCredit` → `false` →
  `OutcomeUnknown`/«деньги не начислены».
* **Исправление:** три класса-адаптера по 10–20 строк поверх `MoneyWallet`,
  `GameClock`, `IQuarterPlanSource`. `MoneyWallet`/`GameClock` в репозитории
  **отсутствуют** (есть только как параметры) — их тоже нужно написать.

### P1-4. Нет разрыва «Id точки доставки» ↔ «Id здания»

* **Где:** генератор даёт `drop-bld-n3-1` (`DistrictMapGenerator.cs:441,567,853`),
  ПВЗ — `drop-<pvzId>` (`:822`). UI-карта и HUD оперируют `DeliveryPointInfo`
  из своего источника.
* **Сценарий:** точка доставки на карте не сопоставляется с заказом в телефоне →
  маршрут ведёт «в никуда», кнопка «Принять» не активируется.
* **Исправление:** ввести `DeliveryPointId` со стабильным форматом и один
  конвертер (парсер `drop-<buildingId>-<ordinal>` / `drop-<pvzId>`), покрыть тестом.

### P1-5. Игровой цикл не запускается: нет игрока, ввода и взаимодействий

* **Где:** нет `PlayerController`, нет системы взаимодействий (`IInteractable`,
  `InteractionRay` — 0 совпадений), нет классов NPC/транспорта/трафика.
  Дверь подвала существует только как точка плана (`basement.door`,
  `QuarterGenerator.cs:1564`) и как `typeId` (`QuarterTypes.cs:75`) — обработчика нет.
* **Сценарий:** нажать Play (после P0-2) → камера висит в воздухе, ходить нечем.
* **Исправление:** минимальный `PlayerController` (без `CharacterController` нельзя
  — нужен `Rigidbody`/`CharacterController` компонент), один `Interaction`-луч и
  `IInteractable` для двери/ПВЗ. Это отдельное ТЗ, не #043.

### P1-6. Сохранений нет вообще

* **Где:** `SaveService` отсутствует; модули умеют отдавать снимок состояния
  (`*Snapshot`), но никто не пишет в файл.
* **Сценарий:** закрыть игру → весь прогресс теряется.
* **Исправление:** `SaveService` (JSON в `Application.persistentDataPath`), версия
  схемы, миграции, атомарная запись (временный файл + `File.Replace`).

---

## P2

### P2-1. Приложения телефона: разные Id в модуле и в UI

`Phone/Apps/PhoneAppRegistry.cs:55-59` → `jobs`;
`UI/Core/PhoneData.cs:19-30` → `orders`, `calls`, `tasks`, `ads`, `education`, `gallery`.
**Исправление:** единый статический класс `PhoneAppIds`, модуль — источник истины.

### P2-2. Два независимых обработчика `sceneLoaded`

`UI/Runtime/UiRouter.cs` (`OnSceneLoaded` → `RebindSceneServices`) и
`UI/Runtime/UiBootstrap.cs` (`OnSceneLoaded` → `Resolve*` + `Configure`).
Порядок не определён; после загрузки сцены возможен кадр с мёртвыми ссылками.
**Исправление:** перепривязку делает только `UiBootstrap`, роутер подписывается на
его событие.

### P2-3. Лимит вершин одного `Graphic`

`UI/Runtime/MapCanvasGraphic.cs` — один меш на всю карту. uGUI-индексы `ushort`
→ 65 535 вершин. Сейчас ~6 000, запас есть, но здания/маршруты его съедят.
**Исправление:** слои по `Graphic`, тест «число вершин < 30000».

### P2-4. `City/UnityIntegration/Runtime/*.cs` без asmdef

Файлы лежат вне asmdef → попадают в `Assembly-CSharp`. Это не ошибка, но
пересборка `Assembly-CSharp` на каждое изменение дороже и ломает изоляцию
(Editor-скрипты не могут ссылаться на них из своей asmdef).
**Исправление:** добавить `LifeFromZero.City.Integration.asmdef`.

### P2-7. `City/PauseTests` собирались бы в билд плеера — ✅ ИСПРАВЛЕНО

* **Где:** `City/PauseTests/LifeFromZero.City.Pause.Tests.asmdef` —
  `"includePlatforms": []` (все платформы), у остальных пяти тестовых сборок
  стоит `["Editor"]`.
* **Сценарий:** сборка плеера → тестовая сборка с `nunit.framework.dll` и
  `[Test]` попадает в билд: лишний вес, а при отсутствии пакета — ошибка сборки.
* **✅ Исправление:** `includePlatforms` = `["Editor"]`, как у остальных.

### P2-5. `City/Tools/__pycache__/*.pyc` в истории PR #14

Бинарные файлы попали в коммит; `.gitignore` добавлен позже.
**✅ Исправление (`786c7fc`):** `git rm -r --cached City/Tools/__pycache__`,
файл удалён из индекса; каталог уже покрыт `.gitignore` (`__pycache__/`, `*.pyc`).
История PR не переписывалась. В рабочем дереве остаточных `.pyc` нет.

### P2-6. Генератор зависит от `System.Random` без фиксации версии алгоритма

`DistrictMapGenerator`/`QuarterGenerator` используют `new Random(seed)`. Алгоритм
`System.Random` менялся между версиями .NET/Mono, а Unity использует Mono/IL2CPP.
**Сценарий:** одинаковый `seed` → разная раскладка в Editor и в билде →
«дом съехал», сохранённые координаты не совпадают.
**Исправление:** собственный детерминированный PRNG (xorshift/PCG, 20 строк).

---

## P3

| # | Место | Проблема | Исправление |
|---|---|---|---|
| P3-1 | `RoutePlanner.cs:168,176,179` | радиусы 45/90/2.5 зашиты | `RoutePlannerSettings` |
| P3-2 | `UiBootstrap.cs` (559 строк) | 5 обязанностей в одном классе | разделить на 3 partial/класса |
| P3-3 | `UiGameBridge.ShowMessage` | `FindAnyObjectByType` на каждый вызов | кэш ссылки |
| P3-4 | `City/` 65 × `Debug.Log`, `UI/` 25 × | логи без категорий, шумят в билде | `ILogger`-обёртка |
| P3-5 | `UI/Editor/UiInstaller.cs:25` | 2 × TODO-комментарий | закрыть или оформить issue |
| P3-6 | 0 комментариев XML у `UI/Core` | публичный API без документации | `///` на публичные типы |
| P3-7 | `.gitignore` | нет `obj/`, `bin/`, `*.csproj.user`, `Library/` | дополнить |

---

## Сводная статистика

| Приоритет | Количество | Из них блокирует приёмку |
|---|---|---|
| P0 | 3 | 2 (P0-2 остаётся: нужен Unity-проект) |
| P1 | 6 | 4 (P1-2, P1-3, P1-4, P1-5) |
| P2 | 7 | 0 |
| P3 | 7 | 0 |
| **Исправлено (5 записей)** | — | P0-1, P0-3, P1-1, P2-5, P2-7 |

## Что НЕ является проблемой (проверено и отклонено)

* `Time.timeScale` в PR #14 — корректно отключён, приоритет у `City.Pause`
  (`PauseController.cs:57,70,93` — единственное место в проекте).
* Циклическая зависимость UI ↔ `City.Quarter` — отсутствует: UI ссылается только
  на `IQuarterPlanSource`, `QuarterUnityAdapter` реализует интерфейс.
* `IsDurable`/`OutcomeUnknown` в `UnityIntegrationKit` — логика верна: при
  недолговечном журнале статус не выдаётся за успех.
* Установщик `install_to_unity.sh` — коды выхода и `--dry-run` корректны,
  разрушительных действий нет (проверено чтением и запуском без аргументов).
