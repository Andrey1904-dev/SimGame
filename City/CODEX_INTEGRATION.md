> **Для District750 не использовать.** Этот документ описывает квартал 250 (`quarter250`). Инструкция для District750: `City/CODEX_TASK.md`.

# Подключение модуля City к Unity-проекту LIFE FROM ZERO — инструкция для Codex

Модуль готов: генератор (ядро без Unity) + Unity-адаптер + точка запуска
`CityBootstrap`. Твоя работа — импорт, тестовая сцена, проверка в настоящей
Unity. Архитектуру не проектировать, генератор не переписывать.

## 1. Файлы для переноса

Из репозитория (папка `City/`) в проект Unity, с сохранением структуры:

| Источник | Назначение |
|---|---|
| `City/Quarter/*.cs` (6 файлов) + `City/Quarter/City.Quarter.asmdef` | Ядро генератора, без зависимостей от Unity |
| `City/UnityAdapter/QuarterUnityAdapter.cs`, `City/UnityAdapter/CityBootstrap.cs`, `City/UnityAdapter/City.Quarter.UnityAdapter.asmdef` | Построение города в сцене и точка запуска |
| `City/Tests/*` (по желанию) | EditMode-тесты; без них модуль работает |

## 2. Куда поместить

```
Assets/_Project/City/Quarter/        <- ядро и его asmdef
Assets/_Project/City/UnityAdapter/   <- адаптер, Bootstrap и его asmdef
Assets/_Project/City/Tests/          <- только если нужны тесты
```

Имена asmdef и пространств имён не переименовывать:
`LifeFromZero.City.Quarter`, `LifeFromZero.City.Quarter.UnityAdapter`.
Адаптер ссылается на ядро по имени asmdef — структура выше достаточна.

## 3. Тестовая сцена CityTest

Создай **новую** сцену `Assets/_Project/Scenes/CityTest.unity`
(существующие сцены, включая `Apartment67`, не трогать):

1. Пустой GameObject **`City`** → Add Component → **`CityBootstrap`**.
   (Адаптер `QuarterUnityAdapter` Bootstrap добавит сам и выключит его
   автозапуск — руками его настраивать не нужно.)
2. **Main Camera**: Position `(125, 200, -80)`, Rotation `(50, 0, 0)` —
   весь квартал 250×250 виден целиком.
3. **Directional Light**: Rotation `(50, -30, 0)`, Intensity `1`.

## 4. Параметры CityBootstrap (Inspector)

- `Seed`: **1904**
- `Generate On Start`: **вкл**
- `Add Colliders`: **вкл**

## 5. Запуск

Play. Город строится сам. Ручной запуск из кода (если понадобится):
`bootstrap.Generate()` — повторный вызов безопасен, старый квартал
уничтожается; `bootstrap.Clear()` — убрать город.

## 6. Проверка результата

- Console: `[CityBootstrap] Город построен: seed=1904, жилых зданий=15,
  объектов=821, точек доставки=27, время=... мс`.
- В Inspector компонента CityBootstrap, блок **Last Report**:
  Seed=1904, ResidentialBuildings=**15**, TotalObjects=821,
  DeliveryPoints=27, ErrorCount=0, RootName=`Quarter_seed1904`.
- В Hierarchy один корень `Quarter_seed1904`; второй Play без выхода из
  первого не плодит дублей (корень пересоздаётся).
- Визуально: 15 жилых зданий (в т.ч. 2 высокие башни ЖК у краёв квартала
  и 2 частных дома с заборами), дороги с разметкой и переходами, ПВЗ с
  оранжевой вывеской, магазин «ПРОДУКТЫ», дворы.
- Повторный `Generate()` (или перезапуск Play): нет наложенных городов.

## 7. Наиболее вероятные ошибки и исправления (без переписывания генератора)

| Симптом | Что делать |
|---|---|
| Ошибки компиляции про NUnit/TestRunner | Не копировать `City/Tests/` либо оставить: обёртка защищена `#if UNITY_INCLUDE_TESTS`; проверь, что Tests-asmdef лежит только в Editor-контексте |
| Магентные (розовые) объекты | Неизвестный шейдер/материал: проверь, что проект — URP; код ищет `Universal Render Pipeline/Lit`, затем `Standard` |
| Всё розовое целиком | Нет URP Render Pipeline Asset в настройках проекта — назначь его в Project Settings/Quality |
| Нет надписей на вывесках/номерах подъездов | Legacy TextMesh без шрифта: назначь Font на TextMesh-объектах или замени на TMP позже; геометрию это не ломает |
| Персонаж/камера проваливается | Проверь `Add Colliders` у Bootstrap и что игра не стартует до генерации; дорог/земли коллайдеры ставятся адаптером |
| Низкий FPS | Отключи `Add Colliders` для замера; уменьшай детализацию через `prefabOverrides`, а не правками ядра |
| Город построился дважды | Не вешай `QuarterUnityAdapter` отдельно с включённым Build On Start рядом с Bootstrap: управляет только Bootstrap |
| Исключение про квоты в Console | Seed/размер квартала из чужих опций: оставь seed=1904 и параметры по умолчанию |

## 8. Категорически нельзя

- Менять код ядра `Quarter/` ради сцены (вся настройка — параметры Bootstrap/опций).
- Переименовывать asmdef, namespaces, TypeId объектов и Id (`player.home`, `delivery.pickup`, `shop.grocery`, `property.*`).
- Трогать сцену `Apartment67` и модули Economy/Delivery/Debts/Phone.
- Подключать город к игровым механикам в рамках этой интеграции (только сцена и проверка).

## 9. Критерии успешной интеграции

1. Проект компилируется без ошибок по модулю City.
2. В сцене CityTest по Play строится город seed=1904: 15 жилых зданий, отчёт Bootstrap без ошибок.
3. Повторная генерация не плодит дубли; очистка (`Clear()`) убирает город.
4. Камера видит весь квартал, коллайдеры на дорогах/зданиях/подвале на месте.
5. Зафиксированы: время генерации из отчёта, субъективный FPS в Play Mode на целевой машине, скриншот обзора квартала.
