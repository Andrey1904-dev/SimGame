# Паспорта систем LIFE FROM ZERO

Формат каждого раздела повторяет ТЗ №043 §3: назначение → статус → код →
архитектура → алгоритм → интеграции → данные → управление → установка →
проверки → риски → рекомендации.

Обозначения статуса:

* **РАБОТАЕТ** — логика проверена исполняемыми тестами вне Unity.
* **ЧАСТИЧНО** — работает часть, остальное требует хоста (MoneyWallet, сцены).
* **НЕ ПРОВЕРЕНО (Unity)** — код есть, исполнения в Unity не было ни разу.
* **ОТСУТСТВУЕТ** — нет кода.

---

## 1. Генератор квартала (`City/Quarter`)

**1. Назначение.** Строит математический план квартала: дома, подъезды, дворы,
парковки, деревья, фонари, точки доставки. Игрок этого не видит напрямую — план
превращается в объекты сцены в `City/UnityAdapter`.

**2. Статус.** **РАБОТАЕТ** — 92 теста, все проходят.

**3. Код.**
`City/Quarter/QuarterGenerator.cs` (1 856 строк, `QuarterGenerator.Generate(int seed)`),
`QuarterTypes.cs` (модели: `QuarterPlan`, `BuildingInfo`, `DeliveryPointInfo`,
`PickupPointInfo`, `CourtyardInfo`), `Housing.cs`, `BuildingVariants.cs`,
`QuarterMath.cs`, `QuarterGeneratorOptions.cs`.

**4. Архитектура.** Чистая функция: seed → детерминированный `QuarterPlan`.
Никакого `UnityEngine` (asmdef `noEngineReferences: true`). Внутри — вложенный
класс-строитель, который последовательно вызывает `BuildBuildings`,
`BuildCourtyards`, `BuildParking`, `BuildDeliveryPoints`, `BuildDecor`.

**5. Алгоритм (по шагам).**
1. `QuarterGenerator.Generate(seed)` создаёт `System.Random(seed)`.
2. По опциям выбирает раскладку (`quarter250` по умолчанию, `district750` — отдельно).
3. Расставляет 15 жилых домов по сетке с вариациями фасадов; Id домов — `bld-*`.
4. Для каждого дома создаёт подъезды, считает точки на земле (`DeliveryPointInfo`,
   Id вида `drop-0001`).
5. Добавляет парковки/дворы/декор, проверяет, что объекты не выходят за границы
   квартала (регрессия из `AuditTests`).
6. Возвращает неизменяемый `QuarterPlan`.

**6. Интеграции.** Вызывается из `CityBootstrap.Generate()` (Unity-адаптер) и из
92 тестов. Обратных вызовов нет — генератор никого не знает.

**7. Данные.** Вход: `int seed` + `QuarterGeneratorOptions`. Выход: `QuarterPlan`
в памяти. Ничего не пишет на диск, не хранит состояние между вызовами.

**8. Управление.** Нет (нет UI, нет клавиш).

**9. Установка.** Ничего не нужно в сцене: это библиотека.

**10. Проверки.** `City/Tests/*`: состав 15 домов на 11 seed, границы, уникальность
Id, детерминизм, подвал, стресс 118 seed, невозможная конфигурация. Сегодня:
**92 кейса, 0 провалов** (`TEST_RESULTS.md`).

**11. Риски.** (а) Параметры «офисного» района зашиты в код (`QuarterGenerator`),
их сложно менять без правки; (б) файл 1 856 строк — тяжело сопровождать;
(в) `district750` и `quarter250` живут в разных генераторах, дублируя логику дорог.

**12. Рекомендации.** Разнести `QuarterGenerator` на 3–4 partial-класса по фазам;
вынести конфигурацию раскладки в данные, а не в константы.

---

## 2. District750 (`City/Quarter/DistrictMap`)

**1. Назначение.** Карта района 750×750 м: 82 улицы, 59 перекрёстков, кварталы,
46 точек доставки, ПВЗ, квартира игрока №67.

**2. Статус.** **РАБОТАЕТ** (план), **НЕ ПРОВЕРЕНО (Unity)** (визуал).

**3. Код.** `DistrictMapGenerator.cs` (941 строка), `DistrictMapTypes.cs`.
Тесты: `City/Tests/DistrictMapTests.cs`, `DistrictPlanChecks.cs`.

**4. Архитектура.** Тот же принцип: seed → `QuarterPlan` с расширенными полями
(дороги, перекрёстки, зоны, декор). Дороги описываются как узлы+сегменты, а не
как меши.

**5. Алгоритм.** `Generate(seed)` → `BuildRoadGrid()` (сетка V{20,150,270,420,505,730}
× H{20,200,300,470,600,738}) → `BuildBlocks()` (10 многоквартирных, 21 зона) →
`BuildDeliveryPoints()` (Id `drop-<buildingId>-<entrance>`, тип
`residential/private/mall/bank/...`) → `BuildPickupPoint()` (ПВЗ, Id `drop-pvz-01`)
→ `BuildPlayerHome()` (`PickPlayerHomeId` → `bld-n3`, дверь (205;636.4),
выход (205;638.4)).

**6. Интеграции.** `CityBootstrap` (генерация), `QuarterUnityAdapter` (меши),
`PlayerHomeAnchor` (дверь/выход), а в PR #14 — `QuarterPlanAdapter` → карта UI.

**7. Данные.** `QuarterPlan` в памяти, детерминирован по seed. Внешний проверочный
скрипт `City/Tools/district_layout_check.py` печатает: 974 объекта, 46 доставок,
0 недостижимых.

**8. Управление.** Нет.

**9. Установка.** `CityBootstrap` на объекте сцены, `LayoutId = "district750"`.
**В репозитории этой сцены нет.**

**10. Проверки.** `DistrictMapTests` (детерминизм, seed не меняет геометрию, ПВЗ
единственный), `district_layout_check.py` — сегодня PASS.

**11. Риски.** Id точек доставки `drop-bld-n3-1` **не совпадают** с Id, которые
ожидает `Jobs.Delivery` (там произвольные `"bakery"`, `"office_12"`, см. §5).
Кроме того, точки доставки бывают в 47–75 м от ближайшей дороги (измерено в ТЗ
#042) — маршрут к ним возможен только с привязкой к ребру.

**12. Рекомендации.** Утвердить единый реестр Id между `DistrictMapGenerator` и
`Jobs.Delivery`; вынести радиус взаимодействия в данные.

---

## 3. Unity-адаптер города (`City/UnityAdapter`)

**1. Назначение.** Превращает `QuarterPlan` в объекты Unity: стены, полы, крыши,
двери, деревья, коллайдеры; управляет генерацией по seed.

**2. Статус.** **НЕ ПРОВЕРЕНО (Unity)**. Компиляция без Unity невозможна
(нужны `Material`, `Mesh`, `Quaternion`, `Instantiate`).

**3. Код.** `CityBootstrap.cs` (`Generate()`, `Clear()`, событие `PlanBuilt`,
`LastPlan`, `CityGenerationReport`), `QuarterUnityAdapter.cs` (1 000+ строк:
`Build(plan,parent)`, `ClearBuilt()`, `Plan`, `HasPlan`, `IQuarterPlanSource`,
кэш материалов, генерация мешей, `BasementDoor`), `City/Quarter/QuarterPlanSlot.cs`
(жизненный цикл плана, чистый C# — проверяется без Unity).

**4. Архитектура.** `CityBootstrap` — оркестратор (когда генерировать),
`QuarterUnityAdapter` — исполнитель (как строить). Оба — `MonoBehaviour`.
`AddColliders` управляет физикой.

**5. Алгоритм.** `CityBootstrap.Generate()` → `DistrictMapGenerator.Generate(seed)`
→ `QuarterUnityAdapter.Build(plan, transform)` → создание `GameObject`-ов и
`Mesh` → `PlanBuilt?.Invoke(plan)`.

**6. Интеграции.** Вызывает генератор; его вызывает `PlayerHomeAnchor`
(подписка на `PlanBuilt`), Editor-инструменты, а в PR #14 — `UiBootstrap`
через `IQuarterPlanSource`.

**7. Данные.** План в памяти; меши и материалы — объекты Unity, освобождаются в
`OnDestroy`/`ClearBuilt`.

**8. Управление.** Нет; инспектор: `Seed`, `BuildOnStart`, `AddColliders`,
`Prefab Overrides`.

**9. Установка.** Объект сцены с `CityBootstrap` + `QuarterUnityAdapter`
(создаёт `LifeFromZeroSetup`), `Seed = 1904`, `LayoutId = district750`.

**10. Проверки.** Тестов Unity-адаптера нет (нужен редактор). **Проверяется
чистая часть:** `QuarterPlanSourceTests` (6 тестов) — жизненный цикл плана;
они падают на исходном коде PR #14 и проходят после исправления. Итог по модулю:
98 проверок, 0 провалов.

**11. Риски.** ~~P0: `Build()` писал `_plan` до `ClearBuilt()`, который его
обнулял → `Plan`/`HasPlan` всегда `false`, карта пустая.~~ **Исправлено в
`786c7fc`.** Остаточные риски: меши/материалы создаются кодом и освобождаются
в `OnDestroy` (утечка при `Destroy` адаптера без `OnDestroy` — маловероятно);
`Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")` требует,
чтобы хотя бы один из шейдеров был включён в билд (проверить в Unity).

**12. Рекомендации.** План теперь хранится в `QuarterPlanSlot`, чей контракт
(`BeginBuild` = очистка + запись) закреплён тестами — при будущих правках
`Build()` порядок защищён. Остальное: вынести `Shader.Find` в настройку,
добавить EditMode-тест адаптера в Unity.

---

## 4. Квартира №67 и переходы (`City/UnityIntegration/Runtime`)

**1. Назначение.** Дверь подъезда дома `bld-n3` ведёт в сцену квартиры, выход
возвращает во двор.

**2. Статус.** **НЕ ПРОВЕРЕНО (Unity)**; сцены квартиры в репозитории нет.

**3. Код.** `PlayerHomeAnchor.cs` (план → точки `player.home.door` /
`player.home.exit`, `OpenApartment()`, `ReturnToDistrict()`),
`District750Link.cs` (`OpenDistrict()`, `ReturnToStart()`).

**4. Архитектура.** Пассивные компоненты: ни `Update`, ни триггеров, ни опроса
клавиш. Только публичные методы для `UnityEvent` существующей системы переходов —
осознанное решение, чтобы не конфликтовать с проектом.

**5. Алгоритм.** `CityBootstrap.PlanBuilt` → `PlayerHomeAnchor` создаёт две точки
по `plan.Homes`/`PickPlayerHomeId`. При вызове `OpenApartment()` грузит сцену по
имени из инспектора; если имя пустое — предупреждение и ничего.

**6. Интеграции.** Подписка на `CityBootstrap.PlanBuilt`; вызывается внешними
`UnityEvent`. Обратной связи нет.

**7. Данные.** Только имя сцены в инспекторе; состояние не хранится.

**8. Управление.** Внешнее событие (дверь/кнопка). Своих клавиш нет.

**9. Установка.** Компонент на объект сцены; `Apartment Scene Name` задаётся
вручную или через `Tools → Life From Zero → Integrate Into Game`.

**10. Проверки.** Нет. Поведение подтверждается только чтением кода.

**11. Риски.** Имя сцены квартиры — строка; если оно не задано, игрок «нажимает
дверь» и ничего не происходит (в логе предупреждение). В репозитории нет сцены
`Apartment67`, значит проверить возврат невозможно.

**12. Рекомендации.** Задать имена сцен в одном месте (ScriptableObject-конфиг),
чтобы не расходились между компонентами.

---

## 5. Доставка (`Jobs/Delivery`)

**1. Назначение.** Курьерские заказы: принять → забрать груз → доставить →
получить деньги один раз.

**2. Статус.** **РАБОТАЕТ** (сервис) — 55 тестов, 0 провалов. **ОТСУТСТВУЕТ**
(создание заказов из мира).

**3. Код.** `DeliveryService.cs` (`RegisterOrder`, `Accept`, `PickUp`, `Complete`,
`Cancel`, `Refresh`, `Abandon`, `GetStatus`), `DeliveryOrder.cs`,
`DeliveryOrderState.cs` (`Available/Accepted/PickedUp/Completed/Failed/Cancelled`),
`DeliverySnapshot.cs` (+валидатор), `Adapters.cs` (`IGameClockAdapter`,
`IPayoutAdapter`, `IIdempotentPayoutAdapter`, `ILocationProvider`).

**4. Архитектура.** Один активный заказ на игрока; все переходы — чистые функции
над словарём заказов; время, деньги и локация приходят адаптерами. Никакого
`UnityEngine`.

**5. Алгоритм.**
1. `RegisterOrder(order)` — только доступный заказ, дубликаты отклоняются.
2. `Accept(id, clock)` — отказ, если есть незакрытый активный заказ.
3. `PickUp(id, clock, location)` — только `Accepted`, только в точке получения,
   только в срок; иначе `WrongLocation`/`DeadlineExpired`.
4. `Complete(id, clock, location, payout)` — только `PickedUp`; выплата через
   `IPayoutAdapter` ровно один раз (`PayoutGranted`), при durable-адаптере — по
   `operationId`.
5. `Refresh(id, clock)` — просрочка → `Failed` без выплаты.

**6. Интеграции.** Вызывается телефоном (`WorkSummaryService`, read-only),
`UnityIntegrationKit.PayoutBridge`, `DeliveryPayout`; в PR #14 — `DeliveryServiceBridge`.
Сам вызывает только адаптеры.

**7. Данные.** `DeliverySnapshot` (заказы + счётчик выплат), валидатор
`DeliverySnapshotValidator`. Сохранение на диск — **вне модуля**.

**8. Управление.** Методы API; UI-действия `order.accept/pickup/complete/cancel/abandon`.

**9. Установка.** В сцене — компонент-держатель (`DeliveryServiceHost` из PR #14)
+ `IGameClockAdapter`/`IPayoutAdapter`/`ILocationProvider`. **Все три реализации
отсутствуют в репозитории.**

**10. Проверки.** `Jobs/Tests`: 55 кейсов — все переходы, выплата один раз,
просрочка, адаптеры с исключениями, идемпотентность. Сегодня 55/55.

**11. Риски.** (а) **Заказы никто не создаёт** — `RegisterOrder` вызывается
только из тестов; (б) `DeliveryPoint.Id` — свободная строка, а район генерирует
`drop-bld-*`; связать их нечем (P1); (в) `ILocationProvider.CurrentLocationId` —
тоже строка, и в репозитории нет её реализации.

**12. Рекомендации.** Добавить `DeliveryPointCatalog`, который строит заказы из
`QuarterPlan.DeliveryPoints`, и мост `QuarterPlan → ILocationProvider` (по
ближайшей точке в радиусе взаимодействия).

---

## 6. Экономика (`Economy`)

**1. Назначение.** Кошелёк, каталог товаров и покупки с защитой от двойного
списания.

**2. Статус.** **РАБОТАЕТ.** 60 тестов, 0 провалов.

**3. Код.** `Core/Money.cs` (readonly struct, рубли), `Core/IWalletAdapter.cs`,
`Core/IIdempotentWalletAdapter.cs`, `Core/FinancialOperationId.cs`,
`Shop/ShopService.cs`, `Shop/ShopCatalog.cs`, `Shop/ShopSnapshot.cs` (+валидатор).

**4. Архитектура.** Идемпотентность через `FinancialOperationId` и журнал
операций; два режима — A (внутрисессионный) и B (durable-журнал).

**5. Алгоритм покупки.** `ShopService.Buy(cart)` → проверка каталога → резерв
(`TrySpend(operationId, money)`) → выдача товара
(`TryAdd(operationId, itemId, qty)`) → при сбое компенсация `TryRefund` → запись
`ShopOperationSnapshot`. Повтор с тем же `purchaseId` возвращает прежний результат
и ничего не списывает.

**6. Интеграции.** `MoneyWallet`/`FoodInventory` (вне репозитория) через
`WalletBridge`/`InventoryBridge` из `UnityIntegrationKit`. Вызывается телефоном
(`BankSummaryService`, read-only).

**7. Данные.** `Money`, `ShopSnapshot` (журнал операций). Валидатор отклоняет
дубликаты `PurchaseId`.

**8. Управление.** API; UI магазина отсутствует.

**9. Установка.** Мосты с делегатами — единственный путь подключения.

**10. Проверки.** 60 кейсов: арифметика, покупка, возврат, повтор, журнал,
повреждённый снимок. 60/60.

**11. Риски.** Без durable-журнала в реальном кошельке доступен только Режим A:
после аварийного завершения результат операции `OutcomeUnknown`, а не «успех» —
это честно, но требует обработки в UI.

**12. Рекомендации.** Реализовать `MoneyWallet` с журналом (Режим B) в проекте —
это разблокирует идемпотентность выплат доставки.

---

## 7. Долги (`Debts`)

**1. Назначение.** Стартовый долг, проценты, штрафы за просрочку, стадии
взыскания, платежи.

**2. Статус.** **РАБОТАЕТ.** 97 тестов, 0 провалов.

**3. Код.** `Core/DebtService.cs` (900+ строк, `TryPay`, `UpdateTime`,
`CreateSnapshot`/`RestoreSnapshot`), `Core/DebtView.cs`, `Core/IDebtReadService.cs`,
`Penalties/*` (`PenaltyPolicy`, `CollectionThresholds`), `Payments/*`,
`Persistence/DebtSnapshot.cs` (+валидатор), `Events/DebtEventHub.cs`.

**4. Архитектура.** `IDebtClockAdapter` даёт игровое время, `IDebtWalletAdapter` —
списание/возврат; события — через `IDebtEventSink` (хаб). Внутри — расчёт
остатка, начисленных пеней и стадии.

**5. Алгоритм.** `UpdateTime()` начисляет пени и переводит стадию по порогам;
`TryPay(debtId, rubles, paymentId)` списывает через кошелёк с `paymentId`,
при сбое — компенсация; повтор с тем же `paymentId` не списывает второй раз.

**6. Интеграции.** Телефон (`BankSummaryService` читает `IDebtReadService`),
`DebtWalletBridge`, `DebtPayment` из `UnityIntegrationKit`.

**7. Данные.** `DebtSnapshot` + валидатор (принимает `RefundPending`).

**8. Управление.** API + уведомления телефона (`PhoneNotificationType.Debt`).

**9. Установка.** `DebtService` создаётся хостом; нужны оба адаптера.

**10. Проверки.** 97 кейсов: платежи, пени, стадии, сохранения, безопасность,
финансовые контракты. 97/97.

**11. Риски.** Как и в Economy: durable-журнал — на стороне кошелька.

**12. Рекомендации.** Ничего критичного; следить за согласованностью порогов
`CollectionThresholds` с геймдизайном.

---

## 8. Телефон (`Phone`)

**1. Назначение.** Модель телефона игрока: приложения, сообщения, контакты,
уведомления, сводки «Работа» и «Банк».

**2. Статус.** **ЧАСТИЧНО РАБОТАЕТ**: 91 из 93 тестов, **2 падают**.

**3. Код.** `Apps/PhoneApp.cs`, `Apps/PhoneAppRegistry.cs`,
`Messages/*` (`PhoneMessageService`, `PhoneInbox`),
`Notifications/*` (`PhoneNotification`, `PhoneNotificationService`,
`PhoneNotificationHub`), `Contacts/PhoneBook.cs`,
`Integrations/PhoneFacade.cs`, `WorkSummaryService.cs`, `BankSummaryService.cs`,
`StorySummaryService.cs`, `PhoneBadgeSync.cs`, `Core/*` (`PhoneId`, `GameMinutes`).

**4. Архитектура.** `PhoneFacade` — композиция четырёх сервисов + `PhoneInbox`
(«напиши игроку») + `PhoneNotificationHub` (уведомление → бейдж приложения).
Модели неизменяемые, мутации — через сервисы.

**5. Алгоритм.** `PhoneNotificationHub.Push(...)` → `Notifications.Add` →
`Apps.AddUnread(appId, 1)`; `PhoneBadgeSync` синхронизирует бейдж «messages» с
`TotalUnread`.

**6. Интеграции.** `WorkSummaryService ← IDeliveryService`,
`BankSummaryService ← IWalletAdapter + IDebtReadService`,
`StorySummaryService ← задачи`. Вызывается из UI (в PR #14 — через
`DeliveryServiceBridge`/`PhoneData`, **но не напрямую**).

**7. Данные.** Снимки: `PhoneAppsSnapshot`, `PhoneNotificationsSnapshot`,
`PhoneMessagesSnapshot`, `PhoneContactsSnapshot`.

**8. Управление.** API; экран — в PR #14 (`PhoneScreen`).

**9. Установка.** `PhoneFacade` создаётся хостом; UI-моста в `main` нет.

**10. Проверки.** 93 кейса; **2 падают** (см. ниже). 91/93.

**11. Риски.**
* **P1 (подтверждено прогоном).** Тесты «Приложения: пустой ID и название
  отклоняются» и «Уведомления: пустые поля отклоняются» падают с
  `ArgumentException`: конструкторы `PhoneApp`/`PhoneNotification` **бросают
  исключение**, а тесты ожидают код результата `InvalidArgument`. То есть контракт
  «модель валидирует и бросает» и контракт «сервис возвращает код» смешаны.
  В бою это означает: неверные данные из игры валят вызывающего, а не возвращают
  ошибку.
* Модуль `Phone` и `UI/Core` (PR #14) — два независимых представления: Id
  приложений расходятся (`jobs` в модуле против `orders` в UI).

**12. Рекомендации.** (а) Не бросать из конструкторов моделей — перенести
валидацию в фабрики/сервисы, возвращающие `PhoneResult`;
(б) согласовать Id приложений между `Phone/Apps` и `UI/Core/PhoneData`;
(в) подписать `WorkSummaryService` на `IDeliveryService` (сейчас вызывается вручную).

---

## 9. Пауза (`City/PauseMenu` + `City/PauseInputSystem`)

**1. Назначение.** Меню паузы: остановка времени, блокировка ввода, сохранение/
загрузка/настройки.

**2. Статус.** **НЕ ПРОВЕРЕНО (Unity)**. Есть 8 NUnit-тестов, но они требуют
`UnityEngine` и `UnityEngine.TestTools` — вне Unity не исполняются.

**3. Код.** `PauseContracts.cs` (`PauseAction`, `PauseSnapshot`, `PauseDataSource`,
`PauseIntegration`, `PauseState`), `PauseController.cs` (143 строки),
`PauseView.cs` (721 строка, uGUI), `StandalonePauseIntegration.cs`,
`City/PauseInputSystem/PauseKeyboardInput.cs`.

**4. Архитектура.** `PauseController` — владелец состояния и `Time.timeScale`;
`PauseView` — вид; `PauseDataSource` — данные (абстрактный, реализует игра);
`PauseIntegration` — мост к существующему вводу/окнам.

**5. Алгоритм.** `HandleEscape()` → `Open()`: сохранить `Time.timeScale`,
`Cursor` → `AcquireGameplayLock()` → `timeScale = 0` → отрисовать вид.
Закрытие — обратный порядок, восстановление прежних значений.

**6. Интеграции.** В PR #14 `UiBootstrap` **находит** `PauseIntegration` и
выключает свои клавиши и свой `timeScale`, а действия меню передаёт в
`PauseIntegration.Execute`. Правильное решение — но проверено только стабами.

**7. Данные.** `PauseSnapshot` (день/время/локация/цели/статусы/деньги/долг).

**8. Управление.** Esc/P (через `PauseKeyboardInput`, `useStandaloneKeyboard=false`
по умолчанию — то есть «включи меня вручную»).

**9. Установка.** Объект `Pause` с `PauseController` + `PauseView` +
`StandalonePauseIntegration` + `PauseKeyboardInput`; `Data Source` — наследник
`PauseDataSource` (шаблон: `City/UnityIntegration/Templates/GamePauseDataSource.cs.txt`).
**В репозитории этой сцены нет.**

**10. Проверки.** 8 тестов PlayMode (вне Unity не идут). Фактически — **0
исполненных проверок**.

**11. Риски.** (а) Единственный владелец `Time.timeScale` в `main` — здесь;
PR #14 это уважает, но без сцены проверить нельзя. (б) `PauseKeyboardInput`
выключен по умолчанию: если не включить, Esc ничего не делает. (в) `PauseView`
на 721 строку собирает uGUI кодом — шрифты и раскладка не проверены.

**12. Рекомендации.** Покрыть `PauseState`/`PauseSnapshot.ResolveSatiety` юнит-
тестами **без** UnityEngine (они чистые) — это даст хоть какую-то проверку.

---

## 10. UI (только в PR #14)

**1. Назначение.** Телефон, карта, HUD, меню паузы на uGUI.

**2. Статус.** **ЧАСТИЧНО**: 98/98 тестов логики и 0 ошибок компиляции против
стабов; **НЕ ПРОВЕРЕНО (Unity)** для вида и кликов.

**3. Код.** `UI/Core` (13 файлов: калибровка карты, граф дорог, маршруты,
навигация, телефонные данные, раскладка паузы, мост района),
`UI/Runtime` (16 файлов: `UiRouter`, `PhoneScreen`, `MapScreen`, `MapCanvasGraphic`,
`HudScreen`, `PauseMenuScreen`, `UiBootstrap`, `UiGameBridge`,
`DeliveryServiceBridge`, `UiSettings`), `UI/Editor/UiInstaller.cs`.

**4. Архитектура.** `UI.Core` без `UnityEngine` → тестируется; `UI.Runtime`
содержит только вью и мосты; `UiRouter` — единственный владелец состояния экранов.

**5. Алгоритм.** `UiRouter.RequestIntent(intent)` → `UiStateMachine.Apply` →
`ApplyState` (показать один экран, скрыть остальные, курсор, ввод, `timeScale`).
Маршрут: `MapScreen → NavigationService → RoutePlanner → RoadGraphData`.

**6–8. Интеграции/данные/управление.** См. `PR14_REVIEW.md` — там разобрано
по файлам.

**9. Установка.** `UiInstaller` (меню `Tools → Life From Zero → UI`), плюс
обязательные поля в инспекторе, которых в репозитории заполнить некому.

**10. Проверки.** 98 кейсов (раскладка паузы, граф, маршруты, калибровка карты,
привязка к рёбрам, данные телефона). Сегодня 98/98.

**11. Риски.** См. `ISSUES.md` P0-2 (план района не доходит из-за дефекта
`QuarterUnityAdapter` — **исправлено**), P1 (адаптеры пустые), P2 (Id приложений).

**12. Рекомендации.** См. `PR14_REVIEW.md` §6.

---

## 11. Инструменты установки и Editor (`City/UnityIntegration`, `UI/Editor`)

**1. Назначение.** Безопасно встроить модули в существующий Unity-проект:
найти стартовую сцену, создать тестовую сцену District750, мигрировать
Quarter250 → District750, поставить UI.

**2. Статус.** **НЕ ПРОВЕРЕНО (Unity)**. Скрипты PowerShell/bash частично
проверены (см. §12).

**3. Код.** `Editor/LifeFromZeroDiscovery.cs`, `LifeFromZeroSetup.cs`,
`LifeFromZeroGameIntegration.cs` (1 100+ строк), `LifeFromZeroMigration.cs`
(800+ строк), `LifeFromZeroIntegrateWindow.cs`; `install_to_unity.sh`,
`run_checks.sh`; в PR #14 — `UI/Editor/UiInstaller.cs`.

**4. Архитектура.** Все Editor-скрипты следуют одному шаблону: «сначала отчёт и
план, потом изменения; сцена/файлы не перезаписываются; при сомнении — СТОП».

**5. Алгоритм `install_to_unity.sh`.** Проверка `Assets/` и `ProjectSettings/` →
список файлов → сравнение с существующими → при конфликте выход с кодом 5 и
списком → при `--apply` копирование без перезаписи.

**6. Интеграции.** Вызываются вручную из меню Unity или CLI.

**7. Данные.** Отчёты пишутся в `City/UnityIntegration/reports/` (в `.gitignore`).

**8. Управление.** Меню `Tools → Life From Zero → …`, batch `-executeMethod`.

**9. Установка.** Скрипты лежат в репозитории; требуют Unity-проект.

**10. Проверки.** `run_checks.sh`: 8 этапов, из них **2 PASS (python-генератор,
запись отчёта), 6 NOT RUN** — нет Unity и `dotnet`. `install_to_unity.sh`
корректно отказывается без пути (код 3).

**11. Риски.** Инструменты большие (1 900+ строк Editor-кода) и не исполнялись
ни разу. Риск ошибок в них высокий именно потому, что они не проверены.

**12. Рекомендации.** Прогнать в Unity на копии проекта, начиная с
`LifeFromZeroDiscovery` в режиме отчёта.

---

## 12. Скрипты проверки (`City/Tools`, `City/UnityIntegration/*.sh`)

**1. Назначение.** Проверки без Unity: раскладка района и сводный отчёт.

**2. Статус.** **РАБОТАЕТ.** Прогнано сегодня: `district_layout_check.py` → OK
(974 объекта, 46 доставок, 0 недостижимых); `run_checks.sh` → 2 PASS / 6 NOT RUN.

**3. Код.** `City/Tools/district_layout_check.py`,
`City/UnityIntegration/run_checks.sh`.

**11. Риски.** `run_checks.sh` честно пишет «NOT RUN — это НЕ успешная проверка»;
главное — не читать «PASS: 2» как «всё хорошо».

---

## 13. Отсутствующие системы (проверено поиском, не по документации)

| Система | Что искали | Результат |
|---|---|---|
| NPC | `class *Npc*`, `*NPC*`, `Pedestrian` | **нет ни одного класса** |
| Транспорт | `Vehicle`, `Car`, `Traffic` | **нет** |
| Взаимодействие с объектами мира | `class *Interaction*` | **нет** (есть только `BasementDoor.Interact()` и точки доставки) |
| Сохранение игры | `SaveService`, `ISaveService` | **нет**: есть только снимки модулей (`DebtSnapshot`, `DeliverySnapshot`, `ShopSnapshot`, `Phone*Snapshot`) без записи на диск |
| Инвентарь игрока | `IInventoryAdapter` | есть только **интерфейс**; реализации нет |
| Хост игры (сцены) | `*.unity`, `ProjectSettings/` | **нет** |
| Материалы/URP/освещение | `*.mat`, `*.shader`, `*.asset`, `UniversalRenderPipelineAsset` | **нет** |
| `.meta`/GUID | `*.meta` | **0 файлов** |

Это ключевой результат аудита: половина систем из ТЗ №043 (NPC, транспорт,
взаимодействия, сохранения, инвентарь) **не существует в коде**, хотя документы
репозитория местами говорят о них как о части игры.
