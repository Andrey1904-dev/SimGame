# START HERE — SimGame → LIFE FROM ZERO

Комплект для подготовки импорта; Unity-проект не был доступен, компиляция и тесты не запускались. Не считать комплект готовым к финансовому production-подключению.

Источник: ветка `arena/896f0f35-simgame`. База текущей ветки `3e88e08d05232bfe9c9e918369db7366b807520c`; её дерево идентично подтверждённому `66e56ff7b9f19b2087283cffcd5a4eb37705909a`. Точный SHA комплекта получить `git rev-parse HEAD` в checkout этой опубликованной ветки.

* Economy: `Economy/Core/`, `Economy/Shop/` — магазин, кошелёк и инвентарь через интерфейсы.
* Delivery: `Jobs/Delivery/` — заказы, игровые часы, выплата, снимок.
* Phone: `Phone/` — приложения, контакты, сообщения, уведомления, снимки.
* Debts: `Debts/` — долги, штрафы, платежи и снимок.
* `UnityIntegrationKit/PhoneSources.cs` — готовые read-only адаптеры Delivery/Debts → Phone; Unity-мосты не реализованы.

Порядок: импорт по `UNITY_IMPORT_MANIFEST.md`, компиляция, EditMode-тесты, Economy, Delivery, Phone, Debts, совместные проверки, затем согласование общего сохранения. Подробности по текущему этапу в `CODEX_EXECUTION_STEPS.md`; восемь коротких промптов — `CODEX_COPY_PASTE_TASKS.md`. Не читать всё заранее.

## Критические риски
* `NEEDS_UNITY_INSPECTION`: неизвестны API MoneyWallet, FoodInventory, GameClock, HUD, взаимодействий и текущего Save System. Не выдумывать сигнатуры и не заменять системы.
* Delivery: ID выплаты добавлен, но защита при перезапуске зависит от атомарного устойчивого журнала реального кошелька. Legacy `IPayoutAdapter` не обеспечивает её. Просрочка при восстановлении, старые выплаты и уникальность ID требуют reconciliation.
* Economy: покупка/компенсация без ID, нет гарантии crash-safe повтора. Остановить финансовую интеграцию до решения транзакций кошелька и инвентаря.
* Debts требует durable идемпотентный wallet journal и согласованный pending. Снимки Phone messages/notifications могут требовать конвертер сериализации. Четыре модуля должны восстанавливаться согласованно.

По необходимости: `CODEX_ARCHITECTURE_MAP.md`, `CODEX_WIRING_MAP.md`, `SAVE_SYSTEM_INTEGRATION_PLAN.md`, `UNITY_TEST_EXECUTION.md`, `CLEANUP_REPORT.md`; исторические аудиты оставлены.

## Первое задание
Прочитай только этап 1 `CODEX_EXECUTION_STEPS.md` и `UNITY_IMPORT_MANIFEST.md`. Проверь конфликты, импортируй указанные исходники с asmdef в `/Users/homepc/Projects/LifeFromZero`, запусти компиляцию Unity 6000.3.27f1. Не изменяй сцены, геометрию и посторонние системы. При ошибке остановись; при успехе сделай коммит и кратко сообщи результат. Не переходи к тестам до успешной компиляции.
