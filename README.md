# SimGame

Игровые модули Life From Zero: независимые C#-библиотеки без Unity-зависимостей.

| Модуль | Сборка | Тесты | Документ |
|---|---|---|---|
| `Economy/` — деньги, магазин, покупки | `LifeFromZero.Economy` | 34 | `Economy/INTEGRATION.md` |
| `Jobs/Delivery/` — курьерские заказы | `LifeFromZero.Jobs.Delivery` | 45 | `Jobs/INTEGRATION.md` |
| `Phone/` — смартфон, сообщения, контакты, уведомления | `LifeFromZero.Phone` | 93 | `Phone/INTEGRATION.md` |
| `Debts/` — долги, платежи, штрафы, стадии взыскания | `LifeFromZero.Debts` | 87 | `Debts/INTEGRATION.md` |

Полный аудит четырёх модулей (ТЗ №017), найденные дефекты, план подключения
к Unity и инструкция для Codex: **`FULL_AUDIT_REPORT.md`**,
**`UNITY_INTEGRATION_PLAN.md`**, **`AUDIT_PROGRESS.md`**.
Предыдущий аудит трёх модулей (ТЗ №015): `INTEGRATION_AUDIT.md`.

Статус проверки: синтаксис и контракты проверены статически (в среде нет
C#-компилятора); **сборка и прогон 259 тестов не выполнялись** — см.
`FULL_AUDIT_REPORT.md` §2 и §10.
