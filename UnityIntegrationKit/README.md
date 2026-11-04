# UnityIntegrationKit

`PhoneSources.cs` — готовые read-only мосты `DeliveryPhoneSource(IDeliveryService)` и `DebtPhoneSource(IDebtReadService)`. Создавать `WorkSummaryService` и `BankSummaryService` с этими источниками; кошелёк для BankSummaryService предоставляется отдельно. Мосты не хранят копии источников. Импортировать вне asmdef (Assembly-CSharp). Проверить компиляцию и подключить к UI.

Другие адаптеры не реализованы: `NEEDS_UNITY_INSPECTION`. Это не готовый транзакционный слой. См. CODEX_WIRING_MAP.md и ограничения ниже. Существующие сервисы уже содержат операции, и повторять их здесь нельзя.

* Delivery: используйте `DeliveryService` и `IIdempotentPayoutAdapter`. Последний должен вызывать атомарную устойчивую операцию реального кошелька; памяти процесса недостаточно. `TryCredit(long)` — legacy и небезопасен при сбое.
* Economy: `ShopService` и `IWalletAdapter`/`IInventoryAdapter`. Контракты операций без ID: повтор покупки/возврата после сбоя нельзя сделать безопасным одним внешним мостом. Не запускать финансовые операции до решения согласованной транзакции.
* Debts: `DebtService` и `IDebtWalletAdapter`: durable атомарный журнал debit/refund/query; требования в интерфейсе.
* Phone: `PhoneFacade`, существующие message/notification services и снимки. Для новых snapshots только concrete PhoneAppRegistry/PhoneBook; счётчик messages после restore пересчитать SyncMessageBadge.
