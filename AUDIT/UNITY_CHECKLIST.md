# Чек-лист для Codex: что сделать в Unity Editor

Составлен по итогам аудита. **Все пункты — про настоящий Unity**, песочница
аудита их выполнить не могла: в репозитории нет ни одной сцены, префаба,
`.meta`-файла и `ProjectSettings`.

Правила работы с этим чек-листом:
1. Идти сверху вниз. Пункт не выполнен — не переходить к следующему.
2. После каждого шага записывать факт: «сделано / не сделано / ошибка + текст».
3. **Не менять игровой код** в шагах 1–4 — только ассеты и сцену.
4. Если шаг требует правки кода — он помечен `[КОД]` и требует разрешения.

---

## Шаг −1. Прогнать проверки ДО Unity (можно прямо сейчас)

Эти команды не требуют Unity и выполняются в песочнице/на машине разработчика.

```bash
cd <repo>

# 1. Генератор района (числа должны совпасть с ожидаемыми)
python3 City/Tools/district_layout_check.py
#   ожидаемо: solids 81, roads 82, points 24, deliveries 46,
#             player_home bld-n3 (205;636.4), OK: все проверки пройдены

# 2. Сводный скрипт интеграции (без Unity часть этапов = NOT RUN, это нормально)
bash City/UnityIntegration/run_checks.sh
#   ожидаемо: PASS 2, FAIL 0, NOT RUN 6

# 3. Тесты UI (нужны pythonnet + Roslyn + .NET 10, см. AUDIT/TEST_RESULTS.md §0)
LFZ_DOTNET_ROOT=<dotnet> LFZ_CORECLR_CONFIG=<rc.json> python3 UI/Tools/checkui.py
LFZ_DOTNET_ROOT=<dotnet> LFZ_CORECLR_CONFIG=<rc.json> python3 UI/Tools/checkeditor.py
LFZ_DOTNET_ROOT=<dotnet> LFZ_CORECLR_CONFIG=<rc.json> python3 UI/Tools/runtests.py
#   ожидаемо: COMPILE: OK errors=0 / Итого: 99/99 PASS, 0 FAIL
```

**Критерий:** все три пункта зелёные. Если нет — сначала разобраться здесь,
в Unity идти рано.

---

## Шаг 0. Создать проект

* [ ] Unity 6 LTS (6000.x), шаблон **3D (Built-In)** — URP не обязателен, код
      не использует URP-шейдеры (проверено: `grep -rn "Universal\|Shader.Find("`).
* [ ] Каталог проекта: `<repo>/UnityProject/` (не внутри `City/`, иначе
      Unity начнёт импортировать `.cs`-файлы вне `Assets/`).
* [ ] `Packages/manifest.json` должен содержать:
      `com.unity.ugui`, `com.unity.test-framework`, `com.unity.inputsystem`
      (последний — только если планируется новый ввод; код поддерживает оба).
* [ ] Player Settings → Active Input Handling: `Both` (код собирается и с
      `ENABLE_INPUT_SYSTEM`, и с `ENABLE_LEGACY_INPUT_MANAGER`).
* [ ] Api Compatibility: `.NET Standard 2.1`, Enable unsafe: выкл.

**Критерий:** пустая сцена открывается, Console чиста.

---

## Шаг 1. Перенести код в `Assets/`

* [ ] Скопировать (или симлинкнуть) папки модулей внутрь `Assets/`:
      `City/`, `Debts/`, `Economy/`, `Jobs/`, `Phone/`, `UI/`,
      `UnityIntegrationKit/`.
* [ ] **Исключить** из импорта: `*/Tests/`, `*NUnitTests.cs`, `*/Tools/`,
      `*/References/`, `*.md`, `*.py`, `*.csproj`.
      Без этого в проект попадут `NUnit`-обёртки (нет пакета) и `UI/Tests/Program.cs`
      с `Main` (конфликт с Unity).
* [ ] Убедиться, что импортированы asmdef:
      `City/Quarter/*.asmdef`, `City/UnityAdapter/*.asmdef`,
      `UI/Core`, `UI/Runtime`, `UI/Editor`, `UI/Tests`.

**Критерий:** Console без ошибок компиляции. Если появились — фиксировать текст,
это первый реальный сигнал о несовместимости с Unity API.

**Ожидаемые проблемы (предсказаны аудитом):**
* ~~`UI/Core` asmdef без ссылки на `LifeFromZero.City.Quarter`~~ — **исправлено
  в `786c7fc`** (был бы `CS0246` по всему UI). Проверить: в Console нет ошибок
  про `QuarterPlan` / `IQuarterPlanSource`.
* `City/UnityIntegration/Runtime/*.cs` без asmdef → `Assembly-CSharp`;
  Editor-скрипты (`City/UnityIntegration/Editor`) должны ссылаться на
  `Assembly-CSharp` или на новый asmdef (см. P2-4 в `ISSUES.md`).
* `City/PauseTests` теперь `Editor`-only — в билд не попадут.

---

## Шаг 2. Добавить в сцену генератор района

* [ ] Новая сцена `Assets/Scenes/Main.unity`.
* [ ] Пустой объект `CityRoot` → компонент `CityBootstrap`
      (`City/UnityAdapter/CityBootstrap.cs`).
* [ ] Настройки `CityBootstrap` в Inspector:
      * `seed` = `12345` (любое; от него зависит раскладка)
      * `generateOnStart` = ✅
      * `addColliders` = ✅ (без коллайдеров не заработает физика/лучи)
* [ ] Отдельный объект `QuarterAdapter` → компонент `QuarterUnityAdapter`,
      поле `cityBootstrap` (или `bootstrap`) указывает на `CityRoot`.
      Точное имя поля — см. `City/UnityAdapter/QuarterUnityAdapter.cs`
      (`[SerializeField]` в начале класса).
* [ ] Нажать Play.

**Критерий:** в иерархии появился объект `Quarter_seed12345`, под ним ≥ 900
дочерних объектов (по `district_layout_check.py`: 974). В Console нет
исключений.

**Если пусто:** проверить `CityBootstrap.generateOnStart` и что
`QuarterUnityAdapter.buildOnStart` не конфликтует (двойной генерации быть не
должно — `LifeFromZeroSetup.cs:121` специально выключает `BuildOnStart`).

---

## Шаг 3. Проверить исправленный P0 (карта/план) — **главная проверка**

Дефект P0-1 уже исправлен в `786c7fc` (жизненный цикл плана вынесен в
`City/Quarter/QuarterPlanSlot.cs`, `Build()` вызывает `ClearBuilt()`, затем
`BeginBuild(plan)`). Здесь нужно убедиться, что в **настоящем Unity** это так.

* [ ] Не правя игровой код, добавить временный тест-компонент:
      ```csharp
      using UnityEngine;
      using LifeFromZero.City.Quarter.UnityAdapter;

      public sealed class PlanProbe : MonoBehaviour
      {
          void Start()
          {
              var a = GetComponent<QuarterUnityAdapter>();
              Debug.Log(a == null
                  ? "PlanProbe: адаптер не найден"
                  : $"PlanProbe: HasPlan={a.HasPlan} Plan={(a.Plan == null ? "null" : "ok seed=" + a.Plan.Seed)}");
          }
      }
      ```
* [ ] Play → **ожидаемый результат: `HasPlan=True Plan=ok seed=<seed>`.**

Если `False` — присвоение плана в `Build()` снова оказалось до очистки;
проверить `City/Quarter/QuarterPlanSlot.cs` и `City/Tests/QuarterPlanSourceTests.cs`.
* [ ] Удалить временный компонент из сцены **и** из проекта (иначе он попадёт в билд).

**Дополнительно (без кода):**

* [ ] Поле `UiBootstrap.districtSource` должно указывать на объект с
      `QuarterUnityAdapter`. Если оставить пустым, `ResolveDistrict()` найдёт
      источник сам (`FindObjectsByType`), но это лишний поиск.
* [ ] Проверить `HasPlan` после повторной генерации (`CityBootstrap.Generate()`
      второй раз из инспектора): план должен обновиться, а не обнулиться.

---

## Шаг 4. Подключить UI

* [ ] Меню `Tools → LIFE FROM ZERO → Установить UI` (пункт `UiInstaller`,
      `UI/Editor/UiInstaller.cs`).
* [ ] Сначала прогнать **dry-run**: пункт меню с `-lfzUiDryRun` (или
      соответствующий флаг в окне). Ожидаемо: отчёт «что будет создано», без
      изменений в сцене.
* [ ] Повторить без dry-run. Установщик делает бэкап сцены — проверить, что
      файл бэкапа появился.
* [ ] Проверить иерархию: `Canvas` (`Screen Space - Overlay`), `EventSystem`,
      канвасы экранов `HUD`/`Map`/`Phone`/`Pause`.
* [ ] Назначить в `UiGameBridge`:
      * `player` → объект игрока (на шаге 4 его может не быть — оставить пустым,
        UI не упадёт, проверено по коду)
      * `districtSource` → `QuarterAdapter`
* [ ] Нажать Play.

**Критерий:** HUD виден, клавиши `M` (карта), `P` (телефон) реагируют.
На карте — сетка района. **С P0 оно будет пустым** — это ожидаемо до фикса.

---

## Шаг 5. Проверить клавиши и паузу

* [ ] `Esc` → меню паузы. Ожидаемо: открывается **существующее** меню
      `City/PauseMenu` (`PauseController`), а не второе из UI.
* [ ] Проверить: `Time.timeScale` возвращается в `1` после закрытия.
      (Единственное место, где он меняется — `PauseController.cs:57,70,93`.)
* [ ] `M` при открытом телефоне → телефон закрывается, карта открывается
      (один активный экран, проверяется `UiStateMachine`).
* [ ] `Esc` во время телефона → телефон закрывается, пауза **не** открывается
      (приоритет у телефона).

**Критерий:** ни одна клавиша не открывает два экрана одновременно;
`timeScale` после всех манипуляций = 1.

---

## Шаг 6. Проверить телефон

* [ ] Открыть телефон: рабочий стол, 6+ плиток приложений.
* [ ] Открыть «Заказы». Ожидаемо: «Система доставки не подключена»
      (`DeliveryServiceBridge.Connected == false`) — это **корректное** поведение
      при отсутствии хоста.
* [ ] Проверить, что плитки «Звонки/Задания/Реклама/Обучение/Галерея» есть, но
      их содержимое — пустые состояния.

**Критерий:** ничего не бросает исключений; каждая плитка открывает свой экран
и закрывается кнопкой «Назад».

---

## Шаг 7. Проверить карту (после фикса P0)

* [ ] Сетка 750×750 м видна, масштаб соответствует (1 клетка = 50 м).
* [ ] Дороги (82) и точки доставки (46) отрисованы.
* [ ] Клик по точке → маршрут. Ожидаемо: линия по дорогам, без прохода сквозь
      дома (`RoutePlanner` + `RouteGeometryBuilder`).
* [ ] Зум колесом: 0.6×…3.2×, при 3.2 надписи не наезжают друг на друга.
* [ ] «Дом игрока» (`bld-n3`, дверь `(205;636.4)`) отмечен иконкой.

**Критерий:** все 46 точек достижимы (это уже доказано `district_layout_check.py`,
но не в Unity: проверяем, что маршрут рисуется для каждой).

---

## Шаг 8. Прогнать тесты внутри Unity

* [ ] `Window → General → Test Runner`.
* [ ] **EditMode:** должны пройти `City/Tests`, `Economy/Tests`, `Jobs/Tests`,
      `Debts/Tests`, `Phone/Tests`, `UI/Tests/UnityEditMode*` (если есть).
      Ожидаемо: 2 провала в `Phone` (дефект P1-1, известен и описан).
* [ ] **PlayMode:** `City/PauseTests` — 8 тестов. В песочнице они **не
      запускались ни разу**; они требуют `UnityEngine.TestTools`.
* [ ] Записать точные числа: всего/пройдено/провалено/пропущено.

**Критерий:** числа совпадают с ожидаемыми или каждое расхождение объяснено.

---

## Шаг 9. Собрать билд и проверить сцену «как у игрока»

* [ ] `File → Build Settings` → добавить `Main.unity` → собрать Windows x64
      (или WebGL, если планируется веб).
* [ ] Запустить билд, повторить шаги 4–7.
* [ ] Проверить `Player.log` на `NullReferenceException` (в редакторе они могут
      не проявиться из-за порядка инициализации).

**Критерий:** в билде карта и телефон работают так же, как в редакторе.

---

## Шаг 10. Отчёт

Записать в `AUDIT/UNITY_RUN_REPORT.md`:
* версия Unity, платформа, ОС;
* результаты каждого шага (✅/❌ + текст ошибки);
* число объектов, кадры/сек в сцене (`Window → Analysis → Profiler`, `CPU`),
  пик `GC Alloc` при открытии карты;
* скриншоты: HUD, карта, телефон, пауза;
* список реально возникших ошибок компиляции Unity (если были) — с полным текстом.

---

## Известные ограничения аудита (не пытаться обойти)

| Что | Почему |
|---|---|
| Компиляция против стабов ≠ Unity | `UI/Tools/unitystubs` — 417 строк рукописных заглушек |
| 1374 ошибки Roslyn на Unity-слое | не дефекты, а отсутствие `UnityEngine` |
| Вид интерфейса | стабы не рендерят; нужна проверка глазами |
| Ввод | `Input`/`InputSystem` в стабах не эмулируются |
| `PauseTests` (8 шт.) | требуют Unity Test Framework |
| Сцены/префабы/GUID | в репозитории их нет физически |

## Что делать, если шаг не проходит

1. Не править код «по интуиции» — сначала записать полный текст ошибки и файл.
2. Соотнести с `ISSUES.md`: возможно, дефект уже описан (P0-1, P1-1, P1-3).
3. Правка игрового кода — только с отдельным разрешением (ТЗ №043, ограничения).
