# Модуль курьерских заказов — ТЗ №014-Г.1

Независимый C#-модуль первой легальной работы игрока: курьерская доставка.
Чистый C# без Unity UI и MonoBehaviour. Не содержит собственного `MoneyWallet`,
`GameClock` или системы перемещения — только интерфейсы-адаптеры к ним.
От модуля экономики (`Economy/`) полностью независим: ни одной ссылки, ни общих типов.

## Финансовые контракты (ТЗ №021)

* У каждого заказа есть собственный `PayOperationId` (`delivery:<orderId>#<n>`), который
  выдаётся при регистрации и сохраняется в снимке. Повтор `Complete` использует ТОТ ЖЕ ID,
  поэтому выплата идемпотентна; заново зарегистрированный заказ с тем же `Id` получает новый ID.
* `IIdempotentPayoutAdapter` дополнен `WasCredited(operationId, out bool)`: «true» без записи
  в журнал считается отказом, а исключение/`false` после реального начисления восстанавливается
  по журналу.
* `DeliveryResult.PayoutOperationId` и `DeliveryService.GetPayoutOperationId(orderId)` отдают
  идентификатор наружу — не собирайте его вручную.
* Тесты: 55 (45 прежних + 10 в `DeliveryFinancialSafetyTests`), реально прогнаны вне Unity.

## Структура

```
Jobs/
├── Delivery/
│   ├── Jobs.Delivery.asmdef      сборка модуля (noEngineReferences)
│   ├── DeliveryPoint.cs          точка на карте (id + отображаемое имя)
│   ├── DeliveryOrder.cs          заказ: id, груз, точки, оплата, срок
│   ├── DeliveryOrderState.cs     состояния и коды результата
│   ├── DeliveryStatus.cs         снимок состояния + DeliveryResult + DeliveryServiceException
│   ├── Adapters.cs               IGameClockAdapter, IPayoutAdapter, ILocationProvider
│   ├── SafeAdapter.cs            безопасный вызов адаптеров
│   ├── IDeliveryService.cs       контракт сервиса заказов
│   └── DeliveryService.cs        реализация: состояния, сроки, выплата, слот
├── Tests/
│   ├── Jobs.Tests.asmdef         тестовая сборка (Editor, NUnit)
│   ├── DeliveryServiceTests.cs   40 тестов
│   ├── MiniTestRunner.cs         раннер без сторонних пакетов
│   ├── JobsNUnitTests.cs         NUnit-обёртка под Unity Test Runner
│   ├── Console.csproj            опциональный проект для `dotnet run`
│   ├── Program.cs                Main для запуска вне Unity
│   └── Fakes/FakeAdapters.cs     тестовые часы, «кошелёк» и локация
└── INTEGRATION.md                инструкция для Codex + допущения
```

## Правила заказов

| Действие | Разрешено когда | Результат |
|---|---|---|
| `Accept` | заказ `Available`, нет активного заказа, срок не истёк | `Accepted` |
| `PickUp` | `Accepted`, игрок в точке получения, срок не истёк | `PickedUp` |
| `Complete` | `PickedUp`, игрок в точке доставки, срок не истёк | `Completed` + выплата один раз |
| `Cancel` | `Available` или `Accepted` (до получения груза) | `Cancelled` |
| `Abandon` | `Accepted` (игрок бросил работу до получения груза) | `Cancelled` |
| `Refresh` | любой открытый заказ | `Failed`, если срок истёк |
| любое | срок истёк у `Available`/`Accepted`/`PickedUp` | `Failed` без выплаты |

* Активный заказ может быть только один (`DeliveryService.ActiveOrderId`); слот выводится
  из фактических состояний заказов, поэтому «зависший» слот не блокирует игру
  (аудит ТЗ №015). Попытка взять второй заказ не меняет состояние запрошенного заказа.
* `PickUp`/`Complete` в неверной точке дают `WrongLocation` и не меняют состояние.
* Повторные `Complete`/`PickUp`/`Accept` дают `InvalidState`; выплата не повторяется.
* Если начисление денег отклонено или упало, заказ остаётся `PickedUp` и выплату можно
  повторить; флаг `PayoutGranted` гарантирует ровно одно начисление при успехе.
* `null`-адаптер → `DeliveryServiceException` (ошибка интеграции). Все бизнес-отказы — коды.

## Проверки

**Код не компилировался, тесты не запускались.** В среде не было Unity и .NET SDK,
установка компиляторов запрещена условиями задачи. Проверено инструментально:
синтаксис всех 9 `.cs` файлов парсером C# (tree-sitter, 0 ошибок), валидность JSON
asmdef-файлов, отсутствие ссылок на `Economy/`, полнота регистрации тестов.
Аудит ТЗ №015 (`../INTEGRATION_AUDIT.md`): исправлены два дефекта (побочный эффект `Accept`
и «зависший» слот активного заказа), добавлено 9 тестов — всего 40.
Подробности и список непроверенного — в `INTEGRATION.md`.
