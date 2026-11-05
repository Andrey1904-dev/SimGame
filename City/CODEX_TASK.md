# Codex: интеграция District750 и Pause Menu (ТЗ #036–#037)

PR #10 и PR #11 уже слиты в `main` (`f4b6b8c`). Исправления аудита (ТЗ #036) и доработка Pause Menu по утверждённому референсу `Pause Menu.PNG` (ТЗ #037) находятся в ветке `arena/2fe575ce-simgame` (PR #12). Подробности — в `City/PAUSE_DISTRICT_INTEGRATION.md`.

## 1. Получение изменений и автопроверки

```bash
git fetch origin
git checkout arena/2fe575ce-simgame
git pull --ff-only origin arena/2fe575ce-simgame
python3 City/Tools/district_layout_check.py
dotnet build City/Tests/Console.csproj
dotnet run --project City/Tests/Console.csproj
```
(Если PR #12 уже слит: `git checkout main && git pull --ff-only origin main`.)

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

1. **Test Runner:** запустить EditMode `QuarterNUnitTests.RunAllQuarterTests` и PlayMode `LifeFromZero.City.Pause.Tests` (8 тестов в `PauseTests`, включая проверку макета `Pause Menu.PNG`).
2. **District750:** в сцене `DistrictTest.unity` с `CityBootstrap` (`Layout Id = district750`, `Seed = 1904`, `Generate On Start = true`, `Add Colliders = true`) проверить лог (`974` объекта, `15` жилых домов, `46` доставок), коллайдеры и проходы.
3. **Pause Menu:** на корневом объекте `Pause` с `PauseController` подключить `PauseIntegration` и `PauseDataSource` (`Satiety`/`Hunger`, `Mood`/`Stress`, `Money`, `Debt`, `DailyIncome`). В Play Mode проверить `Esc`, 7 одинаковых по стилю кнопок левой панели, блоки «Цели», «Статус», «Финансы», «Быстрые ссылки» (`Телефон [P]`), подвал `v0.2.0 (alpha)`, разрешения `1920×1080`, `2560×1440`, `1600×900` и сверку с `Pause Menu.PNG`.
