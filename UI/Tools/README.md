# UI/Tools — проверки без Unity

Скрипты ниже запускаются **из корня репозитория**.

| Команда | Что проверяет |
|---|---|
| `python3 UI/Tools/checkui.py` | компиляция `UI/Core` + `UI/Runtime` + `City/Quarter` против стабов Unity |
| `python3 UI/Tools/checkeditor.py` | то же + `UI/Editor` против стабов UnityEditor |
| `python3 UI/Tools/runtests.py` | 122 теста UI (логика, раскладка, район, привязка, телефон, регрессии ТЗ №045) |
| `python3 UI/Tools/check_graphic_layers.py` | статический контракт ТЗ №045: `CanvasRenderer` у слоёв карты, гейт перерисовки, источник данных телефона |
| `python3 UI/Tools/runtc.py mydiag.cs` | разовый диагностический `Diag.Main()` против ядра |
| `UI/Tools/check.sh "a.cs,b.cs"` | компиляция произвольного набора файлов |

## Что это НЕ заменяет

* **Не компиляция Unity.** `UI/Tools/unitystubs/UnityStubs.cs` и
  `UI/Tools/editorstubs/EditorStubs.cs` — рукописные стабы, покрывают только те
  API, которые реально использует код. Зелёная проверка = «синтаксис и типы
  сходятся», а не «проект собрался в Unity».
* **Не тест внешнего вида.** Раскладка паузы измерена по макету и проверена
  числами, но ни одного пикселя в Unity не отрендерено.

Поэтому после любой правки обязательны ещё два шага в Unity (см.
`UI/CODEX_EXECUTE.md` §3): компиляция проекта и запуск сцены глазами.

## Требования

```bash
pip install pythonnet
# runtime .NET, по умолчанию /tmp/dotnet (переопределяется LFZ_DOTNET_ROOT)
# runtimeconfig для coreclr, по умолчанию /tmp/probe.runtimeconfig.json (LFZ_CORECLR_CONFIG)
```

Если runtime не найден, скрипты скажут об этом прямо, а не упадут с загадочной
ошибкой импорта.

Альтернатива без Roslyn-обвязки — прогон тех же тестов через .NET:

```bash
dotnet run --project UI/Tests/Console.csproj
```
