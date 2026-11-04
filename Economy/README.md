# Модуль экономики — ТЗ №014-В.1

Независимый C#-модуль «ядро экономики и продуктовый магазин» для Life From Zero.
Не содержит Unity UI, MonoBehaviour, сцен, собственного кошелька или инвентаря.
Подключается к `MoneyWallet` / `FoodInventory` через адаптеры, которые пишет Codex.

## Структура

```
Economy/
├── Economy.asmdef                 сборка модуля (noEngineReferences)
├── Core/
│   ├── Money.cs                   сумма в целых рублях + проверка переполнения
│   ├── EconomyResultCode.cs       коды исходов операций
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
│   ├── ShopServiceTests.cs        25 тестов: 9 по п.6 ТЗ + 16 дополнительных
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
  возвращаются через `TryRefund`; если возврат не удался — код `InternalError`.
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
