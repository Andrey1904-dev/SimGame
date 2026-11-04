# UnityIntegrationKit

Два файла, оба не зависят от UnityEngine и компилируются вместе с модулями (проверено).

## PhoneSources.cs

Готовые read-only мосты `DeliveryPhoneSource(IDeliveryService)` и `DebtPhoneSource(IDebtReadService)`. Создавать `WorkSummaryService` и `BankSummaryService` с этими источниками; кошелёк для BankSummaryService предоставляется отдельно. Мосты не хранят копии источников. Импортировать вне asmdef (Assembly-CSharp). Проверить компиляцию и подключить к UI.

## AdapterBridges.cs (ТЗ №021)

Готовые финансовые мосты. Логика внутри проверена тестами модулей; от Codex требуется только проброс минимальных методов из реальных компонентов — блоки `// CODEX:` в файле.

| Мост | Заменяет | Минимум методов в компоненте Codex |
|---|---|---|
| `WalletBridge` | `IWalletAdapter` | `long Balance`, `bool TrySpend(long)`, `bool TryCredit(long)` |
| `JournalWalletBridge` | `IIdempotentWalletAdapter` (Режим B) | `bool TrySpend(string operationId, long)`, `bool TryCredit(string operationId, long)`, `bool TryGetOperation(...)` |
| `InventoryBridge` | `IInventoryAdapter` | `bool CanAccept(string itemId, int qty)` без мутации, `bool TryAdd(string itemId, int qty)` всё-или-ничего |
| `JournalInventoryBridge` | `IIdempotentInventoryAdapter` (Режим B) | `bool TryAdd(string operationId, string itemId, int qty)`, `bool TryGetGrant(...)` |
| `PayoutBridge` | `IIdempotentPayoutAdapter` (выплаты Delivery) | `bool TryCredit(string operationId, long)`, `bool WasCredited(string, out bool)` |
| `DebtWalletBridge` | `IDebtWalletAdapter` (платежи Debts) | `bool TryDebit(string paymentId, long)`, `bool TryRefund(string paymentId, long)`, `bool TryWasDebited(string, out bool)` |
| `ShopCheckout` | — | ничего: выдаёт `purchaseId`, не даёт купить без идентификатора |
| `DeliveryPayout` | — | ничего: даёт ID выплаты из сервиса |
| `DebtPayment` | — | ничего: собирает `paymentId` в пространстве `debt:` |

Ограничения, которые нельзя обойти адаптером:

* Без durable-журнала в реальном MoneyWallet/FoodInventory доступен только Режим A: повтор защищён внутри сессии, после аварийного завершения — `OutcomeUnknown`, а не выдуманный успех.
* `IsDurable` ставьте `true` ТОЛЬКО когда журнал реально пишется в сохранение. По этому флагу модуль решает, можно ли объявлять Режим B.
* `PayoutBridge.TryCredit(long)` (legacy) сознательно возвращает `false`: без идентификатора повтор не защищён.
* Никакой in-memory `HashSet` не заменяет журнал: он не переживает перезапуск и не участвует в согласованной фиксации.

Не реализованы и требуют осмотра проекта (`NEEDS_UNITY_INSPECTION`): HUD-биндинги, подписка на `ShopService.Changed`, экраны телефона. Это UI-слой, а не финансовые контракты.
