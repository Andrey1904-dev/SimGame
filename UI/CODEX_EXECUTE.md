# UI: что готово, что подключить, как проверять (ТЗ №042)

Документ для того, кто будет доводить интерфейсы в настоящем Unity-проекте.
Читается сверху вниз; каждый шаг заканчивается проверяемым результатом.

> **Главное, что нельзя путать.** Всё, что ниже, проверено **вне Unity**:
> компиляцией (`checkui.py`, Roslyn) и тестами (`runtests.py`, 92 теста). Это
> доказывает логику, но **не** доказывает, что UI выглядит правильно в игре.
> Скриншоты, шрифты, порядок батчей, клики по uGUI — не проверены. Пока в проекте
> не запущена сцена, интерфейс считается **непроверенным**, и в отчётах так и надо
> писать.

---

## 1. Что уже есть (и работает без Unity)

| Область | Файлы | Состояние |
|---|---|---|
| Состояния экранов, роутер | `UI/Core/UiStateMachine.cs`, `UI/Runtime/UiRouter.cs` | готово, тесты |
| Карта: данные, граф, маршруты | `UI/Core/MapCalibration*.cs`, `RoadGraphData.cs`, `RoutePlanner.cs`, `RouteGeometryBuilder.cs` | готово, тесты |
| Карта: отрисовка мешем | `UI/Runtime/MapCanvasGraphic.cs`, `MapScreen.cs` | **не проверено в Unity** |
| Навигация, HUD-подсказки | `UI/Core/NavigationService.cs`, `RouteNavigator` | готово, тесты |
| Мост «район → карта» | `UI/Core/QuarterPlanAdapter.cs`, `UI/Runtime/QuarterMapGeometry.cs` | готово, тесты |
| Мост к игре | `UI/Runtime/UiGameBridge.cs` (`UiGameBridge`, `PauseUiSnapshotSource`) | готов; **проверен только стабами** |
| Телефон (экраны, данные) | `UI/Core/PhoneData.cs`, `UI/Runtime/PhoneScreen.cs` | данные готовы; **источник данных не подключён** |
| Меню паузы | `UI/Core/PauseLayout.cs`, `UI/Runtime/PauseMenuScreen.cs` | раскладка измерена по `Pause Menu.PNG`, 0 замечаний `Validate()`; **в игре не сверялась** |
| Установщик в сцену | `UI/Editor/UiInstaller.cs` | готов, идемпотентен, **не запускался в Unity** |
| Тесты | `UI/Tests/*` (92) | зелёные вне Unity |

## 2. Порядок подключения (делать именно так)

### Шаг 1. Установить UI в сцену

```
Tools → Life From Zero → UI → Check UI installation (dry run)   ← сначала план
Tools → Life From Zero → UI → Install UI into game              ← затем применить
```

Пакетно:

```bash
Unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod LifeFromZero.UI.Editor.UiInstaller.InstallBatch \
  -lfzUiScene "Assets/_Project/Scenes/District750IntegrationTest.unity" \
  -lfzUiDryRun -logFile /tmp/lfz-ui.log
```

Проверка: в логе `[LFZ-UI]` и `РЕЗУЛЬТАТ: план` / `OK`. Повторный запуск обязан
напечатать «уже есть» и **ничего** не добавить (идемпотентность). Сцена
копируется в `LifeFromZeroIntegration/backups/<время>/` до изменений.

Если установщик пишет «в сцене уже есть чужое меню паузы» — он **останавливается**:
два меню одновременно хуже одного. Решение принимает владелец игры, а не скрипт.

### Шаг 2. Позиция игрока (`UiWorldAdapter`)

Нужен класс, который отдаёт позицию игрока, поворот и умеет блокировать ввод.
В репозитории его нет — это часть интеграции.

```csharp
public sealed class GameUiWorld : UiWorldAdapter   // наследник из LifeFromZero.UI
{
    public Transform player;                       // перетащить в инспекторе
    public override bool TryGetPlayer(out MapPoint position, out float headingDegrees)
    {
        if (player == null) { position = default; headingDegrees = 0f; return false; }
        position = new MapPoint(player.position.x, player.position.z);
        headingDegrees = player.eulerAngles.y;
        return true;
    }
}
```

Проверка: на карте точка игрока стоит там, где игрок в мире (сверить по району,
не по `Map.PNG` — см. `UI/MAP_CALIBRATION_REPORT.md`).

### Шаг 3. Часы, деньги, долги, потребности (`UiSnapshotSource`)

Тоже пишется интегратором. Пока его нет, HUD показывает «Нет данных» — это
честное состояние, а не заглушка с выдуманными числами.

Проверка: числа в HUD меняются вместе с игрой (потратил деньги — сумма упала).

### Шаг 4. Телефон (`IPhoneDataSource`)

Контракт уже объявлен в `UI/Runtime/UiBootstrap.cs`:

```csharp
public interface IPhoneDataSource
{
    PhoneSnapshot ReadPhone();                       // что показать
    bool TryRun(string action, out string message);  // что нажали
}
```

Реализация должна **вызывать** существующие системы (`PhoneFacade`,
`Jobs.Delivery`), а не дублировать их состояние. Правило: UI не хранит деньги,
заказы и статусы — он их отображает.

### Шаг 5. Заказы и доставка (`IDeliveryUiBridge`)

```csharp
public interface IDeliveryUiBridge
{
    bool TryRun(string verb, string orderId, out string message);
}
```

`verb` приходит из телефона (`accept`, `cancel`, `complete` и т. п.). Если верб
неизвестен — вернуть `false` и текст причины; UI покажет его игроку, а не
«Ничего не произошло».

### Шаг 6. Район

Два равноправных пути — выбрать один:

```csharp
// А. Императивно после генерации города:
UiBootstrap.SetDistrictPlan(cityBootstrap.Plan);

// Б. Инспектором: District Source = QuarterUnityAdapter (реализует IQuarterPlanSource).
```

Проверка: карта рисует **тот же** район, что стоит в сцене (улицы совпадают с
геометрией дорог, а не с картинкой).

### Шаг 7. Клавиши и второе меню паузы

В проекте есть `City.Pause` (`PauseController` + `PauseView` + `PauseKeyboardInput`,
клавиши Esc/P). Поэтому:

* `UiBootstrap` находит `PauseIntegration` и **сам** выключает у `UiRouter` чтение
  клавиш и собственный `Time.timeScale` — иначе два владельца паузы мешали бы
  друг другу (один «продолжает» игру, пока второй ещё в паузе);
* кнопки `save/load/settings/controls/main-menu` уходят в существующую паузу
  через `PauseIntegration.Execute(...)`, а `phone/map/tasks/inventory` — в UI;
* если пауза осталась на старом `PauseView`, поставьте в сцене **одно** меню:
  либо старый `PauseView`, либо новый `PauseMenuScreen`. Установщик предупреждает
  об этом в отчёте, но не удаляет чужое.

Проверка: Esc открывает ровно одно меню, время останавливается один раз.

## 3. Как проверять (и что считается проверкой)

```bash
# 1. Компиляция без Unity (Roslyn, стабы)
python3 UI/Tools/checkui.py        # ждём: COMPILE: OK errors=0

# 2. Тесты логики без Unity
dotnet run --project UI/Tests/Console.csproj    # ждём: 92/92 PASS, exit 0

# 3. Unity: компиляция проекта
Unity -batchmode -quit -projectPath "$PWD" -logFile /tmp/unity.log
grep -E "error CS" /tmp/unity.log

# 4. Unity: тесты EditMode
Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode \
  -testResults /tmp/ui-tests.xml -logFile /tmp/ui-tests.log

# 5. Unity: глазами — единственный способ проверить картинку
```

**Что НЕ является проверкой:** «компилируется стабами» ≠ «компилируется в Unity»;
«тесты логики зелёные» ≠ «экран выглядит как на макете». Ни в одном отчёте эти
вещи не должны стоять рядом как эквивалентные.

## 4. Что осталось неподключённым (честный список)

| Не подключено | Что это значит для игрока | Кто закрывает |
|---|---|---|
| Поля `UiGameBridge` (Player / Input / HUD Roots) | карта не знает, где игрок; персонаж ходит при открытом меню | интегратор, 3 поля в инспекторе |
| `PauseDataSource` в сцене | HUD/пауза без часов, денег, потребностей | интегратор: наследник `PauseDataSource` |
| `IPhoneDataSource` | телефон показывает каркас, действия не работают | интегратор |
| `IDeliveryUiBridge` | принять/закрыть заказ из телефона нельзя | интегратор |
| План района в рантайме | карта пустая, пока не вызван `SetDistrictPlan` | шаг 6 |
| Старый `PauseView` и новый `PauseMenuScreen` вместе | два меню на Esc | владелец игры: оставить одно |
| Ввод (Esc/P/M) | конфликт с существующим роутером | шаг 7 |
| Скриншотная сверка | неизвестно, совпадает ли вид с `Phone.png` / `Pause Menu.PNG` | глазами в Unity |
| Полигоны зданий на карте | на карте только улицы и точки интереса | вне ТЗ (см. отчёт по карте) |

## 5. Известные ограничения реализации

* Карта — один меш (`MapCanvasGraphic`). Плюс: нет сотни `Image`. Минус: без
  дополнительной работы нельзя подсвечивать отдельный квартал как отдельный
  объект — только перерисовать меш.
* Клик по карте маршрут не строит: у игры нет координат клика по миру.
* `QuarterUnityAdapter` намеренно **не** входит в набор компиляции без Unity
  (нужны `Material`/`Mesh`/`Quaternion`), поэтому ошибки именно в нём ловит только
  Unity-компиляция.
* Сборки: `LifeFromZero.UI.Core` (без UnityEngine) → `LifeFromZero.UI` (рантайм) →
  `LifeFromZero.UI.Editor` (только Editor). UI **не** ссылается на
  `City.Quarter.UnityAdapter`: это дало бы цикл сборок. План читается через
  `IQuarterPlanSource` из `City.Quarter`.
