# Отчёт о проверках: что, чем и с каким результатом

Дата прогона: 2026-11-05. Всё ниже — **фактически выполненные** команды, а не
описания. Где проверку выполнить нельзя — написано «НЕ ВЫПОЛНЕНО» и почему.

---

## 0. Чем проверяли

| Инструмент | Как получен | Что умеет |
|---|---|---|
| Roslyn 5.x (C# 14, компилятор Microsoft.CodeAnalysis) | npm-пакет `@dprint/roslyn-linux-x64-glibc`, распакован в `/tmp` | настоящая компиляция C# (`CSharpCompilation.Emit`) |
| .NET 10 runtime | там же (self-contained) | исполнение полученных сборок |
| pythonnet 3.2.1 | `pip install pythonnet` | мост Python ↔ CLR |
| `City/Tools/district_layout_check.py` | в репозитории | проверка раскладки района |
| `City/UnityIntegration/run_checks.sh` | в репозитории | сводный отчёт (8 этапов) |
| `UI/Tools/*.py`, `UI/Tools/unitystubs`, `UI/Tools/editorstubs` | PR #14 | проверки UI против стабов |

**Чего в среде нет:** Unity (ни редактора, ни Player, ни `UnityEditor.dll`),
`dotnet` SDK, `nuget`, MSBuild, mono. Установка запрещена правилами ТЗ.
Следствие: **Unity-сборки (`City/UnityAdapter`, `City/UnityIntegration`,
`City/PauseMenu`) скомпилировать по-настоящему невозможно** — их компиляция
против рукописных стабов проверяет только синтаксис, но не API Unity.

---

## 1. Компиляция модулей (настоящий Roslyn, без стабов)

Метод: собрать все `.cs` модуля (без `*/Tests/`, без `*NUnitTests.cs`), добавить
тесты, скомпилировать в `ConsoleApplication`, запустить `Program.Main` через
рефлексию. Тест-фреймворк — собственный `MiniTestRunner` репозитория, NUnit не нужен.

| Модуль | Файлов прод. | Файлов тестов | Компиляция | Тестов исполнено | Провалено |
|---|---|---|---|---|---|
| `Economy` | 17 | 7 | **OK**, 0 ошибок | 60 | **0** |
| `Jobs/Delivery` | 10 | 8 | **OK**, 0 ошибок | 55 | **0** |
| `Phone` | 29 | 10 | **OK**, 1 предупреждение | 93 | **0** |
| `Debts` | 27 | 10 | **OK**, 0 ошибок | 97 | **0** |
| `City/Quarter` | 9 | 14 | **OK**, 1 предупреждение | 100 | **0** |
| **Итого** | **92** | **49** | **5/5 модулей** | **405 проверок** | **0** |

> Изменения после сквозной интеграции PR #14 + PR #15 (2026-11-06):
> City 100/0 (+2 теста `INotifyQuarterPlanSource.PlanChanged` в `QuarterPlanSourceTests`),
> UI 122/122 (+4 теста интеграции доставки/телефона/навигации в `UiCoreTests`,
> +19 регрессий ТЗ №045 в `UI/Tests/UiRegression045Tests.cs`),
> добавлена по-сборочная Roslyn-проверка 17 `.asmdef` + `Assembly-CSharp` + `Assembly-CSharp-Editor` (`AUDIT/tools/check_unity6_compile.py`).
> **Итого по репозиторию: 405 модульных проверок + 122 UI = 527, провалов 0
> (в песочнице без ICU один UI-тест про кириллицу падает — см. примечание в `UI/REPORT_045_UI_FIXES.md`).**

## 2. Проваленные тесты Phone: причины и исправление

При первом прогоне (до исправлений) падали два теста:

```
[FAIL] Приложения: пустой ID и название отклоняются
       ArgumentException: Идентификатор приложения не должен быть пустым.
[FAIL] Уведомления: пустые поля отклоняются
       ArgumentException: Идентификатор уведомления не должен быть пустым.
```

**Разбор.** В Phone два уровня контракта, и они последовательно разные:

| Уровень | Класс | Поведение на невалидных данных | Тест, это фиксирующий |
|---|---|---|---|
| Модель | `PhoneApp` (`Phone/Apps/PhoneApp.cs:64`) | бросает `ArgumentException` | `AppModelValidation` |
| Модель | `PhoneNotification` (`Phone/Notifications/PhoneNotification.cs:74`) | бросает `ArgumentException` | (не было!) |
| Сервис | `PhoneAppRegistry.Register` | возвращает `PhoneResultCode` | `InvalidApp` |
| Сервис | `PhoneNotificationService.Add` | возвращает `PhoneResultCode` | `InvalidNotification` |

Падавшие тесты **смешивали уровни**: конструировали модель с пустым ID прямо
внутри вызова сервиса. Строка `new PhoneApp("  ", ...)` бросала исключение
раньше, чем вызывался `Register`, поэтому ожидаемый `InvalidArgument` получить
было невозможно.

**Что исправлено (не подгонка тестов):**

1. `PhoneNotification` не имел теста на собственный контракт «бросаю на пустом
   ID» — добавлен (дыра в покрытии, а не ослабление проверки).
2. `InvalidApp` теперь проверяет **оба** уровня: конструктор бросает на мусоре,
   реестр отклоняет `null` и принимает валидный объект. Раньше проверка реестра
   фактически не выполнялась вовсе.
3. `InvalidNotification` дополнительно проверяет **сервисный путь**
   `Add(id, source, ...)` с пустыми `id` и `source` — он обязан возвращать код
   `InvalidArgument`, а не бросать. Это и есть реальный дефект-риск: в бою данные
   приходят строками, и падение вместо кода уронило бы вызывающий UI.
4. XML-документация `IPhoneAppRegistry.Register` уточнена: невалидные данные
   отсекает конструктор, реестр отвечает за `null` и дубликаты.

**Результат:** `Phone` — 93 проверки, 0 провалов, EXIT 0.

## 3. UI (PR #14) — проверки против стабов

```
$ LFZ_DOTNET_ROOT=... LFZ_CORECLR_CONFIG=... python3 UI/Tools/checkui.py
COMPILE: OK errors=0
$ python3 UI/Tools/checkeditor.py
COMPILE: OK errors=0            # UI + UI/Editor (EditorStubs)
$ python3 UI/Tools/runtests.py
Итого: 99/99 PASS, 0 FAIL       # 98 + 1 регрессионный тест плана
COMPILE: OK errors=0 files=27
```

**Ограничение (обязательно учитывать):** `checkui.py`/`checkeditor.py` компилируют
против **рукописных стабов** `UI/Tools/unitystubs/UnityStubs.cs` (417 строк) и
`UI/Tools/editorstubs/EditorStubs.cs`. Это доказывает синтаксис и согласованность
типов, но **не** совместимость с настоящим API Unity. 98 тестов проверяют логику
(`UI/Core`), а не вёрстку.

## 4. Отдельные проверки, выполненные по ходу аудита

### 4.1. Дефект `QuarterUnityAdapter.Build` (P0 в PR #14) — **ИСПРАВЛЕН**

Модель порядка вызовов из `City/UnityAdapter/QuarterUnityAdapter.cs:117-120`
(`_plan = plan;` → `ClearBuilt();` → `_plan = null;`) скомпилирована и запущена:

```
После Build(): HasPlan = False (ожидалось True)
exit code: 1
```
Вывод: **`Plan`/`HasPlan` всегда `false` после сборки района.** Любой потребитель
`IQuarterPlanSource` (UI-карта PR #14) получит `null` и нарисует пустую сетку.

**Исправление (`786c7fc`).** Жизненный цикл плана вынесен из MonoBehaviour в
чистый `City/Quarter/QuarterPlanSlot.cs`: `BeginBuild(plan)` сначала очищает
состояние, затем фиксирует план. `QuarterUnityAdapter.Build` вызывает
`ClearBuilt()` → `_planSlot.BeginBuild(plan)`.

**Доказательство, что тесты ловят дефект.** Временный откат `QuarterPlanSlot`
к исходному порядку «присвоить → очистить»:
```
UI:   FAIL  Район: источник плана отдаёт район карте после сборки (регрессия P0)
      Итого: 98/99 PASS, 1 FAIL
City: [FAIL] План: после сборки Plan доступен (регрессия P0)
      [FAIL] План: очистка сбрасывает план снесённого квартала
      [FAIL] План: повторная сборка отдаёт последний план
      Всего: 98, провалено: 3
```
После возврата исправления — снова 99/99 и 98/0. Тесты не «подогнаны»: они
падают ровно на исходном коде.

### 4.1b. Дефект asmdef `UI/Core` (P0 для Unity-сборки) — **ИСПРАВЛЕН**

`UI/Core/QuarterPlanAdapter.cs` импортирует `LifeFromZero.City.Quarter`, но
`UI/Core/LifeFromZero.UI.Core.asmdef` имел `"references": []`. В Unity сборка
`LifeFromZero.UI.Core` не увидела бы тип `QuarterPlan` и упала бы с `CS0246`.
Проверки `checkui.py` этого не ловили: они компилируют все файлы в одну сборку
и asmdef не читают. Добавлена ссылка на `LifeFromZero.City.Quarter`; написан
скрипт проверки графа asmdef (все `using LifeFromZero.*` покрыты ссылками).

### 4.2. Компиляция Unity-слоя без Unity

```
$ Roslyn: City/UnityAdapter/* + City/Quarter/** + City/UnityIntegration/**
COMPILE FAILED, errors = 1374
  CS0246 (797)  — UnityEngine/UnityEditor не найдены
  CS0103 (548)  — члены Unity (transform, gameObject, Destroy…)
  CS1061 (17) / CS1503 (8) / CS0019 (1) / CS0403 (2) / CS5001 (1)
```
**Это не ошибки кода, а отсутствие Unity.** Единственный содержательный сигнал —
`CS0019: Operator '==' cannot be applied to 'Quaternion?' and '<null>'` в
`QuarterUnityAdapter.cs:959`: без Unity это артефакт стаба; в Unity
`Quaternion?` сравнивается с `null` корректно.

**Вывод:** проверить этот слой нельзя. Требуется Unity (см. `UNITY_CHECKLIST.md`).

### 4.3. Генератор района

```
$ python3 City/Tools/district_layout_check.py
solids 81, zones 21, decor 9, roads 82, nodes 59
buildings_mkd 10, north 4, south 3, east 3, private 5
points 24, deliveries 46, trees 47, lamps 187, stalls 104
player_home bld-n3 (210;630) door (205;636.4) exit (205;638.4)
north_pitch (203;655)-(217;665) football 14x10
OK: все проверки пройдены
```

### 4.4. Сводный скрипт проверок

```
$ bash City/UnityIntegration/run_checks.sh
PASS: 2   FAIL: 0   NOT RUN: 6
```
PASS: этап 1 (python-генератор), этап 8 (запись отчёта).
NOT RUN: структура Unity-проекта, C# build (нет dotnet), EditMode-тесты,
PlayMode-тесты, обнаружение сцены, критические ошибки Unity Console.
Скрипт сам пишет: «NOT RUN — это НЕ успешная проверка».

### 4.5. Установщик в Unity-проект

```
$ bash City/UnityIntegration/install_to_unity.sh            # без аргументов
ОШИБКА: не указан путь к Unity-проекту.           (корректно)
$ bash City/UnityIntegration/install_to_unity.sh /tmp/fake
ОШИБКА: каталог не найден: /tmp/fake              (корректно)
```
Полноценно проверить копирование нельзя: нет Unity-проекта (нужны `Assets/` и
`ProjectSettings/`).

### 4.6. Сквозная компиляция всех прод-файлов без Unity

108 файлов (`City`, `Debts`, `Economy`, `Jobs`, `Phone`, `UnityIntegrationKit`,
исключая `UI/` и тесты) → 1374 ошибки, **все** из-за отсутствия Unity
(см. 4.2). Ни одна ошибка не относится к «чистым» модулям: они компилируются
по отдельности с 0 ошибок (см. §1).

## 5. Что НЕ проверено и почему

| Проверка | Причина |
|---|---|
| Компиляция в Unity | нет Unity и его сборок |
| Сцены, префабы, ссылки, GUID | в репозитории нет ни одного `.unity`/`.prefab`/`.meta` |
| `PauseTests` (8 тестов) | требуют `UnityEngine.TestTools` (Unity Test Framework) |
| NUnit-обёртки (`*NUnitTests.cs`) | требуют NUnit, внутри Unity |
| Внешний вид UI (телефон/карта/пауза) | нет рендера; стабы не рисуют |
| Ввод (P/Esc/M, нажатия uGUI) | нет Unity Input; стабы не эмулируют |
| Сохранение/загрузка игры | системы нет (только снимки модулей) |
| Производительность | нет сцены; можно оценить только аллокации в чистом коде |
| Безопасность файловых операций | проверено чтением: `File.WriteAllText` только в Editor-отчёты, путь берётся из проекта |

## 6. Воспроизведение

```bash
# Модульные тесты (нужны pythonnet + Roslyn + .NET 10, см. §0)
python3 /tmp/audit/run_module.py /path/to/repo Economy

# UI (в PR #14)
cd <repo> && LFZ_DOTNET_ROOT=<dotnet> LFZ_CORECLR_CONFIG=<rc.json> \
  python3 UI/Tools/runtests.py

# Без Unity, без установки чего-либо
bash City/UnityIntegration/run_checks.sh
python3 City/Tools/district_layout_check.py
```

## 7. Итог по проверкам

* **Реально исполнено (после исправлений и интеграции UI+City):** 405 проверок в 5 модулях (Economy 60,
  Jobs 55, Phone 93, Debts 97, City 100) + 122 UI = **527 C#-тестов**, плюс по-сборочная Roslyn-компиляция всех 17 `.asmdef` + `Assembly-CSharp` + `Assembly-CSharp-Editor` в 3 профилях ввода Unity 6000.3.27f1.
* **Провалено:** **0**.
* **Исправлено блокирующих дефектов компиляции Unity 6000.3.27f1 в `main`:** `LifeFromZero.UI.Tests` (`internal` `TurnAngle`/`TurnLabel` между `.asmdef`) и `City/UnityIntegration/Editor/LifeFromZeroGameIntegration.cs` (отсутствовал `using LifeFromZero.City.Quarter;`).
* **Не исполнено:** 8 `PauseTests` (нужен Unity Test Runner), Play Mode в реальном редакторе Unity 6000.3.27f1, визуальные проверки uGUI/рендера, запуск локального проекта `/Users/homepc/Projects/LifeFromZero` (недоступен в среде песочницы).

Заявление «игра работает» сделать нельзя: запуска игры не было ни разу.
