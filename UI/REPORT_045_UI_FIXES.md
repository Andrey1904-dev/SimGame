# ТЗ №045 — отчёт: исправление ошибок UI после реального запуска Unity

Репозиторий: `Andrey1904-dev/SimGame`. Unity 6000.3.27f1, URP 17.3.0.
Ветка: `arena/0e907219-simgame`. PR в `main` **не слит** — ждёт проверки.

> **Что здесь есть и чего здесь нет.**
> Всё, что ниже, проверено **вне Unity**: по-сборочная компиляция Roslyn (17 `.asmdef` +
> `Assembly-CSharp` + `Assembly-CSharp-Editor` в трёх профилях ввода), 122 UI-теста логики,
> статическая проверка контрактов UI. Это **не** Play Mode: Unity Editor в песочнице нет,
> сцен `Apartment67`/`District750` в репозитории нет. Пиксели, uGUI-рендер, `MissingComponentException`
> и Missing Script можно подтвердить только в вашем локальном проекте — инструкция в `CODEX_FIX_UI.md`.

---

## 1. Диагностика: причина каждой ошибки

Аудит не повторялся. Смотрели только UI/Runtime, UI/Core, UI/Editor, `PhoneUiBridge`,
`SceneUiBinding` и связку с City.

### 1.1. «P открывает телефон, приложений нет» + NRE источника данных

**Причина (найдена в коде, не предположение).** Рабочий стол телефона рисуется из
`PhoneSnapshot.Apps`, а снимок отдаёт `IPhoneDataSource`, назначенный в `PhoneScreen.DataProvider`.
В `UiBootstrap.WirePhone()` (до правки) было ровно два источника:

```csharp
IPhoneDataSource provider = snapshotSource as IPhoneDataSource;
if (provider == null) provider = _deliveryBridge;
```

и всё. Следствия:

* готовый мост `UnityIntegrationKit/PhoneUiBridge.cs` (`PhoneFacadeUiBridge` реализует
  `IPhoneDataSource`) **не искался в сцене вообще** — он не `UiSnapshotSource`;
* сериализованное поле `UiBootstrap.phoneSystem` («объект с телефоном») **не читалось нигде**:
  единственное упоминание — пустая проверка `if (deliverySystem == null && phoneSystem == null) { }`;
* если ни один из двух путей не срабатывал, `phone.DataProvider` оставался `null`,
  `PhoneScreen._data` — пустой `new PhoneSnapshot()` (в поле `Apps` — `Array.Empty`),
  и `RenderHome()` показывал текст-заглушку **«Приложения не подключены. Регистрация:
  PhoneAppRegistry.CreateDefault().»**;
* сама подсказка **неверна**: `PhoneAppRegistry` — реестр модуля `LifeFromZero.Phone`,
  он не заполняет `PhoneSnapshot.Apps`. Интегратор шёл по ложному следу.

NRE «источника данных» — из того же места: `PhoneScreen.Refresh()` ловит исключение
провайдера (`catch (Exception)` + `Debug.LogError`), но при `DataProvider == null`
сообщения об этом не было — только пустой экран с вводящим в заблуждение текстом.

### 1.2. «M не открывает карту»: MissingComponentException CanvasRenderer у Grid и Route

**Причина.** `MapScreen.BuildShell()` создавал слои так:

```csharp
RectTransform gridLayer = UiBuilder.Rect("Grid", _viewport, 0f, 0f, 1f, 1f); // только RectTransform
_graphic = gridLayer.gameObject.AddComponent<MapCanvasGraphic>();             // Graphic
```

`MapCanvasGraphic` — `MaskableGraphic`. uGUI рисует только ту графику, у которой есть
`CanvasRenderer`; без него каждый `Rebuild` канваса падает. Именно поэтому ошибка
**повторялась сотни раз**: любой `SetVerticesDirty()` (а `MapScreen.Refresh()` вызывался
на каждое открытие/перестроение карты и на `Tick()`) снова и снова ставил слой в очередь
перестройки, и каждый проход падал.

Сцена давала второй источник того же дефекта: если объект `Grid`/`Route` с
`MapCanvasGraphic` **сохранился в сцене** (например, его создал локальный
`Assets/_Project/Scripts/Runtime/SceneUiBinding.cs`, или компонент когда-то был добавлен
до того, как класс стал `Graphic`), то `[RequireComponent]` у него уже не выполнялся —
рендерера у сохранённого объекта нет. В репозитории таких объектов быть не может:
`MapScreen` создаёт оба слоя в `Awake`, а установщик сцены их не сохраняет, — значит
объект добавил локальный код/сцена.

### 1.3. NRE в `Assets/_Project/Scripts/Runtime/SceneUiBinding.cs:18`

**Файла нет в репозитории.** Проверено поиском по всем `.cs`, `.md`, `.py`, `.sh`, `.asmdef`
и по истории: строки `SceneUiBinding` в `SimGame` нет ни в одной ревизии. Путь
`Assets/_Project/Scripts/Runtime/` — это локальный проект, а не структура репозитория
(модули копируются в `Assets/UI`, `Assets/City`, …). Содержимое файла **не выдумывалось**:
он не изменялся и не переписывался. Что с ним делать — отдельно и точно в `CODEX_FIX_UI.md`
(раздел 3): сначала прочитать строку 18, потом решить «удалить дубликат» либо «передать
данные в штатные точки»; для второго случая ниже описан готовый контракт.

### 1.4. Missing Script на объекте сцены

В репозитории нечего «починить»: ни одного `.unity`/`.prefab`/`.meta` здесь нет
(это подтверждено и предыдущим аудитом). Missing Script появляется, когда в сцене
сохранён компонент, чей скрипт удалён/переименован/не скомпилировался. Наиболее вероятные
кандидаты в этом проекте:

* компонент локального `SceneUiBinding` (после переименования/удаления файла);
* UI-компонент, который не скомпилировался (тогда в консоли рядом есть `error CS…`);
* компонент, добавленный в сцену до появления класса в текущей сборке.

Вместо угадывания добавлена диагностика: `UiInstaller` теперь **перечисляет** объекты
с Missing Script (путь в иерархии + номер слота) и **не удаляет** их автоматически —
решение за владельцем сцены.

### 1.5. «UiBootstrap: план района не подключён»

**Причина — одно сообщение на три разных ситуации.** Было:

```csharp
if (plan == null) { Report.Miss("плана района (QuarterPlan) — ожидание генерации или сцена без района"); return; }
```

* **Apartment67 — это норма.** Района в сцене нет, карта показывает сетку. Сообщение
  при этом попадало в `Missing` («НЕ подключено»), то есть выглядело дефектом.
* **District750 — план появляется позже.** `UiBootstrap` имеет `[DefaultExecutionOrder(-100)]`,
  его `Start()` выполняется раньше `QuarterUnityAdapter.Start()`/`CityBootstrap`
  (генерация района). План подхватывался только через `INotifyQuarterPlanSource.PlanChanged`;
  если источник появлялся в сцене позже или не реализовывал уведомление — план не
  подхватывался никогда, до перезагрузки сцены.

Теперь три состояния (`DistrictPlanState`) + ограниченный повтор (`1 раз в секунду, до 20 попыток`),
сбрасываемый при смене сцены.

### 1.6. Жизненный цикл и повторная инициализация

Найдено и исправлено:

* `UiBootstrap.Ensure()` создавал компонент (его `Awake()` уже выполнял `Install()`) и
  сразу вызывал `created.Install()` **второй раз** — повторное создание экранов,
  повторный `Configure`, затирание отчёта. Добавлен флаг `_installed`.
* `UiSnapshotSource`/`UiWorldAdapter`/`IPhoneDataSource` старой сцены могли остаться
  «мёртвыми ссылками» (Unity `== null`, но поле не пустое). Теперь они нормализуются
  через `LiveReference(...)` и переискиваются в новой сцене; провайдер телефона
  сбрасывается, счётчик попыток района обнуляется.
* `UiBootstrap.OnSceneLoaded` содержал бессмысленные строки `if (x == null) x = null;`
  с комментарием «не обнуляем живые ссылки» — заменены на явную нормализацию.

---

## 2. Исправленные файлы

### Новые (UI/Core — чистый C#, проверяется тестами)

| Файл | Что решает |
|---|---|
| `UI/Core/MapRedrawGate.cs` | `MapFrameKey` + `MapRedrawGate` + `RouteSignature`: перерисовка карты **только при изменении** видимых данных |
| `UI/Core/PhoneHomeState.cs` | что показать на рабочем столе: данные / заглушки + честный текст (без вранья про `PhoneAppRegistry`) |
| `UI/Core/UiWiringDecisions.cs` | `PhoneSourceResolver` (порядок выбора источника телефона) и `DistrictPlanDiagnostics` (3 состояния района) |

### Изменённые

| Файл | Правка |
|---|---|
| `UI/Runtime/MapCanvasGraphic.cs` | явные `[RequireComponent(typeof(CanvasRenderer))]`/`(RectTransform)`, `Awake()` проверяет рендерер и **отключает слой** с одним сообщением, `HasCanvasRenderer`, `Redraw()` не трогает `SetVerticesDirty` без рендерера |
| `UI/Runtime/UiBuilder.cs` | `GraphicLayer<T>()` / `AttachGraphic<T>()`: создание слоя графики с проверкой и добавлением `CanvasRenderer` |
| `UI/Runtime/MapScreen.cs` | `Grid`/`Route` создаются только через `UiBuilder.GraphicLayer`; `Refresh(bool force)` через `MapRedrawGate`; null-защита слоёв с однократным сообщением |
| `UI/Runtime/PhoneScreen.cs` | рабочий стол через `PhoneHomeState.Resolve`; текст «Приложения не подключены…» убран с экрана; однократное предупреждение в консоль с правильной причиной |
| `UI/Runtime/UiBootstrap.cs` | поиск `IPhoneDataSource` в сцене + чтение `phoneSystem`; единый `_phoneSource` для данных/действий/маршрута; `app.*` больше не отвечает «не подключено» до обращения к мосту; `Note()` в отчёте; `DistrictPlanDiagnostics` + повтор получения плана; `_installed`; нормализация ссылок при смене сцены |
| `UI/Runtime/DeliveryServiceBridge.cs` | `UseExternalPhoneDataSource(IPhoneDataSource)` — мост доставки объединяет заказы с телефонным мостом при автоподключении |
| `UI/Editor/UiInstaller.cs` | диагностика и починка: `RepairGraphicRenderers` (Graphic без `CanvasRenderer`), `ReportMissingScripts`, `ReportStaleMapLayers`; меню `Tools → Life From Zero → UI → Repair UI objects in open scenes`; `RepairBatch` (только с `-lfzUiRepair`, с бэкапом сцен); автоподключение `phoneSystem`; всё это попадает в `Verify Scenes & Adapters` |
| `UI/Tests/UiTestSuite.cs`, `UI/Tests/UiRegression045Tests.cs` | 19 регрессионных тестов (см. §3) |
| `UI/Tools/check_graphic_layers.py` | статическая проверка контрактов: `CanvasRenderer` у слоёв, гейт перерисовки, источник телефона |
| `UI/Tools/unitystubs/UnityStubs.cs` | в стабы добавлены `CanvasRenderer`, `Time.unscaledTime`, generic `AddComponent`/`GetComponents`, `RequireComponent(AllowMultiple)` — без этого проверки не видели новых типов |
| `AUDIT/TEST_RESULTS.md`, `UI/Tools/README.md` | числа и список проверок приведены к факту |

Не тронуто: HUD (`HudScreen`), экономика, курьерские механики, `PauseMenuScreen`,
`UiRouter` (кроме подтверждённой совместимости), сцены и `SceneUiBinding` (его нет в репозитории).

---

## 3. Регрессионные проверки

### 3.1. Тесты логики (19 новых в `UI/Tests/UiRegression045Tests.cs`)

| Дефект | Тест |
|---|---|
| Сотни перестроек/исключений карты | «повторный Refresh без изменений НЕ перерисовывает меш», «изменение зума/центра/сетки/геометрии/маршрута/игрока перерисовывает», «дробное смещение игрока внутри полуметра не перерисовывает», «Reset() перерисовывает», «подпись маршрута зависит от маршрута» |
| Пустой рабочий стол телефона | «без источника — неактивные заглушки и честный текст», «подсказка про PhoneAppRegistry больше не выдаётся», «данные пришли — ни заглушек, ни сообщения», «источник есть, список пуст — отдельное сообщение», «заглушки содержат все приложения референса», «плитки в порядке рабочего стола, map отбрасывается» |
| Неподключённый `PhoneFacadeUiBridge` | «поле phoneSystem важнее автопоиска», «мост доставки важнее прочих (объединяет источники)», «PhoneFacadeUiBridge находится без доставки», «UiSnapshotSource — последний вариант, None — честный» |
| «План района не подключён» в квартире | «сцена без района — норма», «источник есть, плана нет — ждём генерацию», «план получен — готово», «три состояния дают три разных сообщения» |

### 3.2. Статическая проверка `UI/Tools/check_graphic_layers.py`

Ловит повторное появление дефекта в исходниках: `MapCanvasGraphic` без
`RequireComponent(CanvasRenderer)`, создание слоя карты в обход `UiBuilder.GraphicLayer`,
`Refresh` в обход `MapRedrawGate`, возврат текста «Приложения не
подключены / Регистрация: PhoneAppRegistry», отсутствие поиска `IPhoneDataSource`
и диагностики района в `UiBootstrap`, отсутствие починки в `UiInstaller`.

### 3.3. Результаты прогонов (в этой песочнице)

```
python3 UI/Tools/checkui.py            COMPILE: OK errors=0
python3 UI/Tools/checkeditor.py        COMPILE: OK errors=0
python3 UI/Tools/check_graphic_layers.py   GRAPHIC-LAYERS: OK
python3 UI/Tools/runtests.py           Итого: 121/122 PASS, 1 FAIL
python3 AUDIT/tools/check_unity6_compile.py
   [PASS] InputSystem / LegacyInput / Both — 17 .asmdef + Assembly-CSharp + Assembly-CSharp-Editor, 0 ошибок
python3 AUDIT/tools/check_docs_consistency.py   Документация согласована с кодом
```

**Про единственный FAIL — честно.** Падает **старый** тест «Телефон: приложения «Карта» в
телефоне нет (§2, §4.8)», и только в этой песочнице: здесь .NET Core 3.1 без ICU
(`System.Globalization.Invariant`), поэтому `"Карта".ToLowerInvariant()` возвращает
`"Карта"` (в Unity с ICU — `"карта"`). Проверено отдельным прогоном:
`OrdinalIgnoreCase` в этом режиме тоже не работает (касается только кириллицы).
Код исправления тут ни при чём; в Unity-проекте тест зелёный.

### 3.4. Чего эти проверки НЕ доказывают

* не доказывают, что `CanvasRenderer` действительно появился в сцене и uGUI рисует карту;
* не доказывают, что телефон в Play Mode показывает плитки (для этого нужен `IPhoneDataSource`
  в сцене — теперь он находится автоматически, но сцена проверяется только в Unity);
* не заменяют `MissingComponentException`/Missing Script в консоли — их закрывает
  запуск `Verify Scenes & Adapters` и Play Mode у вас.

---

## 4. Что нельзя исправить без локальных сцен и Unity Editor

1. **Сам `SceneUiBinding.cs`** (`Assets/_Project/Scripts/Runtime/SceneUiBinding.cs:18`).
   Файла нет в репозитории, его содержимое неизвестно. Требуется прочитать строку 18
   в локальном проекте и выбрать: удалить (если он дублирует `UiBootstrap`) или
   переписать на штатные точки подключения (`UiBootstrap.phoneSystem`, `SetDistrictPlan`,
   `phone.DataProvider`). Пошагово — `CODEX_FIX_UI.md` §3.
2. **Missing Script на конкретном объекте.** Нужно имя объекта из консоли; диагностический
   отчёт покажет путь и слот компонента, но назначить/удалить компонент должен владелец сцены.
3. **Состояние `Grid`/`Route` в сохранённой сцене.** Починка (`Repair UI objects`) выполняется
   в Unity: пересоздание компонента, сохранение сцены, проверка глазами. Автоматически
   применять к вашим сценам отсюда нельзя.
4. **Факт открытия карты по M и вид карты** (порядок батчей, цвета, обрезка `RectMask2D`,
   отсутствие второго `EventSystem`) — только Play Mode.
5. **Существует ли в сцене источник района** и какой именно (`QuarterUnityAdapter`/
   `CityBootstrap`/локальный адаптер) — это состояние вашего проекта; в репозитории
   только контракт `IQuarterPlanSource`.
6. **Переход Apartment67 ↔ District750 и отсутствие второй инициализации** — проверяется
   загрузкой обеих сцен в редакторе (`UiBootstrap` живёт через `DontDestroyOnLoad`,
   сцены в репозитории отсутствуют).

---

## 5. Ожидаемое поведение после исправлений

| Ситуация | Ожидание |
|---|---|
| Apartment67, P | телефон открывается; при подключённом `PhoneFacadeUiBridge`/`DeliveryServiceBridge` — плитки, иначе неактивные плитки и текст «Телефон не подключён к данным игры», в консоли одно предупреждение с правильной причиной |
| Apartment67, M | карта открывается, сетка (района нет — так и должно быть), без `MissingComponentException` |
| District750, M | карта рисует район; при поздней генерации план подхватывается по `PlanChanged` или повтором (до 20 с), в отчёте `UiBootstrap` — `план района (QuarterUnityAdapter)` |
| Смена сцены | второго `UiRouter`/экранов не создаётся, отчёт пересобирается, телефон переподключается к источникам новой сцены |
| Сохранённый в сцене `Grid`/`Route` без рендерера | в консоли **одно** сообщение вместо сотен; `Verify Scenes & Adapters` показывает объект; `Repair UI objects` пересоздаёт компонент |
