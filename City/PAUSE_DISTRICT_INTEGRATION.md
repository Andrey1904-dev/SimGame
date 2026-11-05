# ТЗ #036 — интеграция District750 и Pause Menu (после финального аудита)

PR #10 (District750) и PR #11 (Pause Menu) уже слиты в `main` (`f4b6b8c`). Исправления финального аудита (ТЗ #036) находятся в ветке `arena/2fe575ce-simgame`. В среде репозитория нет Unity 6000.3.27f1 и .NET SDK, поэтому компиляция C#, прогон NUnit/PlayMode, визуальная сверка с референсами и замер FPS выполняются локально в `/Users/homepc/Projects/LifeFromZero`.

## Получение актуального кода

```bash
git fetch origin
git checkout arena/2fe575ce-simgame
git pull --ff-only origin arena/2fe575ce-simgame
```
(Если PR ТЗ #036 уже слит в `main`: `git checkout main && git pull --ff-only origin main`.)

## District750

Перенести в `Assets/` локального проекта `/Users/homepc/Projects/LifeFromZero` папки `City/Quarter/` (включая `DistrictMap/` и `City.Quarter.asmdef`), `City/UnityAdapter/` (`City.Quarter.UnityAdapter.asmdef`) и `City/Tests/*.cs` с `City/Tests/City.Quarter.Tests.asmdef`. Не копировать `City/Tests/Console.csproj` в `Assets/`. Контракт `QuarterPlan` и `QuarterGenerator` (`quarter250`) сохранены без изменений. `CityBootstrap` выбирает `DistrictMapGenerator` при `Layout Id = district750` (без учёта регистра и пробелов); по умолчанию остаётся `quarter250`. В `Awake()` `CityBootstrap` отключает автозапуск `QuarterUnityAdapter`, исключая двойную генерацию при старте.

В проекте Unity 6000.3.27f1 (URP 17.3.0) создать отдельную пустую сцену `DistrictTest.unity` (не менять `CityTest` и `Apartment67`). Добавить корневой объект `City` с компонентом `CityBootstrap`: `Layout Id = district750`, `Seed = 1904`, `Generate On Start = true`, `Add Colliders = true`. Для обзора сверху: ортографическая камера в `(375, 500, 375)`, поворот `(90, 0, 0)`, `orthographicSize = 380` и `Directional Light`. Запустить Play Mode: проверить лог `[CityBootstrap] Город построен: макет=district750, seed=1904, жилых зданий=15, объектов=974, точек доставки=46`, коллайдеры и проходы к входам (см. `City/CODEX_UNITY_STEPS.md` §§ 6–8).

Автоматические проверки до и во время интеграции:
1. Python-зеркало:
   ```bash
   python3 City/Tools/district_layout_check.py
   ```
   Ожидается: `OK: все проверки пройдены`, `objects = 974`, `buildings_mkd = 10` (`north = 4`, `south = 3`, `east = 3`), `private = 5`, `points = 24`, `deliveries = 46`, `entrances_checked = 100`, `unreachable = 0`.
2. Консольные C#-тесты (при наличии .NET SDK 8):
   ```bash
   dotnet build City/Tests/Console.csproj
   dotnet run --project City/Tests/Console.csproj
   ```
3. Unity Test Runner (EditMode): `QuarterNUnitTests.RunAllQuarterTests` (включая 25 тестов `DistrictMapTests`).

## Pause Menu — установка

Перенести `City/PauseMenu/` целиком вместе с `LifeFromZero.City.Pause.asmdef`, `City/PauseInputSystem/PauseKeyboardInput.cs` (вне любых asmdef, в `Assembly-CSharp`) и `City/PauseTests/` с `LifeFromZero.City.Pause.Tests.asmdef`. Используются штатные Unity UI (uGUI) и Unity Test Framework; пакет Input System подключается автоматически при `ENABLE_INPUT_SYSTEM` (с fallback на `StandaloneInputModule` при `ENABLE_LEGACY_INPUT_MANAGER`). Существующие модули `Economy`, `Delivery`, `Debts`, `Phone`, `Apartment67` и `CityTest` не меняются.

1. Создать в игровой сцене отдельный **корневой** объект `Pause` с `PauseController`. Он создаёт один Canvas при `Awake`, сохраняется через `DontDestroyOnLoad`, не пересоздаёт UI при повторном `Esc` и уничтожает дубликаты при смене сцены.
2. Подключить реализацию `PauseIntegration` в поле `Integration` (без неё `Open()` безопасно возвращает `false`). Контроллер сам сохраняет `Time.timeScale` и состояние `Cursor` **до** вызова `AcquireGameplayLock()` и гарантированно восстанавливает их в `finally` при `Close()`.
3. При отсутствии централизованного менеджера окон добавить `StandalonePauseIntegration` на объект `Pause`, назначить его в `Integration`, указать в `Player Input Behaviours` все компоненты управления персонажем/камерой и в `Competing Modal Roots` — корни конкурирующих окон. Адаптер дедуплицирует повторные ссылки в массиве и отклоняет компоненты самой паузы (`PauseController`, `PauseView`, `PauseKeyboardInput`), чтобы персонаж никогда не оставался без управления после закрытия меню.
4. Направить `Esc` в `PauseController.Instance.HandleEscape()`, а физическую клавишу `P` — в `HandlePhoneKey()` (только при открытой паузе). При отсутствии внешнего роутера ввода добавить `PauseKeyboardInput` на объект `Pause` и включить `Use Standalone Keyboard`.
5. Унаследовать `PauseDataSource`, заполнить `PauseSnapshot` реальными данными (неизвестные поля — `null`/пусто, проценты `0..100`, до 3 целей) и назначить в `Data Source`.
6. Реализовать `IsAvailable` и `Execute` для доступных `PauseAction`. Кнопки меню имеют `Navigation.Mode.None` и не остаются постоянно выделенными после клика.
7. При смене scene-local сервисов вызывать `PauseController.Instance.Configure(newIntegration, newDataSource)`.

## Проверка в Unity

1. Unity Test Runner → PlayMode → `LifeFromZero.City.Pause.Tests` (6 тестов в `PauseTests`, включая проверку дубликатов `Behaviour` в `StandalonePauseIntegration` и отсутствие залипания выделения кнопок).
2. В Play Mode проверить: `Esc` (открытие/закрытие), кнопку «Продолжить игру», остановку и восстановление `Time.timeScale`, видимость и блокировку курсора, блокировку и возврат управления персонажем, клавишу `P` для телефона, отсутствие второго `Canvas`/`EventSystem`, работу при `1920×1080` и `2560×1440`, а также визуально сверить с локальными референсами карты v1.1 и макета Pause Menu и замерить FPS на MacBook Air M4.
