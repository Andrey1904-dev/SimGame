# LIFE FROM ZERO — Полный технический аудит модулей (ТЗ №017)

Дата: 2026-11-04
Репозиторий: `Andrey1904-dev/SimGame`
Ветка: `arena/64fd70a9-simgame`
Base commit: `bba3d99c796602e9415f52697e22d96de5bb1a97`

---

## 1. Проверенные версии модулей

| Модуль | Код | Path | Итог |
|---|---|---|---|
| Economy | `8b00ce1` (ветка `arena/458d270c-simgame`) | `Economy/` | совпадает, без правок |
| Jobs/Delivery | `8b00ce1` (ветка `arena/458d270c-simgame`) | `Jobs/Delivery/` | совпадает, без правок |
| Phone | `8b00ce1` (ветка `arena/458d270c-simgame`) | `Phone/` | совпадает, **4 правки** |
| Debts | `be5db037e5a53787eaa0630d66c449f112a335c6` (ветка `arena/52f76c1e-simgame`) | `Debts/` | совпадает, **3 правки** |

Состояние репозитория перед аудитом:

- `git status` — чистая рабочая ветка, без незакоммиченных изменений.
- `git diff HEAD 8b00ce1 -- Economy Jobs Phone` — пустой ⇒ код модулей совпадает с `8b00ce1`.
- `git diff HEAD be5db03 -- Debts` — пустой ⇒ код модуля долгов совпадает с `be5db03`.
- Клон изначально shallow (только merge-commit `bba3d99`); ветки `arena/458d270c-simgame`,
  `arena/4e9c4a52-simgame`, `arena/52f76c1e-simgame` были fetched из `origin`. Force-push
  не выполнялся, ветки не переключались.

Счётчик проверенного кода: **101 C#-файл, 14 590 строк**
(Economy 15 / 2 084, Jobs 13 / 2 193, Phone 37 / 5 073, Debts 36 / 5 240)
плюс 23 файла документации и сборок (`.md`, `.asmdef`, `.csproj`).

---

## 2. Метод проверки

**Нет C#-компилятора в среде.** `dotnet`, `mono`, `csc`, `msbuild` не установлены;
по ТЗ §8 их не устанавливать и не тратить лимиты на обходные решения.
Поэтому **статический анализ ≠ выполнение тестов**, и это прямо зафиксировано в §10.

Выполнено:

- ручной построчный читабельный просмотр всех публичных контрактов и состояний;
- статический прогон: баланс скобок `{}`/`()`/`[]`, наличие `namespace` в каждом файле,
  пересчёт типов по namespace (дубликатов классов нет), проверка на C#-9-специфичные
  конструкции (`record`, `init`, `required`, range-operator, `switch`-expressions — не используются);
- проверка asmdef (names, references, `noEngineReferences`), csproj (`LangVersion 9.0`),
  Unity-запрещённых API (`UnityEngine.*`/`MonoBehaviour`/`ScriptableObject` — не используются);
- пересчёт тестов из исходников и проверка, что API раннера (`TestSuite.Add`, `AddRange`,
  `MiniTestRunner.IsNull/IsNotNull`) действительно существует;
- пересчёт финансовых инвариантов вручную по ключевым сценариям.

---

## 3. Найденные ошибки

### P0 — потеря денег / дублирование / повреждение сохранения

| ID | Модуль | Ошибка | Влияние |
|---|---|---|---|
| P0-1 | Debts | `debt.LastPenaltyPeriodIndex = newPeriod` продвигался **после** рассылки события `PenaltyApplied`. Обработчик, снимающий `CreateSnapshot()` внутри события, получал снимок, где штрафы начислены, но индекс периода ещё «старый» — при загрузке такой снимок начислял бы штраф за тот же период повторно. | дублирование начислений / денежный казус |
| P0-2 | Debts | «Окно списания» `_inDebitWindow` было bool-флагом. Вложенный `TryPay` (адаптер кошелька, вызывающий платёж из подписки на события) выходил из `try/finally` и сбрасывал флаг **внешней** операции — её журнал незавершённых операций терял защиту от краха между списанием и зачётом. | потенциальная потеря денег при крахе |
| P0-3 | Debts | `RestoreSnapshot` можно вызвать реентрантно из обработчика событий или адаптера кошелька, пока выполняется платеж. Замена состояния оставляла внешнюю операцию работать с неактуальным объектом долга. | нестабильное состояние, возможный дубль |

### P1 — бизнес-логика / интеграция

| ID | Модуль | Ошибка | Влияние |
|---|---|---|---|
| P1-1 | Phone | Счётчик приложения Messages **не обновлялся** при доставке сообщения через `PhoneInbox`. Внешние системы (NPC/сюжет) пишут игроку именно через Inbox, поэтому иконка «Сообщений» не показывала новое сообщение. | ошибка отображения, зафиксированный риск ТЗ §4 |
| P1-2 | Phone | `DebtDto` не нёс стадию долга. Телефон мог отобразить остаток и срок, но **не мог показать** текущий/просрочка/взыскание/закрыт — требование интеграции Phone ↔ Debts невыполнимо. | невыполнимая интеграция |

### P2 — документация / удобство сопровождения

| ID | Модуль | Ошибка |
|---|---|---|
| P2-1 | Phone | Идентификаторы приложений были строковыми литералами в трёх местах (`PhoneAppRegistry.CreateDefaultApps`, `PhoneInbox`, тесты). Добавлена константа `PhoneAppIds.Messages`. |
| P2-2 | Economy/Jobs/Phone/Debts | **Нет C#-компилятора.** Тесты не запущены. |

### Не найдено

- переполнение `long`/`int` в денежной арифметике (`Money`, `DebtMath`, `PenaltyCalculator`) —
  все операции с защитой, включая `TryPercent` и `TryAccrue`;
- отрицательные суммы — `Money` отсекает `FromRubles`, `DebtService` отсекает `amountRubles <= 0`;
- покупка при недостатке средств — `CanApprove` вызывается **до** списания;
- потеря денег, если инвентарь отказал после списания — `Compensate` + журнал
  `_pendingRefunds` + блокировка новых покупок кодом `PendingRefund`;
- двойная выплата курьеру — `PayoutGranted` ставится **после** успешного `TryCredit`;
  неуспешная выплата оставляет заказ в `PickedUp`;
- потеря выполненного заказа при сбое выплаты — заказ не переходит в `Completed` до
  подтверждения начисления;
- дубль платежа по долгу — идемпотенция по `paymentId` + журнал `_pending` + валидатор
  снимка (paymentId одновременно в истории и журнале — отклоняется);
- отрицательный баланс — `Balance < TotalCost` / `balance < amountRubles` отсекаются;
- циклические зависимости — `Economy`, `Jobs.Delivery`, `Phone`, `Debts` попарно
  не зависят; `Phone` зависит только от абстракций (`IPhoneClock`, `IBalanceSource`,
  `IDeliveryJobsSource`, `IDebtsSource`, `IStorySource`), а не от других модулей;
- Unity-API в модульном коде — только в comments;
- дублирующиеся классы в namespace — нет.

---

## 4. Исправления

### Debts/Core/DebtService.cs

**P0-1 — порядок обновления индекса периода штрафа.**
Индекс now продвигается **до** рассылки события `PenaltyApplied`, чтобы снимок,
снятый в обработчике, был согласованным:

```csharp
debt.LastPenaltyPeriodIndex = newPeriod;
if (penaltyAmount > 0L) { debt.PenaltiesRemaining += penaltyAmount; debt.PenaltiesAccrued += penaltyAmount; Fire(...); }
```

**P0-2/P0-3 — счётчик вложенных списаний + запрет реентрантного restore.**

```csharp
private int _debitDepth;
// TryPay: _debitDepth++; try { ... } finally { _debitDepth--; }
// ResolvePending: if (_pending.Count == 0 || _debitDepth > 0) return;
// RestoreSnapshot: if (_debitDepth > 0) return DebtResult.Fail(AdapterError, ...);
```

`_inDebitWindow` заменён на счётчик глубины, так как вложенный платёж теперь
выходит из вложенного `try/finally` и не должен закрыть «окно» внешней операции.

Публичные контракты не изменены. `RestoreSnapshot` при реентрантном вызове
возвращает код `DebtResultCode.AdapterError` вместо того, чтобы менять состояние —
документировано в коде.

### Phone/Messages/PhoneInbox.cs (P1-1)

Добавлен второй ctor `PhoneInbox(IPhoneMessageService, IPhoneAppRegistry)`; при
успешной доставке увеличивается счётчик приложения `messages`. Сбой реестра
не отменяет доставку (бейдж вспомогательный). Старый ctor сохраняется
(`_apps = null`) — сигнатура `IPhoneInbox` не изменилась.

### Phone/Notifications/ExternalAdapters.cs (P1-2)

Добавлен `DebtStage` (значения совпадают с `DebtStatus`) и свойство `Stage` в
`DebtDto`. Старый ctor `DebtDto(id, title, amount, due)` сохранён для совместимости
(`Stage = Current`).

### Phone/Apps/PhoneAppIds.cs (P2-1)

Константы ID приложений. `PhoneAppRegistry.CreateDefaultApps` использует их.

### Телефоны/фасад (P1-1)

`PhoneFacade` теперь создаёт `PhoneInbox(Messages, Apps)`.

---

## 5. Регрессионные тесты

Добавлены **только для найденных ошибок** (5 тестов, без искусственного увеличения):

| Файл | Тест |
|---|---|
| `Debts/Tests/DebtPenaltyTests.cs` | `SnapshotInsidePenaltyEventValid` (P0-1) |
| `Debts/Tests/DebtPenaltyTests.cs` | `RestoreInsideDebitWindowRejected` (P0-2/P0-3) |
| `Debts/Tests/DebtPaymentSafetyTests.cs` | `NestedDebitWindowKeepsBothOperations` (P0-2) |
| `Phone/Tests/PhoneIntegrationTests.cs` | `InboxUpdatesMessagesBadge` (P1-1) |
| `Phone/Tests/PhoneIntegrationTests.cs` | `DebtDtoCarriesStage` (P1-2) |

---

## 6. Предыдущие три проблемы — повторная проверка

### 1. Начисленные штрафы не должны теряться во время списания — **соблюдается**
`ApplyConfirmedPayment` пересчитывает распределение по **актуальному** состоянию долга
(не по значению, снятому до адаптера); есть тест `ReentrantPenaltiesNotLost` (29-й
в DebtPaymentSafetyTests). Также P0-1 исправлен.

### 2. Сохранение внутри DebtClosed не должно создавать противоречивый snapshot — **соблюдается**
Журнал `_pending` освобождается **до** рассылки `CollectionStageChanged` и `DebtClosed`.
Тест `SnapshotInsideClosedEventValid` (в DebtPaymentSafetyTests).

### 3. Вложенное восстановление не должно отменять платёж, который ещё выполняется — **соблюдается + улучшено**
`_debitDepth` блокирует `ResolvePending` и `RestoreSnapshot` внутри вложенных
списаний. Тесты `ReentrantRecoveryKeepsJournal`, `NestedDebitWindowKeepsBothOperations`,
`RestoreInsideDebitWindowRejected`.

---

## 7. Сквозной аудит интеграции

| Совместимость | Результат |
|---|---|
| Economy ↔ Delivery | `IPayoutAdapter` → кошелёк. Повторная доставка не создаёт повторную выплату (`PayoutGranted`). Ошибка начисления не уничтожает информацию (заказ остаётся `PickedUp`). |
| Economy ↔ Debts | Списание по долгу однократно (идемпотенция по `paymentId`). ID операций не пересекаются (префикс paymentId, один Журнал). Баланс не становится отрицательным. Возврат магазина и платёж по долгу — отдельные публичные контракты (`IWalletAdapter.TryRefund` vs `IDebtWalletAdapter.TryDebit/TryRefund`), один и тот же кошелёк должен реализовывать идемпотентность по paymentId — это договор с аддаптером, документирован в `Debts/Payments/IDebtWalletAdapter.cs`. |
| Phone ↔ Delivery | `IDeliveryJobsSource` (только чтение). Телефон отображает активный заказ. Нет прямых зависимостей от Unity. |
| Phone ↔ Debts | `IDebtsSource` (только чтение). **Добавлена стадия долга** (P1-2). События долгов могут быть преобразованы в уведомления через `IDebtEventSink` → `DebtEventHub` → `PhoneNotificationHub`. Повторное событие не создаёт дубликатов (дедупликация по ID в `PhoneNotificationService.Add`). |
| Пространства имён | `LifeFromZero.Economy.*`, `LifeFromZero.Jobs.Delivery`, `LifeFromZero.Phone.*`, `LifeFromZero.Debts.*` — не пересекаются. |
| asmdef | 4 прод asmdef с `noEngineReferences: true`, `references: []`. 4 test asmdef с `UNITY_INCLUDE_TESTS`. |
| C# 9 / Unity 6000.3.27f1 | Код использует только C# ≤ 9; нет engine-API; `IReadOnlyList`/`List.AsReadOnly` достаточны для Unity 6000. |

**Фальшивых Unity-адаптеров не создавалось.** Существующие тесты-факсы
(`Economy/Tests/Fakes`, `Jobs/Tests/Fakes`, `Phone/Tests/Fakes`, `Debts/Tests/Fakes`)
явно обозначены как NOT Unity адаптера и являются управляемыми значениями, а не
имитациями интеграции.

---

## 8. Аудит сохранений

| Модуль | Сохранение | Восстановление | Результат |
|---|---|---|---|
| Economy | **Нет.** `ShopService` хранит только `List<Money> _pendingRefunds` в памяти. | **Нет.** | **Ограничение:** модуль не поддерживает сохранение. Незавершённая покупка (деньги списаны, инвентарь отказал, возврат не удался) сохраняется **только в памяти** и теряется после перезапуска. ПКМ: так как `_pendingRefunds` блокирует новые покупки, игрок после перезапуска может просто купить снова — но предыдущая списанная сумма не вернётся. Это **не критично, но требует решения**. |
| Jobs/Delivery | **Нет.** `DeliveryService` — только Dictionary в памяти. | **Нет.** | **Ограничение:** активный заказ (Accepted/PickedUp) теряется после перезапуска. Заказ с выплатой не может дублироваться, но пользователь теряется возможность завершить заказ. |
| Phone | **Snapshot есть** (`PhoneMessagesSnapshot`, `PhoneNotificationsSnapshot`). | **Да** (`PhoneMessagesRestore.RestoreInto`, `PhoneNotificationsRestore.RestoreInto`), с валидацией и атомарной заменой. | Реализовано, но без внедрения в Unity (JsonUtility/System.Text.Json — задача Codex). Snapshot защищён от внешней мутации (defensive copy). |
| Debts | **Snapshot есть** (`DebtSnapshot`, schema v1). | **Да** (`DebtService.RestoreSnapshot`), с полной валидацией и атомарной заменой. | Реализовано. Валидатор проверяет уникальность ID, неотрицательность сумм, финансовые инварианты, согласованность истории платежей и журнала. Повреждённый снимок не меняет рабочее состояние. |

**Нет глобальной системы сохранений.** Сохранения есть **только в Phone и Debts**,
и они не объединены в единый snapshot. Счётчик восстановления модулей:
Phone → Debts → Economy → Delivery (Debts не зависит от Phone; Economy/Delivery не
имеют сохранения).

Незавершённые операции по снимку: Debts (DebitPending / DebitConfirmed / RefundPending)
— есть и проверены. Economy и Delivery — **нет** (ограничение).

---

## 9. Тесты и проверки

### Пересчёт тестов из исходников

| Модуль | Кадры | Тестовые кейсы (`suite.Add`) |
|---|---|---|
| Economy | 15 | **33** |
| Jobs/Delivery | 13 | **40** |
| Phone | 37 | **92** (90 + 2 новых) |
| Debts | 36 | **88** (85 + 3 новых) |
| **Всего** | **101** | **253** |

(Ориентир ТЗ §8: Economy 33, Delivery 40, Phone 90, Debts 85 = 248.
Phone и Debts совпали до правок; к итоговой цифре добавлено 5 регрессий.)

Плюс 4 «обёрточные» NUnit-теста (`EconomyNUnitTests`, `JobsNUnitTests`,
`PhoneNUnitTests`, `DebtsNUnitTests`) — каждый `[Test]` прогоняет весь TestSuite
модуля через `RunAll()` и `Assert.Fail` при провальных кейсах. Итого в Unity
Test Runner зарегистрировано **4 теста**, раскрывающие **253 кейса**.

### Реально запущено тестов

**0.** Нет C#-компилятора. `dotnet`, `mono`, `csc`, `msbuild` не установлены;
по ТЗ §8 их не устанавливать. **Статический анализ не равен выполнению тестов.**

---

## 10. Что исправлено и что осталось

### Исправлено (P0 + P1 без изменения публичных контрактов)

- P0-1, P0-2, P0-3 (Debts/Core/DebtService.cs)
- P1-1 (PhoneInbox + PhoneFacade)
- P1-2 (DebtDto + DebtStage)
- P2-1 (PhoneAppIds)

### Не исправлено

- **P2-2:** отсутствие компилятора ⇒ тесты не запущены.
- **Ограничение Economy:** нет сохранения незавершённой покупки (потеря возврата
  после перезапуска — см. §8).
- **Ограничение Jobs/Delivery:** нет сохранения активного заказа.
- **Нет глобальной системы сохранений** — только Phone и Debts.
- **Договор с аддаптером кошелька:** `IWalletAdapter` и `IDebtWalletAdapter` —
  разные интерфейсы. Если один кошелёк реализует оба, он обязан сохранять
  идемпотентность по paymentId (документировано в `IDebtWalletAdapter.cs`).

---

## 11. Заключение

Модули **Economy, Jobs/Delivery, Phone и Debts** действительно проверены
(101 файл, 14 590 строк). Найдены и исправлены 3 P0 и 2 P1. Тесты не запущены
из-за отсутствия компилятора — это ограничение, а не отсутствие тестов.

**Можно переходить к Unity-интеграции**, соблюдая:
1. Реализовать Unity-адаптеры (`IWalletAdapter`, `IInventoryAdapter`, `IPayoutAdapter`,
   `IGameClockAdapter`, `ILocationProvider`, `IPhoneClock`, `IDebtClockAdapter`,
   `IDebtWalletAdapter`, `IBalanceSource`, `IDeliveryJobsSource`, `IDebtsSource`,
   `IStorySource`, `IDebtEventSink`).
2. Добавить сохранения для Economy и Delivery или документировать ограничения.
3. Провести компиляцию и EditMode-тесты в Unity 6000.3.27f1.

See `UNITY_INTEGRATION_PLAN.md`.
