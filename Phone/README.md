# Ядро игрового смартфона — ТЗ №014-Т.1

Независимое C#-ядро смартфона Life From Zero: приложения, сообщения, контакты,
уведомления и точки интеграции с существующими системами. Без Unity UI,
MonoBehaviour, собственных кошельков, часов и системы доставки.

Модуль не ссылается на `Economy/` и `Jobs/`: связь с ними — только через интерфейсы
и DTO (`IBalanceSource`, `IDeliveryJobsSource`, `IDebtsSource`, `IStorySource`, `IPhoneClock`).

## Структура

```
Phone/
├── Phone.asmdef                     сборка модуля (noEngineReferences)
├── Core/
│   ├── PhoneResult.cs               коды исходов + PhoneResult<T> + PhoneIntegrationException
│   ├── GameMinutes.cs               игровое время (валидация NaN/∞/отрицательных)
│   ├── PhoneClockReader.cs          безопасное чтение времени из адаптера
│   ├── IPhoneClock.cs               адаптер к GameClock
│   ├── PhoneId.cs                   проверка идентификаторов
│   └── IUnreadCounter.cs            общий контракт счётчика непрочитанных
├── Apps/
│   ├── PhoneApp.cs                  модель приложения + категория
│   └── PhoneAppRegistry.cs          реестр приложений + стартовый набор
├── Messages/
│   ├── PhoneMessage.cs              сообщение (входящее/исходящее, время, прочтение)
│   ├── PhoneConversation.cs         переписка с контактом
│   ├── PhoneMessageService.cs       сервис переписок и истории
│   ├── PhoneMessagesSnapshot.cs     DTO для сохранения
│   ├── PhoneMessagesRestore.cs      восстановление из снимка с валидацией (ТЗ №015)
│   └── PhoneInbox.cs                узкий вход для NPC/сюжета
├── Contacts/
│   ├── PhoneContact.cs              контакт
│   └── PhoneBook.cs                 телефонная книга и поиск
├── Notifications/
│   ├── PhoneNotification.cs         уведомление + 6 типов
│   ├── PhoneNotificationService.cs  сервис с дедупликацией
│   ├── PhoneNotificationsSnapshot.cs DTO для сохранения
│   ├── PhoneNotificationHub.cs      фасад для внешних систем + бейджи
│   ├── PhoneNotificationsRestore.cs восстановление уведомлений (ТЗ №015)
│   └── ExternalAdapters.cs          IBalanceSource, IDeliveryJobsSource, IDebtsSource, IStorySource
├── Integrations/
│   ├── BankSummaryService.cs        экран «Банк» (только чтение)
│   ├── WorkSummaryService.cs        экран «Работа» (только чтение)
│   ├── StorySummaryService.cs       сюжетные записи (только чтение)
│   └── PhoneFacade.cs               сборка всех сервисов
├── Tests/                           90 тестов + раннер + NUnit-обёртка + Console.csproj
├── README.md
└── INTEGRATION.md                   инструкция для Codex
```

## Ключевые правила

* **Все ожидаемые ошибки — коды** (`PhoneResultCode`), исключения только для ошибок
  интеграции (`PhoneIntegrationException`) и валидации моделей в конструкторах.
* **Телефон ничего не меняет во внешних системах**: ни денег, ни заказов, ни долгов,
  ни сюжетных флагов. Источники данных — только для чтения.
* **Дедупликация уведомлений по ID**: повтор `Add` возвращает `DuplicateId` и не меняет
  существующее уведомление (ни текст, ни статус, ни счётчик приложения).
* **Идемпотентное прочтение**: повторный `MarkRead` — успех, счётчик не уходит в минус.
* **Счётчики защищены от переполнения** (`CounterOverflow`), суммарные счётчики насыщаются
  на `int.MaxValue`.
* **Сохранение игры**: `CreateSnapshot()` у сообщений и уведомлений возвращает
  иммутабельные DTO (защитная копия списков) без поведения; загрузка —
  `PhoneMessagesRestore.RestoreInto` / `PhoneNotificationsRestore.RestoreInto`
  с полной валидацией и атомарной заменой (аудит ТЗ №015).

## Проверки

**Не компилировалось и не запускалось** (в среде нет Unity/.NET SDK, установка
компиляторов запрещена). Инструментально проверено: синтаксис всех 34 `.cs`
парсером C# (tree-sitter, 0 ошибок), валидность asmdef-JSON, регистрация всех 79 тестов,
отсутствие ссылок на `Economy/` и `Jobs/`. Детали — в `INTEGRATION.md`.
Аудит ТЗ №015 (`../INTEGRATION_AUDIT.md`): добавлено восстановление из сохранения,
защитные копии снимков, устойчивость бейджей к сбою реестра и фильтрация null
из внешних источников; тестов стало 90.
