# Интеграция смартфона (ТЗ №014-Т.1) — инструкция для Codex

Что передаётся, как подключить к Unity-проекту Life From Zero, примеры использования
и список непроверенного.

> **Честно о проверке:** в среде разработки не было Unity и .NET SDK, установка
> компиляторов была запрещена. Код **ни разу не компилировался**, тесты **не запускались**.
> См. раздел 9.

---

## 1. Список файлов и рекомендуемые пути

Пути — относительно `Assets/`.

| Файл | Путь в Unity | Назначение |
|---|---|---|
| `Phone/Phone.asmdef` | `Assets/Phone/Phone.asmdef` | Сборка модуля, `noEngineReferences: true` |
| `Phone/Core/PhoneResult.cs` | `Assets/Phone/Core/PhoneResult.cs` | `PhoneResultCode`, `PhoneResult`, `PhoneResult<T>`, `PhoneIntegrationException` |
| `Phone/Core/GameMinutes.cs` | `Assets/Phone/Core/GameMinutes.cs` | Игровое время, валидация |
| `Phone/Core/PhoneClockReader.cs` | `Assets/Phone/Core/PhoneClockReader.cs` | Безопасное чтение часов |
| `Phone/Core/IPhoneClock.cs` | `Assets/Phone/Core/IPhoneClock.cs` | Адаптер к GameClock |
| `Phone/Core/PhoneId.cs` | `Assets/Phone/Core/PhoneId.cs` | Проверка ID |
| `Phone/Core/IUnreadCounter.cs` | `Assets/Phone/Core/IUnreadCounter.cs` | Общий счётчик |
| `Phone/Apps/PhoneApp.cs` | `Assets/Phone/Apps/PhoneApp.cs` | Приложение, категория |
| `Phone/Apps/PhoneAppRegistry.cs` | `Assets/Phone/Apps/PhoneAppRegistry.cs` | Реестр + стартовый набор |
| `Phone/Messages/PhoneMessage.cs` | `Assets/Phone/Messages/PhoneMessage.cs` | Модель сообщения |
| `Phone/Messages/PhoneConversation.cs` | `Assets/Phone/Messages/PhoneConversation.cs` | Переписка |
| `Phone/Messages/PhoneMessageService.cs` | `Assets/Phone/Messages/PhoneMessageService.cs` | Сервис сообщений |
| `Phone/Messages/PhoneMessagesSnapshot.cs` | `Assets/Phone/Messages/PhoneMessagesSnapshot.cs` | DTO сохранения |
| `Phone/Messages/PhoneInbox.cs` | `Assets/Phone/Messages/PhoneInbox.cs` | Вход для NPC/сюжета |
| `Phone/Contacts/PhoneContact.cs` | `Assets/Phone/Contacts/PhoneContact.cs` | Контакт |
| `Phone/Contacts/PhoneBook.cs` | `Assets/Phone/Contacts/PhoneBook.cs` | Телефонная книга |
| `Phone/Notifications/PhoneNotification.cs` | `Assets/Phone/Notifications/PhoneNotification.cs` | Уведомление, типы |
| `Phone/Notifications/PhoneNotificationService.cs` | `Assets/Phone/Notifications/PhoneNotificationService.cs` | Сервис уведомлений |
| `Phone/Notifications/PhoneNotificationsSnapshot.cs` | `Assets/Phone/Notifications/PhoneNotificationsSnapshot.cs` | DTO сохранения |
| `Phone/Notifications/PhoneNotificationHub.cs` | `Assets/Phone/Notifications/PhoneNotificationHub.cs` | `IPhoneNotifier`, бейджи |
| `Phone/Notifications/ExternalAdapters.cs` | `Assets/Phone/Notifications/ExternalAdapters.cs` | `IBalanceSource`, `IDeliveryJobsSource`, `IDebtsSource`, `IStorySource` |
| `Phone/Integrations/BankSummaryService.cs` | `Assets/Phone/Integrations/BankSummaryService.cs` | Экран «Банк» |
| `Phone/Integrations/WorkSummaryService.cs` | `Assets/Phone/Integrations/WorkSummaryService.cs` | Экран «Работа» |
| `Phone/Integrations/StorySummaryService.cs` | `Assets/Phone/Integrations/StorySummaryService.cs` | Сюжетные записи |
| `Phone/Integrations/PhoneFacade.cs` | `Assets/Phone/Integrations/PhoneFacade.cs` | Сборка всех сервисов |
| `Phone/Tests/*` | `Assets/Phone/Tests/*` | 79 тестов, раннер, NUnit-обёртка |
| `Phone/Tests/Console.csproj`, `Program.cs` | *не переносить в Unity* | Запуск вне Unity |

**Файлы, которые пишет Codex (адаптеры к существующим системам):**

* `Assets/Phone/Integration/UnityGameClockAdapter.cs` — `IPhoneClock` поверх `GameClock`.
* `Assets/Phone/Integration/MoneyWalletBalanceSource.cs` — `IBalanceSource` поверх `MoneyWallet`.
* `Assets/Phone/Integration/DeliveryJobsSource.cs` — `IDeliveryJobsSource` поверх `Jobs/Delivery`.
* `Assets/Phone/Integration/DebtsSource.cs` — `IDebtsSource` поверх будущей системы долгов.
* `Assets/Phone/Integration/StorySource.cs` — `IStorySource` поверх сюжетной системы.

---

## 2. Публичные интерфейсы

### 2.1 Ядро

```csharp
enum PhoneResultCode
{
    Success, InvalidArgument, DuplicateId, NotFound, InvalidState,
    CounterOverflow, AdapterFailure, InternalError
}

readonly struct PhoneResult      { PhoneResultCode Code; bool IsSuccess; string TargetId; }
readonly struct PhoneResult<T>   { PhoneResultCode Code; T Value; bool IsSuccess; }
sealed class PhoneIntegrationException : Exception   // ошибка интеграции, не бизнес-отказ

readonly struct GameMinutes : IEquatable<GameMinutes>, IComparable<GameMinutes>
{
    static GameMinutes Unknown;      // значение недоступно
    bool IsValid;
    double Minutes;                  // 0 для Unknown
    static GameMinutes FromMinutes(double minutes);       // NaN/∞/<0 => Unknown
    static bool TryFromMinutes(double minutes, out GameMinutes value);
}

interface IPhoneClock { double NowMinutes { get; } }     // адаптер к GameClock
static class PhoneClockReader { GameMinutes Read(IPhoneClock clock); }
static class PhoneId { bool IsValid(string id); bool AreSame(string a, string b); }
interface IUnreadCounter { int UnreadCount { get; } }
```

### 2.2 Приложения

```csharp
enum PhoneAppCategory { Communication, Work, Finance, Contacts, System, Story, Other }

sealed class PhoneApp
{
    string Id; string Title; PhoneAppCategory Category; bool IsAvailable; int UnreadCount;
    PhoneApp With(bool isAvailable, int unreadCount);
}

interface IPhoneAppRegistry
{
    PhoneResult Register(PhoneApp app);
    PhoneResult<PhoneApp> Get(string appId);
    IReadOnlyList<PhoneApp> Apps { get; }
    PhoneResult SetAvailability(string appId, bool isAvailable);
    PhoneResult AddUnread(string appId, int delta);   // <0 допустим, уход ниже нуля — InvalidArgument
    PhoneResult ClearUnread(string appId);
    int TotalUnread { get; }
}

sealed class PhoneAppRegistry : IPhoneAppRegistry
{
    static PhoneAppRegistry CreateDefault();          // messages/jobs/bank/contacts/settings
    static IReadOnlyList<PhoneApp> CreateDefaultApps();
}
```

### 2.3 Сообщения

```csharp
enum PhoneMessageDirection { Incoming, Outgoing }

sealed class PhoneMessage
{
    string Id; string ConversationId; PhoneMessageDirection Direction;
    string Text; GameMinutes SentAt; bool IsRead;      // Outgoing всегда IsRead
    PhoneMessage AsRead();
}

interface IPhoneConversation : IUnreadCounter
{
    string ConversationId; IReadOnlyList<PhoneMessage> Messages;
}

interface IPhoneMessageService
{
    PhoneResult StartConversation(string contactId);
    IReadOnlyList<IPhoneConversation> Conversations { get; }
    PhoneResult<IPhoneConversation> GetConversation(string contactId);
    PhoneResult<PhoneMessage> ReceiveFromContact(string contactId, string text, IPhoneClock clock);
    PhoneResult<PhoneMessage> SendFromPlayer(string contactId, string text, IPhoneClock clock);
    PhoneResult<IReadOnlyList<PhoneMessage>> GetHistory(string contactId);
    PhoneResult MarkRead(string contactId, string messageId);
    PhoneResult MarkAllRead(string contactId);
    int TotalUnread { get; }
    PhoneMessagesSnapshot CreateSnapshot();
}

interface IPhoneInbox { PhoneResult<PhoneMessage> Deliver(string contactId, string text, IPhoneClock clock); }
```

### 2.4 Контакты

```csharp
sealed class PhoneContact { string Id; string Name; string PhoneNumber; bool IsAvailable; }

interface IPhoneBook
{
    PhoneResult Add(PhoneContact contact);
    PhoneResult<PhoneContact> GetById(string contactId);
    PhoneResult<PhoneContact> GetByName(string name);   // точное, без учёта регистра;
                                                        // несколько совпадений => DuplicateId
    IReadOnlyList<PhoneContact> Search(string nameQuery); // подстрока, "" => все
    IReadOnlyList<PhoneContact> Contacts { get; }
    PhoneResult SetAvailability(string contactId, bool isAvailable);
}
```

### 2.5 Уведомления

```csharp
enum PhoneNotificationType { Message, Job, Bank, Debt, Story, System }

sealed class PhoneNotification
{
    string Id; string Source; PhoneNotificationType Type;
    string Title; string Text; GameMinutes CreatedAt; bool IsRead;
    PhoneNotification AsRead();
}

interface IPhoneNotificationService
{
    PhoneResult Add(PhoneNotification notification);
    PhoneResult Add(string id, string source, PhoneNotificationType type,
                    string title, string text, IPhoneClock clock);
    IReadOnlyList<PhoneNotification> Notifications { get; }
    IReadOnlyList<PhoneNotification> GetByType(PhoneNotificationType type);
    IReadOnlyList<PhoneNotification> GetUnread();
    PhoneResult<PhoneNotification> Get(string notificationId);
    PhoneResult MarkRead(string notificationId);      // повтор — успех
    PhoneResult MarkAllRead();
    int UnreadCount { get; }
    PhoneNotificationsSnapshot CreateSnapshot();
}

enum PhoneNotificationChannel { Inbox, AppBadge, Banner }

interface IPhoneNotifier
{
    PhoneResult Push(string id, string source, PhoneNotificationType type, string title,
                     string text, string appId, PhoneNotificationChannel channel, IPhoneClock clock);
    IPhoneNotificationService Notifications { get; }
}
```

### 2.6 Интеграции (только чтение)

```csharp
interface IBalanceSource        { bool TryGetBalanceRubles(out long rubles); }
interface IDeliveryJobsSource   { bool TryGetOrders(out DeliveryJobDto[] orders);
                                  bool TryGetActiveOrderId(out string orderId); }
interface IDebtsSource          { bool TryGetDebts(out DebtDto[] debts); }
interface IStorySource          { bool TryGetEntries(out StoryEntryDto[] entries); }

sealed class DeliveryJobDto  { string OrderId, CargoTitle; long PaymentRubles; DeliveryJobPhase Phase; }
sealed class DebtDto         { string Id, Title; long AmountRubles; double DueAtMinutes; }
sealed class StoryEntryDto   { string Id, Title, Text; }

sealed class BankSummaryService  { PhoneResult<BankSummary> GetSummary(); }  // balance + debts
sealed class WorkSummaryService  { PhoneResult<WorkSummary> GetSummary(); }  // заказы, только чтение
sealed class StorySummaryService { PhoneResult<StoryEntryDto[]> GetEntries(); }

sealed class PhoneFacade
{
    PhoneFacade(IPhoneAppRegistry, IPhoneMessageService, IPhoneBook, IPhoneNotificationService);
    IPhoneAppRegistry Apps; IPhoneMessageService Messages; IPhoneBook Contacts;
    IPhoneNotificationService Notifications; IPhoneInbox Inbox; IPhoneNotifier Notifier;
    int UnreadMessages, UnreadNotifications, UnreadApps;
}
```

---

## 3. Подключение к Unity

```csharp
// 1. Сборка сервисов (один раз при загрузке сцены/игры; НЕ Singleton).
var phone = new PhoneFacade(
    PhoneAppRegistry.CreateDefault(),
    new PhoneMessageService(),
    new PhoneBook(),
    new PhoneNotificationService());

// 2. Адаптеры к существующим системам (пишет Codex).
IPhoneClock clock = new UnityGameClockAdapter(gameClock);
var bank = new BankSummaryService(new MoneyWalletBalanceSource(moneyWallet), new DebtsSource(debtSystem));
var work = new WorkSummaryService(new DeliveryJobsSource(deliveryService));

// 3. Стартовые контакты (данные игры, не модуля).
phone.Contacts.Add(new PhoneContact("anna", "Анна", "+7-900-000-00-01", true));

// 4. NPC пишет игроку.
phone.Inbox.Deliver("anna", "Ты уже нашёл работу?", clock);

// 5. Уведомление о новом заказе.
phone.Notifier.Push(
    id: "job-42-available",
    source: "jobs",
    type: PhoneNotificationType.Job,
    title: "Новый заказ",
    text: "Доставить хлеб в офис 12",
    appId: "jobs",
    channel: PhoneNotificationChannel.AppBadge,
    clock: clock);

// 6. UI читает состояние.
PhoneApp jobsApp  = phone.Apps.Get("jobs").Value;            // бейдж: jobsApp.UnreadCount
var history       = phone.Messages.GetHistory("anna");       // переписка
var bankData      = bank.GetSummary().Value;                 // баланс и долги (read-only)
var workData      = work.GetSummary().Value;                 // заказы (read-only)
```

### Пример адаптеров (каркасы для Codex)

```csharp
// Assets/Phone/Integration/UnityGameClockAdapter.cs
using LifeFromZero.Phone.Core;

public sealed class UnityGameClockAdapter : IPhoneClock
{
    private readonly GameClock _clock;   // TODO(Codex): реальный тип
    public UnityGameClockAdapter(GameClock clock) { _clock = clock; }

    // TODO(Codex): перевести игровое время в минуты с начала игры.
    public double NowMinutes => _clock.TotalMinutes;
}
```

```csharp
// Assets/Phone/Integration/MoneyWalletBalanceSource.cs
using LifeFromZero.Phone.Integrations;

public sealed class MoneyWalletBalanceSource : IBalanceSource
{
    private readonly MoneyWallet _wallet;  // TODO(Codex): реальный тип
    public MoneyWalletBalanceSource(MoneyWallet wallet) { _wallet = wallet; }

    public bool TryGetBalanceRubles(out long rubles)
    {
        // TODO(Codex): прочитать баланс. Только чтение! Телефон не меняет деньги.
        rubles = _wallet.Balance;
        return rubles >= 0L;
    }
}
```

```csharp
// Assets/Phone/Integration/DeliveryJobsSource.cs
using LifeFromZero.Phone.Integrations;

public sealed class DeliveryJobsSource : IDeliveryJobsSource
{
    private readonly IDeliveryService _delivery;   // из Jobs/Delivery, только чтение

    public DeliveryJobsSource(IDeliveryService delivery) { _delivery = delivery; }

    public bool TryGetOrders(out DeliveryJobDto[] orders)
    {
        // TODO(Codex): спроецировать заказы модуля доставки в DTO.
        orders = new DeliveryJobDto[0];
        return true;
    }

    public bool TryGetActiveOrderId(out string orderId)
    {
        orderId = _delivery.ActiveOrderId;
        return true;
    }
}
```

### Правила интеграции

1. **Не давайте телефону доступ к мутирующим методам** внешних систем. Только чтение.
2. **Все ожидаемые ошибки — по коду** `PhoneResult.Code`; тексты подбирает UI/локализация.
3. **`GameMinutes.Unknown`** означает «время недоступно» — показывайте без времени, не падайте.
4. **Счётчики несвязанных сущностей считаются раздельно**: `UnreadMessages`,
   `UnreadNotifications`, `UnreadApps`.
5. **Повторный `Push` с тем же ID** не создаёт дубликат и не увеличивает бейдж приложения.

---

## 4. Сохранение игры (задел)

Модуль не пишет на диск. Для сохранения есть DTO-снимки:

```csharp
PhoneMessagesSnapshot      messages      = phone.Messages.CreateSnapshot();
PhoneNotificationsSnapshot notifications = phone.Notifications.CreateSnapshot();
```

Внутри — только простые поля (`string`, `enum`, `bool`, `GameMinutes`), которые
сериализуются любым способом (`JsonUtility`, `System.Text.Json`, собственный формат).
Восстановление (десериализация → регистрация) — задача Codex: ядро менять не нужно,
достаточно методов `Register` / `Add` / `ReceiveFromContact`.

**Ограничение:** сейчас нет метода «восстановить снимок разом». Если понадобится —
добавьте отдельный `RestoreSnapshot` на стороне Codex (интерфейсы открыты), либо
восстанавливайте по одному элементу через существующие методы.

---

## 5. Запуск тестов

### Вариант A — Unity Test Runner

1. Скопируйте `Phone/` в `Assets/Phone/`.
2. Нужен `com.unity.test-framework`. Без него удалите `Phone/Tests/Phone.Tests.asmdef`
   и `PhoneNUnitTests.cs` — остальное от NUnit не зависит.
3. `Window → General → Test Runner → EditMode → Run All`.

### Вариант B — вне Unity

```bash
cd Phone/Tests && dotnet run --project Console.csproj
```

Ожидаемо: **79 тестов**, все PASS (фактически не проверялось).

---

## 6. Покрытие требований ТЗ

| Требование | Где |
|---|---|
| 2. `PhoneAppRegistry`, поля приложения | `Apps/PhoneApp.cs`, `Apps/PhoneAppRegistry.cs` |
| 2. Стартовые 5 приложений | `PhoneAppRegistry.CreateDefault()` |
| 2. Регистрация доп. приложений без правки ядра | `Register(PhoneApp)` |
| 3. Переписки, входящие/исходящие, ID, время, прочтение, история, счётчик | `Messages/*` |
| 3. Структуры для сохранения | `PhoneMessagesSnapshot` |
| 4. `PhoneNotificationService`, 6 типов, дедупликация | `Notifications/PhoneNotificationService.cs` |
| 5. Контакты, добавление, поиск по ID и имени | `Contacts/*` |
| 6. Интерфейсы к GameClock/MoneyWallet/Delivery/долгам/сюжету | `Core/IPhoneClock.cs`, `Notifications/ExternalAdapters.cs` |
| 6. Телефон не меняет деньги/заказы/флаги | все источники read-only |
| 7. Пустые ID, дубли, неизвестные контакты, повторное прочтение, пустая история, ошибки адаптеров, переполнение, плохое время | коды `PhoneResultCode` |
| 8. ≥ 25 тестов | 79 тестов в `Tests/*` |
| 9. Структура каталогов, README, INTEGRATION | соблюдено |
| 10. Чистый C#, Unity 6000, без MonoBehaviour/UI/NuGet, `Economy/` и `Jobs/` не тронуты | asmdef `noEngineReferences`, `LangVersion 9` |

---

## 7. Допущения и ограничения

1. **Телефон — пассивное хранилище.** Он не отправляет сообщения «в мир», не звонит,
   не делает переводов, не принимает заказы.
2. **Сообщения и уведомления — разные сущности с независимыми ID и счётчиками.**
   Одно и то же событие может породить и сообщение, и уведомление — это решает Codex.
3. **Автоматической синхронизации счётчиков нет**: `AddUnread` вызывается явно
   (например, через `IPhoneNotifier.Push`). Если внешняя система кладёт уведомление
   напрямую в `IPhoneNotificationService`, бейдж приложения не изменится.
4. **`GetByName` при нескольких совпадениях** возвращает `DuplicateId` и первый контакт
   в `Value` — имя не уникально, это осознанно.
5. **`MarkRead` идемпотентен**: повторное прочтение — успех, а не `InvalidState`.
6. **`StartConversation` не сбрасывает существующую переписку**: повтор даёт `DuplicateId`.
7. **Входящее сообщение создаёт переписку автоматически**, исходящее — нет.
8. **ID сообщений детерминированы** (`contactId#N`) и уникальны в пределах телефона.
9. **`Time` не монотонизируется модулем**: если GameClock вернёт меньшее значение,
   телефон просто запишет его; проверка монотонности — задача адаптера.
10. **Отрицательный баланс считается недоступным** (`BalanceAvailable = false`).
11. **Нет потокобезопасности**: предполагается вызов из главного потока Unity.
12. **Нет сохранения/загрузки на диск** — только DTO-снимки.

## 8. Что осталось за рамками ТЗ (осознанно)

* Банковские переводы, интернет-магазин, звонки, социальная сеть.
* Пуш-уведомления ОС, вибрация, звуки — сторона Unity.
* Групповые чаты, вложения, реакции на сообщения.
* Поиск по тексту сообщений, архивация переписок.
* Реальная блокировка приложений по сюжету (есть только флаг `IsAvailable`).

## 9. Что не проверено (главное)

1. **Компиляция не выполнялась** — возможны опечатки и несоответствия версии языка.
2. **Тесты не запускались** — «79 PASS» это расчёт по коду, а не результат прогона.
3. **asmdef-ссылки** (`UnityEngine.TestRunner`, `UnityEditor.TestRunner`,
   `nunit.framework.dll`) не сверялись с реальным проектом.
4. **`noEngineReferences: true`** подойдёт, пока модуль не начнёт использовать Unity API.
5. **Совместимость с C# 9** заявлена по синтаксису; фактическая версия языка проекта
   может быть иной (`LangVersion 9.0` в `Console.csproj`).
