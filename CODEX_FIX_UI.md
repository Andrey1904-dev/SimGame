# CODEX_FIX_UI — как применить исправления ТЗ №045 в локальном Unity-проекте

Короткая инструкция. Ожидаемый порядок: **проверка → копирование кода → починка сцены →
`SceneUiBinding` → Play Mode**. Ничего не перестраивается: District750, квартира №67,
HUD, экономика и доставка не трогаются.

Пути подставьте свои:

```bash
UNITY_BIN="/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity"
UNITY_PROJ="/Users/homepc/Projects/LifeFromZero"
SIMGAME_REPO="/Users/homepc/Projects/SimGame"
```

---

## 0. Точка отката (обязательно)

```bash
cd "$UNITY_PROJ"
cat ProjectSettings/ProjectVersion.txt                 # 6000.3.27f1
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git status --porcelain
  git tag -f lfz-before-tz045 HEAD
else
  ditto "$UNITY_PROJ" "${UNITY_PROJ}_backup_$(date -u +%Y%m%dT%H%M%SZ)"
fi
```

---

## 1. Забрать исправления из репозитория

```bash
cd "$SIMGAME_REPO"
git fetch origin
git checkout arena/0e907219-simgame      # ветка ТЗ №045 (в main ещё не слита)
git pull --ff-only

# сухой прогон копирования .cs/.asmdef (ничего не меняет)
bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --dry-run

# применить: копируются только .cs и .asmdef, сцены и ваши ассеты не трогаются
bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --apply

# если в проекте уже лежали копии этих же файлов и вы согласны их обновить
# (скрипт сам делает бэкап заменяемых файлов в LifeFromZeroIntegration/backups/install-<UTC>/):
# bash City/UnityIntegration/install_to_unity.sh "$UNITY_PROJ" --apply --overwrite
```

Откройте Unity и дождитесь компиляции. В консоли **не должно быть** `error CS`.
Если появилось `error CS`, стоп: сцена не менялась, вернитесь к шагу 0.

```bash
# проверка компиляции без открытия Editor (Editor закрыт):
"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" -logFile /tmp/lfz-compile.log
grep -E "error CS" /tmp/lfz-compile.log | head
```

Что именно изменилось — таблица в `UI/REPORT_045_UI_FIXES.md` §2.

---

## 2. Диагностика и починка сцены (в Unity Editor)

1. **`Tools → Life From Zero → UI → Verify Scenes & Adapters (read-only)`**

   В отчёте смотрите новые строки:

   ```
   - Слои карты (Graphic/CanvasRenderer): проблем нет            ← должно быть так
   - Missing Script: нет
   - Сохранённые слои карты (должны создаваться в рантайме): нет
   - IPhoneDataSource (телефон): PhoneFacadeUiBridge            ← или DeliveryServiceBridge
   ```

   * `ПРОБЛЕМ N` у слоёв карты → это и есть причина `MissingComponentException`
     «There is no 'CanvasRenderer' attached to the "Grid"/"Route" game object».
   * `Missing Script` → появится путь объекта и номер слота компонента: это ваш п.4 из ТЗ.
   * `Сохранённые слои карты` → лишние `Grid`/`Route` в сцене: `MapScreen` создаёт их сам.

2. **`Tools → Life From Zero → UI → Repair UI objects in open scenes`**

   Скрипт пересоздаёт каждый `Graphic` без `CanvasRenderer` (удаление + `AddComponent`
   того же типа ⇒ `[RequireComponent(typeof(CanvasRenderer))]` выполняется заново).
   Missing Script **не удаляет** — только перечисляет. Затем сохраните сцену (`Ctrl+S`):
   автоматически сцены не сохраняются.

   Пакетно (Editor закрыт, с бэкапом каждой сцены):

   ```bash
   "$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
     -executeMethod LifeFromZero.UI.Editor.UiInstaller.RepairBatch -lfzUiRepair \
     -logFile /tmp/lfz-ui-repair.log
   grep -E "\[LFZ-UI\]|FIXED|FAIL" /tmp/lfz-ui-repair.log | head -40
   ```

   Без `-lfzUiRepair` метод ничего не делает (защита от случайных правок).

3. **Лишние `Grid`/`Route`, сохранённые в сцене**, удалите вручную: они создаются
   заново в `MapScreen.Awake()`. Это единственный шаг, где нужно решение человека.

4. **Установить/обновить UI в сцене** (если установщик ещё не запускался в этой сцене):

   ```
   Tools → Life From Zero → UI → Check UI installation (dry run)   ← сначала план
   Tools → Life From Zero → UI → Install UI into game
   ```

   Установщик теперь сам кладёт найденный `IPhoneDataSource` (например
   `PhoneFacadeUiBridge`) в поле **`UiBootstrap.phoneSystem`** — это закрывает п.1 ТЗ.
   Второй запуск печатает «уже есть» и ничего не добавляет (идемпотентность).

---

## 3. `Assets/_Project/Scripts/Runtime/SceneUiBinding.cs` — точечно и без догадок

> В репозитории `Andrey1904-dev/SimGame` этого файла **нет** (проверено поиском по всем
> ревизиям и по `.cs/.md/.py/.sh/.asmdef`). Ниже — не «исправленный файл», а точный порядок
> действий: сначала прочитать, потом решить.

### 3.1. Посмотреть строку 18 и что рядом

```bash
cd "$UNITY_PROJ"
nl -ba Assets/_Project/Scripts/Runtime/SceneUiBinding.cs | sed -n '1,60p'
```

NRE в `Start()` на строке 18 — это почти всегда одно из:

| Что в строке 18 | Причина | Что делать |
|---|---|---|
| `x.SomeMember` / `x.ReadPhone()` / `x.Set...()` | `x` не назначен в инспекторе **или** объект уничтожен при смене сцены | Шаг 3.2 (вариант A) |
| `GetComponent<...>()` → `.поле` | компонента на объекте нет | Шаг 3.2 (вариант B) |
| обращение к своему полю-адаптеру, которого нет в сцене | сцена собрана под старую версию UI | Шаг 3.2 (вариант B) |

### 3.2. Вариант A (предпочтительный): удалить дубликат

`UiBootstrap` уже делает всё, что делал «связывающий» скрипт: ищет `UiWorldAdapter`,
`UiSnapshotSource`, `IPhoneDataSource`, `DeliveryServiceBridge`, план района, создаёт
экраны, переживает смену сцены. Если `SceneUiBinding` только «прокидывал» ссылки —
**удалите компонент со сцены** (не файл): выберите объект → `Remove Component`,
сохраните сцену. Проверка: `P` и `M` работают, в консоли нет NRE, `UiBootstrap` в логе
пишет `данные телефона (<источник>)`.

### 3.3. Вариант B: оставить, но переписать на штатные точки

Если в `SceneUiBinding` есть своя логика (например, номер квартиры, локальные эффекты),
не удаляйте его, а замените «ручное связывание» на штатные вызовы:

```csharp
using LifeFromZero.City.Quarter;
using LifeFromZero.UI.Core;
using LifeFromZero.UI.Runtime;
using UnityEngine;

public sealed class SceneUiBinding : MonoBehaviour
{
    [SerializeField] private MonoBehaviour phoneBridge;   // PhoneFacadeUiBridge / DeliveryServiceBridge
    [SerializeField] private MonoBehaviour districtSource; // QuarterUnityAdapter и т. п.

    private void Start()
    {
        // 1. Телефон: НЕ проверяйте поле на null через .TryRun — сначала убедитесь, что мост есть.
        var phone = phoneBridge as IPhoneDataSource;       // ← null-условие ДО обращения (строка 18)
        if (phone == null)
        {
            Debug.LogWarning("[SceneUiBinding] phoneBridge не назначен или не реализует IPhoneDataSource — " +
                             "телефон останется без данных. Назначьте PhoneFacadeUiBridge.");
        }

        // 2. План района: отдайте его один раз, дальше UiBootstrap сам подхватит PlanChanged.
        var plan = districtSource as IQuarterPlanSource;
        if (plan != null && plan.Plan != null) UiBootstrap.SetDistrictPlan(plan.Plan);
    }
}
```

Правила, которые нельзя нарушать (иначе вернутся те же ошибки):

* **не создавайте** `Canvas`, `Grid`, `Route`, `MapCanvasGraphic`, `EventSystem` и вторые
  экраны — это делает `MapScreen.Awake()`/`UiBootstrap`; дубликат `Grid`/`Route` в сцене
  как раз и даёт `MissingComponentException`;
* **не подменяйте** `PhoneScreen.DataProvider` своим делегатом, возвращающим пустой
  `PhoneSnapshot` — иначе рабочий стол снова станет пустым; отдайте мост через
  `UiBootstrap.phoneSystem`;
* **не вызывайте** `DontDestroyOnLoad` на объектах сцены и не создавайте второй `UiRouter`.

После правки: `Assets → Reimport` не нужен, но убедитесь, что в консоли нет
`error CS` (иначе компонент станет **Missing Script** — это ваш п.4).

---

## 4. Play Mode: что проверить (и что считается проверкой)

В сцене **Apartment67**:

- [ ] `P` открывает телефон; плитки на месте; повторный `P`/`Esc` закрывает;
      в консоли **нет** «Приложения не подключены» и **нет** NRE источника данных;
- [ ] если плитки неактивны — в консоли ровно одно предупреждение
      `[PhoneScreen] Рабочий стол без данных: IPhoneDataSource не подключён…`;
      лечится назначением `phoneSystem` (шаг 2.4);
- [ ] `M` открывает карту; видна сетка; **нет** `MissingComponentException`;
- [ ] в консоли **нет** Missing Script (если есть — отчёт шага 2.1 покажет объект);
- [ ] лог `[UiBootstrap]` содержит `норма: сцена без района…` (это ожидаемо для квартиры),
      а не `НЕ подключено: план района`.

В сцене **District750**:

- [ ] карта рисует район; в логе `[UiBootstrap]` — `план района (QuarterUnityAdapter)`
      либо `План района подключён повторной попыткой (N из 20)`;
- [ ] `P`/`M`/`Esc` открывают ровно по одному окну, время останавливается один раз;
- [ ] `Apartment67 → District750 → Apartment67`: один `UiRouter`, экраны не дублируются,
      после возврата `P`/`M` работают.

Пакетные проверки (Editor закрыт):

```bash
bash "$SIMGAME_REPO/City/UnityIntegration/run_checks.sh" \
  --unity-project "$UNITY_PROJ" --unity "$UNITY_BIN"

"$UNITY_BIN" -batchmode -quit -projectPath "$UNITY_PROJ" \
  -executeMethod LifeFromZero.UI.Editor.UiInstaller.RunBatchVerify \
  -logFile /tmp/lfz-ui-verify.log
# отчёт: "$UNITY_PROJ/LifeFromZeroIntegration/reports/ui_scene_verify.md"
```

`-lfzStrictVerify` в `RunBatchVerify` возвращает код 1, если остались проблемы слоёв
карты, Missing Script или сохранённые слои карты.

---

## 5. Откат

```bash
# только сцены и EditorBuildSettings из бэкапа интегратора
cd "$UNITY_PROJ"
rsync -av "$(ls -td LifeFromZeroIntegration/backups/*/*/ | head -n 1)" "$UNITY_PROJ/"

# полный откат проекта к точке из шага 0
cd "$UNITY_PROJ" && git reset --hard lfz-before-tz045

# откат репозитория модулей
cd "$SIMGAME_REPO" && git checkout main
```
