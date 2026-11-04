# START HERE — SimGame → LIFE FROM ZERO

Комплект для подготовки импорта. **ТЗ №021:** финансовые контракты Economy, Delivery и Debts
доведены, тесты **реально прогнаны** (Economy 60, Delivery 55, Debts 97 — все PASS).
Unity-проект по-прежнему не был доступен: сборка внутри Unity и EditMode-прогон — задача Codex.
Режим A (сессия без сохранений) готов к подключению. Режим B (сохранения) НЕ готов: см. §«Режим B».

Ветка комплекта: `arena/98f6320f-simgame` (влита в `main` PR #7 вместе с предшествующим `88ea306`); рабочая ветка проверки — `arena/1316e6a7-simgame`. Актуальный SHA для импорта — тот, что указан в `TZ021_FINANCIAL_CONTRACTS_REPORT.md` §0 (или `git rev-parse HEAD` в checkout этой ветки). Не ориентируйтесь на SHA из ранних редакций этого файла.

* Economy: `Economy/Core/`, `Economy/Shop/` — магазин, кошелёк и инвентарь через интерфейсы;
  журнал операций с идентификаторами (`ShopOperation`, `ShopSnapshot`), идемпотентные контракты
  (`IIdempotentWalletAdapter`, `IIdempotentInventoryAdapter`), событие `Changed`.
* Delivery: `Jobs/Delivery/` — заказы, игровые часы, выплата, снимок.
* Phone: `Phone/` — приложения, контакты, сообщения, уведомления, снимки.
* Debts: `Debts/` — долги, штрафы, платежи и снимок.
* `UnityIntegrationKit/PhoneSources.cs` — готовые read-only адаптеры Delivery/Debts → Phone.
* `UnityIntegrationKit/AdapterBridges.cs` — независимые от Unity мосты: MoneyWallet, FoodInventory,
  выплаты Delivery, платежи Debts, покупки/возвраты Economy. **Компилируются вместе с четырьмя
  модулями (проверено).** Требуют от Codex только проброс минимальных методов (блоки `// CODEX:`).

Порядок: импорт по `UNITY_IMPORT_MANIFEST.md`, компиляция, EditMode-тесты, затем
**Economy по `ECONOMY_INTEGRATION_QUICKSTART.md`** (Режим A), Delivery, Phone, Debts,
совместные проверки, и только потом согласование общего сохранения.
Подробности по этапам — `CODEX_EXECUTION_STEPS.md`; короткие промпты — `CODEX_COPY_PASTE_TASKS.md`.
Не читать всё заранее.

## Два режима — читать до подключения экономики

**Режим A: Play Mode без сохранений.** Покупки и возвраты работают в текущей сессии.
Повтор операции с тем же `purchaseId` внутри сессии не списывает деньги и не выдаёт товар
повторно; `Changed`, недостаток средств и сбой инвентаря обработаны. **Гарантий при аварийном
завершении игры нет** — так и говорите, не обещайте больше.

**Режим B: сохранения.** Требует трёх вещей одновременно: durable-журнал в MoneyWallet,
durable-журнал выдачи в FoodInventory и **единая запись сохранения** для кошелька, инвентаря и
`ShopService.CreateSnapshot()`. Пока это не реализовано и не проверено — Режим B не объявлять
готовым.

## Критические риски (после ТЗ №021)
* `NEEDS_UNITY_INSPECTION`: неизвестны API MoneyWallet, FoodInventory, GameClock, HUD, взаимодействий и текущего Save System. Не выдумывать сигнатуры и не заменять системы.
* Economy: контракты и журнал готовы; остаётся реализовать два адаптера (блоки `// CODEX:` в `UnityIntegrationKit/AdapterBridges.cs`) и проверить атомарность в реальном MoneyWallet.
* Delivery: `PayoutOperation` даёт уникальные идентификаторы выплат; повтор не платит дважды даже после загрузки снимка. Защита при перезапуске всё ещё требует durable-журнала реального кошелька.
* Debts: повтор `paymentId` теперь идемпотентен (полный прежний результат), компенсация помечает платёж. Нужен durable `IDebtWalletAdapter` в реальном кошельке.
* Снимки Phone messages/notifications могут требовать конвертер сериализации. Четыре модуля должны восстанавливаться согласованно.

По необходимости: `CODEX_ARCHITECTURE_MAP.md`, `CODEX_WIRING_MAP.md`, `SAVE_SYSTEM_INTEGRATION_PLAN.md`, `UNITY_TEST_EXECUTION.md`, `CLEANUP_REPORT.md`; исторические аудиты оставлены.

## Первое задание
Прочитай только этап 1 `CODEX_EXECUTION_STEPS.md` и `UNITY_IMPORT_MANIFEST.md`. Проверь конфликты, импортируй указанные исходники с asmdef в `/Users/homepc/Projects/LifeFromZero`, запусти компиляцию Unity 6000.3.27f1. Не изменяй сцены, геометрию и посторонние системы. При ошибке остановись; при успехе сделай коммит и кратко сообщи результат. Не переходи к тестам до успешной компиляции.
