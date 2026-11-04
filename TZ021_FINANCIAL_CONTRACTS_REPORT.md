# ТЗ №021 — финансовые контракты Economy / Delivery / Debts

Дата: 2026-11-04. Ветка: `arena/98f6320f-simgame`.
База: `4b8ddb4` (merge PR #6). Указанный в ТЗ коммит `88ea306f…` в этом репозитории
отсутствует; работа выполнена на текущей ветке сессии поверх `4b8ddb4`.

---

## 1. Как проверялось (честно)

Компилятора .NET в окружении нет (`dotnet`, `csc`, `mono` отсутствуют; сети к `dot.net` и
`nuget.org` нет). Поэтому был собран **реальный** прогон: Chromium + Roslyn/.NET WASM
(`@live-codes/blazor-wasm`), который компилирует и **исполняет** C# из этого репозитория.

| Что | Результат |
|---|---|
| Компиляция Economy (Core+Shop+Tests) | OK |
| Компиляция Delivery (Delivery+Tests) | OK |
| Компиляция Debts (Core/Events/Payments/Penalties/Persistence+Tests) | OK |
| Компиляция `UnityIntegrationKit/*` + 4 модуля | OK |
| Тесты Economy | **60 / 60 PASS** |
| Тесты Delivery | **55 / 55 PASS** |
| Тесты Debts | **96 / 96 PASS** |
| Тесты Phone | не запускались (вне объёма ТЗ №021) |
| Сборка и EditMode-прогон внутри Unity | **не выполнялись** — Unity-проект недоступен |

Это не «статический анализ»: тесты исполнялись. Ограничение одно и важное — **вне Unity**
(тот же компилятор Roslyn, но без Unity-рантайма), поэтому Unity-специфичные ошибки
импорта/asmdef ловит только Codex.

---

## 2. Economy — что исправлено

| Риск из ТЗ | Что сделано |
|---|---|
| Отсутствие ID у покупок, списаний, возвратов | `FinancialOperationId`: списание `shop:<purchaseId>`, возврат `shop-refund:<purchaseId>`. `PurchaseResult.PurchaseId` возвращает ID наружу. |
| Повторное выполнение финансовой операции | Журнал операций `ShopOperation`/`ShopOperationState`; повтор с тем же `purchaseId` возвращает прежний исход (`IsReplay = true`), деньги и товар не двигаются. При идемпотентных адаптерах повтор блокируется ещё и на уровне кошелька/инвентаря. |
| Потеря незавершённых операций при восстановлении | `ShopSnapshot` + `ShopSnapshotValidator` (`CreateSnapshot`/`RestoreSnapshot`) сохраняют состояние, суммы, количество и ID. `RetryPendingOperations` доводит выдачу или возвращает деньги. |
| Неоднозначный результат при исключении обработчика события | Новое событие `ShopService.Changed`: исключение подписчика проглатывается и не влияет на финансовый результат; вложенная покупка из обработчика отклоняется кодом `PendingOperation`. Исключение **адаптера** больше не считается отказом: исход сверяется с журналом (`TryGetOperationState`), иначе `OutcomeUnknown`. |
| Двойное списание / двойная выдача товара | `IIdempotentWalletAdapter` (по `operationId`) и `IIdempotentInventoryAdapter` (журнал выдачи). `false`/исключение сверяются с журналом, повтор выдачи не выполняется, «висящая» операция блокирует новые покупки. |

Совместимость сохранена: `Purchase(wallet, inventory, request)`, `PendingRefundCount`,
`RetryPendingRefunds(wallet)`, `RestorePendingRefunds(...)`, коды `Success…PendingRefund`
не менялись (новые коды `PendingOperation = 7`, `OutcomeUnknown = 8`, `InvalidArgument = 9`
добавлены в конец). Изменено осознанно два поведения: при одном старом тесте
(исключение кошелька на списании) код теперь `OutcomeUnknown` вместо `InternalError`, и
блокировка «висит выдача» сообщается как `PendingOperation` (долг по возврату — прежний
`PendingRefund`).

---

## 3. Delivery и Debts — только финансовые контракты

**Delivery**
* `PayoutOperation`: идентификатор выплаты принадлежит заказу (`delivery:<orderId>#<n>`),
  выдаётся при регистрации и **сохраняется в снимке** (`DeliveryOrderSnapshot.PayOperationId`,
  `DeliverySnapshot.PayoutSequence`). Раньше ID склеивался в `Complete`, и заново
  зарегистрированный заказ с тем же `Id` не оплачивался.
* `IIdempotentPayoutAdapter` дополнен `WasCredited(operationId, out bool)`: «true» без записи
  в журнал считается отказом, а исключение/`false` после реального начисления восстанавливается.
* `DeliveryResult.PayoutOperationId` и `DeliveryService.GetPayoutOperationId(orderId)`.

**Debts**
* `DebtPaymentId`: пространство `debt:`, проверка на чужие префиксы (`shop:`, `delivery:`).
* Повтор `paymentId` теперь идемпотентен и возвращает **полный** прежний результат
  (сумма, части, остаток) без списания и без второй записи в истории.
* `DebtPaymentRecord.Refunded` + поле `Refunded` в снимке: после компенсации повтор
  с тем же `paymentId` отклоняется, а не зачитывается снова.

---

## 4. Unity Integration Kit

`UnityIntegrationKit/AdapterBridges.cs` — независимые от Unity части адаптеров:
`WalletBridge`, `JournalWalletBridge`, `InventoryBridge`, `JournalInventoryBridge`,
`PayoutBridge`, `DebtWalletBridge`, `ShopCheckout`, `DeliveryPayout`, `DebtPayment`.
Файл компилируется вместе с четырьмя модулями (проверено). Реальные Unity API не выдумывались:
Codex пробрасывает свои методы через делегаты, минимальный список — в блоках `// CODEX:`
и в `UnityIntegrationKit/README.md`.

---

## 5. Гарантии только одной сессии (Режим A)

Работает без изменений в реальных компонентах:

* повтор с тем же `purchaseId` внутри сессии не списывает деньги и не выдаёт товар снова;
* недостаток средств не приводит к выдаче;
* исключение обработчика `Changed` не отменяет покупку;
* сбой инвентаря после списания → деньги возвращаются, «висящая» операция блокирует магазин;
* при неизвестном исходе возвращается `OutcomeUnknown`, а не выдуманный отказ;
* повторная выплата курьеру и повторный платёж по долгу внутри сессии не проходят.

Чего **нет** в Режиме A: переживания аварийного завершения игры. `LegacyWalletAdapter`
держит журнал в памяти (`IsDurable == false`), `ShopService.IsDurable(wallet) == false`.

---

## 6. Что остаётся нерешённым для сохранений (Режим B)

1. Durable-журнал `operationId → исход` в реальном `MoneyWallet` (`IIdempotentWalletAdapter`,
   `IsDurable = true`).
2. Durable-журнал выдачи в реальном `FoodInventory` (`IIdempotentInventoryAdapter`).
3. **Согласованная фиксация**: баланс кошелька, содержимое инвентаря и
   `ShopService.CreateSnapshot()` в одной записи сохранения. Без этого сбой между записями
   рассинхронизирует деньги и товар.
4. Изоляция идентификаторов по линии сохранения (новая игра не должна получать старые ключи).
5. Проверка сценариев сохранения/загрузки внутри Unity.

Пока это не сделано, Режим B объявлять готовым нельзя.

---

## 7. Добавленные тесты

| Модуль | Файл | Тестов | Всего в модуле |
|---|---|---|---|
| Economy | `Economy/Tests/ShopFinancialSafetyTests.cs` (+`Fakes/JournalAdapters.cs`) | 26 | 60 |
| Delivery | `Jobs/Tests/DeliveryFinancialSafetyTests.cs` (+`Fakes/JournalAdapters.cs`) | 10 | 55 |
| Debts | `Debts/Tests/DebtFinancialContractTests.cs` | 9 | 96 |
| **Итого новых** | | **45** | 211 |

Покрытие обязательного списка ТЗ: повторная покупка с тем же ID, повторный возврат,
недостаток средств, исключение обработчика `Changed`, сбой между списанием и выдачей,
повторная загрузка снимка, повторная выплата курьеру, повторный платёж по долгу —
все восемь сценариев присутствуют отдельными тестами.

---

## 8. Минимальные действия Codex

1. Импорт по `UNITY_IMPORT_MANIFEST.md` (включая новые файлы §ТЗ №021), компиляция.
2. EditMode-тесты: ожидается Economy 60, Delivery 55, Debts 96.
3. Economy, **Режим A**: реализовать в `MoneyWallet`/`FoodInventory` методы из блоков
   `// CODEX:` и подключить через `ShopCheckout` (`ECONOMY_INTEGRATION_QUICKSTART.md`).
4. Delivery: `PayoutBridge` + ID из `DeliveryService.GetPayoutOperationId`.
5. Debts: `DebtWalletBridge` + `DebtPayment.Pay` (ID из `DebtPaymentId`).
6. Режим B — только после durable-журналов и согласованной фиксации сохранения.

---

## 9. Границы работы

Не изменялся Unity-проект, не создавались фиктивные Unity-компоненты, не менялись игровые
правила, цены и баланс, не добавлялись новые игровые системы, не удалялся полезный код,
полный повторный аудит модулей не проводился.
