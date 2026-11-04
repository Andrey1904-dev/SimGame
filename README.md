# SimGame

Игровые модули Life From Zero (ТЗ №014): независимые C#-библиотеки без Unity-зависимостей.

| Модуль | Сборка | Тесты | Документ |
|---|---|---|---|
| `Economy/` — деньги, магазин, покупки | `LifeFromZero.Economy` | 33 | `Economy/INTEGRATION.md` |
| `Jobs/Delivery/` — курьерские заказы | `LifeFromZero.Jobs.Delivery` | 40 | `Jobs/INTEGRATION.md` |
| `Phone/` — смартфон, сообщения, контакты, уведомления | `LifeFromZero.Phone` | 90 | `Phone/INTEGRATION.md` |

Аудит трёх модулей (ТЗ №015), найденные и исправленные дефекты, порядок подключения
в Unity и инструкция для Codex: **`INTEGRATION_AUDIT.md`**.

Статус проверки: синтаксис и контракты проверены статически (нет доступа к C#-компилятору);
сборка и прогон 163 тестов в этой среде не выполнялись — см. `INTEGRATION_AUDIT.md` §6–7.
