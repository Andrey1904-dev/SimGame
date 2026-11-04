# CODEX_RISKS — риски District750 (ТЗ №034)

Только риски, связанные с конкретным кодом. Статус: **не проверено** — ни один пункт не проверен компилятором или Unity, если не указано иное.

| № | Файл | Метод / класс | Возможный симптом | Минимальное исправление | Способ проверки |
|---|---|---|---|---|---|
| R1 | `City/Quarter/DistrictMap/DistrictMapGenerator.cs`, `DistrictMapTypes.cs`; `City/Tests/DistrictMapTests.cs`, `DistrictPlanChecks.cs`; аддитивные правки `City/Quarter/QuarterTypes.cs` | `DistrictMapGenerator.Builder`, `DistrictPlanChecks`, `DistrictMapTests` | Ошибки компиляции (C# никогда не компилировался) | Исправить первую ошибку, без рефакторинга | `dotnet build City/Tests/Console.csproj` |
| R2 | `City/UnityAdapter/QuarterUnityAdapter.cs`, `CityBootstrap.cs` | весь UnityAdapter | `Console.csproj` эти файлы не компилирует. `City/README.md` ссылается на стаб UnityEngine, но стаба в репозитории нет (поиск `namespace UnityEngine` пуст). Ошибки видны только в Unity | Исправить в Unity по первой ошибке | Unity: компиляция, Console без ошибок |
| R3 | `QuarterUnityAdapter.cs` | `CreatePrimitive` (строка ~693) | `Destroy(existing)` отложен до конца кадра: коллайдер ворот `district.door` или навеса сохраняется до конца кадра. Вызов `Destroy` из Edit Mode даёт ошибку Unity | Вызывать `Build` только из Play Mode; при необходимости `DestroyImmediate`, как для кольца (строка ~689) | Play Mode: через кадр у ворот нет коллайдера |
| R4 | `QuarterUnityAdapter.cs` | `NeedsCollider` (строка ~283), `CreatePrimitive` | Кольцо и площадка погрузки — плоские боксы. Если бокс выше земли, персонаж упирается или застревает | Проверить высоту бокса (0.06 м у погрузки); не поднимать | Пройти через центр кольца и по площадке погрузки |
| R5 | `DistrictMapGenerator.cs` | `AddGasStation` (строка ~594), проезды (115, 387) и (115, 403) | Островки или колонки АЗС-4 могут закрыть въезд с V150. В Python проверка проходит, в Unity не проверено | Сдвинуть островок на доступное место; координаты менять только после сверки с Python | Проехать/пройти от V150 к проездам АЗС-4 |
| R6 | `QuarterUnityAdapter.cs` | `Build`, вызов `BuildBuildingDetails` (строка ~168), `BuildBuildingDetails` (строка ~369) | Фасадные детали коммерции и завода могут быть смещены или наложены на стену, особенно у повёрнутых зданий | Корректировать смещение в `BuildBuildingDetails`; не добавлять новые меши | Визуальный осмотр коммерческих зданий (все `com-*` в плане, их 17) и двух корпусов завода с близкого расстояния |
| R7 | `QuarterUnityAdapter.cs` | `CreateDetailMesh` (строка ~546) | Каждое здание создаёт 3 дополнительных объекта с мешами. Число объектов и draw-вызовов не измерялось | Не добавлять новые детали без замера | Unity Profiler / Stats: число batches и объектов |
| R8 | `CityBootstrap.cs`, `QuarterUnityAdapter.cs` | `CityBootstrap.Generate`, `QuarterUnityAdapter.Build` (строка ~105) | Повторный `Generate()` дублирует город. По коду предыдущий квартал уничтожается, но в Unity не проверено | Если дубли есть — вызвать `ClearBuilt()` перед `Build` | Вызвать `Generate()` дважды из Play; в Hierarchy один корень |
| R9 | `QuarterUnityAdapter.cs` | `ClearBuilt` (строка ~57), очистка `_createdMeshes` (строка ~80) | Утечка мешей при многократной перестройке. По коду очистка есть | Не требуется, если проверка пройдена | Profiler, память после трёх `Generate()` |
| R10 | `City/Tests/QuarterNUnitTests.cs` | `RunAllQuarterTests` | В Unity Test Runner виден **один** тест вместо 24+. Легко принять за «всё проверено» | Смотреть сообщение ошибки; число кейсов брать из вывода `dotnet run` | Сравнить число кейсов в выводе `dotnet run` |
| R11 | `DistrictMapGenerator.cs` и `district_layout_check.py` | `Builder`; декор фасадов, крыша ПВЗ, колонки АЗС | Python-зеркало не содержит декоративных объектов C#. Число объектов C# может не совпасть с 961 | Декор задокументирован; не добавлять его в зеркало без задачи | Сравнить `TotalObjects` в `CityBootstrap.Last Report` с 961 |
| R12 | `CityBootstrap.cs`, `QuarterTypes.cs` (`PlayerHome`) | — | Нет связи с `Apartment67` и нет контроллера игрока в репозитории. Спавн в районе не настроен | Вне этого ТЗ; использовать существующий контроллер проекта | Проверить только построение района |
| R13 | `QuarterUnityAdapter.cs` | `CreatePrimitive` (`NeedsCollider`) | Ворота, навес и фасадные детали без коллайдера (проверено чтением). Если Codex добавит коллайдер, персонаж упрётся в декор | Не добавлять коллайдеры к декору | Пройти у ворот и под навесом |
| R14 | Производительность в целом | `DistrictMapGenerator.Generate`, `QuarterUnityAdapter.Build` | Время генерации и FPS не измерялись. Заявления о производительности не делать | Измерить в Unity до оценки | `CityBootstrap.Last Report.GenerationTimeMs`; Profiler; Stats |

## Что проверено (без Unity и без компилятора)

- Все `QuarterObjectTypes.*`, `DistrictObjectTypes.*`, `RoadKind.*`, используемые в District-файлах, объявлены (grep). Ложные срабатывания обрезанного regex (`District750`, `Quarter250`) — не ошибка.
- Коллайдеры ворот и декора отсутствуют (чтение `CreatePrimitive`).
- Очистка мешей при `ClearBuilt` и повторном `Build` есть (чтение).
- Python-проверка: exit 0, «OK», 24 точки, 46 доставок, 46 входов достижимы.

Тесты C# не запущены. Ни один пункт выше не означает, что код компилируется или работает в Unity.
