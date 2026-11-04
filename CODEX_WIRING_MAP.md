# Карта подключения

Все Unity-компоненты ниже: `NEEDS_UNITY_INSPECTION`. Нельзя предполагать сигнатуры или атомарность. Готовые мосты только read-only в `UnityIntegrationKit/PhoneSources.cs`.

| Связь / исходник | Контракт и готовый адаптер | Действие / проверка |
|---|---|---|
| MoneyWallet ↔ Economy `ShopService` | `IWalletAdapter` (`Economy/Core/IWalletAdapter.cs`); Unity-мост не готов | Изучить баланс, atomic spend/refund и сохранение; неизвестный исход не считать отказом; блокировать покупку пока транзакционность не обеспечена. Тест отказа/сбоя. |
| FoodInventory ↔ Economy | `IInventoryAdapter` CanApprove без изменений, TryAdd all-or-nothing; моста нет | Отобразить itemId/quantity на фактические данные, проверить вместимость и частичный сбой. |
| GameClock ↔ Delivery | `IGameClockAdapter.NowMinutes` double монотонные игровые минуты; моста нет | Учесть сон, ускорение, паузу, загрузку; тест границы срока. |
| Delivery ↔ Economy/MoneyWallet | `IIdempotentPayoutAdapter.TryCredit(string,long)`; моста нет | Атомарный durable журнал ID+сумма и баланс, повтор возвращает true без начисления, конфликт отклонить; ID изолировать по save lineage, никогда не переиспользовать order ID. Test crash/retry и старые сохранения. Legacy API не защищён. |
| Delivery ↔ Phone | `DeliveryPhoneSource(IDeliveryService)` → `IDeliveryJobsSource` | Подать в `WorkSummaryService`, сравнить список и стадии после операций. |
| Debts ↔ MoneyWallet | `IDebtWalletAdapter` durable debit/refund/query, баланс; моста нет | Проверить журнал и атомарность, уникальные paymentId, инъекция сбоя между списанием и записью долга, RetryPendingRecovery. |
| Debts ↔ Phone | `DebtPhoneSource(IDebtReadService)` → `IDebtsSource` | Подать в `BankSummaryService`; проверить стадии и суммы включая штрафы. |
| Phone ↔ HUD TextMeshPro | `PhoneFacade` и `SyncMessageBadge`, доступные unread и результаты; моста нет | Проверить фактический HUD и поток вызовов; после сообщения/прочтения/restore вызвать синхронизацию, сравнить бейдж с unread. |

Delivery `ILocationProvider.CurrentLocationId` должен соответствовать ID точек заказа; проверять взаимодействие и локацию в реальных системах, не полагаться только на HUD. Выделять подтверждённый результат и неизвестный исход финансовой операции; никакой in-memory HashSet не заменяет журнал.
