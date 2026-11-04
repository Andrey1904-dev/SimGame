# Интеграция модуля долгов — инструкция для Codex (ТЗ №016 / №016.2)

> **Честно о проверке.** В среде разработки модуля не было Unity и .NET SDK,
> установка компилятора запрещена условиями ТЗ. Код **ни разу не компилировался**,
> тесты **не запускались**. Выполнен только статический анализ (см. §9).
> Ниже перечислено, что именно нужно проверить при первой сборке.

---

## 1. Подключение к Unity

Скопируйте папку `Debts/` в `Assets/`, сохранив относительную структуру.

| Путь в репозитории | Путь в Unity | Назначение |
|---|---|---|
| `Debts/Debts.asmdef` | `Assets/Debts/Debts.asmdef` | сборка `LifeFromZero.Debts`, `noEngineReferences: true` |
| `Debts/Core/*.cs` | `Assets/Debts/Core/` | ядро модуля |
| `Debts/Payments/*.cs` | `Assets/Debts/Payments/` | контракты платежей |
| `Debts/Penalties/*.cs` | `Assets/Debts/Penalties/` | штрафы и пороги стадий |
| `Debts/Events/*.cs` | `Assets/Debts/Events/` | события |
| `Debts/Persistence/*.cs` | `Assets/Debts/Persistence/` | снимки и валидация |
| `Debts/Tests/Debts.Tests.asmdef` + `*.cs` | `Assets/Debts/Tests/` | тестовая сборка (Editor) |
| `Debts/Tests/Program.cs` | **не переносить** | `Main` для запуска вне Unity |
| `Debts/Tests/Console.csproj` | **не переносить** | проект `dotnet run` |

Сборка модуля не ссылается ни на `UnityEngine`, ни на `Economy`, `Phone`, `Jobs`.
Тестовая сборка ссылается только на `LifeFromZero.Debts` и Test Runner, включается
по `UNITY_INCLUDE_TESTS`. Если Unity Test Framework не подключён, удалите
`DebtsNUnitTests.cs` — остальные тесты от NUnit не зависят.

**Файлы, которые пишет Codex (модуль их намеренно не содержит):**

* `Assets/Debts/Integration/MoneyWalletDebtAdapter.cs` — `IDebtWalletAdapter` поверх `MoneyWallet`.
* `Assets/Debts/Integration/GameClockDebtAdapter.cs` — `IDebtClockAdapter` поверх `GameClock`.
* `Assets/Debts/Integration/DebtsSourceAdapter.cs` — `IDebtsSource` для телефона.
* `Assets/Debts/Integration/DebtEventBridge.cs` — мост событий в телефон/сюжет.

---

## 2. Адаптер кошелька — критический контракт

```csharp
public interface IDebtWalletAdapter
{
    bool TryGetBalance(out long rubles);
    bool TryDebit(string paymentId, long rubles);
    bool TryRefund(string paymentId, long rubles);
    bool TryWasDebited(string paymentId, out bool debited);
}
```

Модуль **не хранит баланс** и не содержит своего кошелька. Адаптер обязан выполнить
пять условий — от них напрямую зависит, могут ли потеряться или удвоиться деньги:

1. **`TryDebit` идемпотентен по `paymentId`.** Повторный вызов с тем же `paymentId`
   после успешного списания возвращает `true` и **не списывает** деньги снова.
2. **`TryDebit` атомарен.** Либо списана вся сумма, либо ничего. При `false` баланс
   не изменён.
3. **`TryRefund` идемпотентен по `paymentId`.** Возвращает списанное ровно один раз;
   повторный вызов — `true` без повторного начисления. Возврат несписанных денег
   должен отклоняться (`false`), иначе появится «деньги из воздуха».
4. **`TryWasDebited` отвечает по долговременному журналу.** Журнал списаний по
   `paymentId` должен переживать перезапуск игры (сохраняться вместе с сейвом).
   Это единственный способ безопасно разрешить «зависший» платёж после краха.
   Возврат `false` означает «журнал недоступен, исход неизвестен».
5. **Исключения — не штатный путь.** Если адаптер всё же бросил исключение, сервис
   считает исход неизвестным и разрешает его через `TryWasDebited`.

### Почему нужен именно журнал, а не только идемпотентность

Между успешным списанием и зачётом в долг есть окно. Если игра упала в этом окне,
после загрузки сервис видит запись `DebitPending` и должен узнать, ушли ли деньги.
Без долговременного журнала адаптера ответить на это невозможно: повторное списание
удвоило бы платёж, а слепой возврат создал бы деньги из воздуха.

**Минимальная реализация журнала:** словарь `paymentId → сумма` в сохранении кошелька
плюс отметка о возврате. Если у существующего `MoneyWallet` такого журнала нет, его
нужно добавить на стороне адаптера — это обязательное условие интеграции.

### Пример каркаса адаптера

```csharp
public sealed class MoneyWalletDebtAdapter : IDebtWalletAdapter
{
    private readonly MoneyWallet _wallet;          // существующая система
    private readonly IDebitJournal _journal;       // долговременный журнал (пишет Codex)

    public bool TryGetBalance(out long rubles)
    {
        try { rubles = _wallet.BalanceRubles; return rubles >= 0; }
        catch { rubles = 0; return false; }
    }

    public bool TryDebit(string paymentId, long rubles)
    {
        if (_journal.WasDebited(paymentId)) return true;      // идемпотентность
        if (!_wallet.TrySpend(rubles)) return false;          // атомарное списание
        _journal.Record(paymentId, rubles);                   // фиксируем ДО возврата
        return true;
    }

    public bool TryRefund(string paymentId, long rubles)
    {
        if (!_journal.WasDebited(paymentId)) return false;    // нечего возвращать
        if (_journal.WasRefunded(paymentId)) return true;     // идемпотентность
        if (!_wallet.TryCredit(rubles)) return false;
        _journal.MarkRefunded(paymentId);
        return true;
    }

    public bool TryWasDebited(string paymentId, out bool debited)
        => _journal.TryQuery(paymentId, out debited);
}
```

Имена методов `MoneyWallet` здесь условны — реальный маппинг делает Codex.

---

## 3. Адаптер игрового времени

```csharp
public interface IDebtClockAdapter { long NowMinutes { get; } }
```

Целые минуты с начала игры, монотонно не убывают, неотрицательны. Целый тип выбран
специально: границы периодов и скачки времени проверяются точно, без ошибок
округления.

```csharp
public sealed class GameClockDebtAdapter : IDebtClockAdapter
{
    private readonly GameClock _clock;
    public long NowMinutes => (long)Math.Max(0d, Math.Floor(_clock.TotalMinutes));
}
```

Если существующие часы отдают `double`, приведение делайте **в адаптере**
(`Floor`, не округление вверх). Отрицательное значение сервис считает сбоем
(`AdapterError`). Уменьшение времени между вызовами `UpdateTime()` тоже даёт
`AdapterError` — состояние при этом не меняется.

`UpdateTime()` нужно вызывать при продвижении игрового времени (например, раз в
игровую минуту или после сна/ускорения). Вызов в тот же момент идемпотентен.

---

## 4. Источник данных для телефона

Модуль **не ссылается** на `Phone/`. Телефон читает долги через `IDebtReadService`:

```csharp
public interface IDebtReadService
{
    IReadOnlyList<DebtView> GetDebts();
    bool TryGetDebt(string debtId, out DebtView debt);
    IReadOnlyList<DebtPaymentRecord> GetPaymentHistory(string debtId);
    bool TryGetTotalRemainingRubles(out long rubles);
}
```

`DebtService` реализует этот контракт. `DebtView` — неизменяемая защитная копия:
телефон не может испортить долг через неё.

В `Phone/` уже есть готовый `IDebtsSource.TryGetDebts(out DebtDto[])` и
`BankSummaryService`, который его читает. Нужен только адаптер — **существующий
Phone менять не требуется**:

```csharp
public sealed class DebtsSourceAdapter : IDebtsSource   // интерфейс из Phone
{
    private readonly IDebtReadService _debts;

    public bool TryGetDebts(out DebtDto[] debts)
    {
        try
        {
            var views = _debts.GetDebts();
            var list = new List<DebtDto>();
            foreach (var v in views)
            {
                if (v.Status == DebtStatus.Closed) continue;   // закрытые не показываем
                list.Add(new DebtDto(v.Id, v.Title, v.TotalRemaining, v.DueAtMinutes));
            }
            debts = list.ToArray();
            return true;
        }
        catch { debts = new DebtDto[0]; return false; }
    }
}
```

`DebtDto.DueAtMinutes` в Phone — `double`, `-1` означает «срок не задан». В модуле
долгов срок задаётся всегда, поэтому передавайте `(double)v.DueAtMinutes`.

---

## 5. Подключение событий к телефону и сюжету

```csharp
var hub = new DebtEventHub();
var service = new DebtService(clockAdapter, walletAdapter, hub);

hub.Subscribe(e =>
{
    switch (e.Type)
    {
        case DebtEventType.PaymentDue:
        case DebtEventType.DebtOverdue:
        case DebtEventType.PenaltyApplied:
        case DebtEventType.CollectionStageChanged:
            notifier.Push(PhoneNotificationType.Debt, e.DebtId, e.AtMinutes);
            break;
        case DebtEventType.DebtClosed:
            storyFlags.Set("debt_closed:" + e.DebtId);
            break;
    }
});
```

События отправляются **после** фиксации состояния. Исключение обработчика
перехватывается: подтверждённый платёж не отменяется, долг не повреждается.
`DebtEventHub` изолирует подписчиков друг от друга.

Одноразовые события (`PaymentDue`, `DebtOverdue`, `DebtClosed`) не дублируются при
повторном `UpdateTime()` и не переигрываются после загрузки снимка.

Модуль только сообщает о стадии взыскания. Реакцию (звонки, письма, сюжетные
сцены) реализует внешняя система — NPC-коллекторов, суда и угроз в модуле нет.

---

## 6. Сохранение и восстановление

```csharp
DebtSnapshot snapshot = service.CreateSnapshot();
string json = JsonUtility.ToJson(snapshot);       // формат выбирает Codex

// при загрузке:
DebtSnapshot loaded = JsonUtility.FromJson<DebtSnapshot>(json);
DebtResult result = service.RestoreSnapshot(loaded);
if (!result.IsSuccess) { /* снимок повреждён: состояние НЕ изменено */ }

// после загрузки кошелька и его журнала:
service.RetryPendingRecovery();
```

Снимок — плоские массивы DTO с публичными полями и `[Serializable]`, без словарей
и интерфейсов, поэтому `JsonUtility` его сериализует. Модуль сам на диск не пишет.

**Порядок загрузки важен:**

1. Восстановить кошелёк **и его журнал списаний**.
2. `RestoreSnapshot(snapshot)`.
3. `RetryPendingRecovery()` — довести или компенсировать незавершённые платежи.
4. `UpdateTime()`.

Если вызвать `RetryPendingRecovery()` до восстановления журнала кошелька, сервис
не сможет определить исход зависшего платежа и оставит операцию ждать (новые
платежи будут заблокированы кодом `PendingRecovery`). Это безопасно, но требует
повторного вызова после готовности журнала.

Сохраняются: долги и кредиторы, основной долг, штрафы (остаток и начислено за всё
время), сроки, стадии, история платежей с их ID, последние периоды начисления
штрафов, флаги одноразовых событий, последнее обработанное время и журнал
незавершённых операций.

Валидация выполняется целиком до применения. Проверяются версия схемы, ссылки,
уникальность ID, неотрицательность сумм, финансовые инварианты и согласованность
истории платежей с остатками. Повреждённый снимок даёт `InvalidSnapshot` и не
меняет рабочее состояние.

---

## 7. Ограничения атомарности платежей

Полная распределённая атомарность между кошельком и модулем долгов **не
гарантируется** — это два независимых хранилища. Модуль даёт следующие гарантии
**при соблюдении контракта адаптера из §2**:

| Ситуация | Поведение | Гарантия |
|---|---|---|
| `TryDebit` вернул `false`, списания не было | журнал снят, `WalletFailure` | деньги и долг не изменены |
| `TryDebit` бросил исключение до списания | `TryWasDebited` → не списано → журнал снят | деньги и долг не изменены |
| `TryDebit` списал и бросил исключение | `TryWasDebited` → списано → зачёт | деньги списаны один раз, долг уменьшен |
| `TryDebit` списал, но вернул `false` | `TryWasDebited` → списано → зачёт | деньги не потеряны |
| Журнал адаптера недоступен | операция остаётся `DebitPending`, новые платежи блокируются | повторного списания нет |
| Крах между списанием и зачётом | после загрузки `RetryPendingRecovery()` доводит платёж | ровно один зачёт |
| Зачёт невозможен (долг изменился) | `RefundPending` → `TryRefund` | деньги возвращены |
| Возврат не удался | запись остаётся `RefundPending`, повторяется | деньги не «сгорают» |

**Чего модуль гарантировать не может:**

* Если адаптер **нарушает контракт** — например, `TryDebit` возвращает `true`, не
  списав деньги, — модуль уменьшит долг без списания. Успешному `true` модуль
  доверяет (перепроверка каждого успешного списания удвоила бы число обращений к
  кошельку). Достоверность `true` — ответственность адаптера.
* Если журнал адаптера **не сохраняется** между запусками, зависший после краха
  платёж разрешить невозможно: операция останется ждать неограниченно долго и
  заблокирует новые платежи. Это сознательный выбор в пользу безопасности денег.
* Пока журнал непуст, **все** новые платежи блокируются (`PendingRecovery`), а не
  только по проблемному долгу. Это предотвращает расход денег, которые могут быть
  возвращены компенсацией.
* Вызов `CreateSnapshot()` внутри обработчика события безопасен (журнал
  освобождается до рассылки), но снимать снимок в момент, когда кошелёк ещё не
  зафиксировал своё состояние, не следует: снимки двух систем должны быть
  согласованы на стороне загрузчика.

**Реентрантность.** Адаптер кошелька и обработчик событий — внешний код, который
может вызвать сервис повторно. Модуль защищён: распределение платежа считается по
актуальному состоянию в момент зачёта, вложенное восстановление не трогает
операцию «в полёте», журнал освобождается до рассылки событий. Тем не менее
адаптерам не рекомендуется вызывать `TryPay` из `TryDebit`.

---

## 8. Минимальные проверки после импорта

1. **Компиляция.** Открыть проект в Unity 6000.3.27f1, убедиться, что сборки
   `LifeFromZero.Debts` и `LifeFromZero.Debts.Tests` собираются без ошибок и
   предупреждений. Это первая реальная компиляция модуля.
2. **Прогон тестов.** `Window → General → Test Runner → EditMode` → запустить
   `RunAllDebtsTests`. Ожидается 85 успешных кейсов. Вне Unity:
   `dotnet run --project Debts/Tests/Console.csproj`.
3. **Контракт кошелька.** Проверить на реальном `MoneyWallet`: идемпотентность
   `TryDebit`/`TryRefund` по `paymentId`, атомарность, сохранение журнала между
   запусками, отказ возврата несписанных денег.
4. **Смонтированный сценарий.** Стартовый долг 38 400 ₽, наличные 1 270 ₽:
   частичный платёж проходит, платёж больше баланса отклоняется
   (`InsufficientFunds`), переплата отклоняется (`Overpayment`).
5. **Просрочка.** Продвинуть игровое время за срок + льготный период: приходят
   `PaymentDue`, `DebtOverdue`, `PenaltyApplied`, стадия повышается. Повторный
   `UpdateTime()` в тот же момент ничего не начисляет.
6. **Сейв/лоад.** Сохранить, перезапустить игру, загрузить: остатки, стадии,
   история и защита от повторного `paymentId` сохранились; одноразовые события не
   переигрались.
7. **Сбой питания.** Смоделировать крах между списанием и зачётом (например,
   исключение в адаптере после списания), перезапустить, вызвать
   `RetryPendingRecovery()` — платёж должен зачесться ровно один раз.
8. **Телефон.** Убедиться, что `BankSummaryService` получает долги через адаптер и
   существующий `Phone/` не изменялся.

---

## 9. Что реально проверено в среде разработки

Компилятора нет (`dotnet`, `csc`, `mcs`, `mono` отсутствуют), установка запрещена
ТЗ. Выполнены **статические** проверки:

* синтаксический разбор всех 36 C#-файлов парсером tree-sitter C# — 0 ошибок;
* соответствие namespace структуре папок, единый корень `LifeFromZero.Debts`;
* совместимость с C# 9 (нет file-scoped namespaces, `required`, `init`,
  `global using`, record-типов);
* независимость: нет обращений к `UnityEngine`, `UnityEditor`, `LifeFromZero.Economy`,
  `LifeFromZero.Phone`, `LifeFromZero.Jobs`;
* отсутствие `float`/`double` в денежном коде модуля;
* корректность обоих `asmdef` (JSON, имена сборок, ссылки, `defineConstraints`);
* разрешение типов и `using`-директив — все типы объявлены в модуле или BCL;
* перекрёстная проверка обращений к членам типов и арности вызовов;
* регистрация 85 тестов, отсутствие дублей имён, наличие тел всех тест-методов.

**Статический анализ не является компиляцией.** Утверждения «код собирается» и
«тесты проходят» здесь не делаются — это проверяется пунктами §8.1–8.2.
