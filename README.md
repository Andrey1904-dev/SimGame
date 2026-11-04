# SimGame

Игровые модули Life From Zero: независимые C#-библиотеки без Unity-зависимостей.

| Модуль | Сборка | Тесты | Документ |
|---|---|---|---|
| `Economy/` — деньги, магазин, покупки | `LifeFromZero.Economy` | 60 | `Economy/INTEGRATION.md`, `ECONOMY_INTEGRATION_QUICKSTART.md` |
| `Jobs/Delivery/` — курьерские заказы | `LifeFromZero.Jobs.Delivery` | 55 | `Jobs/INTEGRATION.md` |
| `Phone/` — смартфон, сообщения, контакты, уведомления | `LifeFromZero.Phone` | 93 | `Phone/INTEGRATION.md` |
| `Debts/` — долги, платежи, штрафы, стадии взыскания | `LifeFromZero.Debts` | 97 | `Debts/INTEGRATION.md` |
| `City/` — процедурный генератор квартала 250×250 (ТЗ №023) | `LifeFromZero.City.Quarter` | 44 | `City/README.md` |

Полный аудит четырёх модулей (ТЗ №017), найденные дефекты, план подключения
к Unity и инструкция для Codex: **`FULL_AUDIT_REPORT.md`**,
**`UNITY_INTEGRATION_PLAN.md`**, **`AUDIT_PROGRESS.md`**.
Предыдущий аудит трёх модулей (ТЗ №015): `INTEGRATION_AUDIT.md`.

Статус проверки. ТЗ №021: финансовые контракты Economy/Delivery/Debts доведены, добавлены
идентификаторы операций, идемпотентность, журнал состояний и снимки. Тесты этих трёх модулей
**реально прогнаны вне Unity** (Roslyn / .NET WASM): Economy 60/60, Delivery 55/55, Debts 97/97 —
все PASS. Компиляция и EditMode-прогон **внутри Unity не выполнялись** (Unity-проект недоступен);
Phone-тесты в этом прогоне не запускались. См. `ECONOMY_INTEGRATION_QUICKSTART.md`,
`FINANCIAL_OPERATION_IDS.md`, `TZ021_FINANCIAL_CONTRACTS_REPORT.md`.
