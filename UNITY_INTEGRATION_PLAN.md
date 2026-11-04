# UNITY_INTEGRATION_PLAN — LIFE FROM ZERO

План подключения модулей Economy, Jobs/Delivery, Phone и Debts в Unity 6000.3.27f1.

See `FULL_AUDIT_REPORT.md` для полного списка найденных ошибок и ограничений.

---

## 1. Порядок подключения

Рекомендуется подключать в этом порядке (зависимости только абстракции, но
загрузка/сохранение должна быть огорожена):

1. **Economy** — никакие другие модули не зависят от него.
2. **Jobs/Delivery** — не зависит от Economy (только `IPayoutAdapter` — аддаптер
   кошелька, который потом будет подключен к Economy).
3. **Debts** — зависит только от `IDebtClockAdapter`, `IDebtWalletAdapter`,
   `IDebtEventSink`.
4. **Phone** — последним, так как он читает данные из Delivery и Debts через
   `IDeliveryJobsSource` / `IDebtsSource`, и конвертирует события долгов в
   уведомления через `DebtEventHub` → `PhoneNotificationHub`.

asmdef-сборки уже имеют правильные `references: []` (модули не ссылаются на друг
друга в коде). Для Phone, если нужны прямые вызовы из Delivery/Debts, добавлять
`"LifeFromZero.Jobs.Delivery"` / `"LifeFromZero.Debts"` в Phone.asmdef, но
**рекомендуется не добавлять** — держать Phone только через DTO-адаптеры.

---

## 2. Необходимые адаптеры (Codex)

**Фальшивых Unity-адаптеров не создаётся.** Ниже — список интерфейсов, которые
Codex обязан реализовать над существующими системами игры.

### 2.1 Economy

| Интерфейс | Нужно | Meсто в Unity |
|---|---|---|
| `IWalletAdapter` | Да | MoneyWallet → `Balance`, `TrySpend`, `TryRefund` |
| `IInventoryAdapter` | Да | FoodInventory → `CanApprove`, `TryAdd` |

### 2.2 Jobs/Delivery

| Интерфейс | Нужно | Место в Unity |
|---|---|---|
| `IGameClockAdapter` | Да | GameClock → `NowMinutes` (double) |
| `IPayoutAdapter` | Да | MoneyWallet → `TryCredit(long)` |
| `ILocationProvider` | Да | Location/PlayerPosition → `CurrentLocationId` |

### 2.3 Debts

| Интерфейс | Нужно | Место в Unity |
|---|---|---|
| `IDebtClockAdapter` | Да | GameClock → `NowMinutes` (long) |
| `IDebtWalletAdapter` | Да | MoneyWallet → `TryGetBalance`, `TryDebit(paymentId, rubles)`, `TryRefund(paymentId, rubles)`, `TryWasDebited(paymentId, out bool)` |
| `IDebtEventSink` | Да | Делегирующий sink → `DebtEventHub` (или напрямую в Phone) |

**КРИТИЧЕСКИЙ договор адаптера кошелька:**

- `TryDebit` идемпотентна по `paymentId` (должна хранить журнал списаний,
  который переживает перезапуск).
- `TryRefund` идемпотентна по `paymentId`.
- `TryWasDebited` отвечает по **долговременному** журналу адаптера — это единственный
  способ безопасно восстановить «зависший» платёж после сбоя.
- `TryDebit` атомарна: либо всё, либо ничего.
- Не бросает исключений в штатных ситуациях; если бросает — сервис считает исход
  неизвестным и восстанавливает через `TryWasDebited`.

Если один кошелёк реализует и `IWalletAdapter` и `IDebtWalletAdapter`, то он обязан
разделять журнал по paymentId (на том же storage), чтобы не было пересечения ID.

### 2.4 Phone

| Интерфейс | Нужно | Место в Unity |
|---|---|---|
| `IPhoneClock` | Да | GameClock → `NowMinutes` (double) |
| `IBalanceSource` | Да | MoneyWallet → `TryGetBalanceRubles(out long)` |
| `IDeliveryJobsSource` | Да | DeliveryService → `TryGetOrders(out DeliveryJobDto[])`, `TryGetActiveOrderId(out string)` |
| `IDebtsSource` | Да | DebtService → `TryGetDebts(out DebtDto[])` — перевод `DebtView` → `DebtDto`, включая **`Stage`** (P1-2) |
| `IStorySource` | Опционально | Сюжетный журнал → `TryGetEntries(out StoryEntryDto[])` |
| `IDebtEventSink` | Да (через Debts) | `DebtEventHub` → `PhoneNotificationHub.Push` (тип уведомления `PhoneNotificationType.Debt`/`Bank`) |

**Phone не должен зависеть от Unity API.** Все 4 модуля — `noEngineReferences: true`.

---

## 3. Совместимость с Unity 6000.3.27f1

- **asmdef:** 4 прод asmdef (`LifeFromZero.Economy`, `LifeFromZero.Jobs.Delivery`,
  `LifeFromZero.Phone`, `LifeFromZero.Debts`) с `noEngineReferences: true`,
  `references: []`, `autoReferenced: true`.
- **Test asmdef:** `*.Tests` с `UNITY_INCLUDE_TESTS`, `overrideReferences: true`,
  `precompiledReferences: ["nunit.framework.dll"]`, Editor-only.
- **C# 9:** код использует только C# ≤ 9. `LangVersion 9.0` в csproj.
- **Нет engine API:** `UnityEngine.*`, `MonoBehaviour`, `ScriptableObject`, `JsonUtility`
  в модульном коде не используются (только в comments).
- **Nullable:** `Nullable=disable` в csproj. В модуле вручную проверены null —
  публичные API при null-адаптерах бросают `ArgumentNullException`/
  `ShopServiceException`/`DeliveryServiceException`/`PhoneIntegrationException`.

**Для .NET Standard / IL2CPP:** модульный код использует только
`System`, `System.Collections.Generic` — compatible.

---

## 4. Требования к сохранениям

| Модуль | Требование |
|---|---|
| **Economy** | **Нет сохранения.** Незавершённая покупка (деньги списаны, инвентарь отказал, возврат не удался) теряется при перезапуске. **Ограничение.** Решение: либо добавить snapshot `ShopService` (суммы `_pendingRefunds`), либо документировать, что игрок просто купит снова. |
| **Jobs/Delivery** | **Нет сохранения.** Активный заказ (Accepted/PickedUp) теряется. **Ограничение.** Решение: добавить snapshot состояния заказа. |
| **Phone** | **Snapshot есть** (`PhoneMessagesSnapshot`, `PhoneNotificationsSnapshot`). Нужно выбрать сериализатор (JsonUtility / System.Text.Json) и внедрить в GameSaveManager. |
| **Debts** | **Snapshot есть** (`DebtSnapshot`, schema v1). Нужно выбрать сериализатор и внедрить в GameSaveManager. |

**Нет глобальной системы сохранений.** Snapshotы Phone и Debts не объединены.
**Очерёдность восстановления:** Phone → Debts → Economy → Delivery.

**Незавершённые операции по снимку:**
- Debts: `DebitPending` / `DebitConfirmed` / `RefundPending` — есть и проверены.
- Economy / Delivery: **нет**.

---

## 5. Чек-лист компиляции

1. Установить Unity 6000.3.27f1.
2. Открыть проект.
3. Добавить asmdef в `Assets/` (или symlink существующие) — модульные asmdef уже
   есть в repo.
4. Собрать EditMode test asmdef:
   ```
   Window → General → Test Runner → EditMode
   ```
5. Проверить компиляцию: `Window → Analysis → Compile Errors` — должно быть 0 ошибок.
6. Запустить EditMode-тесты (4 NUnit-обертки, раскрывающие 253 кейса):
   ```
   Window → General → Test Runner → EditMode → Run All
   ```
7. Проверить все EditMode-тесты PASS.
8. Проверить PlayMode-тесты (если есть).

### Possible компиляционные проблемы

- **Phone.Messages/PhoneInbox.cs** — `using LifeFromZero.Phone.Apps;` для
  `IPhoneAppRegistry` и `PhoneAppIds`.
- **Phone.Notifications/ExternalAdapters.cs** — `DebtStage` enum.
- **Phone.Integrations/PhoneFacade.cs** — `PhoneInbox(Messages, Apps)`.
- **Debts/Core/DebtService.cs** — `_debitDepth` counter (replaces `_inDebitWindow`).

---

## 6. Минимальные задания для Codex

### Задание 1 — Компиляция

```
1. Скопировать Economy/, Jobs/, Phone/, Debts/ в Assets/
2. Проверить 4 asmdef: references [], noEngineReferences true
3. Window → General → Test Runner → EditMode → Run All
4. Все 4 теста PASS, 0 errors
```

### Задание 2 — Unity-адаптеры

```
1. Implement IWalletAdapter, IInventoryAdapter (Economy)
2. Implement IGameClockAdapter, IPayoutAdapter, ILocationProvider (Delivery)
3. Implement IDebtClockAdapter, IDebtWalletAdapter, IDebtEventSink (Debts)
4. Implement IPhoneClock, IBalanceSource, IDeliveryJobsSource, IDebtsSource, IStorySource (Phone)
5. Реализовать долговременно persisted journal для TryDebit/TryRefund/TryWasDebited
```

### Задание 3 — Сохранения

```
1. Serializize Phone snapshots (JsonUtility или System.Text.Json) → GameSaveManager
2. Serializize Debts snapshot (JsonUtility) → GameSaveManager
3. Add snapshots for Economy and Delivery (одна из двух: либо implementation, либо документированное ограничение)
4. Ordering: Phone → Debts → Economy → Delivery
5. Verify round-trip: snapshot → restore → UI читает корректно
```

### Задание 4 — Интеграция

```
1. Subclass/Composition: DebtEventHub ← DebtEventSink → PhoneNotificationHub.Push
2. Phone facade: PhoneFacade(apps, messages, contacts, notifications)
3. Connect IPhoneInbox (PhoneFacade.Inbox) — внешние системы пишут через Inbox
4. Phone AppRegistry: CreateDefault() → 5 приложений
```

---

## 7. Что нужно исправить до интеграции

- **P0-1/P0-2/P0-3** — исправлено (DebtService.cs).
- **P1-1** — исправлено (PhoneInbox).
- **P1-2** — исправлено (DebtDto.Stage).
- **P2-1** — исправлено (PhoneAppIds).
- **P2-2** — нет компилятора ⇒ тесты не запущены. **Нужно провести компиляцию
  и EditMode-тесты в Unity** — это единственный способ подтвердить готовность.
