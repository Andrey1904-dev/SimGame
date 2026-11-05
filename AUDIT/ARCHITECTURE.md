# Архитектура LIFE FROM ZERO

Все факты ниже проверены чтением файлов и компиляцией (см. `TEST_RESULTS.md`).
Где что-то нельзя проверить без Unity — это написано прямо.

---

## 1. Что лежит в репозитории

```
/                      37 .md-документов, 0 файлов Unity-проекта
├── City/              37 файлов, 13 257 строк
│   ├── Quarter/       генератор квартала и района (без UnityEngine)
│   │   └── DistrictMap/  District750: 974 объекта, 46 точек доставки
│   ├── UnityAdapter/  QuarterUnityAdapter + CityBootstrap (UnityEngine)
│   ├── PauseMenu/     пауза: контракты, контроллер, вид, интеграция
│   ├── PauseInputSystem/  PauseKeyboardInput (вне asmdef Pause)
│   ├── UnityIntegration/  Editor-инструменты + 2 runtime-моста + install-скрипт
│   ├── Tests/         98 тестов квартала (исполняемые вне Unity)
│   └── Tools/         district_layout_check.py
├── Economy/           25 файлов, 5 330 строк — кошелёк, магазин, идемпотентность
├── Jobs/Delivery/     19 файлов, 3 432 строки — заказы, состояния, выплаты
├── Debts/             38 файлов, 5 592 строки — долги, штрафы, платежи
├── Phone/             40 файлов, 5 263 строки — приложения, сообщения, уведомления
├── UnityIntegrationKit/  2 файла, 560 строк — финансовые мосты-делегаты
└── UI/                ТОЛЬКО в PR #14 (в main отсутствует)
    ├── Core/          13 файлов — логика без UnityEngine
    ├── Runtime/       16 файлов — экраны на uGUI
    ├── Editor/        UiInstaller
    ├── Tests/         99 тестов
    └── Tools/         run.py, checkui.py, runtests.py + стабы Unity
```

**Отсутствует полностью:** `ProjectSettings/`, `Packages/`, `*.unity`,
`*.prefab`, `*.asset`, `*.meta`, `*.mat`, `*.shader`, `.csproj`/`.sln` для Unity.
Есть только `Console.csproj` у пяти тестовых наборов — они для запуска вне Unity
и намеренно не входят в Unity-сборку.

Следствие: **GUID-ссылки, привязки в Inspector, слои, теги, физические материалы,
освещение и URP проверить нечем.** Это ограничение, а не «наверное, настроено».

## 2. Сборки (asmdef) и их реальные связи

| Сборка | Файл | Ссылается на | `noEngineReferences` |
|---|---|---|---|
| `LifeFromZero.City.Quarter` | `City/Quarter/City.Quarter.asmdef` | — | **true** |
| `LifeFromZero.City.Quarter.UnityAdapter` | `City/UnityAdapter/*.asmdef` | City.Quarter | false |
| `LifeFromZero.City.Pause` | `City/PauseMenu/*.asmdef` | `UnityEngine.UI` | false |
| `LifeFromZero.Economy` | `Economy/Economy.asmdef` | — | true |
| `LifeFromZero.Jobs.Delivery` | `Jobs/Delivery/Jobs.Delivery.asmdef` | — | true |
| `LifeFromZero.Debts` | `Debts/Debts.asmdef` | — | true |
| `LifeFromZero.Phone` | `Phone/Phone.asmdef` | — | true |
| тестовые сборки | `*/Tests/*.asmdef`, `City/PauseTests/*` | модуль + NUnit | — |
| `LifeFromZero.UI` (PR #14) | `UI/Runtime/LifeFromZero.UI.asmdef` | UI.Core, City.Quarter, City.Pause, Jobs.Delivery, UnityEngine.UI | false |
| `LifeFromZero.UI.Core` (PR #14) | `UI/Core/*.asmdef` | — | true |

Наблюдения:

* Четыре «денежных» модуля (`Economy`, `Jobs`, `Debts`, `Phone`) **не ссылаются
  друг на друга** — связи только через интерфейсы-адаптеры и
  `UnityIntegrationKit`. Это осознанное решение (видно по комментариям), и оно
  работает: каждый модуль компилируется отдельно.
* `City/UnityIntegration/Runtime/*.cs` **не имеют своего asmdef** — попадут в
  `Assembly-CSharp`, то есть в сборку по умолчанию, которая видит все
  auto-referenced сборки. Формально это допустимо, но означает, что порядок
  компиляции зависит от проекта, а не от репозитория.
* `City/PauseInputSystem/PauseKeyboardInput.cs` намеренно вынесен из asmdef Pause
  (комментарий в файле: так доступен `UnityEngine.InputSystem` при его наличии).

## 3. Поток данных (кто кого вызывает)

```
                    ┌──────────────────────────┐
                    │   QuarterGenerator       │  чистый C#, детерминирован
                    │   DistrictMapGenerator   │  по seed
                    └────────────┬─────────────┘
                                 │ QuarterPlan (здания, дороги, 46 доставок)
                    ┌────────────▼─────────────┐
                    │  CityBootstrap (Unity)   │  Generate() → PlanBuilt
                    └────────────┬─────────────┘
              ┌──────────────────┴───────────────────┐
   ┌──────────▼───────────┐              ┌───────────▼─────────────┐
   │ QuarterUnityAdapter  │              │ PlayerHomeAnchor        │
   │ строит меши/коллайдер│              │ дверь/выход квартиры №67│
   └──────────────────────┘              └─────────────────────────┘

   Деньги:  MoneyWallet(вне репо) ──IWalletAdapter──> Economy.Shop
                    ▲                                      │
                    │ IPayoutAdapter                       │ IIdempotentWalletAdapter
            Jobs.Delivery ────── RegisterOrder/Accept/PickUp/Complete
                    ▲
                    │ IDeliveryService
            Phone.WorkSummaryService (read-only)

   Долги:   Debts.DebtService ──IDebtWalletAdapter──> MoneyWallet
                             ──IDebtClockAdapter────> GameClock (вне репо)

   Телефон: PhoneFacade ── Apps / Messages / Contacts / Notifications
                          └─ WorkSummaryService ← IDeliveryService
                          └─ BankSummaryService ← IWalletAdapter + IDebtReadService

   UI (PR #14):
     UiRouter ── экраны (Phone/Map/Hud/Pause)
        ├── NavigationService ── RoadGraphData ← QuarterPlanAdapter ← QuarterPlan
        ├── UiSnapshotSource (PauseUiSnapshotSource → City.Pause.PauseDataSource)
        ├── UiWorldAdapter   (UiGameBridge → камера/трансформ)
        └── IDeliveryUiBridge (DeliveryServiceBridge → IDeliveryService)
```

Важное следствие для аудита: **почти каждая стрелка заканчивается объектом,
которого в репозитории нет** — `MoneyWallet`, `GameClock`, `FoodInventory`,
`DeliveryService`-композиция, сцены. Модули спроектированы как «ядро без хоста»,
и это правильно для тестируемости, но означает, что «игра» собирается только
вне этого репозитория.

## 4. Слои и их качество

| Слой | Тестируемость вне Unity | Наличие Unity-зависимостей |
|---|---|---|
| Доменные модели (Economy/Debts/Jobs/Phone/City.Quarter) | полная, тесты исполняются | нет |
| Мосты-адаптеры (`UnityIntegrationKit`) | полная | нет |
| Unity-адаптеры (`City/UnityAdapter`) | невозможна | полная |
| Editor-инструменты (`City/UnityIntegration/Editor`) | невозможна | полная + UnityEditor |
| UI (PR #14) | логика — да (99 тестов), вид — нет | полная |

Это **здоровая** граница: 60 % кода можно гонять в CI без Unity, и это уже
делается (сегодня впервые — см. `TEST_RESULTS.md`).

## 5. Точки, где архитектура расходится с документацией

| Документ говорит | Код делает | Файл |
|---|---|---|
| `COMPILATION_*`/README намекают на «интеграцию» должным быть собранным | в репозитории нет ни одной сцены | корень репозитория |
| `UnityIntegrationKit/README.md`: «мосты готовы, Codex пробрасывает методы» | так и есть, но у `PayoutBridge.TryCredit(long)` **всегда** `false` — это ограничение, а не баг | `AdapterBridges.cs` |
| UI README PR #14: «карта рисует район» | работало бы только при живом плане; дефект `_plan = null` **исправлен** (`786c7fc`) | `QuarterPlanSlot.cs`, `QuarterUnityAdapter.cs` |

## 6. Вывод по архитектуре

Сильные стороны: чистое разделение на «ядро без Unity» и «адаптеры», asmdef
дисциплинированы, зависимости направлены в одну сторону, нет циклических ссылок
между модулями.

Слабые стороны: (1) нет хоста — половина стрелок упирается в несуществующие
объекты; (2) Editor-инструменты (`LifeFromZeroGameIntegration.cs` — 1 100+ строк)
настолько велики, что их невозможно проверить без Unity и трудно читать;
(3) два независимых представления телефона (модуль `Phone` и `UI/Core`), которые
уже разошлись в идентификаторах приложений.
