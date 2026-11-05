# CODEX_EXECUTE — интеграция LIFE FROM ZERO (ТЗ #038 + #039)

Исполняемый сценарий. Выполняй шаги по порядку, не проектируй интеграцию заново.
Для каждого шага: команда → ожидаемый результат → признак успеха → что делать при ошибке.

**Три части задачи:**
* **Часть A (шаги 1–15)** — установка модулей, тестовая сцена, тесты. Проверяет,
  что код рабочий, но НЕ является результатом.
* **Часть B (шаги 16–22, обязательна)** — интеграция в настоящую стартовую сцену игры,
  включая привязку квартиры №67. Задача не выполнена, пока пользователь не откроет
  обычную стартовую сцену LIFE FROM ZERO, не нажмёт Play и не получит работающую игру.
* **Часть C (шаги 23–28)** — переход со старого района Quarter250 на District750:
  диагностика, отключение двойной генерации, спавн/камера/границы/коллизии, откат.
  Выполняется, если игра до этого жила на квартале 250×250.

Не читай старые отчёты (`FULL_AUDIT_REPORT.md`, `INTEGRATION_AUDIT.md`, `TZ021_*`,
`CODEX_COPY_PASTE_TASKS.md` и т.п.) и не проводи повторный аудит проекта: всё нужное
для интеграции уже собрано здесь и в `City/UnityIntegration/`.

| Параметр | Значение |
|---|---|
| Репозиторий | `https://github.com/Andrey1904-dev/SimGame` |
| PR с подготовкой | `https://github.com/Andrey1904-dev/SimGame/pull/13` |
| Ветка с подготовкой | `arena/672047f2-simgame`; после слияния PR #13 — `main` |
| Локальный Unity-проект | `/Users/homepc/Projects/LifeFromZero` |
| Unity / URP | 6000.3.27f1 / 17.3.0 |
| Машина | macOS, Apple Silicon M4, 16 ГБ RAM |
| Город | District750, 750×750 м, `seed = 1904`, `layoutId = district750` |
| Меню паузы | `Esc`, дизайн по `Pause Menu.PNG` (уже в проекте) |
| Телефон | английская `P`. Никаких `I`, `M`, `J` |

Что уже готово в репозитории: исходники модулей (`City/Quarter`, `City/UnityAdapter`,
`City/Tests`, `City/PauseMenu`, `City/PauseInputSystem`, `City/PauseTests`), установщик
`City/UnityIntegration/install_to_unity.sh`, Editor-инструменты
`City/UnityIntegration/Editor/` (`LifeFromZeroSetup`, `LifeFromZeroDiscovery`,
`LifeFromZeroGameIntegration`, `LifeFromZeroIntegrateWindow`, `LifeFromZeroMigration`),
Runtime-мосты `City/UnityIntegration/Runtime/District750Link.cs` и
`PlayerHomeAnchor.cs` (привязка квартиры №67), проверки
`City/UnityIntegration/run_checks.sh`, шаблон данных
`City/UnityIntegration/Templates/GamePauseDataSource.cs.txt`.

Что НЕ проверено (Unity в среде репозитория нет): компиляция C# в Unity, импорт
`.asmdef`, работа сцены в Play Mode, PlayMode-тесты, FPS. Это и есть работа шагов 5–13.
Не выдумывай результаты этих проверок: пиши ровно то, что увидел.

---

## Шаг 1. Проверка локального Unity-проекта и сохранение пользовательских изменений

```bash
cd /Users/homepc/Projects/LifeFromZero
ls -d Assets ProjectSettings
cat ProjectSettings/ProjectVersion.txt
git status --porcelain            # если это git-репозиторий
```

**Ожидаемо:** каталоги `Assets/` и `ProjectSettings/` есть, `m_EditorVersion: 6000.3.27f1`,
`git status` пуст или показывает только твои будущие правки.

**Успех:** оба каталога на месте, версия Unity совпадает с 6000.3.27f1.

**Если ошибка:**
* нет `Assets/` или `ProjectSettings/` → СТОП. Это не Unity-проект, путь неверный. Сообщи блокер.
* версия Unity другая → СТОП и сообщи (интеграция проверялась под 6000.3.27f1).
* `git status` не пуст (есть незакоммиченная работа):
  ```bash
  git stash push -u -m "codex-before-tz038"
  ```
* `fatal: not a git repository` → сделай резервную копию и продолжай:
  ```bash
  ditto "/Users/homepc/Projects/LifeFromZero" \
        "/Users/homepc/Projects/LifeFromZero_backup_$(date +%Y%m%d%H%M)"
  ```

**Далее:** шаг 2. Возврата к шагу 1 больше не требуется.

---

## Шаг 2. Получение актуального кода из GitHub без перезаписи локальной работы

```bash
cd /Users/homepc/Projects
git clone https://github.com/Andrey1904-dev/SimGame.git SimGame
cd /Users/homepc/Projects/SimGame
git fetch origin
git checkout main
git pull --ff-only origin main
ls City/UnityIntegration/install_to_unity.sh City/UnityIntegration/run_checks.sh
```

**Ожидаемо:** клон создан, оба скрипта существуют.

**Успех:** `ls` печатает оба пути без ошибок.

**Если ошибка:**
* `install_to_unity.sh: No such file` → PR ТЗ #038 ещё не слит. Переключись на ветку подготовки:
  ```bash
  git fetch origin arena/672047f2-simgame
  git checkout arena/672047f2-simgame
  git pull --ff-only origin arena/672047f2-simgame
  ```
* клон уже существует → `cd SimGame && git fetch origin && git checkout main && git pull --ff-only origin main`.

**Важно:** клон репозитория — отдельный каталог. Ничего не копируй из него в Unity-проект
руками: только установщиком из шага 4.

**Далее:** шаг 3, только если шаг 2 успешен.

---

## Шаг 3. Dry-run установщика

```bash
cd /Users/homepc/Projects/SimGame
bash City/UnityIntegration/install_to_unity.sh \
  "/Users/homepc/Projects/LifeFromZero" --dry-run
echo "exit=$?"
```

**Ожидаемо:** отчёт из 4 блоков (исходники → проект → план → границы сборок), строки
`ADD Assets/City/...` (41 файл при первом запуске) и итог
`добавить: N, пропустить (идентичные): M, конфликтов: 0`, `exit=0`.

**Успех:** `exit=0` и `конфликтов: 0`.

**Если ошибка:**
* `exit=5` — конфликты. Установщик печатает точный список файлов и НИЧЕГО не копирует.
  Покажи список владельцу и остановись. `--overwrite` применяй только после явного
  подтверждения по каждому файлу.
* `exit=3` — нет `Assets/`/`ProjectSettings/`. Вернись к шагу 1.
* `exit=4` — нет исходников: неверный каталог клона. Вернись к шагу 2.
* `exit=2` — ошибка аргументов (пробелы в пути не забыты в кавычках).

**Далее:** шаг 4 только при `exit=0`.

---

## Шаг 4. Установка модулей

```bash
cd /Users/homepc/Projects/SimGame
bash City/UnityIntegration/install_to_unity.sh \
  "/Users/homepc/Projects/LifeFromZero" --apply
echo "exit=$?"
```

**Ожидаемо:** строки `записан Assets/City/...`, итог
`записано новых/обновлённых: 35, пропущено идентичных: 0, конфликтов: 0`, `exit=0`.

**Успех:** `exit=0` и в проекте появились:
`Assets/City/Quarter/`, `Assets/City/UnityAdapter/`, `Assets/City/Tests/`,
`Assets/City/PauseMenu/`, `Assets/City/PauseInputSystem/`, `Assets/City/PauseTests/`,
`Assets/City/UnityIntegration/Editor/LifeFromZeroSetup.cs`.
Проверь: `ls /Users/homepc/Projects/LifeFromZero/Assets/City`.

**Если ошибка:** повтори шаг 3 (повторный запуск идемпотентен: идентичные файлы
пропускаются). Сцены, `CityTest.unity`, `Apartment67` и локальные файлы установщик не трогает.

**Далее:** шаг 5.

---

## Шаг 5. Открытие Unity и проверка компиляции

Если Unity с этим проектом ещё не открыт — удобнее сначала headless-импорт (он же создаст `.meta`):

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "/Users/homepc/Projects/LifeFromZero" \
         -logFile /tmp/lfz-compile.log
echo "exit=$?"
grep -E "error CS|Compilation failed|Scripts have compiler errors" /tmp/lfz-compile.log || echo "compile errors: none"
```

Затем открой проект в Unity Editor и посмотри Console (окно Console → Clear on Play выкл.).

**Ожидаемо:** `exit=0`, `compile errors: none`; в Console нет `error CS`.
Допустимы предупреждения (не ошибки): «Assembly ... will not be compiled, because ...» нет,
`CS0649`/`CS0414` по неиспользуемым полям, предупреждения импорта материалов.

**Успех:** ни одной строки `error CS` и ни одного «Assembly has reference to non-existent assembly».

**Если ошибка:**
* `error CS0234/CS0246` с `UnityEngine.UI`, `Text`, `Button`, `Image` → сборка
  `LifeFromZero.City.Pause` ссылается на несуществующее имя сборки. Симптом и лечение — в
  разделе «Риски», риск R1.
* `error CS0246: PauseController` в `PauseKeyboardInput.cs` → файл попал не в
  `Assembly-CSharp`. Риск R2.
* `The type or namespace name 'TestRunner' could not be found` → не установлен пакет
  Test Framework. Риск R5.
* Unity не стартует в batchmode → открой Editor вручную и прочитай Console. Не запускай
  второй экземпляр при открытом Editor (наличие файла `Temp/UnityLockfile`).

**Далее:** шаг 6 только при отсутствии `error CS`.

---

## Шаг 6. Запуск Editor-инструмента создания сцены

Вариант A (меню, если Editor открыт):
`Tools → Life From Zero → Setup District750 & Pause`

Вариант B (headless, если Editor закрыт):

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -quit -projectPath "/Users/homepc/Projects/LifeFromZero" \
         -executeMethod LifeFromZero.Setup.LifeFromZeroSetup.SetupBatch \
         -logFile /tmp/lfz-setup.log
echo "exit=$?"
grep "LFZ-SETUP" /tmp/lfz-setup.log
```

**Ожидаемо:** строка
`[LFZ-SETUP] RESULT: OK scene=Assets/_Project/Scenes/District750IntegrationTest.unity seed=1904 layout=district750`
и файл `Assets/_Project/Scenes/District750IntegrationTest.unity`.

**Успех:** сцена создана, `exit=0`; в ней объекты `City` (CityBootstrap + QuarterUnityAdapter),
`District Camera`, `Directional Light`, `Pause` (PauseController + StandalonePauseIntegration
+ PauseKeyboardInput), `EventSystem`.

**Если ошибка:**
* `СТОП: сцена уже существует` → инструмент не перезаписывает сцену. Не удаляй её без
  указания владельца: открой существующую и проверяй её, либо создай новую сцену вручную
  под другим именем и сообщи путь.
* `Scene already exists` + `exit!=0` — то же самое (в batchmode это исключение).
* `Тип PauseKeyboardInput не найден` → он не установился или попал под чужой asmdef. Риск R2.

**Далее:** шаг 7. Сцена должна остаться открытой (или открой её:
`Assets/_Project/Scenes/District750IntegrationTest.unity`).

---

## Шаг 7. Подключение существующего контроллера движения и камеры

Это единственный обязательный ручной шаг. Без него пауза не откроется —
`StandalonePauseIntegration.CanOpenPause()` возвращает `false`, пока список компонентов пуст,
и Esc безопасно ничего не делает. Это не баг.

1. Найди компоненты: открой игровую сцену проекта (ту, где живёт персонаж),
   затем `Tools → Life From Zero → Report input candidates`.
   В Console появится список «путь → полное имя типа». Компоненты паузы в список не попадут.
2. Вернись в `District750IntegrationTest`, выбери объект `Pause` →
   `StandalonePauseIntegration` → поле **Player Input Behaviours**:
   * `Size` = число компонентов (минимум 1);
   * перетащи туда компоненты, управляющие **движением** и **камерой**
     (обычно скрипты вида `…Movement`, `…Controller`, `…Look`, `…Camera`, `…Input`);
   * не назначай `PauseController`/`PauseView`/`PauseIntegration`/`PauseKeyboardInput` —
     они отвергаются адаптером.
3. Поле **Competing Modal Roots** — корни конкурирующих окон (телефон, инвентарь), если они есть.
4. `Pause` → `PauseKeyboardInput` → **Use Standalone Keyboard** уже включён.
   Если в проекте есть свой роутер `Esc` — выключи его здесь и вместо этого вызывай
   `PauseController.Instance.HandleEscape()` из своего роутера.
5. Сохрани сцену: `Cmd+S` (или `File → Save`).

**Ожидаемо:** ссылки в Inspector заполнены, сцена сохранена.

**Успех (проверяется в Play Mode):**
* `Esc` → меню открылось, `Time.timeScale == 0`, курсор виден и не заблокирован;
* персонаж не двигается и камера не вращается (Input при заблокированных компонентах);
* `Esc` или кнопка «Продолжить игру» → меню закрылось, персонаж снова управляется,
  `Time.timeScale` вернулся к прежнему значению, состояние курсора восстановлено.

**Если ошибка:**
* Esc ничего не делает → список `Player Input Behaviours` пуст, или все элементы отклонены
  (Console: сообщений нет). Проверь размер массива.
* После закрытия персонаж не управляется → в массив попал компонент самой паузы; убери его.
* Открываются два меню → в проекте уже есть свой обработчик `Esc`: выключи
  `Use Standalone Keyboard` (п. 4).
* `Pause: no UI input module available` → в сцене нет `EventSystem` с UI-модулем; добавь
  `EventSystem` в сцену (инструмент создаёт его, если объекта не было).

**Далее:** шаг 8.

---

## Шаг 8. Подключение доступных игровых данных

Меню работает и без источника данных: все поля показывают «Нет данных». Реальные данные
подключаются так:

```bash
cp /Users/homepc/Projects/SimGame/City/UnityIntegration/Templates/GamePauseDataSource.cs.txt \
   "/Users/homepc/Projects/LifeFromZero/Assets/City/PauseMenu/GamePauseDataSource.cs"
```
Затем в Unity: дождись компиляции, повесь `GamePauseDataSource` на объект `Pause`,
назначь его в `PauseController → Data Source`, сохрани сцену.

Источники, которые точно есть в репозитории (файл → тип → метод):

| Поле меню | Файл | Тип | Метод/свойство |
|---|---|---|---|
| Деньги | `Economy/Core/IWalletAdapter.cs` | `LifeFromZero.Economy.Core.IWalletAdapter` | `Balance` → `Money.Rubles` (`long`) |
| Долг | `Debts/Core/IDebtReadService.cs` | `LifeFromZero.Debts.Core.IDebtReadService` | `TryGetTotalRemainingRubles(out long rubles)` |
| Время | `Phone/Core/IPhoneClock.cs` | `LifeFromZero.Phone.Core.IPhoneClock` | `NowMinutes` (`double`) |
| Кошелёк-мост | `UnityIntegrationKit/AdapterBridges.cs` | `WalletBridge` | `new WalletBridge(() => wallet.BalanceRubles, …, …)` |

День, локация, цели, здоровье, сытость, энергия, настроение, доход за день в репозитории
ОТСУТСТВУЮТ. Их источник есть только в локальном проекте. Найди его так:

```bash
cd /Users/homepc/Projects/LifeFromZero
grep -rln --include=*.cs -E "GameClock|CurrentDay|DayNumber|Satiety|Hunger|Energy|Mood|Stress|Health" Assets | grep -v "/City/Pause" | head -40
grep -rn --include=*.cs -E "class (GameClock|PlayerStats|PlayerVitals|PlayerNeeds|DailyIncome)" Assets | head -20
```
После этого в своём коде выставь делегаты, например:

```csharp
dataSource.ReadMoneyRubles = () => walletAdapter.Balance.Rubles;
dataSource.ReadDebtRubles  = () => debtService.TryGetTotalRemainingRubles(out long r) ? r : (long?)null;
dataSource.ReadLocation    = () => currentLocationName;
dataSource.NotifyChanged();   // после любого изменения данных
```

**Ожидаемо:** в меню появляются реальные значения; недоступные — «Нет данных».

**Успех:** ни одно значение не выдумано; проценты 0..100; деньги в целых рублях;
`Money`, `Debt`, `DailyIncome` не пересчитываются в чужих модулях.

**Если ошибка:** нет реального источника → оставь `null` (меню покажет «Нет данных»)
и запиши это в отчёт. Не создавай фальшивые данные и не переписывай `Economy`, `Delivery`,
`Debts`, `Phone`. Если для данных нужна правка этих модулей — СТОП, это блокер.

**Далее:** шаг 9.

---

## Шаг 9. Запуск EditMode и PlayMode-тестов

Вариант A (Test Runner в Editor): `Window → General → Test Runner`
→ вкладка **EditMode** → `Run All`; затем вкладка **PlayMode** → `Run All`.

Вариант B (headless, Editor должен быть закрыт):

```bash
cd /Users/homepc/Projects/SimGame
bash City/UnityIntegration/run_checks.sh \
  --unity-project "/Users/homepc/Projects/LifeFromZero"
echo "exit=$?"
cat City/UnityIntegration/reports/*/report.md | tail -20
```

**Ожидаемо:**
* EditMode: один тест `LifeFromZero.City.Quarter.Tests.QuarterNUnitTests.RunAllQuarterTests`
  (внутри — весь набор модуля City/Quarter), результат `Passed`;
* PlayMode: 8 тестов `LifeFromZero.City.Pause.Tests.PauseTests`, все `Passed`;
* `run_checks.sh`: этапы 1, 2, 4, 5, 6, 8 = `PASS`, этап 3 = `PASS` при наличии `dotnet`,
  иначе `NOT RUN`, `exit=0` (этап 6 — обнаружение стартовой сцены, этап 7 — ошибки Console).

**Успех:** нет упавших тестов и нет `error CS` в логах этапа 7.

**Если ошибка:**
* тесты не видны → не установлен Test Framework или asmdef не собрался. Риск R5.
* PlayMode-тесты падают на `MissingIntegrationAndButtonNavigationAreSafe` и подобных →
  прочитай сообщение `Assert` и правь только подтверждённую причину (шаг 14).
* этап 4/5 = `NOT RUN` из-за `Temp/UnityLockfile` → закрой Editor и повтори.

**Далее:** шаг 10.

---

## Шаг 10. Проверка District750 в сцене

1. Открой `Assets/_Project/Scenes/District750IntegrationTest.unity`.
2. Проверь Inspector объекта `City`: `Seed = 1904`, `Layout Id = district750`,
   `Generate On Start = on`, `Add Colliders = on`; у `QuarterUnityAdapter`: `Build On Start = off`.
3. `Play`. Смотри Console и поле `Last Report` у `CityBootstrap`.

**Ожидаемо** (одна строка Console):
`[CityBootstrap] Город построен: макет=district750, seed=1904, жилых зданий=15, объектов=974, точек доставки=46, время=… мс.`
`Last Report`: `ResidentialBuildings=15`, `TotalObjects=974`, `DeliveryPoints=46`, `ErrorCount=0`.

**Успех:** цифры совпадают, карта 750×750 целиком в кадре ортографической камеры,
`Clear()`/повторный `Generate()` не оставляет дублей квартала.

**Если ошибка:** см. риск R4. Числа, отличные от перечисленных, — блокер: сообщи фактические.

**Далее:** шаг 11.

---

## Шаг 11. Проверка меню Esc по `Pause Menu.PNG`

В Play Mode нажми `Esc` и сверь с референсом (файл в проекте):

- левая панель: логотип `LIFE FROM ZERO` (ZERO красным), заголовок «Пауза», **7 кнопок**
  в порядке: Продолжить игру / Сохранить игру / Загрузить игру / Настройки / Управление /
  Выйти в главное меню / Выйти из игры — все одного тёмного стиля, кнопка выхода **не красная**;
- правая панель: сверху день/время/локация, середина — «Цели» (слева) и «Статус» (справа:
  Здоровье, Сытость, Энергия, Настроение с полосами), низ — «Финансы» (Деньги:, Долг:,
  Доход за день:) и «Быстрые ссылки» 2×2: Телефон `[P]`, Инвентарь, Карта, Задания;
- подвал: `v0.2.0 (alpha)` слева, бейдж `Esc` + «Закрыть меню» справа;
- нет «Погоды», «Уровня», «Голода», «Стресса», «Наличных»/«На карте», подсказки «Выбрать»;
- фон игры виден сквозь полупрозрачную подложку; верстка не ломается на 1600×900,
  1920×1080 и 2560×1440 (`Game view` → Aspect);
- кнопки не остаются выделенными после клика.

**Успех:** все пункты совпадают; «Продолжить игру» закрывает меню.

**Если ошибка:** фиксируй конкретное расхождение (элемент, ожидание, факт). Дизайн меню
не переделывай без указания владельца.

**Далее:** шаг 12.

---

## Шаг 12. Проверка телефона `P` и восстановления управления

1. Открой паузу (`Esc`), нажми английскую `P`.
2. Проверь, что физическая `P` не назначена ни на что другое и что `I`, `M`, `J` не вызывают
   ни телефон, ни пункты меню.
3. Закрой меню (`Esc` или «Продолжить игру») и проверь восстановление:
   персонаж двигается, камера вращается, `Time.timeScale` прежний, курсор в прежнем состоянии.
4. Смени сцену (например, загрузи `CityTest.unity`) и проверь: второго `Canvas`/`EventSystem`
   нет, пауза закрыта, блокировка снята.

**Ожидаемо:** `P` срабатывает только при открытой паузе и только если `PauseIntegration`
сообщает `IsAvailable(PauseAction.Phone)`; иначе — ничего (без фиктивных действий).

**Успех:** все четыре пункта выполнены, в Console нет ошибок.

**Если ошибка:** `P` ничего не делает при открытой паузе → `IsAvailable(Phone)` возвращает
`false`: назначь `Callback`/`enabled` для `Phone` в `StandalonePauseIntegration` (или
реализуй `IsAvailable/Execute` в своём `PauseIntegration`). Управление не восстановилось —
риск R3.

**Далее:** шаг 13.

---

## Шаг 13. Проверка производительности на MacBook Air M4

В Play Mode сцены `District750IntegrationTest`:

1. `Window → Analysis → Profiler`, `Window → Analysis → Rendering` — открой и оставь открытыми;
2. `Game` view → разрешение 1600×900, `Stats` включён;
3. замерь: время генерации (`GenerationTimeMs` в `Last Report`), FPS в покое (камера сверху
   без движения), FPS при движении камеры, число объектов в `Stats` (`Batches`, `Tris`);
4. заполни таблицу фактическими числами.

**Ожидаемо:** генерация завершается один раз, игра не «подвисает» каждый кадр.

**Правило приёмки:** если FPS в покое на 1600×900 ниже 30 или генерация дольше 10 секунд —
это блокер производительности. Зафиксируй числа и сообщи; **не оптимизируй самостоятельно**:
оптимизация потребует менять `QuarterUnityAdapter` (архитектура) — это за пределами задачи.

**Если ошибка:** числа не снимаются → укажи это прямо («не измерено»), не придумывай.

**Далее:** шаг 14.

---

## Шаг 14. Исправление только подтверждённых ошибок

Правила:

1. Правится только то, что реально воспроизвелось: сообщение компилятора, упавший тест,
   расхождение с референсом, цифра из Console.
2. Правь минимально: одна причина — одно изменение в одном-двух файлах.
3. После каждой правки повтори **только** соответствующую проверку:
   * `error CS` → шаг 5;
   * упавший тест → шаг 9;
   * сцена/объекты → шаг 6 и 10;
   * меню/управление → шаги 7, 11, 12.
4. Запрещено: менять планировку District750, `seed`, `Quarter250`, `CityTest`, `Apartment67`,
   `Economy`, `Delivery`, `Debts`, `Phone`, дизайн меню, asmdef-имена сборок, добавлять новые
   игровые системы, делать рефакторинг «для красоты».
5. Если правка требует изменения архитектуры или существующих игровых систем — **СТОП**,
   сообщи блокер с симптомом, причиной, файлом и предложением.

---

## Шаг 15. Итоговый отчёт части A

Сохрани отчёт вне `Assets`, например
`/Users/homepc/Projects/SimGame/City/UnityIntegration/reports/final-report.md`:

```
1. Что сделано (шаги 1–14), дата, версия Unity, commit репозитория.
2. Компиляция: ошибок / предупреждений (фактические строки).
3. Тесты: EditMode — результат; PlayMode — сколько всего/упало; ссылка на xml.
4. District750: объектов, жилых домов, точек доставки, время генерации, ErrorCount.
5. Меню: сверка с Pause Menu.PNG — список пунктов и расхождений.
6. Управление: какие компоненты назначены в Player Input Behaviours;
   блокировка/восстановление — факт.
7. Данные: какие поля реальные, какие «Нет данных», откуда взяты (файл → тип → метод).
8. Производительность: FPS, время генерации, батчи/трис.
9. Что НЕ проверено и почему.
10. Блокеры и незакрытые риски.
11. (после части B) стартовая сцена игры, файлы резервных копий, изменённые сцены.
12. (после части B) что подключено автоматически, что назначено вручную.
13. (после части B) переход квартира №67 → District750: есть или требует решения владельца.
```

Не пиши «интеграция завершена», если не выполнены фактически шаги 5–12 И шаги 16–22.

---

# ЧАСТЬ B. Финальный этап: интеграция в настоящую игру (обязательно)

Тестовая сцена `District750IntegrationTest.unity` — не результат. Результат — обычная
стартовая сцена игры, которая запускается Play и работает.

Все команды ниже выполняются из клона репозитория, если не указано иное. Для batch-режима
**Editor должен быть закрыт** (иначе `Temp/UnityLockfile`).

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
PROJ="/Users/homepc/Projects/LifeFromZero"
OUT="$PROJ/LifeFromZeroIntegration"
```

## Шаг 16. Обнаружить настоящую стартовую сцену

```bash
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroDiscovery.DetectBatch \
  -logFile /tmp/lfz-detect.log
echo "exit=$?"
grep -E "LFZ-DISCOVERY" /tmp/lfz-detect.log | head -80
ls -1 "$OUT/reports/" | tail -3
```

Альтернатива без batchmode: `Tools → Life From Zero → Detect start scene`
(только чтение, ничего не меняет).

**Ожидаемо:** строка `[LFZ-DISCOVERY] RESULT: OK report=<путь>`; в отчёте таблица всех сцен
с пометками `[PLAY-START]`, `[build N]`, `[profile N]`, `[camera]`, плюс разделы «Переходы»,
«Обработчики Esc», «Вызовы SceneManager»; строка `Стартовая сцена: <путь>`.

**Успех:** `exit=0`, отчёт создан, стартовая сцена определена ИЛИ явно перечислены причины
неопределённости и список кандидатов.

**Если ошибка:**
* `Стартовая сцена: НЕ ОПРЕДЕЛЕНА` → автоматически НЕ выбирай по названию. Возьми кандидатов
  из раздела «Сцены проекта» (`[PLAY-START]`, `[build 0]`), открой их в Editor, определи,
  где начинается игра (квартира №67 или сцена-загрузчик), и передай путь в шаг 18 явно.
* `тип UnityEditor.Build.Profile.BuildProfile не найден` → Unity младше 6.0 или API переехал:
  работай по `[build N]` из Build Settings, это штатный сигнал.
* пустой список сцен → неверный `-projectPath`.

**Далее:** шаг 17. Запиши выбранный путь — он нужен как `START` ниже.

```bash
START="Assets/…/ВашаСтартоваяСцена.unity"   # путь из шага 16, внутри Assets
```

## Шаг 17. Проверить существующий игровой сценарий (ничего не меняя)

1. Открой `START` в Unity Editor (`Assets` → двойной клик).
2. `Play`. Зафиксируй фактами: где появляется игрок, работает ли управление и камера,
   есть ли уже своё меню/Esc, что происходит при нажатии `Esc`, как выглядит переход на улицу.
3. `Stop`. Ничего не сохраняй.

**Успех:** ты знаешь, какой сценарий игры нельзя ломать, и какие компоненты управления
реальные (их имена понадобятся в шаге 20).

**Если ошибка:** игра не запускается ещё до наших изменений → это блокер проекта,
не связанный с ТЗ: сообщи симптом и остановись.

**Далее:** шаг 18.

## Шаг 18. Dry-run интеграции (план без изменений)

```bash
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.IntegrateBatch \
  -lfzStartScene "$START" -lfzDryRun -logFile /tmp/lfz-integrate-dry.log
echo "exit=$?"
grep -E "LFZ-INTEGRATE" /tmp/lfz-integrate-dry.log | head -60
```

**Ожидаемо:** `[LFZ-INTEGRATE] RESULT: DRY_RUN_OK start=… district=… changes=N`, разделы
«Найдено», «Изменения», «Кандидаты для выбора вручную», «Действия», и файл
`$OUT/reports/integration-<время>-dryrun.md`. Ни один файл проекта не изменён.

**Успех:** `exit=0`, нет строки `RESULT: BLOCKED`, план понятен.

**Если ошибка:**
* `BLOCKED: в стартовой сцене уже есть собственная пауза: …` → два меню на Esc недопустимы.
  Решение владельца: (а) отключить старое меню и повторить, либо (б) не добавлять наше меню.
  Не удаляй чужой код молча.
* `BLOCKED: не передан -lfzStartScene` → передай путь (шаг 16).
* `BLOCKED: тип District750Link не найден` → повтори установку (шаг 4) и дождись компиляции.
* `BLOCKED: стартовая сцена открыта с несохранёнными изменениями` → закрой Editor,
  сохранив сцену, и повтори.
* `BLOCKED: нельзя интегрировать в тестовую сцену` → выбран неверный путь.

**Далее:** шаг 19 только при `DRY_RUN_OK`.

## Шаг 19. Резервные копии и интеграция

```bash
"$UNITY" -batchmode -quit -projectPath "$PROJ" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.IntegrateBatch \
  -lfzStartScene "$START" -logFile /tmp/lfz-integrate.log
echo "exit=$?"
grep -E "LFZ-INTEGRATE" /tmp/lfz-integrate.log | head -80
ls -1 "$OUT/backups/" | tail -3
```

Альтернатива в Editor: `Tools → Life From Zero → Integrate Into Game` → выбери сцену →
«Интегрировать в игру» (окно показывает те же данные, что и dry-run).

**Ожидаемо:** `RESULT: OK start=… district=Assets/_Project/Scenes/District750.unity pauseCreated=…
keyboard=… inputAssigned=… backups=…`; в `$OUT/backups/<время>/` лежат копии изменяемых файлов
(как минимум `Assets/…/START.unity`, при изменении — `ProjectSettings/EditorBuildSettings.asset`);
в проекте появились `Assets/_Project/Scenes/District750.unity` (с объектом `City` и компонентом
`PlayerHomeAnchor`), объект `Life From Zero — District750 Link` в стартовой сцене.

В отчёте `integration-<время>.md` обязательны строки привязки квартиры №67:
`квартира №67: дом bld-n3 (5 эт., центр (210; 630)), домов северного квартала: 4` и
`квартира №67: дверь (205; 636.4), выход во двор (205; 638.4) (2 м от двери)`.
Если вместо них `БЛОКЕР: квартира №67 привязана к …` — см. R15, изменения не сохранены.

**Успех:** `exit=0`, резервные копии существуют, отчёт `integration-<время>.md` записан,
стартовая сцена сохранена (в ней появился объект `Pause`, если своего меню не было).

**Если ошибка:**
* `exit!=0` + `RESULT: BLOCKED` → изменения НЕ сохранены (или откатены). Разбери блокеры,
  резервные копии в `$OUT/backups/` уже есть.
* `не удалось сохранить сцену` → сцена защищена от записи/VCS: откати вручную из
  `$OUT/backups/` и сообщи блокер.
* нет каталога `backups` → интеграция не начиналась (см. блокеры в логе).

**Далее:** шаг 20.

## Шаг 20. Подключить реальные компоненты управления и данных

1. Открой отчёт `$OUT/reports/integration-*.md` → раздел «Кандидаты для выбора вручную».
   Там перечислены реальные компоненты с путями в иерархии (`'Player/Player' → Namespace.Type`).
   Если кандидат был ровно один (или все явно «движение/камера»), инструмент уже назначил его
   в `playerInputBehaviours` — проверь это в Inspector.
2. Если назначения не было: `Pause` → `StandalonePauseIntegration` → **Player Input Behaviours**
   → `Size` → перетащи компоненты движения и камеры. Не назначай компоненты паузы.
3. Esc: если в отчёте `keyboard=false`, значит Esc уже читается кодом проекта — вызови
   `PauseController.Instance.HandleEscape()` из этого обработчика (файлы и строки есть в отчёте
   обнаружения, раздел «Обработчики Esc»). Если `keyboard=true`, `PauseKeyboardInput` работает сам.
4. Данные: при необходимости скопируй шаблон (шаг 8) и повесь `GamePauseDataSource` на `Pause`.

**Успех:** в Play Mode `Esc` открывает меню, управление блокируется, после закрытия —
восстанавливается; в Console нет ошибок.

**Если ошибка:** нет кандидатов управления → `Tools → Life From Zero → Report input candidates`
в игровой сцене; назначение только вручную. Без заполненного массива пауза не открывается
(`CanOpenPause() = false`) — это не баг, а защита.

**Далее:** шаг 21.

## Шаг 21. Квартира №67: привязка к дому и переход «квартира ↔ District750»

По утверждённой карте района v1.1 квартира №67 находится в **юго-западном многоквартирном
доме Северного квартала, южнее футбольной площадки**. Привязка уже задана в данных генератора
(`DistrictMapGenerator`), вручную координаты не расставляются:

| Что | Значение (seed = 1904) |
|---|---|
| Дом квартиры №67 | `bld-n3`, панельная пятиэтажка, центр (210; 630), след 195–225 × 624–636 |
| Футбольная площадка двора | прямоугольник (203; 655)–(217; 665), объект `playground` c `sport=football`, 14×10 м |
| Дверь подъезда `player.home.door` | (205; 0; 636.4), поворот 0 — фасад на север, во двор |
| Выход во двор `player.home.exit` | (205; 0; 638.4) — 2 м от двери; точка возврата из квартиры |
| Квартира | `PlayerHomeInfo.ApartmentNumber = 67`, маркер `player.home` в центре дома |

Дом выбирается кодом, а не константой: `PickPlayerHomeId` берёт кандидатов южнее площадки
и самого западного из них; если кандидатов ноль или ничья — генерация падает с понятным
сообщением, а не угадывает. Поэтому при правках раскладки привязка не «съезжает».

```bash
python3 City/Tools/district_layout_check.py | tail -4
```

**Ожидаемо:** `player_home  bld-n3 (210;630) door (205;636.4) exit (205;638.4)` и
`north_pitch  (203;655)-(217;665) football 14x10`, итог «OK: все проверки пройдены».
Скрипт проверяет: дом южнее площадки и самый западный из южных, дверь вне стен,
выход в 2 м и во двор (не внутрь дома и не в чужое здание).

### 21.1. Компонент привязки в сцене района

`PlayerHomeAnchor` (файл `Assets/City/UnityIntegration/Runtime/PlayerHomeAnchor.cs`) ставится
на объект `City` в сцене района автоматически при шаге 19. Если сцены района ещё нет или
компонент пропал:

```bash
U="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
P="/Users/homepc/Projects/LifeFromZero"
"$U" -batchmode -quit -projectPath "$P" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.BindHomeBatch \
  -lfzApartmentScene "<имя сцены квартиры без .unity>" \
  -logFile /tmp/lfz-home.log
grep -E "LFZ-INTEGRATE|PlayerHomeAnchor" /tmp/lfz-home.log | head -40
```

**Ожидаемо:** `RESULT: OK home-anchor scene=Assets/_Project/Scenes/District750.unity`,
перед изменением — резервная копия сцены в `LifeFromZeroIntegration/backups/<время>/`.

### 21.2. Имя сцены квартиры задаёт Codex, а не инструмент

Инструмент НЕ угадывает имя существующей сцены квартиры. Возьми его из отчёта обнаружения
(шаг 16) и задай одним из способов:
* `-lfzApartmentScene Apartment67` в шаге 19/21.1; или
* `Inspector → PlayerHomeAnchor → Apartment Scene Name`.

Пока поле пустое, `OpenApartment()` пишет предупреждение и **ничего не делает** — фиктивного
перехода нет. Убедись, что сцена квартиры присутствует в Build Settings / Build Profile.

### 21.3. Соединить с существующими переходами (без выдуманных механик)

Компонент пассивный: нет `Update`, нет опроса клавиш, нет самодельных триггеров.

1. **Квартира → район.** В сцене квартиры у существующей двери/выхода в `UnityEvent`
   выбери объект с `District750Link` → метод `District750Link.OpenDistrict`.
   Если перехода из квартиры на улицу в проекте нет — это решение владельца (стоп-условие 7).
2. **Район → квартира.** У `PlayerHomeAnchor` есть `OpenApartment()`. Вызови его из
   существующей системы взаимодействия (интеракция у двери подъезда). Если система работает
   по триггеру — включи `Create Door Trigger` у компонента (по умолчанию выключено) и повесь
   обработчик по правилам проекта. Привязку «дверь → компонент» делает Codex в редакторе:
   объект двери создаётся в рантайме, поэтому ссылка назначается через существующий механизм,
   а не через Inspector сцены.
3. **Возврат из квартиры.** Спавн бери из данных, а не из константы:
   `PlayerHomeAnchor.SpawnPosition` и `SpawnRotation` (точка `player.home.exit`),
   либо `ApplyToSpawn(playerTransform)` — метод сам корректно переносит `CharacterController`
   и `Rigidbody`. Если в проекте своя система спавна — передай ей эти координаты.

### 21.4. Проверка

Play Mode:

* дверь подъезда `bld-n3` находится в северном дворе, южнее футбольной площадки;
* Console: `[PlayerHomeAnchor] Квартира №67 привязана: дом=bld-n3, дверь=(205; 0; 636.4),
  выход=(205; 0; 638.4), сцена квартиры=<имя>`;
* переход в квартиру и обратно работает; после возврата игрок появляется во дворе у подъезда,
  а не в центре карты и не внутри дома.

**Если ошибка:**
* `В плане «district750» нет player.home` → раскладка сломана: смотрите `PickPlayerHomeId`
  и `district_layout_check.py` (дом квартиры выбирается по данным).
* `Сцена квартиры не задана` → шаг 21.2.
* `сцена … недоступна` → добавь сцену квартиры в Build Settings / Build Profile.
* Игрок появляется не там → используется чужая система спавна: передай ей
  `PlayerHomeAnchor.SpawnPosition`, не переписывай генератор.

**Далее:** шаг 22.

## Шаг 22. Финальная проверка обычного запуска (критерий завершения)

Если игра до этого работала на старом районе Quarter250 — сначала выполни часть C (шаги 23–28):
без неё в проекте могут строиться два района одновременно.

Открой `START`, нажми Play и проверь по пунктам:

- [ ] игра начинается там, где предусмотрено проектом (квартира №67 или сцена-загрузчик);
- [ ] управление персонажем работает;
- [ ] камера работает;
- [ ] District750 загружается и строится (лог CityBootstrap, ожидаемые числа);
- [ ] `Esc` открывает меню паузы, повторный `Esc` закрывает;
- [ ] во время паузы управление и камера заблокированы, время остановлено, курсор доступен;
- [ ] после закрытия меню управление и курсор восстанавливаются;
- [ ] телефон по `P` работает только при открытой паузе (и только если так было задумано);
- [ ] существующие механики живы (экономика, доставка, долги, телефон — без правок);
- [ ] в Console нет `error CS`, `NullReferenceException`, `MissingReferenceException`;
- [ ] тестовая сцена НЕ стала стартовой: `ProjectSettings/EditorBuildSettings.asset` →
      проверь, что `District750IntegrationTest.unity` не стоит первой;
- [ ] проект сохранён: `Cmd+S` для сцены и `File → Save Project`.

**Успех:** все пункты отмечены фактически. Только тогда интеграция завершена.

**Если что-то не работает** — не объявляй завершение: укажи конкретный пункт, симптом,
вероятную причину и файл (см. риски R7–R10 и шаг 14). Правки только подтверждённых причин.

---

# ЧАСТЬ C. Миграция Quarter250 → District750 (шаги 23–28)

Выполняется, если игра раньше строила квартал 250×250 (`QuarterGenerator`, `layoutId=quarter250`).
Цель: District750 становится **основным районом**, старый квартал остаётся в проекте и доступен
для отката. Числа 250 на 750 не подменяются «слепой заменой»: район переключается полем
`Layout Id` компонента, код старого генератора не правится и не удаляется.

```bash
U="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
P="/Users/homepc/Projects/LifeFromZero"
START="Assets/…/СценаСCityBootstrap.unity"   # $START из шага 16/отчёта шага 23
OUT="$P/LifeFromZeroIntegration"
```

## Шаг 23. Диагностика проекта (только чтение)

```bash
"$U" -batchmode -quit -projectPath "$P" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroMigration.DiagnoseBatch \
  -logFile /tmp/lfz-migration.log
echo "exit=$?"
grep -E "LFZ-MIGRATION" /tmp/lfz-migration.log | head -30
ls -1 "$OUT/reports/" | tail -3
```

Альтернатива без batchmode: `Tools → Life From Zero → Migration: diagnose Quarter250 to District750`
(или кнопка «Диагностика миграции» в окне `Integrate Into Game`).

**Ожидаемо:** `[LFZ-MIGRATION] RESULT: OK report=…/migration-<время>.md`, exit=0.
Инструмент ничего не изменяет: только открывает сцены аддитивно и читает код; отчёт пишется вне `Assets`.

Разделы отчёта: «Генераторы района» (`CityBootstrap` по сценам: `layoutId`, `seed`,
`generateOnStart`, `addColliders`, `QuarterUnityAdapter`, `buildOnStart`), «Камеры»,
«Кандидаты точки спавна игрока» (это список для проверки, не автопривязка), «Build Settings»,
«Совпадения в коде», «Риски», «Действия».

**Если ошибка:** сцена не открылась → закрой Editor и повтори в batch; отчёт не записан →
проверь права на `$OUT`.

## Шаг 24. Устранить риск двойной генерации

Прочитай «Риски» отчёта и действуй по правилам:

1. **Два `CityBootstrap` в одной сцене** — оставить один. Какой именно — решает владелец (стоп-условие 9).
2. **`QuarterUnityAdapter.Build On Start = on`** рядом с `CityBootstrap` — выключить: иначе при старте
   строятся два города (шаг 26/27 сам ставит `off` на адаптере того же объекта, отдельный адаптер — блокер).
3. **Два района в разных сценах, загружаемых аддитивно**, статически не видны. В Play Mode в Console
   должна быть ровно одна строка `[CityBootstrap] Город построен`. Если их две — сцены грузятся
   одновременно, оставь одну районную.
4. **`Generate On Start = off`** — район сам не появится: нужен вызов `CityBootstrap.Generate()`
   из существующего кода. Не переписывай генератор, используй существующую точку запуска.

**Успех:** в отчёте нет строк «ДВОЙНАЯ ГЕНЕРАЦИЯ».

## Шаг 25. Решения по коду: что НЕ менять

* `QuarterLayoutIds.Quarter250` в коде — значение Layout Id **по умолчанию**, а не «магическое число».
  Код не переписывается: район переключается полем `Layout Id` в компоненте (шаг 27).
* Совпадения `magic-250` / `magic-125` — **не заменять на 750/375 автоматически**. В каждом месте
  нужен смысл: размер квартала, половина стороны, координата, лимит массива, радиус. Оформи в отчёте
  таблицу «файл:строка → смысл → решение (оставить / параметризовать / изменить)».
* `old-generator` (`QuarterGenerator`) и его тесты **не удалять и не переписывать**: District750 —
  отдельный генератор того же контракта `QuarterPlan`. Quarter250 остаётся рабочим для отката.
* Сцены, скрипты и ассеты Quarter250 не удалять; пользовательские сцены не трогать.

## Шаг 26. Dry-run переключения основного района

```bash
"$U" -batchmode -quit -projectPath "$P" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroMigration.MigrateBatch \
  -lfzScene "$START" \
  -logFile /tmp/lfz-main-district-dry.log
grep -E "LFZ-MIGRATION" /tmp/lfz-main-district-dry.log | head -60
```

**Ожидаемо:** `RESULT: DRY_RUN_OK report=…/main-district-<время>-dryrun.md`, в «Изменениях»:
`Layout Id: … → district750`, `Generate On Start`, `Add Colliders`, `Adapter Build On Start: on → off`;
«Блокеры» — пусто. Ничего не сохранено, бэкап в dry-run не создаётся.

**Если `сцена не найдена`** — `$START` не та сцена: возьми путь из отчёта шага 23 (раздел «Генераторы района»).
**Если `в сцене 2 CityBootstrap`** — шаг 24.
**Если `сцена открыта с несохранёнными изменениями`** — сохрани её (`Cmd+S`) и повтори.

## Шаг 27. Применить и проверить

```bash
"$U" -batchmode -quit -projectPath "$P" \
  -executeMethod LifeFromZero.Setup.LifeFromZeroMigration.MigrateBatch \
  -lfzScene "$START" -lfzApply \
  -logFile /tmp/lfz-main-district.log
echo "exit=$?"
grep -E "LFZ-MIGRATION" /tmp/lfz-main-district.log | head -60
```

**Ожидаемо:** `RESULT: OK`, exit=0, резервная копия сцены и снимок `EditorBuildSettings` в
`$OUT/backups/<время>/` (+ `README.txt` с составом). Сцена сохраняется **только** если блокеров 0:
пост-проверки (`Layout Id == district750`, ровно один `CityBootstrap`) выполняются до записи на диск.

Проверка в Play Mode — по одному пункту, фактами:

- [ ] район строится один раз: одна строка `[CityBootstrap] Город построен: макет=district750,
      seed=1904, жилых зданий=15, объектов=974, точек доставки=46`;
- [ ] спавн игрока внутри 0..750, на земле, не внутри здания;
- [ ] камера: `orthographicSize ≥ 375` для обзора всей карты либо `far clip ≥ 800` для перспективы;
- [ ] границы мира: на краю карты нет провала, объекты не выходят за 750;
- [ ] коллизии: игрок упирается в стены домов и бордюры (`Add Colliders` = on);
- [ ] старый квартал не строится одновременно с District750;
- [ ] квартира №67 на месте (шаг 21): дверь и выход во дворе `bld-n3`;
- [ ] FPS не хуже, чем на Quarter250 (фактический замер, не оценка).

Если пункт не выполнен — не объявляй миграцию завершённой: зафиксируй пункт, симптом и файл.

## Шаг 28. Откат (если что-то не так)

```bash
ls "$OUT/backups/" | tail -3
cp -v "$OUT/backups/<время>/Assets/…/<сцена>.unity" "$P/Assets/…/<сцена>.unity"
# затем в Inspector компонента CityBootstrap вернуть Layout Id = quarter250 и нажать Cmd+S
```

**Ожидаемо:** игра снова строит Quarter250, все старые механики работают.
Quarter250 не удалялся и не изменялся — откат это одна резервная копия сцены и одно поле.
Если бэкапа нет — остановись и сообщи блокер, не восстанавливай район «руками».

**Далее:** шаг 22 (финальная проверка обычного запуска).

## Риски и минимальные исправления

**R1. Ссылка на несуществующую сборку uGUI**
* Симптом: `error CS0234: The type or namespace name 'UI' does not exist in the namespace 'UnityEngine'`,
  либо `CS0246: Text/Button/Image/EventSystem`; в asmdef-инспекторе ссылка с предупреждением.
* Причина: в asmdef указано имя `Unity.ugui` — такого assembly в Unity 6000.3 нет.
* Файл: `Assets/City/PauseMenu/LifeFromZero.City.Pause.asmdef` (и `…Pause.Tests.asmdef`).
* Минимальная правка: `"references": ["UnityEngine.UI"]`, пересохранить, дождаться рекомпиляции.
* Повторная проверка: шаг 5.

**R2. `PauseKeyboardInput.cs` попал не в `Assembly-CSharp`**
* Симптом: `error CS0246: The type or namespace name 'PauseController' could not be found`.
* Причина: над `Assets/City` есть свой `.asmdef`, который захватывает `PauseInputSystem/`.
  (Установщик об этом предупреждает на этапе 4.)
* Файл: `Assets/City/PauseInputSystem/PauseKeyboardInput.cs` + тот `.asmdef`.
* Минимальная правка: добавить в этот asmdef ссылку `LifeFromZero.City.Pause`
  (`"references"` += `"LifeFromZero.City.Pause"`). Не двигать файл.
* Повторная проверка: шаг 5.

**R3. Управление не восстанавливается после закрытия паузы**
* Симптом: персонаж/камера не реагируют на ввод после `Esc`.
* Причина: в `Player Input Behaviours` попал компонент самой паузы либо компонент, который
  включён не был (`StandalonePauseIntegration` запоминает исходное состояние и возвращает его).
* Файл: `Assets/City/PauseMenu/StandalonePauseIntegration.cs` (правится только при
  воспроизведении), поле в Inspector — фактическая причина.
* Минимальная правка: убрать из массива компоненты паузы; заново назначить только
  движение и камеру.
* Повторная проверка: шаг 7, затем шаг 12.

**R4. District750 не строится / строятся два города**
* Симптом: в Console `[CityBootstrap] Генерация города не удалась…`, либо два корня
  `Quarter_seed1904`.
* Причина: `Generate On Start` и `Build On Start` включены одновременно, либо `Layout Id`
  указан с опечаткой (`district750` — без пробелов и в любом регистре, но без иных символов).
* Файл: `Assets/City/UnityAdapter/CityBootstrap.cs`, `QuarterUnityAdapter.cs` (менять код
  не требуется — только поля в Inspector).
* Минимальная правка: `CityBootstrap.GenerateOnStart = on`, `QuarterUnityAdapter.BuildOnStart = off`.
* Повторная проверка: шаг 10.

**R5. Тестовые сборки не компилируются**
* Симптом: `Assembly has reference to non-existent assembly 'UnityEngine.TestRunner'`,
  в Test Runner нет тестов.
* Причина: не установлен пакет `com.unity.test-framework`.
* Файл: `Packages/manifest.json` проекта.
* Минимальная правка: `Window → Package Manager → Unity Test Framework → Install`
  (или добавить `"com.unity.test-framework": "1.4.x"` в manifest).
* Повторная проверка: шаг 9.

**R6. Медленный первый кадр / низкий FPS**
* Симптом: провал FPS после Play, сотни тысяч треугольников.
* Причина: 974 объекта строятся примитивами; детали домов — отдельные меши (так задумано).
* Файл: `Assets/City/UnityAdapter/QuarterUnityAdapter.cs`.
* Минимальная правка: НЕ править код. Зафиксировать числа и сообщить блокер (шаг 13).

**R7. В стартовой сцене уже есть собственное меню паузы**
* Симптом: `RESULT: BLOCKED`, в блокере перечислены компоненты с «paus» в имени.
* Причина: проект уже имеет свой обработчик Esc/меню; два меню на одну клавишу недопустимы.
* Файл: компоненты из блокера (локальный проект), наш — `Assets/City/PauseMenu/PauseController.cs`.
* Минимальная правка: по решению владельца либо отключить/убрать старый обработчик и повторить
  шаг 19, либо не добавлять наше меню и реализовать `PauseIntegration` поверх старого.
* Повторная проверка: шаг 18 (dry-run) → шаг 19.

**R8. Двойная обработка Esc**
* Симптом: меню открывается дважды / открывается и сразу закрывается; в отчёте
  интеграции `keyboard=false`.
* Причина: Esc читают и существующий код проекта, и наш `PauseKeyboardInput`.
* Файл: места Esc из отчёта обнаружения (`LifeFromZeroIntegration/reports/discovery-*.md`),
  наш — `Assets/City/PauseInputSystem/PauseKeyboardInput.cs`.
* Минимальная правка: оставить один обработчик. Правильный путь — вызывать
  `PauseController.Instance.HandleEscape()` из существующего ввода; `useStandaloneKeyboard`
  при этом остаётся выключенным.
* Повторная проверка: шаг 20, затем шаг 22.

**R9. District750 не открывается из игры**
* Симптом: `[District750Link] сцена 'District750' недоступна: добавьте её в Build Settings`.
* Причина: сцена района не зарегистрирована в списке сцен сборки (или проект грузит сцены
  по-своему). Инструмент добавляет её только если стартовая сцена уже в Build Settings.
* Файл: `ProjectSettings/EditorBuildSettings.asset`, отчёт `integration-*.md`, раздел «Действия».
* Минимальная правка: добавить `Assets/_Project/Scenes/District750.unity` в ту же систему сцен,
  что и стартовая; порядок существующих сцен не менять.
* Повторная проверка: шаг 21.

**R10. Инструмент отказался работать со стартовой сценой**
* Симптом: `BLOCKED: нельзя интегрировать в тестовую сцену` / `сцена не найдена` /
  `несохранённые изменения`.
* Причина: передан путь тестовой сцены, неверный путь, либо сцена открыта с несохранёнными
  правками (в batch-режиме сохранение пользовательских правок не выполняется).
* Файл: аргумент `-lfzStartScene`; состояние Editor.
* Минимальная правка: передать настоящую стартовую сцену; сохранить/закрыть сцену в Editor.
* Повторная проверка: шаг 18.

**R11. Интеграция изменила сцену, но что-то пошло не так**
* Симптом: после шага 19 в стартовой сцене появились лишние объекты или она не запускается.
* Причина: локальные особенности проекта (скрипты с `[ExecuteAlways]`, несовместимые настройки).
* Файл: `LifeFromZeroIntegration/backups/<время>/` — там точные копии изменённых файлов.
* Минимальная правка: скопировать файлы из бэкапа обратно (сохранив относительные пути),
  закрыть Editor, сообщить блокер с точным путём бэкапа.
* Повторная проверка: открыть стартовую сцену и Play.

**R12. Два района строятся одновременно**
* Симптом: в Console две строки `[CityBootstrap] Город построен`, объекты двух районов наложены,
  FPS падает вдвое; либо `QuarterUnityAdapter` строит город до `CityBootstrap`.
* Причина: два `CityBootstrap` в одной сцене или загружаемых аддитивно сценах; `Build On Start = on`
  у адаптера рядом с `CityBootstrap`.
* Диагностика: `LifeFromZeroMigration.DiagnoseBatch` → раздел «Риски» (строки «ДВОЙНАЯ ГЕНЕРАЦИЯ»).
* Минимальная правка: оставить один `CityBootstrap`, выключить `Build On Start` у отдельного адаптера.
  Не удалять сцены и не «отключать» генерацию кодом.

**R13. Слепая замена 250 → 750**
* Симптом: после «массовой замены» рассыпаются координаты, лимиты массивов, радиусы, тесты.
* Причина: числа 250/125 в проекте означают разное (размер квартала, половина стороны, координата, лимит).
* Минимальная правка: не заменять. Разобрать совпадения `magic-250` / `magic-125` из отчёта шага 23
  по одному, с решением на каждую строку. Район переключается полем `Layout Id`.

**R14. Спавн, камера или границы не соответствуют району 750×750**
* Симптом: игрок появляется вне карты или в воздухе; видна только часть района; на краю карты провал;
  камера «режет» дальние дома.
* Причина: старые координаты спавна от квартала 250×250, `orthographicSize < 375`, малый `far clip`,
  выключенные коллизии, смещённый объект `City`.
* Минимальная правка: спавн — в точку `player.home.exit` (или по данным проекта), камера — `size ≥ 375`
  либо `far clip ≥ 800`, `Add Colliders = on`, `City` в начале координат. Числа брать из отчёта
  шага 23, не придумывать.

**R15. Привязка квартиры №67 «съехала»**
* Симптом: генерация падает с `Квартира №67: ни один дом северного квартала не стоит южнее футбольной
  площадки` или `…определён неоднозначно`; либо дверь/выход не там.
* Причина: изменена раскладка северного двора (`BuildNorthQuarter`, площадка, дома `bld-n*`).
* Минимальная правка: восстановить раскладку по данным `DistrictMapGenerator` и `district_layout_check.py`
  (эталон: `player_home bld-n3 (210;630) door (205;636.4) exit (205;638.4)`). Не «прописывать» координаты
  дома руками — правило выбора (`PickPlayerHomeId`) должно остаться в данных.

---

## Стоп-условия (сообщи блокер и остановись)

1. Правка требует изменения архитектуры или существующих систем (`Economy`, `Delivery`,
   `Debts`, `Phone`, `Apartment67`, `CityTest`).
2. Нет реального контроллера движения/камеры — назначать нечего.
3. Числа District750 в Console не совпадают с ожидаемыми после установки без правок.
4. Конфликты файлов при установке, не подтверждённые владельцем.
5. Изменения в репозитории `SimGame` обязательны для локальной интеграции
   (сначала отчитайся, не правь ветку сам).
6. В стартовой сцене уже есть чужое меню паузы (R7) — решение принимает владелец.
7. В проекте нет перехода из квартиры №67 на улицу: где он должен быть — решает владелец,
   а не Codex.
8. Стартовую сцену нельзя определить однозначно и владелец недоступен: покажи кандидатов
   и остановись (не выбирай «по названию»).
9. В сцене несколько `CityBootstrap` (или два района грузятся аддитивно), и непонятно, какой
   из них основной: решение владельца, а не выбор Codex.
10. Имя существующей сцены квартиры неизвестно: не подставляй `Apartment67` «по аналогии» —
    возьми из отчёта обнаружения или спроси владельца.
11. Миграция требует замены чисел 250 → 750 в коде: это отдельное решение владельца (R13).

## Критерий завершения

Интеграция считается выполненной, когда на этом Mac фактически проверено:

* **часть A:** компиляция без `error CS`; EditMode и PlayMode-тесты зелёные; District750
  строится с указанными числами в тестовой сцене; отчёт шага 15 заполнен фактами;
* **часть B:** пользователь открывает обычную стартовую сцену LIFE FROM ZERO, нажимает Play
  и получает работающую игру — игра начинается в предусмотренном месте, управление персонажем
  и камера работают, District750 корректно загружается, `Esc` открывает меню с блокировкой
  и восстановлением управления, существующие механики живы, критических ошибок в Console нет,
  тестовая сцена не стала стартовой;
* **часть B, квартира №67:** привязка к дому `bld-n3` (юго-западный дом Северного квартала,
  южнее футбольной площадки) подтверждена отчётом и Play Mode; переход «квартира ↔ район»
  работает, игрок возвращается к подъезду (шаг 21);
* **часть C (если проект жил на Quarter250):** District750 — основной район, район строится
  один раз, спавн/камера/границы/коллизии проверены, Quarter250 сохранён и откат возможен
  (резервная копия + `Layout Id`), числа 250 → 750 в коде не заменялись.

Если хотя бы один пункт части B не выполнен — интеграция НЕ завершена. Сообщи конкретную
причину и что именно осталось.
