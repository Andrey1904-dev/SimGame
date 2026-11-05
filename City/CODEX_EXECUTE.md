# CODEX_EXECUTE — интеграция LIFE FROM ZERO (ТЗ #038)

Исполняемый сценарий. Выполняй шаги по порядку, не проектируй интеграцию заново.
Для каждого шага: команда → ожидаемый результат → признак успеха → что делать при ошибке.

Не читай старые отчёты (`FULL_AUDIT_REPORT.md`, `INTEGRATION_AUDIT.md`, `TZ021_*`,
`CODEX_COPY_PASTE_TASKS.md` и т.п.) и не проводи повторный аудит проекта: всё нужное
для интеграции уже собрано здесь и в `City/UnityIntegration/`.

| Параметр | Значение |
|---|---|
| Репозиторий | `https://github.com/Andrey1904-dev/SimGame` |
| Ветка с подготовкой | `main` (после слияния PR ТЗ #038); до слияния — `arena/672047f2-simgame` |
| Локальный Unity-проект | `/Users/homepc/Projects/LifeFromZero` |
| Unity / URP | 6000.3.27f1 / 17.3.0 |
| Машина | macOS, Apple Silicon M4, 16 ГБ RAM |
| Город | District750, 750×750 м, `seed = 1904`, `layoutId = district750` |
| Меню паузы | `Esc`, дизайн по `Pause Menu.PNG` (уже в проекте) |
| Телефон | английская `P`. Никаких `I`, `M`, `J` |

Что уже готово в репозитории: исходники модулей (`City/Quarter`, `City/UnityAdapter`,
`City/Tests`, `City/PauseMenu`, `City/PauseInputSystem`, `City/PauseTests`), установщик
`City/UnityIntegration/install_to_unity.sh`, Editor-инструмент
`City/UnityIntegration/Editor/LifeFromZeroSetup.cs`, проверки
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
`ADD Assets/City/...` (35 файлов при первом запуске) и итог
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
* `run_checks.sh`: этапы 1, 2, 4, 5, 6, 7 = `PASS`, этап 3 = `PASS` при наличии `dotnet`,
  иначе `NOT RUN`, `exit=0`.

**Успех:** нет упавших тестов и нет `error CS` в логах этапа 6.

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

## Шаг 15. Итоговый отчёт

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
```

Не пиши «интеграция завершена», если хотя бы один из шагов 5–12 не выполнен фактически.

---

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

---

## Стоп-условия (сообщи блокер и остановись)

1. Правка требует изменения архитектуры или существующих систем (`Economy`, `Delivery`,
   `Debts`, `Phone`, `Apartment67`, `CityTest`).
2. Нет реального контроллера движения/камеры — назначать нечего.
3. Числа District750 в Console не совпадают с ожидаемыми после установки без правок.
4. Конфликты файлов при установке, не подтверждённые владельцем.
5. Изменения в репозитории `SimGame` обязательны для локальной интеграции
   (сначала отчитайся, не правь `main` сам).

## Критерий завершения

Интеграция считается выполненной, когда на этом Mac фактически проверено: компиляция без
`error CS`; EditMode и PlayMode-тесты зелёные; District750 строится с указанными числами;
`Esc` открывает и закрывает меню с блокировкой и восстановлением управления; `P` работает
только для телефона; отчёт шага 15 заполнен фактическими данными.
