# Codex: District750 и Pause Menu (ТЗ #035)

Начни только с этого файла. Реализация: точный commit `e15088ce798d54c4f05224b26bd2bab7f85f2f06`; база с уже слитым PR #10 — `327fbaa6388792d39dc55537e05be5efe1031a0d`. Получи также последнюю версию этого документа из ветки `arena/beb4cad1-simgame`; не затирай локальные изменения. Полные шаги: `City/PAUSE_DISTRICT_INTEGRATION.md`.

**Файлы:** перенеси `City/Quarter/`, `City/UnityAdapter/`, `City/Tests/*.cs` и их asmdef, `City/PauseMenu/`, `City/PauseInputSystem/PauseKeyboardInput.cs` вне asmdef и `City/PauseTests/`. Не копируй csproj в Assets. Карта и Quarter250 не переписаны.

**Проверки:** `python3 City/Tools/district_layout_check.py`; при наличии SDK 8 — `dotnet build City/Tests/Console.csproj` и `dotnet run --project City/Tests/Console.csproj`. Unity Test Runner: EditMode `QuarterNUnitTests`, PlayMode `LifeFromZero.City.Pause.Tests`. Остановись при первой существенной ошибке; исправляй только подтверждённую причину.

**Карта:** в существующем Unity-проекте создай отдельную пустую `DistrictTest.unity`, добавь `CityBootstrap`: `district750`, seed `1904`, Generate On Start и Add Colliders включены. Запусти Play, проверь Console, отчёт, коллайдеры и проходы. Подробности и материалы — в интеграционном документе.

**Меню:** создай отдельный корневой `Pause` с `PauseController`; подключи существующий маршрутизатор окон/ввода через `PauseIntegration`, реальные данные — через `PauseDataSource`. Без интеграции открытие намеренно запрещено. При отсутствии менеджера настрой `StandalonePauseIntegration` и `PauseKeyboardInput` по инструкции. Проверь Esc, время, курсор, блокировку управления, отсутствие дубликатов при смене сцен и недоступные действия. Только P вызывает телефон из паузы; другие клавиши не назначать.

Сравни меню с приложенным `Pause Menu.PNG`, карту с `District750.PNG`; файлы референсов не лежат в Git. Проверь 1920×1080 и 2560×1440. Не менять CityTest, Apartment67, Economy, Delivery, Debts, Phone, планировку и seed; не создавать систему сохранений. Не заявляй успешную компиляцию, визуальное совпадение или FPS без проверки. После отчёта остановись.
