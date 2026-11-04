# Модуль экономики — ТЗ №014-В.1, финансовые контракты — ТЗ №021

Независимый C#-модуль «ядро экономики и продуктовый магазин» для Life From Zero.
Не содержит Unity UI, MonoBehaviour, сцен, собственного кошелька или инвентаря.
Подключается к `MoneyWallet` / `FoodInventory` через адаптеры, которые пишет Codex.

## Финансовые гарантии (ТЗ №021)

* Каждая покупка с `purchaseId` — запись журнала с явным состоянием
  (`Reserved → Debited → Granted`, либо `→ RefundPending → Refunded/RefundFailed`, либо `Declined`).
* Списание — `shop:<purchaseId>`, возврат — `shop-refund:<purchaseId>`. Повтор с тем же ID
  не двигает деньги и не выдаёт товар (при идемпотентных адаптерах — на уровне кошелька/инвентаря).
* Исключение или «лживый» `false` адаптера больше не означают «денег нет»: исход сверяется
  с журналом адаптера, иначе возвращается `OutcomeUnknown` и покупки блокируются.
* Незавершённая операция блокирует новые покупки (`PendingOperation` / `PendingRefund`)
  до `RetryPendingOperations` или `TryResolveUnknown`.
* `CreateSnapshot` / `RestoreSnapshot` сохраняют незавершённые операции вместе с ID.
* Исключение обработчика `Changed` не влияет на финансовый результат.

Ограничение: гарантии при аварийном завершении требуют durable-журналов в реальных
`MoneyWallet`/`FoodInventory` и согласованной фиксации сохранения — см.
`ECONOMY_INTEGRATION_QUICKSTART.md` (Режимы A и B).

## Структура

```
Economy/
├── Economy.asmdef                 сборка модуля (noEngineReferences)
├── Core/
│   ├── Money.cs                   сумма в целых рублях + проверка переполнения
│   ├── EconomyResultCode.cs       коды исходов операций (+ PendingOperation, OutcomeUnknown)
│   ├── FinancialOperationId.cs    пространство ID: shop:<id> / shop-refund:<id>
│   ├── WalletOperationState.cs    наблюдаемое состояние операции в журнале кошелька
│   ├── IdempotentWalletAdapter.cs IIdempotentWalletAdapter + LegacyWalletAdapter (Режим A)
│   ├── IdempotentInventoryAdapter.cs IIdempotentInventoryAdapter (защита от двойной выдачи)
│   ├── IWalletAdapter.cs          абстракция кошелька (MoneyWallet)
│   ├── IInventoryAdapter.cs       абстракция инвентаря (FoodInventory)
│   └── SafeCall.cs                безопасный вызов адаптеров
├── Shop/
│   ├── ProductType.cs             тип товара (Food / NonFood — задел)
│   ├── Product.cs                 модель товара
│   ├── ShopCatalog.cs             каталог + IShopCatalog + ProductPrice.WithPrice
│   ├── PurchaseRequest.cs         запрос на покупку
│   ├── PurchaseResult.cs          результат + IShopService
│   └── ShopService.cs             сервис покупки (атомарность, компенсации)
├── Tests/
│   ├── Economy.Tests.asmdef       сборка тестов (Editor, NUnit)
│   ├── ShopServiceTests.cs        33 теста: 9 по п.6 ТЗ + 24 дополнительных
│   ├── MiniTestRunner.cs          раннер без сторонних пакетов
│   ├── Program.cs                 Main для запуска вне Unity
│   ├── Console.csproj             опциональный проект для `dotnet run`
│   ├── EconomyNUnitTests.cs       NUnit-обёртка под Unity Test Framework
│   └── Fakes/FakeAdapters.cs      тестовые IWalletAdapter / IInventoryAdapter
└── README.md
```

## Ключевые решения

* **Деньги — `Money`**: `long` рублей, запрет отрицательных значений, `TryMultiply`
  с проверкой переполнения. Цены и суммы не могут «поехать» в float.
* **Атомарность**: порядок «проверка товара → количество → стоимость → баланс →
  возможность принять товар → списание → выдача». Если выдача не удалась, деньги
  возвращаются через `TryRefund`; если возврат не удался — код `InternalError`,
  а сумма попадает в журнал незавершённых возвратов (`PendingRefundCount`).
  Журнал повторяется при следующем `Purchase` и в `RetryPendingRefunds`; пока он не пуст,
  новые покупки блокируются кодом `PendingRefund` — деньги игрока не «сгорают»
  из-за одного сбоя кошелька (аудит ТЗ №015).
* **Адаптеры, а не копии**: модуль ничего не знает о внутренностях `MoneyWallet`
  и `FoodInventory`. Методы существующих классов не выдумываются — их маппинг
  делает Codex (см. `INTEGRATION.md`).
* **Никаких текстов уведомлений** в игровой логике: результат — `PurchaseResult`
  с кодом `EconomyResultCode`; тексты и локализация — на стороне UI.
* **Никаких Singleton**: `ShopService` создаётся там, где нужен, зависимости — в конструкторе.
* **Задел под расширение**: каталог за интерфейсом `IShopCatalog` (разные ассортименты
  магазинов), `PurchaseRequest` — точка расширения под доставку/онлайн-заказы,
  `ProductType.NonFood`, `ProductPrice.WithPrice` для изменения цен.

## Статус проверки

Код **не компилировался и тесты не запускались**: в среде разработки не было
ни Unity, ни .NET SDK, а установка компилятора запрещена условиями задачи.
Подробный список непроверенного — в `INTEGRATION.md`, раздел «Допущения и непроверенное».

Аудит ТЗ №015 (`../INTEGRATION_AUDIT.md`): синтаксис и контракты проверены статически
(tree-sitter + собственный анализатор), найден и исправлен дефект потери денег при
неудачной компенсации, добавлено 8 тестов (всего 33). Сборка и прогон тестов
по-прежнему не выполнялись.
