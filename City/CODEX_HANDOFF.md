# CODEX_HANDOFF — District750 (ТЗ №034)

Точка входа: `City/CODEX_TASK.md`. Этот файл — справка по Git, сборке и состоянию.

## 1. Состояние

- Репозиторий: `Andrey1904-dev/SimGame`
- Ветка: `arena/02138937-simgame`
- Коммит: `e34a244` (ТЗ №033). Новые коммиты этого ТЗ добавляются поверх него.
- PR #10: https://github.com/Andrey1904-dev/SimGame/pull/10 — open, **не сливать** без разрешения.

Готово (код, Python-проверка пройдена):
- Генератор `DistrictMapGenerator.Generate(seed)` — 750×750, 10 МКД (С4/Ю3/В3), 5 частных домов, 4 АЗС (АЗС-4 на (85, 395) южнее ломбарда), ПВЗ `pvz-01` (85, 316), 46 точек доставки.
- `CityBootstrap` с полем `Layout Id = district750`.
- Тесты `DistrictMapTests` (24 кейса) и `DistrictPlanChecks`, включая 9 негативных.

Не проверено: компиляция C#, запуск тестов, Unity, визуал, FPS. Ни один из этих пунктов нельзя считать пройденным.

## 2. Что читать

1. `City/CODEX_TASK.md`
2. `City/CODEX_UNITY_STEPS.md` — только для Unity.
3. `City/CODEX_RISKS.md` — при ошибке.
4. `City/Quarter/DistrictMap/DistrictMapGenerator.cs` — точечно, только при падении теста.
5. `City/Tests/DistrictMapTests.cs` — точечно, только при падении теста.

## 3. Что не читать

- Корневые отчёты и аудиты: `FULL_AUDIT_REPORT.md`, `INTEGRATION_AUDIT.md`, `AUDIT_PROGRESS.md`, `TZ021_FINANCIAL_CONTRACTS_REPORT.md`, `CODEX_*.md` и `START_HERE_CODEX.md` в корне, `UNITY_INTEGRATION_PLAN.md`, `SAVE_SYSTEM_INTEGRATION_PLAN.md`, `ECONOMY_INTEGRATION_QUICKSTART.md`.
- `City/FINAL_REPORT.md`, `City/CODEX_INTEGRATION.md` — относятся к кварталу 250, для District750 не нужны.
- Модули Economy, Jobs, Debts, Phone, а также `City/Tests/*`, кроме трёх файлов из раздела 2.
- Каталог `.git` и `/tmp/audit` (вне репозитория).

## 4. Git (из корня репозитория)

```sh
git status --short
git branch --show-current              # должно быть arena/02138937-simgame
git fetch origin arena/02138937-simgame
# Если git status --short не пуст, сохрани локальные изменения:
git stash push -u -m "codex-local-before-pr10"
git merge --ff-only origin/arena/02138937-simgame
# Если stash создавался:
git stash pop
git log --oneline -1
```

`--ff-only` не даёт потерять локальные коммиты: при расхождении команда остановится с ошибкой. В этом случае остановись и сообщи.

## 5. Сборка и тесты

```sh
dotnet build City/Tests/Console.csproj
dotnet run --project City/Tests/Console.csproj
python3 City/Tools/district_layout_check.py
```

- `Console.csproj`: net8.0, компилирует `City/Quarter/**` и `City/Tests/**`. Файлы `City/UnityAdapter/**` **не компилируются** (нужен UnityEngine).
- `dotnet run` вызывает `QuarterTestSuite.CreateAll()`, в который входит `DistrictMapTests`. Exit 0 — все кейсы прошли.
- Python: ожидается exit 0 и строка «OK: все проверки пройдены».

## 6. Unity

В репозитории **нет Unity-проекта** (нет `.unity`-сцен и `ProjectSettings`). Проект пользователя находится вне репозитория. Шаги — в `City/CODEX_UNITY_STEPS.md`.

Unity EditMode-тест — `City/Tests/QuarterNUnitTests.cs`: один NUnit-кейс `RunAllQuarterTests`, который внутри запускает все кейсы. В Test Runner он виден как **один** тест; подробности упавших — в сообщении ошибки.

Команда из `UNITY_TEST_EXECUTION.md` (путь редактора и проекта там Mac-specific, проверь перед запуском):

```sh
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults /tmp/simgame-editmode-results.xml -logFile /tmp/simgame-editmode.log
```

## 7. Критерии успешной интеграции

1. `dotnet build` без ошибок; `dotnet run` exit 0.
2. Python: exit 0, «OK».
3. Unity: компиляция без ошибок; `RunAllQuarterTests` зелёный.
4. `DistrictTest.unity`: в Console строка `[CityBootstrap] Город построен: макет=district750, seed=1904`; `ErrorCount = 0`; `DeliveryPoints = 46`; повторный `Generate()` не дублирует город.
5. Коллайдеры и входы проверены по чек-листу в `CODEX_UNITY_STEPS.md`.
6. Визуальное соответствие карте, FPS и память — только после измерения. Без измерений этих заявлений не делать.

## 8. Риски

Полный список с файлами, методами и проверками — `City/CODEX_RISKS.md`.
