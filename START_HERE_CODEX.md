# START HERE — SimGame → LIFE FROM ZERO

> **Актуальное состояние (интеграция `City` + `Pause` + `UI` + доменных модулей под Unity `6000.3.27f1`):**
> единый сценарий установки, проверки и отката для Codex находится в **`UI/CODEX_EXECUTE.md` (§0)** и **`City/CODEX_EXECUTE.md`**.
>
> * Единый безопасный установщик всех модулей (`City`, `UI`, `Economy`, `Jobs/Delivery`, `Phone`, `Debts`, `UnityIntegrationKit`, 172 файла без перезаписи сцен, пользовательских ассетов, `.meta` и `ProjectSettings`):
>   ```bash
>   bash City/UnityIntegration/install_to_unity.sh "/Users/homepc/Projects/LifeFromZero" --dry-run
>   bash City/UnityIntegration/install_to_unity.sh "/Users/homepc/Projects/LifeFromZero" --apply
>   ```
> * Сводная проверка (статические проверки + структура `Assets/` + EditMode/PlayMode + `DetectBatch` + `UiInstaller.RunBatchVerify`):
>   ```bash
>   bash City/UnityIntegration/run_checks.sh --unity-project "/Users/homepc/Projects/LifeFromZero"
>   ```
> * Откат: копии изменённых файлов и сцен автоматически сохраняются в `<проект>/LifeFromZeroIntegration/backups/<UTC-штамп>/` (точные команды отката — в `UI/CODEX_EXECUTE.md` §0.4; базовый тег в `SimGame` до интеграции — `backup-before-integration`).

Комплект для подготовки импорта. **ТЗ №021 + интеграция PR #14 / PR #15:** финансовые контракты Economy, Delivery и Debts, город District750 и система UI
доведены, тесты **реально прогнаны** (Economy 60, Delivery 55, Phone 93, Debts 97, City 100, UI 103 — всего **508 PASS**, плюс по-сборочная Roslyn-компиляция всех 17 `.asmdef` + `Assembly-CSharp` + `Assembly-CSharp-Editor`).
Unity-проект в облачной среде по-прежнему недоступен: сборка внутри Unity 6000.3.27f1, EditMode/PlayMode-прогон и визуальная проверка в Play Mode — задача Codex.
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
