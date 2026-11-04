# Карта подключения

Все Unity-компоненты ниже: `NEEDS_UNITY_INSPECTION`. Нельзя предполагать сигнатуры или атомарность.
В `UnityIntegrationKit/` лежат готовые мосты: `PhoneSources.cs` (read-only, Phone) и
`AdapterBridges.cs` (финансовые: MoneyWallet, FoodInventory, выплаты, платежи, покупки).
Мосты не зависят от Unity и скомпилированы вместе с модулями; Codex только пробрасывает
минимальные методы (блоки `// CODEX:`). Идентификаторы операций — `FINANCIAL_OPERATION_IDS.md`.

| Связь / исходник | Контракт и готовый адаптер | Действие / проверка |
|---|---|---|
| MoneyWallet ↔ Economy `ShopService` | `IWalletAdapter` + `IIdempotentWalletAdapter` (`Economy/Core/`); мост — `WalletBridge` / `JournalWalletBridge` | Реализовать `Balance`, `TrySpend(long)`, `TryCredit(long)`, и (Режим B) `TrySpend(operationId, rubles)` + `TryGetOperation(...)`. Готовые ID: `shop:<purchaseId>`, `shop-refund:<purchaseId>`. Проверить: повтор с тем же ID не списывает; `Changed`-подписчик с исключением не ломает покупку. |
| FoodInventory ↔ Economy | `IInventoryAdapter` + `IIdempotentInventoryAdapter`; мост — `InventoryBridge` / `JournalInventoryBridge` | `CanAccept(itemId, qty)` без мутации, `TryAdd(itemId, qty)` всё-или-ничего, (Режим B) `TryAdd(operationId, …)` + `TryGetGrant(...)` — защита от ДВОЙНОЙ выдачи. Проверить частичный сбой и полный инвентарь. |
| GameClock ↔ Delivery | `IGameClockAdapter.NowMinutes` double монотонные игровые минуты; моста нет | Учесть сон, ускорение, паузу, загрузку; тест границы срока. |
| Delivery ↔ Economy/MoneyWallet | `IIdempotentPayoutAdapter.TryCredit(string,long)` + `WasCredited(...)`; мост — `PayoutBridge`, помощник — `DeliveryPayout` | ID выплаты приходит из `DeliveryService.GetPayoutOperationId(orderId)` / `DeliveryResult.PayoutOperationId` — НЕ собирайте `"delivery:" + orderId`. Повтор возвращает true без начисления; «true» без записи в журнал считается отказом. Проверить crash/retry, загрузку снимка и старые сохранения. |
| Delivery ↔ Phone | `DeliveryPhoneSource(IDeliveryService)` → `IDeliveryJobsSource` | Подать в `WorkSummaryService`, сравнить список и стадии после операций. |
| Debts ↔ MoneyWallet | `IDebtWalletAdapter` durable debit/refund/query, баланс; мост — `DebtWalletBridge`, помощник — `DebtPayment` | Идентификаторы платежей — `DebtPaymentId.ForPayment(key)` (`debt:`), чтобы не пересечься с `shop:`/`delivery:`. Повтор `paymentId` теперь идемпотентен; после компенсации повтор отклоняется. Проверить сбой между списанием и зачётом, `RetryPendingRecovery`. |
| Debts ↔ Phone | `DebtPhoneSource(IDebtReadService)` → `IDebtsSource` | Подать в `BankSummaryService`; проверить стадии и суммы включая штрафы. |
| Phone ↔ HUD TextMeshPro | `PhoneFacade` и `SyncMessageBadge`, доступные unread и результаты; моста нет | Проверить фактический HUD и поток вызовов; после сообщения/прочтения/restore вызвать синхронизацию, сравнить бейдж с unread. |

Delivery `ILocationProvider.CurrentLocationId` должен соответствовать ID точек заказа; проверять взаимодействие и локацию в реальных системах, не полагаться только на HUD. Выделять подтверждённый результат и неизвестный исход финансовой операции; никакой in-memory HashSet не заменяет журнал.

**Границы режимов.** `ShopService.IsDurable(wallet)` = false означает Режим A: повтор
защищён внутри сессии, после перезапуска — честный `OutcomeUnknown`. Режим B требует durable
журналов в кошельке И инвентаре плюс сохранения `ShopService.CreateSnapshot()` в ОДНОЙ записи
с балансом и инвентарём (`SAVE_SYSTEM_INTEGRATION_PLAN.md`).
