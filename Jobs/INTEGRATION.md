# Интеграция курьерских заказов (ТЗ №014-Г.1) — инструкция для Codex

Документ описывает, что передаётся, какие контракты есть, как подключить модуль
`Jobs/Delivery/` к настоящему Unity-проекту Life From Zero и что осталось непроверенным.

> **Честно о проверке:** в среде разработки не было Unity и .NET SDK, установка
> компиляторов была запрещена. Код **ни разу не компилировался**, тесты **не запускались**.
> Ниже перечислено, что именно нужно проверить при первой сборке.

---

## 1. Список файлов и рекомендуемые пути

Пути — относительно `Assets/` Unity-проекта.

| Файл в репозитории | Путь в Unity | Назначение |
|---|---|---|
| `Jobs/Delivery/Jobs.Delivery.asmdef` | `Assets/Jobs/Delivery/Jobs.Delivery.asmdef` | Сборка модуля, `noEngineReferences: true` |
| `Jobs/Delivery/DeliveryPoint.cs` | `Assets/Jobs/Delivery/DeliveryPoint.cs` | Точка (id + имя) |
| `Jobs/Delivery/DeliveryOrder.cs` | `Assets/Jobs/Delivery/DeliveryOrder.cs` | Модель заказа |
| `Jobs/Delivery/DeliveryOrderState.cs` | `Assets/Jobs/Delivery/DeliveryOrderState.cs` | Состояния и коды |
| `Jobs/Delivery/DeliveryStatus.cs` | `Assets/Jobs/Delivery/DeliveryStatus.cs` | Статус, результат, исключение |
| `Jobs/Delivery/Adapters.cs` | `Assets/Jobs/Delivery/Adapters.cs` | Три интерфейса-адаптера |
| `Jobs/Delivery/SafeAdapter.cs` | `Assets/Jobs/Delivery/SafeAdapter.cs` | Безопасный вызов адаптеров |
| `Jobs/Delivery/IDeliveryService.cs` | `Assets/Jobs/Delivery/IDeliveryService.cs` | Контракт сервиса |
| `Jobs/Delivery/DeliveryService.cs` | `Assets/Jobs/Delivery/DeliveryService.cs` | Реализация |
| `Jobs/Tests/Jobs.Tests.asmdef` | `Assets/Jobs/Tests/Jobs.Tests.asmdef` | Тестовая сборка (Editor) |
| `Jobs/Tests/DeliveryServiceTests.cs` | `Assets/Jobs/Tests/DeliveryServiceTests.cs` | 31 тест |
| `Jobs/Tests/MiniTestRunner.cs` | `Assets/Jobs/Tests/MiniTestRunner.cs` | Раннер без пакетов |
| `Jobs/Tests/JobsNUnitTests.cs` | `Assets/Jobs/Tests/JobsNUnitTests.cs` | NUnit-обёртка (опционально) |
| `Jobs/Tests/Fakes/FakeAdapters.cs` | `Assets/Jobs/Tests/Fakes/FakeAdapters.cs` | Тестовые адаптеры |
| `Jobs/Tests/Program.cs` | *не переносить в Unity* | `Main` для запуска вне Unity |
| `Jobs/Tests/Console.csproj` | *не переносить в Unity* | Проект `dotnet run` |

**Новые файлы, которые пишет Codex (адаптеры)** — модуль их не содержит:

* `Assets/Jobs/Integration/GameClockAdapter.cs` — `IGameClockAdapter` поверх GameClock.
* `Assets/Jobs/Integration/MoneyWalletPayoutAdapter.cs` — `IPayoutAdapter` поверх MoneyWallet.
* `Assets/Jobs/Integration/PlayerLocationAdapter.cs` — `ILocationProvider` поверх системы перемещения.

---

## 2. Публичные контракты

### 2.1 Модель

```csharp
readonly struct DeliveryPoint : IEquatable<DeliveryPoint>
{
    string Id { get; }            // пустой запрещён (ArgumentException)
    string DisplayName { get; }   // null => равен Id
}

sealed class DeliveryOrder
{
    string Id { get; }                 // уникальный, непустой
    string CargoTitle { get; }         // название груза
    DeliveryPoint PickupPoint { get; } // где получить
    DeliveryPoint DropOffPoint { get; }// куда доставить
    long PaymentRubles { get; }        // > 0, целые рубли
    long DeadlineGameMinutes { get; }  // абсолютное игровое время, >= 0
}

enum DeliveryOrderState { Available, Accepted, PickedUp, Completed, Failed, Cancelled }

enum DeliveryResultCode
{
    Success, OrderNotFound, InvalidState, AlreadyHasActiveOrder, WrongLocation,
    DeadlineExpired, InvalidArgument, AdapterFailure, InternalError
}

readonly struct DeliveryStatus
{
    string OrderId;                  // null, если заказ не найден
    DeliveryOrderState State;
    double? AcceptedAtMinutes;
    double? PickedUpAtMinutes;
    double? ClosedAtMinutes;
    bool PayoutGranted;              // выплата начислена
}

readonly struct DeliveryResult
{
    DeliveryResultCode Code;
    DeliveryStatus Status;           // состояние после операции (или до неё при отказе)
    long PaidRubles;                 // фактически начислено этой операцией (0 при отказе)
    double NowMinutes;               // время, зафиксированное операцией (NaN, если недоступно)
    bool IsSuccess { get; }
}
```

### 2.2 Адаптеры (пишет Codex)

```csharp
interface IGameClockAdapter
{
    double NowMinutes { get; }        // монотонно не убывает, минуты игрового времени
}

interface IPayoutAdapter
{
    bool TryCredit(long rubles);      // true => начислено; false => баланс не изменён
}

interface ILocationProvider
{
    string CurrentLocationId { get; } // может быть пустым
}
```

### 2.3 Сервис

```csharp
interface IDeliveryService
{
    DeliveryResult RegisterOrder(DeliveryOrder order);
    DeliveryResult Accept(string orderId, IGameClockAdapter clock);
    DeliveryResult PickUp(string orderId, IGameClockAdapter clock, ILocationProvider location);
    DeliveryResult Complete(string orderId, IGameClockAdapter clock,
                            ILocationProvider location, IPayoutAdapter payout);
    DeliveryResult Cancel(string orderId, IGameClockAdapter clock);
    DeliveryResult Abandon(string orderId, IGameClockAdapter clock);
    DeliveryResult Refresh(string orderId, IGameClockAdapter clock);
    DeliveryResult GetStatus(string orderId);
    string ActiveOrderId { get; }                 // null, если активного нет
    IReadOnlyList<DeliveryOrder> Orders { get; }
}

sealed class DeliveryService : IDeliveryService { /* без аргументов конструктора */ }
sealed class DeliveryServiceException : Exception { }  // ошибка интеграции, не бизнес-отказ
```

---

## 3. Временная модель (важно для адаптера часов)

Срок хранится как **абсолютное игровое время в минутах** (`DeadlineGameMinutes`), а не как
«сколько осталось». Сравнение: `now > DeadlineGameMinutes` — просрочка.

Почему так:
* пауза, ускорение и сон не ломают логику — время берётся из GameClock в момент действия;
* модуль не тикает сам и не подписывается на события (нет MonoBehaviour и таймеров);
* просрочка фиксируется в момент обращения (`Accept`, `PickUp`, `Complete`, `Cancel`,
  `Abandon`, `Refresh`). Отдельный `Refresh` нужен, чтобы UI показывал провал,
  даже если игрок ничего не делает.

Адаптер часов обязан:

* возвращать минуты (дробные допустимы), монотонно не убывающие;
* не бросать исключений — если бросает, модуль вернёт `AdapterFailure` и **ничего не изменит**;
* не возвращать `NaN`/`Infinity` (модуль считает это отказом адаптера).

```csharp
// Assets/Jobs/Integration/GameClockAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class GameClockAdapter : IGameClockAdapter
{
    private readonly GameClock _clock;   // TODO(Codex): реальный тип из проекта

    public GameClockAdapter(GameClock clock) { _clock = clock; }

    // TODO(Codex): перевести игровое время в минуты с начала игры.
    public double NowMinutes => _clock.TotalMinutes;
}
```

---

## 4. Адаптеры: каркасы и требования

```csharp
// Assets/Jobs/Integration/MoneyWalletPayoutAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class MoneyWalletPayoutAdapter : IPayoutAdapter
{
    private readonly MoneyWallet _wallet;   // TODO(Codex): реальный тип из проекта

    public MoneyWalletPayoutAdapter(MoneyWallet wallet) { _wallet = wallet; }

    public bool TryCredit(long rubles)
    {
        if (rubles <= 0L) return false;
        // TODO(Codex): реальное начисление. Пример-заглушка:
        // _wallet.Add((int)rubles);
        return true;
    }
}
```

Требования:

* `TryCredit` — атомарно: либо начислил всю сумму, либо вернул `false` и не изменил баланс.
* Если кошелёк хранит `int`, проверяйте переполнение на стороне адаптера.

```csharp
// Assets/Jobs/Integration/PlayerLocationAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class PlayerLocationAdapter : ILocationProvider
{
    private readonly IPlayerLocationService _player;  // TODO(Codex): реальная система перемещения

    public PlayerLocationAdapter(IPlayerLocationService player) { _player = player; }

    // TODO(Codex): вернуть id текущей локации, совпадающий с DeliveryPoint.Id.
    public string CurrentLocationId => _player.CurrentLocationId;
}
```

Требования и допущения:

* `CurrentLocationId` должен совпадать с `DeliveryPoint.Id` **строково** (посимвольно,
  `StringComparison.Ordinal`, с учётом регистра).
* Если в проекте локации заданы enum/ScriptableObject — сопоставление делает адаптер.

---

## 5. Подключение

```csharp
var service = new DeliveryService();

// Заказы создаются данными игры (пока — вручную или из конфига):
service.RegisterOrder(new DeliveryOrder(
    id: "bakery_to_office",
    cargoTitle: "Свежий хлеб",
    pickupPoint: new DeliveryPoint("bakery", "Пекарня"),
    dropOffPoint: new DeliveryPoint("office_12", "Офис 12"),
    paymentRubles: 150L,
    deadlineGameMinutes: startMinutes + 120L));

// UI-действия игрока:
DeliveryResult accepted = service.Accept("bakery_to_office", clockAdapter);

// Дошёл до пекарни:
DeliveryResult picked = service.PickUp("bakery_to_office", clockAdapter, locationAdapter);

// Принёс в офис:
DeliveryResult done = service.Complete("bakery_to_office", clockAdapter, locationAdapter, payoutAdapter);

if (!done.IsSuccess)
{
    // Текст подбирается по done.Code на стороне UI/локализации.
}
```

Рекомендации по UI (делает Codex, не этот модуль):

* показывать `ActiveOrderId` и состояние через `GetStatus`;
* периодически (например, раз в игровую минуту) вызывать `Refresh(ActiveOrderId, clock)`,
  чтобы зафиксировать просрочку и снять слот;
* после `Complete` проверять `PaidRubles`, а не считать его самим.

---

## 6. Запуск тестов

### Вариант A — Unity Test Runner

1. Скопируйте `Jobs/` в `Assets/Jobs/`.
2. Нужен `com.unity.test-framework`. Если пакета нет — удалите `Jobs/Tests/Jobs.Tests.asmdef`
   и `JobsNUnitTests.cs` (остальные тесты от NUnit не зависят).
3. `Window → General → Test Runner → EditMode → Run All`.

### Вариант B — вне Unity

```bash
cd Jobs/Tests
dotnet run --project Console.csproj
```

Ожидаемо: **31 тест**, все PASS (фактически не проверялось — см. п.8).

---

## 7. Покрытие требований ТЗ

| Требование | Где |
|---|---|
| 1. Заказ: ID, груз, точки, оплата, срок | `Delivery/DeliveryOrder.cs` |
| 2. Состояния Available/Accepted/PickedUp/Completed/Failed/Cancelled | `DeliveryOrderState.cs` |
| 3. Только один активный заказ | `DeliveryService.ActiveOrderId`, код `AlreadyHasActiveOrder` |
| 4. Груз только после принятия | `PickUp` требует `Accepted` |
| 5. Завершение только после груза и в правильной точке | `Complete` требует `PickedUp` + `WrongLocation` |
| 6. Деньги ровно один раз | `PayoutGranted`, повторный `Complete` → `InvalidState` |
| 7. Просрочка → Failed без выплаты | `Fail(...)`, `DeadlineExpired` |
| 8. Повторные/неверные действия отклоняются | `InvalidState`, `WrongLocation`, `InvalidArgument`, `OrderNotFound` |
| Чистый C#, без UI/MonoBehaviour | asmdef `noEngineReferences`, нет Unity API |
| Свои MoneyWallet/GameClock/перемещение не создаются | только `Adapters.cs` (интерфейсы) |
| Независимость от магазина | нет ссылок на `Economy/` |
| Тесты: успех, просрочка, двойная выплата, неверная точка, отмена, исключения | `Tests/DeliveryServiceTests.cs` (31 кейс) |

---

## 8. Допущения и непроверенное

### Не проверено (главное)

1. **Компиляция не выполнялась** — в среде не было Unity/.NET SDK. Возможны опечатки,
   неверные `using`, несоответствия версии языка.
2. **Тесты не запускались** — «31 PASS» это расчёт, а не результат.
3. **asmdef-ссылки** (`UnityEngine.TestRunner`, `UnityEditor.TestRunner`,
   `nunit.framework.dll`) не сверялись с реальным проектом.
4. **`noEngineReferences: true`** подойдёт, только если модуль не начнёт использовать
   Unity API (например, `ScriptableObject` для заказов). Каталог заказов в SO — отдельный
   слой Codex поверх `RegisterOrder`.

### Допущения о существующих системах

5. **GameClock** умеет отдать игровое время в минутах. Если только `DateTime` или
   «часы:минуты» — перевод делает адаптер.
6. **MoneyWallet** умеет атомарно начислять деньги.
7. **Система перемещения** отдаёт идентификатор текущей локации строкой. Если позиция
   в мире (координаты) — сопоставление с `DeliveryPoint` делает адаптер (например,
   сравнение с зоной).
8. Названия и содержание груза — только UI-данные; логика на них не опирается.

### Осознанные ограничения дизайна

9. **Отмена разрешена только до получения груза** (`Available`, `Accepted`).
   После `PickedUp` — нельзя, иначе груз «испарится». Для бросания работы есть `Abandon`
   (тоже только до груза) и просрочка.
10. **`Abandon` и `Cancel` — разные операции**: `Cancel` — отказ от заказа,
    `Abandon` — «бросил работу». Обе переводят в `Cancelled`; разделение оставлено,
    чтобы позже добавить штраф за брошенную работу без изменения `Cancel`.
11. **Просрочка не тикает сама**: нужен вызов `Refresh` (или любое действие) —
    иначе заказ останется `Accepted` до первого обращения. Это следствие отказа
    от таймеров/MonoBehaviour.
12. **Просроченный заказ в `Cancel`/`Abandon` → `Failed`, а не `Cancelled`**:
    срок истёк раньше, и это честнее отражает причину.
13. **Неудачное начисление не закрывает заказ**: он остаётся `PickedUp`, выплату можно
    повторить. Если время успело уйти за срок — повторная попытка даст `Failed` без денег
    (тест `PayeeRejectsAfterClockShift`).
14. **`DeadlineGameMinutes = 0` разрешён** — это валидный заказ с немедленным истечением
    (полезно для тестов и «срочных» заказов). Отрицательный — `ArgumentOutOfRangeException`.
15. **Оплата `long`**, а не `Money` из модуля экономики: модуль доставки не зависит от
    магазина. Если Codex захочет единый тип — конвертация `Money.FromRubles` на границе
    (в адаптере), сам модуль менять не нужно.
16. **Заказы хранятся в памяти** (`Dictionary` + `List`). Сохранение/загрузка, генерация
    заказов, несколько курьеров, штрафы — вне ТЗ.
17. **Все операции синхронные и однопоточные**: предполагается вызов из главного потока Unity.
    Потокобезопасность не реализована.

### Что осталось за рамками ТЗ

* Несколько одновременных заказов/курьеров, очередь заказов.
* Штрафы за провал, рейтинг курьера, опыт/уровни.
* Сохранение состояния в файл/сейв, генерация заказов по расписанию.
* UI, звук, локализация текстов.
