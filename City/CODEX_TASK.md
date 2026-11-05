# Codex: интеграция District750 и Pause Menu (после аудита ТЗ #036)

PR #10 и PR #11 уже слиты в `main` (`f4b6b8c`). Исправления финального аудита находятся в ветке `arena/2fe575ce-simgame`. Подробности — в `City/PAUSE_DISTRICT_INTEGRATION.md`.

## 1. Получение изменений и автопроверки

```bash
git fetch origin
git checkout arena/2fe575ce-simgame
git pull --ff-only origin arena/2fe575ce-simgame
python3 City/Tools/district_layout_check.py
dotnet build City/Tests/Console.csproj
dotnet run --project City/Tests/Console.csproj
```
(Если PR ТЗ #036 уже слит: `git checkout main && git pull --ff-only origin main`.)

## 2. Перенос файлов в `/Users/homepc/Projects/LifeFromZero`

Скопировать в `Assets/` с сохранением структуры и `.asmdef`:
- `City/Quarter/` (включая `DistrictMap/`)
- `City/UnityAdapter/`
- `City/Tests/*.cs` и `City/Tests/City.Quarter.Tests.asmdef` (без `Console.csproj`)
- `City/PauseMenu/`
- `City/PauseInputSystem/PauseKeyboardInput.cs` (вне asmdef, в `Assembly-CSharp`)
- `City/PauseTests/`

Не изменять `Apartment67`, `CityTest`, `Economy`, `Delivery`, `Debts`, `Phone`, планировку и `seed = 1904`.

## 3. Проверка в Unity 6000.3.27f1 (URP 17.3.0)

1. **Test Runner:** запустить EditMode `QuarterNUnitTests.RunAllQuarterTests` и PlayMode `LifeFromZero.City.Pause.Tests`.
2. **District750:** создать новую сцену `DistrictTest.unity`, добавить корневой объект `City` с `CityBootstrap` (`Layout Id = district750`, `Seed = 1904`, `Generate On Start = true`, `Add Colliders = true`), камеру сверху `(375, 500, 375)` и `Directional Light`. В Play Mode проверить лог (`974` объекта, `15` жилых домов, `46` доставок), коллайдеры, проходы и сверку с референсом «Карта района v1.1».
3. **Pause Menu:** добавить корневой объект `Pause` с `PauseController`, подключить `PauseIntegration` (или `StandalonePauseIntegration` + `PauseKeyboardInput`) и `PauseDataSource`. В Play Mode проверить `Esc`, «Продолжить игру», `Time.timeScale`, курсор, возврат управления персонажем, клавишу `P` (телефон), разрешения `1920×1080` и `2560×1440`, сверку с макетом Pause Menu и FPS на MacBook Air M4.
