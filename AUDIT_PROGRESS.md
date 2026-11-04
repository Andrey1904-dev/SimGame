# AUDIT_PROGRESS — ТЗ №017

Аудит Economy, Delivery, Phone и Debts (LIFE FROM ZERO).

**Текущий этап:** ✅ Завершён (static analysis + исправления P0/P1 + regression-тесты + отчёт).

---

## Что уже проверено

### Репозиторий (§1)

- [x] Факты о репозитории: 4 модуля присутствуют в рабочем дереве.
- [x] Ветки fetched: `arena/458d270c-simgame` (8b00ce1), `arena/52f76c1e-simgame` (be5db03).
- [x] `git status` — чистый рабочий коудок до правок.
- [x] `git diff HEAD 8b00ce1 -- Economy Jobs Phone` — пустой (совпадает).
- [x] `git diff HEAD be5db03 -- Debts` — пустой (совпадает).
- [x] Force-push не выполнен; ветки не переключались.

### Economy (§2) — 15 файлов, 2 084 строки

- [x] Целочисленная арифметика `Money` (`TryMultiply`, `TryAdd` — overflow-checked).
- [x] `Money.FromRubles` отсекает отрицательные (throw + Try).
- [x] Покупка при недостатке средств — `InsufficientFunds` без списания.
- [x] `CanApprove` вызывается **до** списания.
- [x] Инвентарь отказал после списания → `Compensate` → `TryRefund`.
- [x] Сбой возврата → журнал `_pendingRefunds` → повтор в начале следующей покупки.
- [x] Новые покупки заблокированы кодом `PendingRefund`, пока журнал непуст.
- [x] Исключение адаптера → `InternalError`/`AdapterFailure`, не throw.
- [x] **Не найдено:** потеря денег при отказе инвентаря после списания.

### Delivery (§3) — 13 файлов, 2 193 строки

- [x] Все переходы состояний: Available → Accepted → PickedUp → Completed/Failed/Cancelled.
- [x] Принятие, получение, завершение, отмена, просрочка (Refresh/Abandon/Cancel).
- [x] Выплата награды ровно один раз (`PayoutGranted` ставится **после** `TryCredit`).
- [x] Повторное завершение — `InvalidState`, выплата не повторяется.
- [x] Несколько активных заказов — `AlreadyHasActiveOrder` + слот восстанавливается
  из фактических состояний (`FindActiveEntry`/`SyncActiveSlot`).
- [x] Ошибки часов (throw, NaN/Infinity) и локации — `AdapterFailure`.
- [x] **Не найдено:** дублирование выплаты; потеря выполненного заказа при сбое выплаты.

### Phone (§4) — 37 файлов, 5 073 строки

- [x] Контакты (PhoneBook), сообщения (PhoneMessageService), уведомления
  (PhoneNotificationService), счётчики непрочитанных.
- [x] Повторная доставка сообщений — dedup по ID; исходящие всегда прочитаны.
- [x] Регистрация приложений — DuplicateId, overflow-check.
- [x] Исключения обработчиков — подавляются (SafeAdapter / Fire).
- [x] Snapshot (PhoneMessagesSnapshot / PhoneNotificationsSnapshot) — defensive copy.
- [x] Восстановление snapshot — Validate + RestoreInto, атомарная замена.
- [x] Защита от изменения snapshot извне — defensive copy.
- [x] Пустые и некорректные данные — InvalidArgument / NotFound.
- [x] **Найдено:** счётчик Messages не обновлялся при доставке через Inbox (P1-1) — **исправлено**.
- [x] **Найдено:** DebtDto не нёс стадию долга (P1-2) — **исправлено**.

### Debts (§5) — 36 файлов, 5 240 строки

- [x] Создание долгов, кредиторы (RegisterCreditor / DuplicateCreditor).
- [x] Начальная задолженность 38 400 ₽ — `StartingDebtFactory.StartingDebtRubles`.
- [x] Частичные и полные платежи; порядок: штрафы перед основным долгом.
- [x] Защита от двойного списания — идемпотенция по `paymentId` + журнал `_pending`.
- [x] Штрафы (PenaltyCalculator) и ограничение начислений (`MaxTotalRubles`).
- [x] Просрочки и стадии взыскания (CollectionThresholds).
- [x] Большие скачки игрового времени — `PenaltyTimeJump` (perPeriod × periodsToCharge).
- [x] Повторные вызовы через внешние адаптеры — `TryWasDebited` + ResolvePending.
- [x] Журнал платежей (`_payments` + `_paymentOrder`).
- [x] Восстановление после сбоя — DebitPending / DebitConfirmed / RefundPending.
- [x] Сохранение и загрузка — `DebtSnapshot` (schema v1) + validator + атомарная замена.
- [x] Корректность событий — `DebtEventHub` (multi-cast, exception-safe).
- [x] **Найдено (P0-1):** индекс периода штрафа продвигался после события →
  несогласованный snapshot → **исправлено**.
- [x] **Найдено (P0-2):** bool-флаг вложенного списания → потеря защиты → **исправлено**.
- [x] **Найдено (P0-3):** реентрантный restore во время платежа → **исправлено**.

### Previous three issues (§5)

- [x] Штрафы не теряются во время списания — соблюдается + regression test.
- [x] DebtClosed snapshot не противоречив — соблюдается + regression test.
- [x] Вложенное восстановление не отменяет платёж — соблюдается + regression test.

### Сквозной аудит (§6)

- [x] Economy ↔ Delivery: выплата → кошелёк; повторная доставка не дублирует; ошибка начисления не теряет информацию.
- [x] Economy ↔ Debts: списание по долгу однократно; нет конфликта возвратов и платежей; ID не пересекаются; баланс не отрицательный.
- [x] Phone ↔ Delivery: телефон отображает активный заказ (`IDeliveryJobsSource`).
- [x] Phone ↔ Debts: телефон отображает список долгов с остатками, сроками, статусами, стадиями.
- [x] События долгов → уведомления (DebtEventHub → PhoneNotificationHub).
- [x] Повторное событие не создаёт дубликатов (dedup по ID).
- [x] Совместимость пространств имён, интерфейсов, asmdef, C# 9, Unity 6000.3.27f1.
- [x] Нет циклических зависимостей.
- [x] Фальшивых Unity-адаптеров не создано.

### Сохранения (§7)

- [x] Economy: **нет** сохранения — ограничение.
- [x] Jobs/Delivery: **нет** сохранения — ограничение.
- [x] Phone: snapshot + restore — есть.
- [x] Debts: snapshot + restore — есть (schema v1).
- [x] Нет глобальной системы сохранений.
- [x] Очерёдность восстановления: Phone → Debts → Economy → Delivery.

### Тесты и проверки (§8)

- [x] Синтаксис всех C#-файлов — баланс скобок, namespace.
- [x] Все типы/интерфейсы — пересчёт по namespace, дубликатов нет.
- [x] Assembly references, csproj (LangVersion 9.0), Unity API absence.
- [x] Тестовые регистрации — API раннера существует (AddRange, IsNotNull).
- [x] Пересчёт тестов: Economy 33, Delivery 40, Phone 92, Debts 88 = **253**.
- [x] **Нет компилятора** ⇒ тесты не запущены (P2-2).
- [x] Добавлены 5 regression-тестов только для найденных ошибок.

### Исправления (§9)

- [x] P0-1, P0-2, P0-3 — Debts/Core/DebtService.cs.
- [x] P1-1 — Phone/Messages/PhoneInbox.cs, Phone/Integrations/PhoneFacade.cs.
- [x] P1-2 — Phone/Notifications/ExternalAdapters.cs.
- [x] P2-1 — Phone/Apps/PhoneAppIds.cs, Phone/Apps/PhoneAppRegistry.cs.
- [x] Публичные контракты не изменены (debt-stage добавлен как расширяющее поле).

### Документы (§10)

- [x] `FULL_AUDIT_REPORT.md`
- [x] `UNITY_INTEGRATION_PLAN.md`
- [x] `AUDIT_PROGRESS.md` (this file)

### Git (§12)

- [x] Diff проверен — затронуты только файлы аудита (8 modified + 4 новых).
- [x] Commit: `38f625c9660f0bb4104c3c314a2ea13d8e2a612f`
  («ТЗ №017: аудит Economy/Delivery/Phone/Debts, исправление P0/P1, отчёты»).
- [x] Push в `arena/64fd70a9-simgame` — **успех** (`* [new branch]`).
- [x] Локальный и удалённый SHA совпадают: `38f625c`.
- [x] Force-push не выполнен; ветки не переключались.

---

## Что осталось

1. **P2-2:** провести компиляцию и EditMode-тесты в Unity 6000.3.27f1 — единственное
   способ подтвердить готовность к интеграции.
2. **Economy/Delivery сохранения:** либо добавить snapshot, либо документировать
   ограничение (§8).
3. **Глобальная система сохранений:** либо объединить Phone + Debts snapshot,
   либо документировать раздельность.
4. **Real Unity adapters:** `IWalletAdapter`, `IDebtWalletAdapter` и прочие — Codex.
5. **Договор адаптера кошелька:** идемпотентность по paymentId (один и тот же кошелёк).

---

## Найденные проблемы (кратко)

| Критика | Число | Fix |
|---|---|---|
| P0 | 3 | ✅ |
| P1 | 2 | ✅ |
| P2 | 2 | ✅ (1 — исправлено, 1 — ограничение) |
| Regression-тестов добавлено | 5 | ✅ |
| Тестов найдено | 253 | не запущено |
| Файлов проверено | 101 | ✅ |
| Строк проверено | 14 590 | ✅ |

---

## Можно ли переходить к Unity-интеграции?

**Да, с оговорками.** Критерий §13: absence of **known critical errors**.
- P0/P1 исправлены.
- Ограничения (Economy/Delivery сохранения) документированы и не являются критичными
  для функционирования (игрок просто купит/взьмёт заказ снова).
- Оставшийся риск — **компиляция и тесты в Unity не пройдены**.

**Рекомендация:** сначала выполнить §2 (компиляция + EditMode-тесты), затем §3
(адаптеры), затем §4 (сохранения).

See `UNITY_INTEGRATION_PLAN.md`.
