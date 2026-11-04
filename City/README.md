# City/Quarter — генератор городского квартала (ТЗ №023)

Автономный C#-генератор российского спального района 250×250 м для
последующего подключения к Unity 6 URP. Существующие модули
(Economy, Delivery, Debts, Phone) не затрагиваются.

## Что генерируется

По фиксированному seed (свой SplitMix64, без `System.Random` — побитово
воспроизводимо на любой платформе): сетка улиц 3×3 с перекрёстками,
тротуарами, осевой разметкой, переходами и стоп-линиями; пятиэтажные
дома по периметрам блоков с подъездами; 3 двора (газон, детская
площадка, лавочки, деревья, парковка, дворовый проезд, дворовые
фонари); уличные фонари и деревья. Для seed=1904: 635 объектов,
39 домов, 82 подъезда, 27 сегментов дорог, 9 перекрёстков.

## Файлы

| Файл | Назначение |
|---|---|
| `Quarter/QuarterMath.cs` | Vec2/Vec3/Rect2, детерминированный SeededRandom. Без UnityEngine |
| `Quarter/QuarterTypes.cs` | Контракт данных: `QuarterPlan`, `PlacedObject` (тип, позиция, поворот, размер, параметры), `RoadSegment`, `BuildingInfo`, `CourtyardInfo` |
| `Quarter/QuarterGeneratorOptions.cs` | Параметры, `BuildingArchetype`, точка расширения `IQuarterObjectPlacer` |
| `Quarter/QuarterGenerator.cs` | Генератор + `RoadGraph` (связность) + `QuarterPlanSerializer` (каноническая форма) |
| `Quarter/City.Quarter.asmdef` | Сборка `LifeFromZero.City.Quarter`, `noEngineReferences: true` |
| `UnityAdapter/QuarterUnityAdapter.cs` | Единственный файл с UnityEngine: строит GameObject/примитивы/материалы/коллайдеры по плану; префабы по TypeId — через `prefabOverrides` |
| `UnityAdapter/City.Quarter.UnityAdapter.asmdef` | Сборка адаптера, ссылается на ядро |
| `Tests/` | 18 тестов (MiniTestRunner + NUnit-обёртка для Unity), `Console.csproj` для прогона вне Unity |

## Расширяемость

- Новый тип дома: добавить `BuildingArchetype` в `Options.Archetypes`.
- Новый тип объектов: свой `IQuarterObjectPlacer` в `Options.CustomPlacers`
  (даёт `IsFree(rect)` и `Add(obj)`); TypeId — произвольная строка,
  адаптер для неизвестных типов рисует magenta-примитив или префаб из overrides.

## Как подключить к Unity

1. Скопировать папку `City/` в `Assets/` Unity-проекта (Unity 6, URP).
2. Ядро соберётся как `LifeFromZero.City.Quarter`, адаптер — как
   `LifeFromZero.City.Quarter.UnityAdapter`.
3. На пустой объект сцены повесить `QuarterUnityAdapter`, задать Seed —
   квартал построится при старте. Программно:
   `new QuarterUnityAdapter().Build(QuarterGenerator.Generate(seed), transform)`.
4. Для продакшен-вида заменить примитивы префабами в `prefabOverrides`
   (ключи — TypeId: `building`, `tree`, `lamp.street` и т.д.). Материалы
   адаптера процедурные, шейдер `Universal Render Pipeline/Lit`
   (fallback `Standard`).

**Статус проверки в Unity: НЕ запускалось.** Unity-проекта в репозитории
нет, поэтому импорт, компиляция asmdef в редакторе и построение сцены
владельцем проекта не проверены. Адаптер проверен только компиляцией
против стаба UnityEngine вне Unity.

## Тесты

Вне Unity (нужен .NET SDK):

```
dotnet run --project City/Tests/Console.csproj
```

В этой сессии прогон выполнен эквивалентно: автономный модуль + тесты
скомпилированы Roslyn (C# 8) и запущены на .NET Core 3.1 — **18/18 PASS**
(дороги и связность графа, переходы/разметка, дома вне проезжей части и
без пересечений, подъезды, комплектность дворов, проезды, фонари,
повторяемость по seed на seed=42 и серия seed 1..10, расширяемость).
В Unity тесты появятся в Test Runner (EditMode) как
`LifeFromZero.City.Quarter.Tests` — там не запускались.

## Не реализовано (вне ТЗ или осознанно отложено)

- Запуск и проверка в Unity, URP-материалы «по-взрослому», префабы/модели.
- NPC, транспорт, симуляция, навигация (NavMesh), LOD/оптимизация мешей
  (каждый объект — отдельный примитив; для продакшена нужен батчинг/меш-слияние).
- Другие типы зданий из коробки (только архетип панельной пятиэтажки;
  механизм архетипов есть), этажность ≠ 5, кварталы ≠ 250×250
  (размер параметризован, но планировка и тесты заточены под 250 м).
- Диагональные улицы, Т-образные перекрёстки, разная ширина улиц.
