# LIFE FROM ZERO — ТЗ №015. Аудит трёх модулей и подготовка к Unity

Дата аудита: 2026-11-04. Ветка: `arena/458d270c-simgame` (база: `b6bbf25`, merge PR #1).
Аудит проверял: Economy, Jobs/Delivery, Phone.

> **Главное, что нужно знать до чтения.**
> 1. **Компиляции не было.** В среде нет `dotnet`, `csc`, `mcs`, `msbuild`, а установка
>    невозможна: исходящая сеть ограничена `github.com`, `registry.npmjs.org`, `pypi.org`;
>    там нет рабочих C#-компиляторов под Linux (в npm есть только обёртки над .NET Framework
>    4.x и `dotnet-sdk` 2.0, которые на Linux не запускаются; .NET SDK тянется с `dot.net`
>    и `packages.microsoft.com`, а они недоступны). Ниже везде, где написано «проверено»,
>    имеется в виду **статическая проверка**, а не сборка.
> 2. **Тесты не запускались.** 163 теста зарегистрированы и проверены статически
>    (регистрация, наличие утверждений, отсутствие дублей), но ни один не исполнялся.
> 3. Поэтому **«всё работает» здесь не утверждается**. Утверждается: код синтаксически
>    разбирается, контракты согласованы по статическому анализу, найденные дефекты
>    исправлены и покрыты тестами, а список того, что нужно проверить в Unity, — ниже.
> 4. Известные коммиты `1173590` (Economy), `2257314` (Delivery), `8637ebd` (Phone)
>    **в этом репозитории отсутствуют**: история здесь — один merge-коммит от
>    PR #1 (squash/импорт), все файлы трёх модулей добавлены одним коммитом.
>    Проверить принадлежность файлов конкретным коммитам невозможно; проверена
>    целостность рабочего дерева.

---

## 1. Структура трёх модулей

```
Economy/                             сборка LifeFromZero.Economy      (noEngineReferences)
├── Economy.asmdef
├── Core/    Money.cs, EconomyResultCode.cs, IWalletAdapter.cs (+IInventoryAdapter), SafeCall.cs
├── Shop/    ProductType.cs, Product.cs, ShopCatalog.cs, PurchaseRequest.cs,
│            PurchaseResult.cs (+IShopService, ShopServiceException), ShopService.cs
├── Tests/   Economy.Tests.asmdef, ShopServiceTests.cs, MiniTestRunner.cs, Program.cs,
│            Console.csproj, EconomyNUnitTests.cs, Fakes/FakeAdapters.cs
├── README.md, INTEGRATION.md

Jobs/
├── Delivery/  Jobs.Delivery.asmdef, DeliveryPoint.cs, DeliveryOrder.cs,
│              DeliveryOrderState.cs (+DeliveryResultCode), DeliveryStatus.cs
│              (+DeliveryResult, DeliveryServiceException), Adapters.cs, SafeAdapter.cs,
│              IDeliveryService.cs, DeliveryService.cs
├── Tests/     Jobs.Tests.asmdef, DeliveryServiceTests.cs, MiniTestRunner.cs, Program.cs,
│              Console.csproj, JobsNUnitTests.cs, Fakes/FakeAdapters.cs
└── README.md, INTEGRATION.md

Phone/                             сборка LifeFromZero.Phone        (noEngineReferences)
├── Phone.asmdef
├── Core/          PhoneResult.cs, GameMinutes.cs, PhoneClockReader.cs, IPhoneClock.cs,
│                  PhoneId.cs, IUnreadCounter.cs
├── Apps/          PhoneApp.cs (+PhoneAppCategory), PhoneAppRegistry.cs
├── Messages/      PhoneMessage.cs, PhoneConversation.cs, PhoneMessageService.cs,
│                  PhoneMessagesSnapshot.cs, PhoneInbox.cs, PhoneMessagesRestore.cs  ← новое
├── Contacts/      PhoneContact.cs, PhoneBook.cs
├── Notifications/ PhoneNotification.cs, PhoneNotificationService.cs,
│                  PhoneNotificationsSnapshot.cs, PhoneNotificationHub.cs,
│                  ExternalAdapters.cs, PhoneNotificationsRestore.cs              ← новое
├── Integrations/  BankSummaryService.cs, WorkSummaryService.cs, StorySummaryService.cs,
│                  PhoneFacade.cs
├── Tests/         Phone.Tests.asmdef, PhoneTestSuite.cs, PhoneMessageTests.cs,
│                  PhoneNotificationTests.cs, PhoneAppRegistryTests.cs,
│                  PhoneContactTests.cs, PhoneIntegrationTests.cs, MiniTestRunner.cs,
│                  Program.cs, Console.csproj, PhoneNUnitTests.cs, Fakes/PhoneFakes.cs
└── README.md, INTEGRATION.md
```

Статистика: **64 `.cs`**, **6 `.asmdef`**, **3 `.csproj`**, **8 `.md`** (+ этот документ),
120 объявленных типов, 163 зарегистрированных теста.

`README.md` есть у всех трёх модулей; `INTEGRATION.md` — у Economy, Jobs и Phone.

### Проверки целостности Git

| Проверка | Результат |
|---|---|
| Ветка | `arena/458d270c-simgame` (переключать запрещено — не переключалась) |
| История | один коммит `b6bbf25` (merge PR #1); коммиты `1173590`, `2257314`, `8637ebd` отсутствуют |
| Потерянные файлы | не найдено: все файлы, на которые ссылаются документы, существуют (кроме путей `Assets/...`, которые описывают будущее размещение) |
| Случайные изменения прошлых модулей | в этом коммите исправлений — только 3 модуля и их документы; ничего вне `Economy/`, `Jobs/`, `Phone/` + `README.md` корня не тронуто |
| История переписана | нет (новый коммит поверх `b6bbf25`) |
| `.asmdef` | 6 файлов, JSON валиден, граф ссылок корректен (см. §2) |

Замечание по истории: так как три «известных коммита» в репозитории отсутствуют,
утверждение «модули пришли отдельными коммитами» документально не подтверждается.
На код это не влияет, но Codex не должен искать эти хеши.

---

## 2. Проверенные зависимости

### 2.1 Что реально запускалось

Написаны два инструмента **вне репозитория** (в `/tmp`, в Git не попадают):

1. **`parse.js`** — разбор всех `.cs` через tree-sitter-c-sharp 0.23.5 (wasm).
   Результат: **64 файла, 0 узлов `ERROR`/`missing`**. Это проверка синтаксиса, **не сборка**.
2. **`check.js`** — статический анализатор по исходникам. Проверяет:
   * соответствие `namespace` каталогу (таблица в §1);
   * разрешимость каждого `using` (включая проверку, что namespace не «пустой» → CS0246);
   * отсутствие `using` другого модуля внутри модуля (изоляция);
   * отсутствие Unity API в модулях с `noEngineReferences: true`;
   * дубли типов (namespace + имя + арность) внутри сборки;
   * разрешимость типов в позициях `new`, generic-аргументов, `catch`, базовых типов;
   * арность конструкторов (`new X(a,b)` против объявленных);
   * существование членов: `Type.Member(...)` и `variable.Member(...)` (по объявленным
     типам переменных);
   * реализацию интерфейсов: наличие требуемых членов и совпадение арности методов;
   * C# 9: нет file-scoped namespace, `record struct`, `global using`, `required`, raw strings,
     `static abstract`, `scoped`, `file`-типов;
   * корректность asmdef: ссылки существуют, модули не ссылаются друг на друга,
     тестовые сборки Editor-only + `UNITY_INCLUDE_TESTS` + `nunit.framework.dll` +
     `overrideReferences` + `autoReferenced:false`;
   * что каждый `.cs` накрыт ровно одной asmdef;
   * регистрацию тестов: каждый `suite.Add("...", Method)` указывает на существующий
     `private static void Method()`, нет дублей имён/методов, каждый тест содержит
     утверждение (`MiniTestRunner.*`/`Assert.*`), нет `IsTrue(true)`.

**Проверка самого анализатора (mutation testing).** Чтобы не полагаться на «ноль ошибок»
как на самоцель, в копию репозитория (`/tmp/mut`, вне Git) были внесены 5 заведомых
дефектов: (1) вызов экземплярного метода из `static` (CS0120), (2) переименованный член
интерфейса, (3) `using` на несуществующий namespace (CS0246), (4) неверная арность
конструктора, (5) продублированная регистрация теста. Анализатор поймал 4 из 5 сразу;
пропущенный случай (переименованный член интерфейса «удовлетворял» сам себя) показал
реальную дыру в проверке реализации интерфейсов — она исправлена (учитываются только
собственные члены класса). После исправления ловятся все 5. Это оценка **покрытия
анализатора**, а не сборки.

Итог последнего прогона: `ERRORS (1)` — единственная запись:
`Phone/Notifications/ExternalAdapters.cs: namespace ["LifeFromZero.Phone.Integrations"] (folder suggests "LifeFromZero.Phone.Notifications")`.
Это **не дефект сборки** (namespace и файл лежат в одной сборке `LifeFromZero.Phone`), а
косметическое расхождение каталога и namespace: файл содержит read-only интерфейсы-адаптеры
к внешним системам и логически относится к `Integrations`. Решение: **оставить как есть**,
чтобы не ломать публичные имена (`LifeFromZero.Phone.Integrations.IBalanceSource` и др.)
и не переписывать рабочий код. Зафиксировано в §5.

### 2.2 Граф сборок

```
LifeFromZero.Economy            references: []                     noEngineReferences: true
LifeFromZero.Jobs.Delivery      references: []                     noEngineReferences: true
LifeFromZero.Phone              references: []                     noEngineReferences: true
LifeFromZero.Economy.Tests      → LifeFromZero.Economy, UnityEngine.TestRunner,
                                  UnityEditor.TestRunner (Editor, UNITY_INCLUDE_TESTS)
LifeFromZero.Jobs.Tests         → LifeFromZero.Jobs.Delivery, UnityEngine.TestRunner,
                                  UnityEditor.TestRunner (Editor, UNITY_INCLUDE_TESTS)
LifeFromZero.Phone.Tests        → LifeFromZero.Phone, UnityEngine.TestRunner,
                                  UnityEditor.TestRunner (Editor, UNITY_INCLUDE_TESTS)
```

Три модуля **не ссылаются друг на друга** ни в asmdef, ни в `using` — проверено статически.
Прямых зависимостей от Unity в модулях нет.

Одно замечание для Unity: тестовые сборки лежат **внутри** папок модулей
(`Economy/Tests`, `Jobs/Tests`, `Phone/Tests`) и ссылаются на «родительскую» сборку.
Unity допускает вложенные asmdef, но при импорте стоит проверить, что
`Economy.Tests` не попала в `LifeFromZero.Economy` (asmdef-файл должен «отрезать»
подпапку). Это надо глазами проверить в Unity (§10).

---

## 3. Найденные дефекты

Классификация: **исправлен** — изменён код, **риск** — исправление требует Unity/решения
Codex, **допущение** — сознательное ограничение, задокументировано.

### 3.1 Economy

| № | Дефект | Серьёзность | Статус |
|---|---|---|---|
| E-1 | **Потеря денег при неудачной компенсации.** Если `TryAdd` вернул `false`, а `TryRefund` — тоже `false` (или бросил исключение), сумма оставалась списанной **навсегда**: ни повторный `Purchase`, ни какой-либо другой путь не пытались вернуть деньги. Игрок терял 50–220 ₽ за один сбой кошелька; тест `RefundFailureReportsInternalError` это «закреплял» как норму (проверял `950` на балансе). | критическая (потеря денег) | **исправлен** |
| E-2 | **Нет блокировки покупок при незавершённой компенсации.** Данные кошелька и инвентаря могли разойтись на произвольное число покупок; повторные покупки только увеличивали разрыв. | высокая | **исправлен** (код `PendingRefund`) |
| E-3 | Тест `RefundFailureReportsInternalError` фиксировал дефект как ожидаемое поведение («деньги не вернулись — случай требует сверки»), т.е. регрессия не ловилась. | средняя | **исправлен** (тест 1 дополнен, добавлены 5 регрессионных) |
| E-4 | `IShopService.PendingRefundCount` и `RetryPendingRefunds` отсутствовали — UI не мог узнать о расхождении. | средняя | **исправлен** (добавлено в интерфейс, расширение только в сторону) |
| E-5 | Адаптер кошелька вызывается до проверки `CanApprove`? — **нет**, порядок верный (проверка места → списание → выдача). Перепроверено. | — | дефекта нет |
| E-6 | Переполнение `long` в `Money.TryMultiply` и `int` в `Product.TryGetTotalUnits` — защита есть, включая `Rubles == 0` (иначе деление на 0). | — | дефекта нет |
| E-7 | Отрицательные и нулевые количества, `null`/пробельный `ProductId`, дубли ID в каталоге — отклоняются до любых мутаций. | — | дефекта нет |
| E-8 | Если адаптер инвентаря **нарушает контракт** и добавляет предметы, вернув `false`, магазин не снимает предметы (осознанно: иначе можно уничтожить уже лежащие у игрока). Дубль предметов в этом случае возможен. | риск | **риск** §5 |

### 3.2 Jobs/Delivery

| № | Дефект | Серьёзность | Статус |
|---|---|---|---|
| D-1 | **Побочный эффект при отказе.** `Accept` проверял срок **до** проверки «уже есть активный заказ». Попытка взять второй заказ у Available-заказа с истёкшим сроком возвращала `DeadlineExpired` **и переводила этот заказ в `Failed`**. То есть операция, которая не должна ничего менять, меняла состояние заказа, к которому не относится; причина отказа тоже подменялась. | высокая (ложный провал заказа) | **исправлен** |
| D-2 | **«Зависший» слот активного заказа.** `_activeOrderId` — отдельное поле, которое не сверялось с состояниями заказов. Если после ручной правки состояния/загрузки сохранения оно указывало на закрытый заказ, `Accept` **навсегда** отвечал `AlreadyHasActiveOrder` — игрок не мог взять ни один заказ. Симметрично: заказ в `Accepted`/`PickedUp`, не записанный в слот, позволял обойти правило «один активный заказ». | высокая (блокировка прогресса) | **исправлен** (слот выводится из состояний, `FindActiveEntry`/`SyncActiveSlot`) |
| D-3 | `GetStatus` не синхронизировал слот с реальными состояниями. | низкая | **исправлен** |
| D-4 | `Refresh`/`Complete`/`PickUp`/`Cancel`/`Abandon` однократные выплаты, проверка точки, просрочка, повторные вызовы — перепроверены по коду и по тестам, дефектов не найдено. | — | дефекта нет |
| D-5 | Скачки игрового времени: `now > Deadline` — просрочка; `NaN`/`Infinity` от часов считаются отказом адаптера. Проверено тестами на `1e18` и на границе `now == deadline`. | — | дефекта нет |
| D-6 | Два «активных» заказа, появившихся извне, не диагностируются отдельно: `Accept` вернёт `AlreadyHasActiveOrder` и оставит оба; слот будет указывать на первый по порядку регистрации. Плата за восстановление из сохранения не реализована (модуль в памяти). | риск | **риск** §5 |

### 3.3 Phone

| № | Дефект | Серьёзность | Статус |
|---|---|---|---|
| P-1 | **Восстановление из сохранения отсутствовало.** Снимки создавались, но загрузить их можно было только по одному элементу через `ReceiveFromContact`, что теряло время, направление, статус прочтения и ID, и создавало новые ID. | высокая (потеря данных при загрузке) | **исправлен, аддитивно** (`PhoneMessagesRestore`, `PhoneNotificationsRestore`) |
| P-2 | **Снимки не защищали от мутации вызывающим кодом**: `PhoneMessagesSnapshot`/`PhoneConversationSnapshot`/`PhoneNotificationsSnapshot` сохраняли переданный список как есть (`messages ?? ...`). Сохранение могло «поехать» после записи. | средняя | **исправлен** (защитная копия) |
| P-3 | **Сбой реестра приложений ломал `Push`.** `PhoneNotificationHub.Push` вызывал `_apps.AddUnread(...)` без `try/catch`: кастомный реестр (адаптер Codex) мог бросить исключение — уведомление уже создано, но вызывающий код получал исключение и мог повторить `Push` (двойной бейдж). Комментарий «результат намеренно не пробрасывается» не соответствовал коду. | средняя | **исправлен** (`FailSafeAddUnread`) |
| P-4 | **`null`-элементы из внешних источников попадали в UI.** `BankSummaryService`/`WorkSummaryService` копировали массив адаптера как есть: `Debts[i] == null` → NRE в UI. | средняя | **исправлен** (фильтрация `null`) |
| P-5 | Дубли ID, повторные уведомления, повторный `MarkRead`, отрицательный счётчик, переполнение `int`, пустые строки, `NaN`/отрицательное игровое время, ошибки адаптеров — перепроверены, дефектов не найдено. | — | дефекта нет |
| P-6 | Отрицательный баланс: `BankSummaryService` помечает его как недоступный (`BalanceAvailable = false`). Осознанно (телефон не показывает долг как деньги), но для будущей системы долгов стоит решить отдельно. | допущение | **допущение** §5 |
| P-7 | Автоматической синхронизации `UnreadCount` приложения с непрочитанными сообщениями нет: бейдж растёт только через `IPhoneNotifier.Push`. Если NPC кладёт сообщение через `Inbox.Deliver`, бейдж «Сообщения» не изменится. | допущение (задокументировано) | **допущение** §5 |
| P-8 | Отдельный `PhoneUnreadSync`/`PhoneSaveService` (агрегат «сообщения + уведомления + бейджи + контакты») в ядро **не внедрялся**: это изменение архитектуры, а не исправление. Описан в §9. | риск/предложение | **предложение** §9 |

### 3.4 Мелочи, найденные при чтении

* `Economy/Tests/ShopServiceTests.cs`: опечатка в тексте теста (`AreEqual(1, purchaseResult...)`) не влияет на поведение — не трогал.
* `Jobs/Delivery/DeliveryService.cs`: в исходном тексте был обрезанный комментарий
  (`Повторное завершени` → строка в §6 исходной версии) — заменён при правке.
* `Phone/Tests/PhoneNotificationTests.cs`: `" шесть типов по ТЗ"` — лишний пробел в сообщении.
* В репозитории нет `.gitignore` и `README.md` корня содержит одну строку (`# SimGame`).

---

## 4. Исправленные дефекты (что именно изменено)

| Файл | Изменение | Тесты |
|---|---|---|
| `Economy/Core/EconomyResultCode.cs` | новый код `PendingRefund = 6` (в конец, существующие значения не менялись) | косвенно |
| `Economy/Shop/PurchaseResult.cs` | в `IShopService` добавлены `int PendingRefundCount` и `bool RetryPendingRefunds(IWalletAdapter)` | — |
| `Economy/Shop/ShopService.cs` | журнал незавершённых возвратов `_pendingRefunds`; `FlushPendingRefunds`/`TryRefund`/`RetryPendingRefunds`; в начале `Purchase` — закрытие долга и блокировка кодом `PendingRefund`; `Compensate` стал нестатическим и ведёт журнал | `PendingRefundIsRetried`, `RefundExceptionIsJournaled`, `PendingRefundBlocksPurchases`, `PendingRefundIsClosedBeforePurchase`, `NoItemsWithoutMoney`, `RefundFailureReportsInternalError` |
| `Jobs/Delivery/DeliveryService.cs` | `Accept`: порядок проверок «состояние → активный заказ → просрочка»; слот выводится из состояний (`FindActiveEntry`, `SyncActiveSlot`); `GetStatus` синхронизирует слот; обновлены XML-комментарии | `SecondAcceptAttemptDoesNotMutateRequestedOrder`, `RepeatedAccept`, `ActiveOrderDoesNotMaskExpiry`, `StaleActiveSlotDoesNotLockPlayer` |
| `Phone/Messages/PhoneMessagesSnapshot.cs` | защитная копия списков в конструкторах | `SnapshotIsDefensiveCopy` |
| `Phone/Messages/PhoneMessagesRestore.cs` *(новый)* | `Validate` + `RestoreInto` с полной валидацией и атомарной заменой | `RestoreFromSnapshot`, `RestoreKeepsIdSpace`, `RestoreEmptySnapshot`, `RestoreRejectsDuplicateIds`, `RestoreRejectsBrokenSnapshot` |
| `Phone/Messages/PhoneMessageService.cs` | `internal ReplaceAll(...)` — замена состояния после валидации, восстановление индекса ID сообщений | там же |
| `Phone/Notifications/PhoneNotificationsSnapshot.cs` | защитная копия списка | `SnapshotIsDefensiveCopy` (уведомления) |
| `Phone/Notifications/PhoneNotificationsRestore.cs` *(новый)* | `Validate` + `RestoreInto`, проверка известности типа уведомления | `RestoreFromSnapshot`, `RestoreRejectsBrokenSnapshot` |
| `Phone/Notifications/PhoneNotificationService.cs` | `internal ReplaceAll(...)` | там же |
| `Phone/Notifications/PhoneNotificationHub.cs` | `FailSafeAddUnread` (исключение реестра не всплывает), `using System` | `HubBadgeFailureKeepsSingleNotification` |
| `Phone/Integrations/BankSummaryService.cs` | `RemoveNulls` для долгов | `AdaptersReturnNullEntries` |
| `Phone/Integrations/WorkSummaryService.cs` | `RemoveNulls` для заказов | `AdaptersReturnNullEntries` |
| `Jobs/Tests/Fakes`, `Economy/Tests/Fakes` | `RefundFailuresRemaining`, `RefundThrowsRemaining` | — |

**Публичные контракты:** ни один существующий член не удалён и не переименован.
Добавления: `PendingRefund` (enum, в конец), `PendingRefundCount`, `RetryPendingRefunds`,
два новых класса восстановления, три новых тестовых класса-фикстуры. `internal ReplaceAll`
не расширяет публичную поверхность.

**Что сознательно НЕ сделано** (чтобы не переписывать архитектуру):
* не добавлены `PhoneUnreadSync`, `PhoneSaveService`, общий `PhoneSnapshot` (см. §9);
* не менялись `Call`-сигнатуры сервисов и порядок шагов покупки, кроме необходимого;
* не вводились ссылки между модулями;
* файлы не перемещались (кроме новых), `ExternalAdapters.cs` остался на месте.

---

## 5. Непроверенные риски

Все пункты — то, что **нельзя** подтвердить без Unity/компилятора или без решения Codex.

| № | Риск | Как проверить / что делать |
|---|---|---|
| R-1 | **Компиляция не выполнялась.** Возможны ошибки, которые ловит только компилятор: неоднозначность имён, неявные преобразования, порядок объявлений в `#if`, конфликт `TestSuite`/`TestCase`/`TestRunResult`/`MiniTestRunner` при компиляции тестов **вместе** с модулем (в `Console.csproj` тесты и модуль — одна сборка; в Unity — разные). | Первая же сборка в Unity или `dotnet build` на машине Codex. Ожидаемые предупреждения: `nullable` выключен, `IReadOnlyList<T>` из `System.Collections.Generic` — нормально. |
| R-2 | **163 теста не исполнялись.** Утверждения выверены чтением и повторным выводом вручную; арифметика и состояния перепроверены, но фактический прогон — за Codex. Два места, где легко ошибиться (и где ошибки были найдены и исправлены при аудите): количество покупок и число вызовов реестра бейджей. | `dotnet run --project {Economy,Jobs,Phone}/Tests/Console.csproj` или Test Runner. Ожидается `Провалено: 0`. |
| R-3 | **Ссылки asmdef на Unity-сборки** (`UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `nunit.framework.dll`) не сверены с реальным проектом Unity 6000.3.27f1. | После импорта: если Test Runner не видит тесты — сверить имена ссылок в `*_Tests.asmdef`. |
| R-4 | **Вложенные тестовые asmdef** внутри папок модулей: Unity должен создавать подпапку как отдельную сборку. | В Unity проверить `Assembly-CSharp` / `LifeFromZero.Economy` — файлы из `Tests/` не должны попадать в модуль. |
| R-5 | **Нарушение контракта адаптером инвентаря** (добавил предметы и вернул `false`): дубль предметов. Магазин не снимает предметы наугад. | Адаптер `FoodInventory` обязан: `TryAdd` → `true` при успехе, `false` **без** мутации. Проверить в первой интеграции. |
| R-6 | **Два активных заказа**, появившихся из внешнего источника (правка состояния, загрузка сейва): слот указывает на первый по регистрации, второй не диагностируется. | Для будущего сохранения заказов — добавить диагностический метод (`GetConflictingActiveOrders`) отдельным ТЗ, не сейчас. |
| R-7 | **Точка-семантика времени.** `DeadlineGameMinutes` — абсолютные игровые минуты; адаптер часов должен отдавать именно минуты с начала игры, монотонно. Если GameClock отдаёт `DateTime`/«часы:минуты» — перевод в адаптере, иначе просрочки будут ложными. | См. §8, `Jobs/Integration/GameClockAdapter`. |
| R-8 | **Бейдж приложения не связан с сообщениями** (`Inbox.Deliver` не увеличивает счётчик «Сообщения»). | Решение UI/интеграции: вызывать `Apps.AddUnread("messages", 1)` после доставки либо ждать §9. |
| R-9 | **Культура/локаль.** Все сравнения ID — `StringComparison.Ordinal`; чисел — `InvariantCulture`. Риск только при собственном коде UI. | Не требуется. |
| R-10 | **Потокобезопасности нет** (по дизайну): вызовы из главного потока Unity. | Не вызывать из `Task`/фоновых потоков. |
| R-11 | **Unity-сериализация снимков.** `GameMinutes` — `readonly struct` с приватными полями и свойством `IsValid`; `JsonUtility` его **не** сериализует корректно (readonly-поля без `[SerializeField]`). Снимки — обычные C#-классы `sealed` со свойствами только для чтения: `JsonUtility` тоже не подходит. | Использовать `System.Text.Json`/собственный DTO на стороне Codex, либо писать DTO с публичными полями. Описано в §9. |
| R-12 | `Phone/Notifications/ExternalAdapters.cs` лежит не в той папке относительно namespace (косметика, см. §2.1). | Не исправлять, если не планируется рефакторинг имён. |

---

## 6. Результаты компиляции

**Компиляция не выполнялась. Компилятор недоступен.**

Проверено фактически:

| Инструмент | Наличие | Результат |
|---|---|---|
| `dotnet` | нет (`command not found`) | — |
| `csc` | нет | — |
| `mcs` | нет | — |
| `msbuild` | нет | — |
| `mono` | нет | — |
| установка из сети | невозможна: `archive.ubuntu.com`, `packages.microsoft.com`, `dot.net`, `nuget.org` закрыты (HTTP 000); доступны только `github.com`, `registry.npmjs.org`, `pypi.org`; в npm нет рабочего C#-компилятора под Linux | — |
| tree-sitter-c-sharp (разбор синтаксиса) | **есть** | 64 файла, 0 синтаксических ошибок |
| собственный статический анализатор | **есть** | 1 «ошибка» — косметическое несоответствие namespace каталогу (§2.1), дефектов сборки не найдено |

Что именно проверено статически (см. §2.1): namespace, `using`, доступность типов,
generic-арность, сигнатуры конструкторов и членов, реализация интерфейсов,
asmdef-зависимости, отсутствие Unity API, отсутствие межмодульных ссылок, C# 9-совместимость
(нет конструкций C# 10+).

Итоговая формулировка, которую можно использовать: **«синтаксис и контракты проверены
статически; сборка не выполнялась»**.

---

## 7. Результаты тестов

| Модуль | Было заявлено | Фактически зарегистрировано | Добавлено при аудите |
|---|---|---|---|
| Economy | 25 | **33** | 8 |
| Jobs/Delivery | 31 | **40** | 9 |
| Phone | 79 | **90** | 11 |
| **Итого** | **135** | **163** | **28** |

Проверено статически:
* все 163 теста зарегистрированы (`suite.Add(...)`) и указывают на существующие методы;
* дублей имён и методов нет;
* каждый тест содержит хотя бы одно утверждение;
* нет утверждений-тавтологий (`IsTrue(true)`) и пустых `catch`;
* у каждого модуля есть тесты на отказ адаптеров, повторные операции и крайние значения.

**Тесты не запускались** (нет компилятора и рантайма). Число «163» — это число
зарегистрированных кейсов, а не число успешных прогонов.

Новые тесты (регрессия на найденные дефекты и закрытие пробелов):

* **Economy (8):** `PendingRefundIsRetried`, `RefundExceptionIsJournaled`,
  `PendingRefundBlocksPurchases`, `PendingRefundIsClosedBeforePurchase`,
  `NoItemsWithoutMoney`, `BalanceReadException`, `CatalogThrows`, `ManyRepeatedPurchases`.
  Плюс изменён `RefundFailureReportsInternalError` — теперь он проверяет попадание долга
  в журнал, а не «деньги исчезли».
* **Delivery (9):** `SecondAcceptAttemptDoesNotMutateRequestedOrder` (E/D-1),
  `StaleActiveSlotDoesNotLockPlayer` (D-2), `ActiveOrderDoesNotMaskExpiry`,
  `HugeTimeJump`, `DeadlineBoundary`, `RepeatedCancel`, `AbandonOnlyWhileAccepted`,
  `RefreshAfterCompletion`, `PayoutOnceUnderMixedCalls`.
* **Phone (11):** восстановление сообщений (5), восстановление уведомлений (2),
  защитные копии снимков (2), сбой реестра бейджей (1), `null`-элементы адаптеров (1).

---

## 8. Порядок подключения в Unity

Рекомендуемый порядок — **Economy → Delivery → Phone** (у каждого следующего модуля
адаптеры зависят от предыдущих данных, но не от их кода).

0. **Подготовка проекта** (Unity 6000.3.27f1):
   * `Edit → Project Settings → Player → Other Settings → Api Compatibility Level`:
     `.NET Standard 2.1` (модули используют только BCL).
   * Убедиться, что установлен `com.unity.test-framework` (Window → Package Manager).
   * Убедиться, что в проекте есть `MoneyWallet`, `FoodInventory`, `GameClock`
     и система перемещения — модули их **не** содержат.
1. **Economy**
   1. Скопировать `Economy/` → `Assets/Economy/` (сохранив структуру).
   2. Проверить, что `Economy.asmdef` создал сборку `LifeFromZero.Economy`
      и что в неё **не** попали `Tests/`.
   3. Создать `Assets/Economy/Integration/` и положить туда адаптеры (`MoneyWalletAdapter`,
      `FoodInventoryAdapter`) — примеры в §9.1. Своя asmdef для `Integration` нужна только
      если проект так устроен; по умолчанию — `Assembly-CSharp`.
   4. Собрать: `Window → General → Test Runner → EditMode → Run All`
      (или `dotnet run --project Assets/Economy/Tests/Console.csproj` вне Unity).
   5. Ручная проверка: покупка при нехватке денег, при полном инвентаре
      (деньги не должны списываться), повторные покупки.
2. **Delivery**
   1. Скопировать `Jobs/` → `Assets/Jobs/`.
   2. Адаптеры: `IGameClockAdapter` (минуты с начала игры), `IPayoutAdapter` (начисление),
      `ILocationProvider` (id текущей локации).
   3. Test Runner: `LifeFromZero.Jobs.Tests`.
   4. Ручная проверка: принять заказ, получить груз не в той точке (отказ),
      доставка в срок, доставка после срока (`Failed` без денег), повторный `Complete`.
   5. Настроить периодический `Refresh(ActiveOrderId, clock)` — иначе просрочка
      фиксируется только при действии игрока.
3. **Phone**
   1. Скопировать `Phone/` → `Assets/Phone/`.
   2. Адаптеры: `IPhoneClock`, `IBalanceSource`, `IDeliveryJobsSource`, `IDebtsSource`,
      `IStorySource` (последние два — заглушки до появления систем).
   3. Собрать `PhoneFacade` (пример в §9.3), не делать его Singleton.
   4. Test Runner: `LifeFromZero.Phone.Tests`.
   5. UI: бейджи берутся из `Apps.Get(id).UnreadCount`, лента — `Notifications.Notifications`,
      переписки — `Messages.GetHistory(contactId)`.
4. **Сохранение** (после трёх шагов): `CreateSnapshot()` → сериализация;
   загрузка → `PhoneMessagesRestore.RestoreInto` / `PhoneNotificationsRestore.RestoreInto`
   (см. §9.4). Для Economy/Delivery сохранения пока нет (модули в памяти) — это отдельное ТЗ.

---

## 9. Необходимые адаптеры

Примеры **не добавлены в репозиторий** (по ТЗ) — их пишет Codex, подставив реальные типы.
Все примеры — чистый C#, без `MonoBehaviour`, чтобы модули оставались независимыми.

### 9.1 Economy → `MoneyWallet` / `FoodInventory`

```csharp
// Assets/Economy/Integration/MoneyWalletAdapter.cs
using LifeFromZero.Economy.Core;

public sealed class MoneyWalletAdapter : IWalletAdapter
{
    private readonly MoneyWallet _wallet;                       // TODO(Codex): реальный тип

    public MoneyWalletAdapter(MoneyWallet wallet) { _wallet = wallet; }

    // Без исключений и побочных эффектов.
    public Money Balance => Money.FromRubles(_wallet.Amount);   // TODO(Codex)

    // Атомарно: либо вся сумма, либо ничего. false => баланс не изменён.
    public bool TrySpend(Money amount)                           // TODO(Codex)
    {
        if (_wallet.Amount < amount.Rubles) return false;
        _wallet.Add(-amount.Rubles);                             // если в проекте int — проверять переполнение
        return true;
    }

    // Возврат ровно той суммы. false => НЕ изменено (магазин повторит попытку позже).
    public bool TryRefund(Money amount)                          // TODO(Codex)
    {
        _wallet.Add(amount.Rubles);
        return true;
    }
}
```

```csharp
// Assets/Economy/Integration/FoodInventoryAdapter.cs
using LifeFromZero.Economy.Core;

public sealed class FoodInventoryAdapter : IInventoryAdapter
{
    private readonly FoodInventory _inventory;                   // TODO(Codex): реальный тип

    public FoodInventoryAdapter(FoodInventory inventory) { _inventory = inventory; }

    // Точная проверка места БЕЗ мутации.
    public bool CanApprove(string itemId, int quantity) { /* TODO(Codex) */ return false; }

    // true — добавлено ровно quantity; false — состояние НЕ изменено (важно!).
    public bool TryAdd(string itemId, int quantity) { /* TODO(Codex) */ return false; }
}
```

### 9.2 Delivery → `GameClock` / `MoneyWallet` / перемещение

```csharp
// Assets/Jobs/Integration/GameClockAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class GameClockAdapter : IGameClockAdapter
{
    private readonly GameClock _clock;                           // TODO(Codex): реальный тип
    public GameClockAdapter(GameClock clock) { _clock = clock; }

    // Минуты с начала игры; монотонно не убывают; без NaN/Infinity и без исключений.
    public double NowMinutes => _clock.TotalMinutes;             // TODO(Codex)
}
```

```csharp
// Assets/Jobs/Integration/MoneyWalletPayoutAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class MoneyWalletPayoutAdapter : IPayoutAdapter
{
    private readonly MoneyWallet _wallet;                        // TODO(Codex)
    public MoneyWalletPayoutAdapter(MoneyWallet wallet) { _wallet = wallet; }

    public bool TryCredit(long rubles)
    {
        if (rubles <= 0L) return false;
        // TODO(Codex): _wallet.Add((int)rubles) с проверкой переполнения.
        return false;
    }
}
```

```csharp
// Assets/Jobs/Integration/PlayerLocationAdapter.cs
using LifeFromZero.Jobs.Delivery;

public sealed class PlayerLocationAdapter : ILocationProvider
{
    private readonly IPlayerLocationService _player;             // TODO(Codex)

    public PlayerLocationAdapter(IPlayerLocationService player) { _player = player; }

    // Строка должна посимвольно совпадать с DeliveryPoint.Id.
    public string CurrentLocationId => _player.CurrentLocationId;
}
```

### 9.3 Phone → часы / кошелёк / Delivery / долги / сюжет

```csharp
// Assets/Phone/Integration/UnityGameClockAdapter.cs
using LifeFromZero.Phone.Core;

public sealed class UnityGameClockAdapter : IPhoneClock
{
    private readonly GameClock _clock;                           // TODO(Codex)
    public UnityGameClockAdapter(GameClock clock) { _clock = clock; }
    public double NowMinutes => _clock.TotalMinutes;             // та же шкала, что и в Delivery
}
```

```csharp
// Assets/Phone/Integration/MoneyWalletBalanceSource.cs
using LifeFromZero.Phone.Integrations;

public sealed class MoneyWalletBalanceSource : IBalanceSource
{
    private readonly MoneyWallet _wallet;                        // TODO(Codex)
    public MoneyWalletBalanceSource(MoneyWallet wallet) { _wallet = wallet; }

    public bool TryGetBalanceRubles(out long rubles)             // только чтение!
    {
        rubles = _wallet.Amount;                                 // TODO(Codex)
        return rubles >= 0L;
    }
}
```

```csharp
// Assets/Phone/Integration/DeliveryJobsSource.cs
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Phone.Integrations;

public sealed class DeliveryJobsSource : IDeliveryJobsSource
{
    private readonly IDeliveryService _delivery;                 // из LifeFromZero.Jobs.Delivery

    public DeliveryJobsSource(IDeliveryService delivery) { _delivery = delivery; }

    // Единственное место, где Phone видит Delivery. Модули при этом не ссылаются друг на друга.
    public bool TryGetOrders(out DeliveryJobDto[] orders)
    {
        var list = new System.Collections.Generic.List<DeliveryJobDto>();

        foreach (DeliveryOrder order in _delivery.Orders)
        {
            DeliveryResult status = _delivery.GetStatus(order.Id);
            list.Add(new DeliveryJobDto(
                order.Id,
                order.CargoTitle,
                order.PaymentRubles,
                MapPhase(status.Status.State)));
        }

        orders = list.ToArray();
        return true;
    }

    public bool TryGetActiveOrderId(out string orderId)
    {
        orderId = _delivery.ActiveOrderId;
        return true;
    }

    private static DeliveryJobPhase MapPhase(DeliveryOrderState state)
    {
        switch (state)
        {
            case DeliveryOrderState.Accepted:  return DeliveryJobPhase.Accepted;
            case DeliveryOrderState.PickedUp:  return DeliveryJobPhase.PickedUp;
            case DeliveryOrderState.Completed: return DeliveryJobPhase.Completed;
            case DeliveryOrderState.Failed:    return DeliveryJobPhase.Failed;
            case DeliveryOrderState.Cancelled: return DeliveryJobPhase.Cancelled;
            default:                           return DeliveryJobPhase.Available;
        }
    }
}
```

Баланс для телефона и выплаты для доставки берутся из **одного** `MoneyWallet` — двух
кошельков быть не должно; адаптеры `MoneyWalletAdapter` (Economy) и
`MoneyWalletPayoutAdapter`/`MoneyWalletBalanceSource` (Delivery/Phone) — тонкие обёртки
над одним и тем же объектом.

**Долги (на будущее):** `IDebtsSource.TryGetDebts(out DebtDto[])` уже есть, `BankSummaryService`
его читает, UI получает `BankSummary.Debts`/`DebtsAvailable`. Когда появится система долгов,
Codex пишет один адаптер `DebtsSource` — интерфейсы модуля менять не нужно.
Уведомления о долгах — `PhoneNotificationType.Debt` через `IPhoneNotifier.Push`.

### 9.4 Сборка телефона

```csharp
var phone = new PhoneFacade(
    PhoneAppRegistry.CreateDefault(),
    new PhoneMessageService(),
    new PhoneBook(),
    new PhoneNotificationService());

phone.Contacts.Add(new PhoneContact("anna", "Анна", "+7-900-000-00-01", true));
phone.Messages.StartConversation("anna");

// NPC пишет игроку (бейдж «Сообщения» при этом не растёт — см. R-8):
phone.Inbox.Deliver("anna", "Ты уже нашёл работу?", clock);

// Уведомление с бейджем приложения:
phone.Notifier.Push(
    "job-42-available", "jobs", PhoneNotificationType.Job,
    "Новый заказ", "Доставить хлеб в офис 12",
    "jobs", PhoneNotificationChannel.AppBadge, clock);
```

### 9.5 Сохранение и загрузка телефона (новое)

```csharp
// Сохранение: DTO без поведения, сериализуются чем угодно, кроме JsonUtility (см. R-11).
PhoneMessagesSnapshot      savedMessages      = phone.Messages.CreateSnapshot();
PhoneNotificationsSnapshot savedNotifications = phone.Notifications.CreateSnapshot();

// Загрузка: новый сервис (не Singleton), атомарная замена с валидацией.
var messages = new PhoneMessageService();
string error;

if (!PhoneMessagesRestore.RestoreInto(messages, savedMessages, out error).IsSuccess)
{
    Debug.LogWarning("Сохранение сообщений отклонено: " + error);   // сервис не изменён
}

var notifications = new PhoneNotificationService();

if (!PhoneNotificationsRestore.RestoreInto(notifications, savedNotifications, out error).IsSuccess)
{
    Debug.LogWarning("Сохранение уведомлений отклонено: " + error);
}

var phone = new PhoneFacade(
    PhoneAppRegistry.CreateDefault(), messages, new PhoneBook(), notifications);
```

Сериализация: рекомендую промежуточные DTO с публичными полями и `double minutes` +
`bool hasTime` (вместо `GameMinutes`/`GameMinutes.Unknown`) — пример:

```csharp
[System.Serializable]
public sealed class MessageSaveDto
{
    public string id;
    public string conversationId;
    public int direction;      // 0 = Incoming, 1 = Outgoing
    public string text;
    public bool hasTime;
    public double minutes;
    public bool isRead;
}
```

---

## 10. Минимальные проверки после импорта

Чек-лист Codex (по порядку, каждый пункт — минута):

1. **Компиляция.** `Assets` собирается без ошибок; в консоли нет `CS0246`/`CS0117`.
   Ожидаемые предупреждения: `nullable` не включён.
2. **Сборки.** В `Window → General → Test Runner` видны три ассембли `*_Tests`;
   файлы из `*/Tests/` не попали в модульные сборки.
3. **Тесты.** EditMode → `Run All` → 163 зелёных, 0 провалов.
   Если провал — смотреть имя теста: оно совпадает с регрессией из §7.
4. **Экономика.** Купить хлеб при балансе 100 → баланс 50, хлеб в инвентаре 1.
   Купить при полном инвентаре → деньги не изменились, код `InventoryRejected`.
   Купить 2 хлеба при балансе 50 → `InsufficientFunds`, состояние не изменилось.
5. **Деньги не дублируются.** 10 покупок подряд → ровно 10 предметов и −500 ₽.
6. **Доставка.** Принять заказ → `PickUp` не в пекарне → `WrongLocation` (состояние
   `Accepted`). Уйти в пекарню → `PickedUp`. Прийти в офис → `Complete` → выплата 1 раз.
   Повторить `Complete` → `InvalidState`, деньги не начислены второй раз.
7. **Просрочка.** Перевести игровые часы за `Deadline` → `Refresh` → `Failed`, денег нет;
   `ActiveOrderId == null`.
8. **Телефон.** `Push` с одним ID дважды → одно уведомление, бейдж 1.
9. **Сохранение.** Снимок → в JSON → загрузка в новый `PhoneMessageService` →
   история, направление, время и статус прочтения совпадают; `TotalUnread` совпадает.
   Битый снимок (например, дубль ID) → код `DuplicateId`, данные не тронуты.
10. **Время.** Убедиться, что `GameClock.TotalMinutes` в адаптерах Delivery и Phone —
    одна и та же шкала (иначе сроки и время сообщений будут расходиться).

---

## 11. Короткая инструкция для Codex

Чтобы не анализировать репозиторий заново:

* **Три независимые сборки**, между собой не ссылаются: `LifeFromZero.Economy`,
  `LifeFromZero.Jobs.Delivery`, `LifeFromZero.Phone`. Все `noEngineReferences: true`,
  чистый C# 9, без пакетов.
* **Никаких собственных кошельков/часов/инвентаря** внутри — только интерфейсы-адаптеры:
  Economy: `IWalletAdapter`, `IInventoryAdapter`; Delivery: `IGameClockAdapter`,
  `IPayoutAdapter`, `ILocationProvider`; Phone: `IPhoneClock`, `IBalanceSource`,
  `IDeliveryJobsSource`, `IDebtsSource`, `IStorySource`.
* **Адаптеры писать в `Assets/{Module}/Integration/`**, не в репозиторий модулей.
* **Точки входа:** `new ShopService(ShopCatalog.CreateDefault())`,
  `new DeliveryService()`, `new PhoneFacade(PhoneAppRegistry.CreateDefault(),
  new PhoneMessageService(), new PhoneBook(), new PhoneNotificationService())`.
* **Все отказы — коды** в результате (`PurchaseResult.Code`, `DeliveryResult.Code`,
  `PhoneResult.Code`); исключения — только для ошибок интеграции
  (`ShopServiceException`, `DeliveryServiceException`, `PhoneIntegrationException`).
* **Читать в первую очередь:** этот файл (разделы 3–5, 9) и §7 конкретного модуля в
  `Economy/INTEGRATION.md` / `Jobs/INTEGRATION.md` / `Phone/INTEGRATION.md`.
  В этих трёх документах устарели только два утверждения: «тестов 25/31/79»
  (теперь 33/40/90) и «нет восстановления из сохранения» (появилось, см. §9.5).
* **Что нельзя ломать:** порядок шагов покупки в `ShopService.Purchase`, инвариант
  «ровно одна выплата» в `DeliveryService.Complete`, идемпотентность `MarkRead`,
  правило «модули не ссылаются друг на друга».

---

## 12. Что осталось проверить (сводка)

1. Сборка в Unity 6000.3.27f1 и/или `dotnet build` — **не выполнялась**.
2. Прогон 163 тестов — **не выполнялся**.
3. Реальные сигнатуры `MoneyWallet`, `FoodInventory`, `GameClock`, системы перемещения —
   неизвестны, адаптеры пишет Codex по TODO в §9.
4. Имена asmdef-ссылок на Unity-сборки — не сверены.
5. Сериализация снимков (`GameMinutes` не дружит с `JsonUtility`, см. R-11).
6. Поведение UI при «время недоступно» (`GameMinutes.Unknown`) и «данные недоступны»
   (`BalanceAvailable = false`) — на стороне UI.
