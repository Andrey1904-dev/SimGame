# UI: что готово, что подключить, как проверять (ТЗ №042)

Документ для того, кто будет доводить интерфейсы в настоящем Unity-проекте.
Читается сверху вниз; каждый шаг заканчивается проверяемым результатом.

> **Главное, что нельзя путать.** Всё, что ниже, проверено **вне Unity**:
> по-сборочной компиляцией всех 17 `.asmdef` + `Assembly-CSharp` + `Assembly-CSharp-Editor` (`AUDIT/tools/check_unity6_compile.py`, `checkui.py`, `checkeditor.py`, Roslyn) и тестами (`runtests.py`, 103 UI-теста; всего по репозиторию 508 C#-проверок). Это
> доказывает логику и границы сборок, но **не** доказывает, что UI выглядит правильно в игре.
> Скриншоты, шрифты, порядок батчей, клики по uGUI — не проверены. Пока в проекте
> не запущена сцена, интерфейс считается **непроверенным в Unity**, и в отчётах так и надо
> писать.

---

## 0. Точные команды интеграции и отката для Codex (`Unity 6000.3.27f1`)

### 0.1. Подготовка и точка отката в локальном проекте

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
UNITY_PROJ="/Users/homepc/Projects/LifeFromZero"
SIMGAME_REPO="/Users/homepc/Projects/SimGame"

# 1. Проверить проект и зафиксировать точку отката ДО копирования
cd "$UNITY_PROJ"
ls -d Assets ProjectSettings
cat ProjectSettings/ProjectVersion.txt
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git status --porcelain
  git tag -f lfz-before-ui-city-integration HEAD
else
  ditto "$UNITY_PROJ" "${UNITY_PROJ}_backup_$(date -u +%Y%m%dT%H%M%SZ)"
fi
```

### 0.2. Копирование модулей (`City` + `UI` + `Economy` + `Jobs/Delivery` + `Phone` + `Debts` + `UnityIntegrationKit`)

`install_to_unity.sh` копирует только `.cs` и `.asmdef` (172 файла), **не трогает** `.unity`, префабы, пользовательские ассеты, существующие `.meta`, `Packages/` и `ProjectSettings/`, а при наличии одноимённого `.asmdef` в другом месте `Assets/` обновляет его на месте без создания дубликата сборки:

```bash
cd "$SIMGAME_REPO"
git fetch origin
git checkout arena/c02a16e9-simgame   # либо main после слияния PR
git pull --ff-only

# Шаг 1: сухой прогон (ничего не изменяет)
bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --dry-run

# Шаг 2: копирование без перезаписи конфликтов
bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --apply

# Только если в проекте уже лежали старые копии этих же .cs/.asmdef и владелец подтвердил обновление
# (автоматически сохраняет бэкап заменяемых файлов и их .meta в LifeFromZeroIntegration/backups/install-<UTC>/):
# bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --apply --overwrite
```

### 0.3. Пакетная проверка компиляции, обнаружение сцены и сухие прогоны в Unity (при закрытом Editor)

```bash
# Полный прогон этапов 1–8 (статические проверки + структура Assets + EditMode/PlayMode + DetectBatch + RunBatchVerify)
bash "$SIMGAME_REPO/City/UnityIntegration/run_checks.sh" \
  --unity-project "$UNITY_PROJ" \
  --unity "$UNITY_BIN"

# Посмотреть выбранную стартовую сцену из отчёта обнаружения:
ls -t "$UNITY_PROJ/LifeFromZeroIntegration"/discovery-*.md | head -n 1 | xargs head -n 45

# Сухой прогон интеграции города и паузы в стартовую сцену (ЗАМЕНИТЕ <START_SCENE> на путь из отчёта discovery):
"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.IntegrateBatch \
  -lfzStartScene "<START_SCENE>" -lfzDryRun \
  -logFile /tmp/lfz-city-dryrun.log

# Применение интеграции города и паузы (делает бэкап сцены и EditorBuildSettings.asset в LifeFromZeroIntegration/backups/):
"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.IntegrateBatch \
  -lfzStartScene "<START_SCENE>" \
  -logFile /tmp/lfz-city-apply.log

# Сухой прогон и установка UI в стартовую сцену:
"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.UI.Editor.UiInstaller.InstallBatch \
  -lfzUiScene "<START_SCENE>" -lfzUiDryRun \
  -logFile /tmp/lfz-ui-dryrun.log

"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.UI.Editor.UiInstaller.InstallBatch \
  -lfzUiScene "<START_SCENE>" \
  -logFile /tmp/lfz-ui-apply.log

# Read-only пост-проверка всех сцен проекта:
"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.UI.Editor.UiInstaller.RunBatchVerify \
  -logFile /tmp/lfz-ui-verify.log
```

### 0.4. Точные команды отката (Rollback)

1. **Откат сцен и `EditorBuildSettings.asset` из автоматического бэкапа интегратора** (при закрытом Unity Editor):
   ```bash
   cd "$UNITY_PROJ"
   LAST_BACKUP="$(ls -td LifeFromZeroIntegration/backups/* | head -n 1)"
   echo "Восстановление из: $LAST_BACKUP"
   rsync -av "$LAST_BACKUP/" "$UNITY_PROJ/"
   ```
2. **Полный откат локального Unity-проекта к точке до импорта** (если проект в Git и тег создан на шаге 0.1):
   ```bash
   cd "$UNITY_PROJ"
   git reset --hard lfz-before-ui-city-integration
   git clean -fd -- Assets/City Assets/UI Assets/_Project/SimGame
   ```
3. **Откат в репозитории `SimGame` к коммиту до интеграции** (при необходимости):
   ```bash
   cd "$SIMGAME_REPO"
   git checkout backup-before-integration   # f8334ba8d95dafdfd39ef63f346e9bccf585af2b
   ```

---

## 1. Что уже есть (и работает без Unity)

| Область | Файлы | Состояние |
|---|---|---|
| Состояния экранов, роутер | `UI/Core/UiStateMachine.cs`, `UI/Runtime/UiRouter.cs` | готово, координировано с `City.Pause`, тесты |
| Карта: данные, граф, маршруты | `UI/Core/MapCalibration*.cs`, `RoadGraphData.cs`, `RoutePlanner.cs`, `RouteGeometryBuilder.cs` | готово, тесты |
| Карта: отрисовка мешем | `UI/Runtime/MapCanvasGraphic.cs`, `MapScreen.cs` | **не проверено в Unity** |
| Навигация, HUD-подсказки | `UI/Core/NavigationService.cs`, `RouteNavigator`, `UiBootstrap.cs` | готово, живой апдейт HUD в `Update()`, тесты |
| Мост «район → карта/доставка» | `UI/Core/QuarterPlanAdapter.cs`, `UI/Runtime/QuarterMapGeometry.cs`, `UI/Runtime/QuarterLocationProvider.cs` | готово (`INotifyQuarterPlanSource.PlanChanged`, `ResolveLocationId`), тесты |
| Мост к игре | `UI/Runtime/UiGameBridge.cs` (`UiGameBridge`, `PauseUiSnapshotSource`) | готов; **проверен стабами** |
| Телефон и `PhoneFacade` | `UI/Core/PhoneData.cs`, `UI/Runtime/PhoneScreen.cs`, `UnityIntegrationKit/PhoneUiBridge.cs` | мост `PhoneFacadeUiBridge : IPhoneDataSource` и `DeliveryServiceBridge` готовы |
| Меню паузы | `UI/Core/PauseLayout.cs`, `UI/Runtime/PauseMenuScreen.cs` | раскладка измерена по `Pause Menu.PNG`, 0 замечаний `Validate()`; **в игре не сверялась** |
| Установщик в сцену | `UI/Editor/UiInstaller.cs` | готов (`InstallBatch`, `RunBatchVerify`, `SerializedObject`), идемпотентен, **не запускался в Unity** |
| Тесты | `UI/Tests/*` (103) + `AUDIT/tools/check_unity6_compile.py` | 103/103 PASS вне Unity, 0 ошибок Roslyn |

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

### Шаг 5. Заказы и доставка (`Jobs.Delivery`)

Мост **уже написан**: `DeliveryServiceBridge` реализует `IPhoneDataSource` и
`IDeliveryUiBridge` поверх настоящего `IDeliveryService` (`accept`, `pickup`,
`complete`, `cancel`, `abandon`), координаты точки доставки берёт из
`QuarterPlan`, отказы объясняет игроку по коду (`DeliveryPhoneCommands.Describe`).

Что нужно сделать в проекте — четыре подключения, все через инспектор:

| Поле `DeliveryServiceBridge` | Что положить | Зачем |
|---|---|---|
| `Delivery Service Source` | наследник `DeliveryServiceHost` (см. ниже) | сервис — обычный класс, сцене нужен компонент |
| `Clock Adapter` | реализация `IGameClockAdapter` | сроки заказа |
| `Payout Adapter` | реализация `IPayoutAdapter` | выплата за заказ |
| `Location Provider` | реализация `ILocationProvider` | «я в точке получения/доставки» |

```csharp
public sealed class GameDeliveryHost : DeliveryServiceHost
{
    [SerializeField] private MonoBehaviour serviceOwner;   // ваш объект с DeliveryService
    protected override IDeliveryService CreateService() => serviceOwner as IDeliveryService;
}
```

Если что-то из четырёх не назначено, кнопки заказа вернут понятную причину
(«Кошелёк не подключён»), а не ложный успех. Телефон при отсутствии моста
показывает «Система доставки не подключена» — это честное состояние.

В реальном `DeliveryService` уже есть правила (один активный заказ, выдача только
в точке получения, выплата один раз). UI их **не дублирует**.

### Шаг 5б. Кнопки меню паузы и настройки

`UiMenuActions` — единственное место, где кнопки «Сохранить», «Загрузить»,
«Главное меню» получают исполнителя:

```csharp
public sealed class GameSaveAction : MonoBehaviour, IUiGameAction
{
    public bool Run(out string message) { /* сохранить игру */ }
}
```

Пока исполнителя нет, кнопка неактивна, а игрок не получает ложного «Игра
сохранена». Громкость и полный экран UI применяет сам (`UiSettings`).

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
