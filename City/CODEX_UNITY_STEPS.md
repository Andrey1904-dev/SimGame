# CODEX_UNITY_STEPS — подключение District750 в Unity (ТЗ №034)

Цель: открыть `DistrictTest.unity` в **существующем** проекте пользователя и проверить построение района. Новый проект не создавать. Сцены `Apartment67` и `CityTest` не менять.

Ограничение: в репозитории нет Unity-проекта, поэтому эти шаги выполняются только при доступе к Unity. Без него — отметить как «не запущено».

## 1. Файлы для переноса

Скопировать (или обновить) в проект пользователя с сохранением структуры:

- `City/Quarter/` — включая `DistrictMap/` и `City.Quarter.asmdef`.
- `City/UnityAdapter/` — включая `City.Quarter.UnityAdapter.asmdef`, `CityBootstrap.cs`, `QuarterUnityAdapter.cs`.
- `City/Tests/` — `City.Quarter.Tests.asmdef`, `MiniTestRunner.cs`, `QuarterTestSuite.cs`, `DistrictMapTests.cs`, `DistrictPlanChecks.cs`, `QuarterNUnitTests.cs` и остальные тесты квартала.

Не переносить: `City/Tests/Console.csproj`, `City/Tools/`, `*.md`, `Program.cs` (внутри Unity не нужны).

Путь назначения в проекте (`Assets/_Project/City/...`) упоминается в старых документах, в этом репозитории не проверен. Использовать путь, который уже принят в проекте.

## 2. Компоненты

- **`CityBootstrap`** (`City/UnityAdapter/CityBootstrap.cs`) — единственный компонент, который нужно добавить вручную. Он создаёт `QuarterUnityAdapter` на том же объекте.
- Поля в Inspector:
  - `Seed` = `1904`
  - `Layout Id` = `district750` (по умолчанию `quarter250`)
  - `Generate On Start` = включено
  - `Add Colliders` = включено
- Не вызывать `QuarterGenerator` напрямую: при `district750` `CityBootstrap` сам выбирает `DistrictMapGenerator`.

## 3. Игрок

В репозитории **нет контроллера игрока** (поиск по `CharacterController`, `PlayerController`, `FirstPerson`, `ThirdPerson` — пусто). Использовать существующий контроллер из проекта пользователя. Новый не создавать.

Связи с `Apartment67` в коде нет: `QuarterPlan.PlayerHome` задаёт только данные (`bld-n4`, квартира 67). Спавн игрока в районе этим шагом не настраивается.

## 4. Создание `DistrictTest.unity`

1. File → New Scene (пустая). Сохранить как `DistrictTest.unity` в каталоге сцен проекта (в старых документах — `Assets/_Project/Scenes/`, проверить по проекту).
2. Создать пустой GameObject `City` в корне сцены.
3. Add Component → `CityBootstrap`.
4. Установить поля по разделу 2.
5. Сохранить сцену. Сцену `CityTest` и `Apartment67` не открывать для изменения.

## 5. Вид сверху (для проверки, не для коммита)

Предложение, не проверено в Unity:

- Main Camera: Projection = Orthographic, Size ≈ 380.
- Position ≈ (375, 500, 375), Rotation = (90, 0, 0).
- Координаты: X и Z плана совпадают с X и Z Unity (адаптер ставит `new Vector3(X, Y, Z)`).
- Карта занимает квадрат 0..750 по X и Z.

## 6. Проверка построения

1. Play.
2. В Console: строка `[CityBootstrap] Город построен: макет=district750, seed=1904, …`. Ошибок `[CityBootstrap]` быть не должно.
3. В Inspector `CityBootstrap` → `Last Report`:
   - `ErrorCount` = 0
   - `DeliveryPoints` = 46
   - `TotalObjects` — сравнить с **961** (Python-зеркало). Значение C# не проверено; расхождение разобрать, не подгонять.
4. Повторный вызов `CityBootstrap.Generate()` из временного скрипта или Play-режима кодом (кнопки в Inspector нет): в Hierarchy должен остаться один корень, без дублей. Затем `Clear()` — корень должен исчезнуть.

## 7. Коллайдеры и доступность

Проверять в Scene view и Inspector. Ожидаемое поведение — из `NeedsCollider` и `CreatePrimitive` в `QuarterUnityAdapter.cs`.

| Объект | Ожидается | Как проверить |
|---|---|---|
| Дороги, проезды, тротуары | коллайдер есть | ходить по улице |
| Кольцо V270×H300 (`district.roundabout`) | плоский BoxCollider, проходимо | пройти через центр кольца |
| Центральный газон кольца | проходимо | то же |
| Площадка погрузки (`industry.loading`) | плоский бокс, проходимо | пройти к точке (652, 662) |
| Ворота заводов (`district.door`) | коллайдера **нет** | подойти к воротам |
| Навес АЗС (`gas.canopy`) | коллайдера **нет** | проехать/пройти под навесом |
| Островки и колонки АЗС (`gas.island`, `gas.pump`, `gas.kiosk`) | коллайдер есть | не должны блокировать проезд к проездам (115, 387) и (115, 403) |
| Коммерция, завод, трубы | коллайдер есть | упереться |
| Фасадные детали окон и дверей | mesh без коллайдера | не влияют на ходьбу |

Материалы: все детали используют существующую палитру (`MaterialFor`, `MaterialFromHex`). Новых материалов и мешей не добавлять.

## 8. Точки доставки и входы

Точки доставки — `QuarterPlan.DeliveryPoints` (46 шт.). В Unity автоматического обхода нет. Вручную проверить:

- ПВЗ `pvz-01`: точка (85, 310.4), вход z = 311.7 — с улицы H300 через площадку.
- АЗС-4: `gas-04` (85, 395), проезды (115, 387) и (115, 403).
- Погрузка: точка (652, 662) — с улицы H600.
- Банк `com-bank`, ТЦ `com-mall` — вход с улицы.

Если точка недоступна, зафиксировать координату и сообщить. Не менять координаты в C# без сверки с Python.

## 9. Что не делать

- Не создавать новый проект и не менять настройки проекта.
- Не менять `Apartment67`, `CityTest`, Economy, Delivery, Debts, Phone.
- Не заявлять визуальное соответствие карте, FPS или память без измерения.
