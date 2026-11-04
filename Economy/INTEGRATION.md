# Интеграция модуля экономики (ТЗ №014-В.1) — инструкция для Codex

Документ описывает, что передаётся, какие публичные контракты есть, как подключить модуль
к настоящему Unity-проекту Life From Zero и что осталось непроверенным.

> **Честно о проверке:** в среде разработки модуля не было Unity и .NET SDK, а установка
> компиляторов была запрещена. Код **ни разу не компилировался**, тесты **не запускались**.
> Ниже перечислено, что именно нужно проверить при первой сборке.

---

## 1. Список файлов и рекомендуемые пути

Все пути — относительно папки `Assets/` Unity-проекта (если у проекта принято иное
расположение скриптов, сохраните относительную структуру `Economy/...`).

| Файл в этом репозитории | Рекомендуемый путь в Unity | Назначение |
|---|---|---|
| `Economy/Economy.asmdef` | `Assets/Economy/Economy.asmdef` | Сборка модуля, `noEngineReferences: true` |
| `Economy/Core/Money.cs` | `Assets/Economy/Core/Money.cs` | Денежная сумма, переполнение |
| `Economy/Core/EconomyResultCode.cs` | `Assets/Economy/Core/EconomyResultCode.cs` | Коды исходов |
| `Economy/Core/IWalletAdapter.cs` | `Assets/Economy/Core/IWalletAdapter.cs` | Абстракция кошелька |
| `Economy/Core/IInventoryAdapter.cs` | `Assets/Economy/Core/IInventoryAdapter.cs` | Абстракция инвентаря |
| `Economy/Core/SafeCall.cs` | `Assets/Economy/Core/SafeCall.cs` | Безопасный вызов адаптеров |
| `Economy/Shop/ProductType.cs` | `Assets/Economy/Shop/ProductType.cs` | Тип товара |
| `Economy/Shop/Product.cs` | `Assets/Economy/Shop/Product.cs` | Модель товара |
| `Economy/Shop/ShopCatalog.cs` | `Assets/Economy/Shop/ShopCatalog.cs` | Каталог, `IShopCatalog`, `ProductPrice` |
| `Economy/Shop/PurchaseRequest.cs` | `Assets/Economy/Shop/PurchaseRequest.cs` | Запрос покупки |
| `Economy/Shop/PurchaseResult.cs` | `Assets/Economy/Shop/PurchaseResult.cs` | Результат, `IShopService` |
| `Economy/Shop/ShopService.cs` | `Assets/Economy/Shop/ShopService.cs` | Сервис покупки |
| `Economy/Tests/Economy.Tests.asmdef` | `Assets/Economy/Tests/Economy.Tests.asmdef` | Тестовая сборка (Editor) |
| `Economy/Tests/ShopServiceTests.cs` | `Assets/Economy/Tests/ShopServiceTests.cs` | 33 теста |
| `Economy/Tests/MiniTestRunner.cs` | `Assets/Economy/Tests/MiniTestRunner.cs` | Раннер без пакетов |
| `Economy/Tests/EconomyNUnitTests.cs` | `Assets/Economy/Tests/EconomyNUnitTests.cs` | NUnit-обёртка (опционально) |
| `Economy/Tests/Fakes/FakeAdapters.cs` | `Assets/Economy/Tests/Fakes/FakeAdapters.cs` | Тестовые адаптеры |
| `Economy/Tests/Program.cs` | *не переносить в Unity* | `Main` для запуска вне Unity |
| `Economy/Tests/Console.csproj` | *не переносить в Unity* | Проект `dotnet run` |

**Новые файлы, которые пишет Codex (адаптеры).** Модуль намеренно не содержит их:

* `Assets/Economy/Integration/MoneyWalletAdapter.cs` — `IWalletAdapter` поверх `MoneyWallet`.
* `Assets/Economy/Integration/FoodInventoryAdapter.cs` — `IInventoryAdapter` поверх `FoodInventory`.

---

## 2. Публичные интерфейсы и типы

### 2.1 Ядро (`LifeFromZero.Economy.Core`)

```csharp
readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    static Money Zero { get; }
    long Rubles { get; }                       // всегда >= 0
    static Money FromRubles(long rubles);      // бросает ArgumentOutOfRangeException при < 0
    static bool TryFromRubles(long rubles, out Money money);
    bool TryMultiply(int quantity, out Money total);   // false при quantity < 0 или переполнении
    bool TryAdd(Money other, out Money sum);           // false при переполнении
}

enum EconomyResultCode { Success, InsufficientFunds, ProductNotFound, InvalidQuantity, InventoryRejected, InternalError, PendingRefund }

interface IWalletAdapter
{
    Money Balance { get; }
    bool TrySpend(Money amount);    // атомарно: либо списал всё, либо ничего
    bool TryRefund(Money amount);   // компенсация
}

interface IInventoryAdapter
{
    bool CanApprove(string itemId, int quantity); // можно ли принять; без побочных эффектов
    bool TryAdd(string itemId, int quantity);     // false => состояние НЕ изменилось
}

static class SafeCall { bool Try(Action action); bool Try(Func<bool> func, out bool result); }
```

### 2.2 Магазин (`LifeFromZero.Economy.Shop`)

```csharp
enum ProductType { Food, NonFood }

sealed class Product
{
    string Id, Title, InventoryItemId;
    Money Price;            // цена за одну покупку (упаковку)
    ProductType Type;
    int UnitsPerPurchase;   // единиц предмета за одну покупку
    bool TryGetTotalUnits(int purchases, out int totalUnits);
}

interface IShopCatalog
{
    bool TryGet(string productId, out Product product);
    IReadOnlyList<Product> Products { get; }
}

sealed class ShopCatalog : IShopCatalog
{
    ShopCatalog(IEnumerable<Product> products);       // бросает при дублях id
    static ShopCatalog CreateDefault();
    static IReadOnlyList<Product> CreateDefaultProducts(); // bread 50, instant_noodles 65, prepared_meal 220
}

static class ProductPrice { Product WithPrice(Product source, Money newPrice); } // без мутации

readonly struct PurchaseRequest { string ProductId; int Quantity; } // Quantity — число покупок

readonly struct PurchaseResult
{
    EconomyResultCode Code;
    Product Product;          // null при ProductNotFound
    int RequestedQuantity;
    int PurchasedQuantity;    // 0 при ошибке
    int UnitsGranted;         // 0 при ошибке
    Money TotalCost;          // считается, если товар найден и количество > 0
    bool IsSuccess { get; }
}

sealed class ShopServiceException : Exception   // ошибка интеграции, а не бизнес-отказ

interface IShopService
{
    PurchaseResult Purchase(IWalletAdapter wallet, IInventoryAdapter inventory, PurchaseRequest request);
    IShopCatalog Catalog { get; }
    int PendingRefundCount { get; }                       // незавершённые возвраты (ТЗ №015)
    bool RetryPendingRefunds(IWalletAdapter wallet);      // повтор компенсаций
}

sealed class ShopService : IShopService { ShopService(IShopCatalog catalog); }
```

---

## 3. Семантика покупки

Порядок шагов в `ShopService.Purchase`:

1. Найти товар → `ProductNotFound`.
2. `Quantity <= 0` → `InvalidQuantity`.
3. `Price × Quantity` с проверкой переполнения `long` → `InvalidQuantity` при переполнении
   (в результате `TotalCost = 0`).
4. `UnitsPerPurchase × Quantity` с проверкой переполнения `int` → `InvalidQuantity`.
5. Прочитать баланс; `Balance < TotalCost` → `InsufficientFunds` (в результате есть `TotalCost`).
6. `inventory.CanApprove(...)` → при `false` `InventoryRejected` **без списания денег**.
7. `wallet.TrySpend(TotalCost)`; `false` → `InsufficientFunds`; исключение → `InternalError`.
8. `inventory.TryAdd(...)`; при `false`/исключении → `wallet.TryRefund(TotalCost)`:
   успешно → `InventoryRejected`, неуспешно → `InternalError` **и сумма уходит в журнал
   незавершённых возвратов** (аудит ТЗ №015). Шаг 0 каждой следующей покупки — повтор
   журнала; если он не закрылся, покупка возвращает `PendingRefund` и ничего не списывает.
9. Успех → `Success`, `PurchasedQuantity = Quantity`, `UnitsGranted = UnitsPerPurchase × Quantity`.

**Гарантия:** деньги списываются только после подтверждённой возможности принять товар,
и любая неудача после списания компенсируется возвратом. Текстов уведомлений в модуле нет.

---

## 4. Что делает Codex: адаптеры

Модуль **не знает** реальных сигнатур `MoneyWallet` и `FoodInventory` — их нужно
сопоставить в адаптерах. Ниже каркас; строки с `// TODO(Codex)` заменить на реальные вызовы.

```csharp
// Assets/Economy/Integration/MoneyWalletAdapter.cs
using LifeFromZero.Economy.Core;

public sealed class MoneyWalletAdapter : IWalletAdapter
{
    private readonly MoneyWallet _wallet;   // TODO(Codex): реальный тип из проекта

    public MoneyWalletAdapter(MoneyWallet wallet) { _wallet = wallet; }

    public Money Balance => Money.FromRubles(_wallet.Amount); // TODO(Codex): как читается баланс

    public bool TrySpend(Money amount)
    {
        if (_wallet.Amount < amount.Rubles) return false;
        _wallet.Spend((int)amount.Rubles);   // TODO(Codex): реальный метод списания
        return true;
    }

    public bool TryRefund(Money amount)
    {
        _wallet.Add((int)amount.Rubles);     // TODO(Codex): реальный метод начисления
        return true;
    }
}
```

Требования к адаптеру кошелька:

* `Balance` — без исключений и побочных эффектов.
* `TrySpend` — **атомарно**: нельзя списать половину суммы.
* `TryRefund` — возвращает ровно то, что было списано.

```csharp
// Assets/Economy/Integration/FoodInventoryAdapter.cs
using LifeFromZero.Economy.Core;

public sealed class FoodInventoryAdapter : IInventoryAdapter
{
    private readonly FoodInventory _inventory;   // TODO(Codex): реальный тип из проекта

    public FoodInventoryAdapter(FoodInventory inventory) { _inventory = inventory; }

    // Точная проверка вместимости без изменения состояния.
    public bool CanApprove(string itemId, int quantity) { /* TODO(Codex) */ }

    // Добавление; при false состояние не меняется.
    public bool TryAdd(string itemId, int quantity) { /* TODO(Codex) */ }
}
```

Требования к адаптеру инвентаря (важно для атомарности):

* `CanApprove` — точная проверка вместимости **без изменения состояния**. Именно она
  гарантирует, что деньги не будут списаны при переполненном инвентаре.
* `TryAdd` при `false` **не должен** менять состояние. Если существующий инвентарь так не умеет,
  лучше вернуть `false` до мутации, чем «добавить частично».
* Если в проекте понадобится возврат/отмена покупки — расширьте интерфейс на стороне Codex
  (например, `bool Remove(...)`); магазин покупки от этого не меняется.

Подключение на сцене (вручную или через отдельный installer-скрипт Codex):

```csharp
IShopCatalog catalog = ShopCatalog.CreateDefault();
IShopService shop = new ShopService(catalog);

var walletAdapter = new MoneyWalletAdapter(wallet);
var inventoryAdapter = new FoodInventoryAdapter(foodInventory);

PurchaseResult result = shop.Purchase(
    walletAdapter, inventoryAdapter, new PurchaseRequest("bread", 1));

if (!result.IsSuccess)
{
    // Здесь и только здесь — тексты: UI сам решает по result.Code.
}
```

---

## 5. Запуск тестов

### Вариант A — Unity Test Runner (рекомендуется)

1. Скопируйте `Economy/` в `Assets/Economy/`.
2. Убедитесь, что установлен **Test Framework** (`com.unity.test-framework`).
   Если пакета нет — либо установите, либо удалите `Economy/Tests/Economy.Tests.asmdef`
   и `EconomyNUnitTests.cs` (остальные тесты от NUnit не зависят).
3. `Window → General → Test Runner → EditMode → Run All`.

`Economy.Tests.asmdef` ссылается на `LifeFromZero.Economy`, `UnityEngine.TestRunner`,
`UnityEditor.TestRunner` и подключён только для Editor через `UNITY_INCLUDE_TESTS`.

### Вариант B — вне Unity, обычным .NET SDK

```bash
cd Economy/Tests
dotnet run --project Console.csproj
```

`Console.csproj` компилирует `Core`, `Shop` и тесты как консольное приложение
и возвращает `0` при успехе, `1` при провале.

Ожидаемый результат: **33 теста**, все PASS (см. п.7 — фактически не проверялось).

---

## 6. Покрытие требований ТЗ

| Пункт ТЗ | Где реализовано |
|---|---|
| 1. Модель товара (id, название, цена, тип, единиц при покупке, id предмета) | `Shop/Product.cs` |
| 1. Начальный каталог, цены легко менять | `ShopCatalog.CreateDefaultProducts()`, `ProductPrice.WithPrice` |
| 1. Эффекты еды не дублируются | В `Product` нет полей сытости/питательности |
| 2. Логика покупки, шаги 1–6 | `Shop/ShopService.cs`, `PurchaseKnownProduct` |
| 2. Атомарность, компенсация | `ShopService.Compensate`, `SafeCall` |
| 2. Абстракции кошелька и инвентаря | `Core/IWalletAdapter.cs`, `Core/IInventoryAdapter.cs` |
| 3. Структурированный результат, коды, стоимость, количество | `Shop/PurchaseResult.cs`, `Core/EconomyResultCode.cs` |
| 3. Без текстов уведомлений в логике | Тексты только в `README`/UI-примере, не в сервисе |
| 4. Задел под несколько магазинов / цены / непродовольственные | `IShopCatalog`, `ProductPrice`, `ProductType.NonFood` |
| 4. Онлайн-заказы и доставка | `PurchaseRequest` — точка расширения; не реализовано (по ТЗ) |
| 5. Unity 6000, C# 9, без MonoBehaviour/UI/Singleton/пакетов | asmdef `noEngineReferences: true`, `LangVersion 9` |
| 5. Деньги целыми рублями, контроль переполнения | `Core/Money.cs` |
| 6. 9 обязательных тестов | `Tests/ShopServiceTests.cs` (+16 дополнительных) |
| 6. Тестовые реализации адаптеров | `Tests/Fakes/FakeAdapters.cs` |
| 7. Полный код, тесты, пути, интерфейсы, интеграция, допущения | Все файлы + этот документ |

---

## 7. Допущения и непроверенное

### Не проверено (главное)

1. **Компиляция не выполнялась.** Ни один файл не собирался: в среде не было Unity/.NET SDK.
   Возможны опечатки, неверные `using`, несоответствия версии языка.
2. **Тесты не запускались.** Ожидание «25 PASS» — расчётное, а не результат прогона.
3. **Названия asmdef-ссылок** (`UnityEngine.TestRunner`, `UnityEditor.TestRunner`,
   `nunit.framework.dll`) соответствуют типовому Unity 6000, но не сверялись с проектом.
4. **`noEngineReferences: true`** подходит, только если модуль не будет использовать Unity API.
   Если Codex захочет держать каталог в ScriptableObject — это отдельный слой поверх `IShopCatalog`.
5. **`LangVersion 9.0`** в `Console.csproj` выбран для совместимости с Unity 6000;
   фактический уровень языка проекта может отличаться.

### Допущения о существующей системе

6. **`MoneyWallet`** умеет читать баланс и атомарно списывать/начислять деньги. Адаптер — тонкая
   обёртка; если в проекте деньги хранятся `int`, в адаптере потребуется приведение с проверкой.
7. **`FoodInventory`** позволяет проверить вместимость без мутации. Если нет — `CanApprove` в адаптере
   реализуется по правилам слотов; тогда «отказ инвентаря» может проявиться уже на `TryAdd`,
   и сработает компенсация возврата денег.
8. **`InventoryItemId`** — строковый идентификатор, совпадающий с тем, что понимает `FoodInventory`.
   Если в проекте используются enum/ScriptableObject-ссылки, нужен маппинг в адаптере.
9. **Эффекты еды** (сытость) полностью остаются в `FoodInventory`/`NeedsRuntime`; магазин их не трогает.
10. **`GameClock`** магазину сейчас не нужен. Для будущих онлайн-заказов/доставки его время
    понадобится в `PurchaseRequest`/отдельном сервисе — текущий API это допускает, но не реализует.

### Осознанные ограничения дизайна

11. **`Quantity` в `PurchaseRequest` — число покупок (упаковок), а не единиц предмета.**
    Для текущего каталога `UnitsPerPurchase = 1`, поэтому разницы нет. Задокументировано в XML-комментариях.
12. **Переполнение стоимости возвращает `InvalidQuantity`**, а не отдельный код: ТЗ фиксирует
    закрытый список кодов, а `InternalError` здесь вводил бы в заблуждение (операция корректно отклонена).
13. **Компенсация не снимает предметы после неудачного `TryAdd`** — по контракту адаптера состояние
    не изменилось. Если адаптер контракт нарушает, магазин не списывает предметы наугад,
    чтобы не уничтожить уже лежащие у игрока; сверка — на стороне адаптера.
    Это осознанное сужение интерфейса: в первой версии адаптер объявлял ещё методы
    `Approved`/`CanAccept`/`Remove`, но семантику проверки и списания пришлось бы угадывать
    вслепую, поэтому интерфейс сведён к двум надёжным операциям (`CanApprove`, `TryAdd`).
14. **`Purchase` бросает `ShopServiceException`, если передан `null` вместо кошелька/инвентаря.**
    Это ошибка интеграции, а не отказ игрока; отказы всегда возвращаются кодом.
15. **Исключения адаптеров превращаются в коды** (`InternalError` или `InventoryRejected`)
    и не пробрасываются в UI; логирование — задача адаптера.
16. **Переполнение `int` в количестве единиц** отсекается тем же кодом `InvalidQuantity`
    (товар с `UnitsPerPurchase`, близким к `int.MaxValue`).
17. **Пустой или `null` идентификатор товара** даёт `ProductNotFound` ещё до обращения к каталогу,
    поэтому каталогам не нужно отдельно защищаться от `null`.

### Что осталось за рамками ТЗ (не реализовано осознанно)

* Несколько магазинов с разными ассортиментами — `IShopCatalog` готов, конкретные магазины не созданы.
* Онлайн-заказы, доставка, изменение цен во времени, непродовольственные товары —
  только точки расширения (`ProductType.NonFood`, `ProductPrice.WithPrice`, `PurchaseRequest`).
* Сохранение/загрузка состояния, UI, локализация текстов, звук — вне модуля.
