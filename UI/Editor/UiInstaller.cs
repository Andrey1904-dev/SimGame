// Life From Zero — UI/Editor/UiInstaller.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Editor
//
// БЕЗОПАСНЫЙ И ПОВТОРНО ЗАПУСКАЕМЫЙ установщик интерфейсов в НАСТОЯЩУЮ игру.
//
// Меню:    Tools → Life From Zero → UI → Install UI into game
// Пакетно: Unity -batchmode -quit -projectPath "<проект>" \
//            -executeMethod LifeFromZero.UI.Editor.UiInstaller.InstallBatch \
//            -lfzUiScene "Assets/…/Start.unity" [-lfzUiDryRun] -logFile /tmp/lfz-ui.log
//
// Принципы (те же, что у существующего LifeFromZeroGameIntegration):
//   1. Сцена НЕ угадывается: путь передаётся явно (аргумент или окно) либо берётся
//      из последнего отчёта LifeFromZeroDiscovery. Иначе — СТОП с объяснением.
//   2. Перед изменением файла сцены делается резервная копия в
//      <проект>/LifeFromZeroIntegration/backups/<время>/ (вне Assets).
//   3. Ничего не удаляется и не заменяется: добавляются только НЕДОСТАЮЩИЕ объекты.
//      Повторный запуск не создаёт дубликатов и печатает «уже установлено».
//   4. Чужое меню паузы не трогается: если найден другой PauseView/меню — СТОП,
//      два меню хуже одного. Решение — за владельцем игры.
//   5. -lfzUiDryRun печатает план и НИЧЕГО не меняет.
//
// Ограничение, которое нельзя скрыть: установщик не знает, какой компонент в
// проекте реализует UiWorldAdapter/UiSnapshotSource/IPhoneDataSource — таких
// классов в репозитории нет, их пишет интегратор (см. UI/CODEX_EXECUTE.md).
// Установщик кладёт готовый каркас (UiGameBindings) с явными TODO и печатает
// список того, что осталось подключить.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LifeFromZero.UI.Runtime;

namespace LifeFromZero.UI.Editor
{
    /// <summary>Результат установки: что сделано, что осталось.</summary>
    public sealed class UiInstallResult
    {
        public bool Completed;
        public bool Blocked;
        public bool DryRun;
        public string ScenePath = string.Empty;
        public string BackupDir = string.Empty;

        public readonly List<string> Changes = new List<string>();
        public readonly List<string> AlreadyDone = new List<string>();
        public readonly List<string> Blockers = new List<string>();
        public readonly List<string> ManualSteps = new List<string>();

        public string ToText()
        {
            var text = new StringBuilder();
            text.AppendLine("=== Life From Zero: UI installer (" + (DryRun ? "DRY RUN" : "apply") + ") ===");
            text.AppendLine("Сцена: " + (string.IsNullOrEmpty(ScenePath) ? "не определена" : ScenePath));
            if (!string.IsNullOrEmpty(BackupDir)) text.AppendLine("Резервная копия: " + BackupDir);
            text.AppendLine("Изменения (" + Changes.Count + "):");
            foreach (string change in Changes) text.AppendLine("  + " + change);
            text.AppendLine("Уже было (" + AlreadyDone.Count + "):");
            foreach (string done in AlreadyDone) text.AppendLine("  = " + done);
            if (Blockers.Count > 0)
            {
                text.AppendLine("БЛОКЕРЫ (" + Blockers.Count + "):");
                foreach (string blocker in Blockers) text.AppendLine("  ! " + blocker);
            }
            if (ManualSteps.Count > 0)
            {
                text.AppendLine("Осталось сделать вручную (" + ManualSteps.Count + "):");
                foreach (string step in ManualSteps) text.AppendLine("  · " + step);
            }
            text.AppendLine(Blocked ? "РЕЗУЛЬТАТ: ОСТАНОВЛЕНО" : "РЕЗУЛЬТАТ: " + (DryRun ? "план" : "OK"));
            return text.ToString();
        }
    }

    /// <summary>Установщик UI. Все методы идемпотентны.</summary>
    public static class UiInstaller
    {
        public const string LogTag = "[LFZ-UI]";
        private const string RootObjectName = "LifeFromZero UI";

        [MenuItem("Tools/Life From Zero/UI/Install UI into game", false, 40)]
        public static void InstallFromMenu()
        {
            string scenePath;
            if (!TryResolveScene(out scenePath))
            {
                EditorUtility.DisplayDialog("Life From Zero UI",
                    "Не удалось определить стартовую сцену.\n\n" +
                    "Запустите Tools → Life From Zero → Detect start scene, затем повторите.\n" +
                    "Установщик не угадывает сцену, чтобы не испортить чужую.", "Понятно");
                return;
            }

            UiInstallResult result = Install(scenePath, false);
            Debug.Log(LogTag + "\n" + result.ToText());
            if (result.Blocked)
            {
                EditorUtility.DisplayDialog("Life From Zero UI",
                    "Установка остановлена, файлы не изменены.\n\n" + string.Join("\n", result.Blockers.ToArray()), "Понятно");
                return;
            }

            EditorUtility.DisplayDialog("Life From Zero UI",
                (result.DryRun ? "План установки" : "Готово") + "\n\n" + result.ToText(), "OK");
        }

        [MenuItem("Tools/Life From Zero/UI/Check UI installation (dry run)", false, 41)]
        public static void DryRunFromMenu()
        {
            string scenePath;
            if (!TryResolveScene(out scenePath))
            {
                EditorUtility.DisplayDialog("Life From Zero UI",
                    "Стартовая сцена не определена: сначала Tools → Life From Zero → Detect start scene.", "Понятно");
                return;
            }

            UiInstallResult result = Install(scenePath, true);
            Debug.Log(LogTag + "\n" + result.ToText());
            EditorUtility.DisplayDialog("Life From Zero UI (проверка)", result.ToText(), "OK");
        }

        [MenuItem("Tools/Life From Zero/UI/Verify Scenes & Adapters (read-only)", false, 42)]
        public static void VerifyScenesFromMenu()
        {
            string report = BuildScenesVerificationReport(out int issues);
            Debug.Log(LogTag + " Проверка сцен (issues=" + issues + "):\n" + report);
            EditorUtility.DisplayDialog("Life From Zero UI — диагностика сцен", report, "OK");
        }

        // ------------------------------------------------------------------ ТЗ №045: починка объектов UI

        /// <summary>
        /// Починка объектов UI в ОТКРЫТЫХ сценах: Graphic без CanvasRenderer
        /// (MissingComponentException на «Grid»/«Route» в Play Mode), плюс отчёт о
        /// Missing Script и о сохранённых в сцене слоях карты.
        /// </summary>
        /// <remarks>
        /// Почему пересоздание компонента, а не AddComponent&lt;CanvasRenderer&gt;: Unity
        /// не даёт добавлять нативные компоненты скриптом не во всех версиях, а
        /// удаление Graphic и повторное AddComponent того же типа гарантированно
        /// выполняет [RequireComponent(typeof(CanvasRenderer))].
        /// </remarks>
        [MenuItem("Tools/Life From Zero/UI/Repair UI objects in open scenes", false, 43)]
        public static void RepairOpenScenesFromMenu()
        {
            var report = new StringBuilder();
            int repaired = 0;
            int problems = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                report.AppendLine("## Сцена `" + (string.IsNullOrEmpty(scene.path) ? scene.name : scene.path) + "`");
                int graphicProblems;
                repaired += RepairGraphicRenderers(scene, report, true, out graphicProblems);
                problems += graphicProblems;
                problems += ReportMissingScripts(scene, report);
                problems += ReportStaleMapLayers(scene, report);
            }

            report.AppendLine();
            report.AppendLine("Исправлено объектов: " + repaired + "; осталось проблем: " + problems + ".");
            if (repaired > 0)
                report.AppendLine("Сцены помечены изменёнными — сохраните их (Ctrl+S), автоматически не сохраняем.");

            Debug.Log(LogTag + " Починка UI-объектов:\n" + report);
            EditorUtility.DisplayDialog("Life From Zero UI — починка объектов", report.ToString(), "OK");
        }

        /// <summary>
        /// Пакетная починка в сценах Build Settings (с резервной копией каждой сцены).
        /// Запускается ТОЛЬКО с явным аргументом:
        /// Unity -batchmode -quit -executeMethod LifeFromZero.UI.Editor.UiInstaller.RepairBatch -lfzUiRepair
        /// </summary>
        public static void RepairBatch()
        {
            if (!HasArgument("-lfzUiRepair"))
            {
                Debug.Log(LogTag + " RepairBatch без -lfzUiRepair ничего не делает: " +
                                   "нужен явный флаг, чтобы случайно не изменить сцены.");
                return;
            }

            var report = new StringBuilder();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                Debug.LogWarning(LogTag + " Build Settings пуст — нечего чинить.");
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                string path = scenes[i].path;
                if (!scenes[i].enabled || string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                catch (Exception exception)
                {
                    report.AppendLine("## `" + path + "` — [FAIL] не открылась: " + exception.Message);
                    continue;
                }

                try
                {
                    report.AppendLine("## Сцена `" + path + "`");
                    int graphicProblems;
                    int repaired = RepairGraphicRenderers(scene, report, true, out graphicProblems);
                    ReportMissingScripts(scene, report);
                    ReportStaleMapLayers(scene, report);

                    if (repaired > 0)
                    {
                        string backup = BackupScene(path);
                        if (backup == null)
                        {
                            report.AppendLine("- [FAIL] резервная копия не создана — сцена НЕ сохранена.");
                            continue;
                        }
                        if (EditorSceneManager.SaveScene(scene, path))
                            report.AppendLine("- Сохранено (бэкап: " + backup + ").");
                        else
                            report.AppendLine("- [FAIL] не удалось сохранить сцену.");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            Debug.Log(LogTag + " Починка сцен:\n" + report);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Пересоздаёт графику без CanvasRenderer. Возвращает число исправленных.
        /// С <paramref name="apply"/> = false только перечисляет проблемы.
        /// </summary>
        private static int RepairGraphicRenderers(Scene scene, StringBuilder report, bool apply, out int problems)
        {
            int repaired = 0;
            problems = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UnityEngine.UI.Graphic graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (graphic == null) continue;

                    GameObject go = graphic.gameObject;
                    if (go.GetComponent<CanvasRenderer>() != null) continue;

                    string path = HierarchyPath(go);
                    Type type = graphic.GetType();

                    if (!apply)
                    {
                        problems++;
                        report.AppendLine("- [FAIL] " + type.Name + " без CanvasRenderer: `" + path +
                                          "` — uGUI не сможет это нарисовать (и будет сыпать MissingComponentException).");
                        continue;
                    }

                    try
                    {
                        UnityEngine.Object.DestroyImmediate(graphic);
                        Component recreated = go.AddComponent(type);
                        if (recreated != null && go.GetComponent<CanvasRenderer>() != null)
                        {
                            report.AppendLine("- [FIXED] " + type.Name + ": добавлен CanvasRenderer (`" + path + "`).");
                            repaired++;
                        }
                        else
                        {
                            problems++;
                            report.AppendLine("- [FAIL] " + type.Name + " пересоздан, но CanvasRenderer так и не появился: `" +
                                              path + "` — удалите объект и запустите установку UI заново.");
                        }
                    }
                    catch (Exception exception)
                    {
                        problems++;
                        report.AppendLine("- [FAIL] " + type.Name + " (`" + path + "`): " + exception.Message);
                    }
                }
            }

            return repaired;
        }

        /// <summary>Объекты с Missing Script — только отчёт: удалять чужое автоматически нельзя.</summary>
        private static int ReportMissingScripts(Scene scene, StringBuilder report)
        {
            int found = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == null) continue;
                    Component[] components = transform.gameObject.GetComponents<Component>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] != null) continue;
                        found++;
                        report.AppendLine("- [FAIL] Missing Script: объект `" + HierarchyPath(transform.gameObject) +
                                          "`, слот компонента #" + i +
                                          ". Скрипт удалён или переименован — назначьте нужный компонент вручную " +
                                          "(автоматически не удаляем: владелец сцены решает сам).");
                    }
                }
            }

            return found;
        }

        /// <summary>Сохранённые в сцене слои карты: MapScreen создаёт их заново в Awake.</summary>
        private static int ReportStaleMapLayers(Scene scene, StringBuilder report)
        {
            int found = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MapCanvasGraphic graphic in root.GetComponentsInChildren<MapCanvasGraphic>(true))
                {
                    if (graphic == null) continue;
                    found++;
                    report.AppendLine("- [FAIL] В сцене сохранён слой карты `" + HierarchyPath(graphic.gameObject) +
                                      "` (" + graphic.GetType().Name + "). MapScreen создаёт «Grid»/«Route» сам в Awake — " +
                                      "сохранённый объект лишний, его нужно удалить вручную.");
                }
            }

            return found;
        }

        private static string HierarchyPath(GameObject go)
        {
            if (go == null) return "<null>";
            string path = go.name;
            Transform parent = go.transform.parent;
            int guard = 0;
            while (parent != null && guard++ < 32)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>
        /// Пакетная проверка компиляции и связей сцен (Apartment67, District750, Build Settings)
        /// БЕЗ сохранения и перезаписи ассетов. Вызывается через Unity -batchmode -executeMethod.
        /// </summary>
        public static void RunBatchVerify()
        {
            string report = BuildScenesVerificationReport(out int issues);
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string dir = Path.Combine(projectRoot, "LifeFromZeroIntegration", "reports");
                Directory.CreateDirectory(dir);
                string outPath = Path.Combine(dir, "ui_scene_verify.md");
                File.WriteAllText(outPath, report);
                Debug.Log(LogTag + " Отчёт проверки записан: " + outPath + "\n" + report);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogTag + " Не удалось записать отчёт: " + exception.Message);
            }

            if (issues > 0 && HasArgument("-lfzStrictVerify"))
                Fail("найдены незакрытые блокеры сцен: " + issues);
        }

        /// <summary>Точка входа для Unity -batchmode -executeMethod.</summary>
        public static void InstallBatch()
        {
            string scenePath = Argument("-lfzUiScene");
            bool dryRun = HasArgument("-lfzUiDryRun");

            if (string.IsNullOrEmpty(scenePath) && !TryResolveScene(out scenePath))
            {
                Fail("стартовая сцена не определена: передайте -lfzUiScene \"Assets/…/Start.unity\"");
            }

            UiInstallResult result = Install(scenePath, dryRun);
            Debug.Log(LogTag + "\n" + result.ToText());

            if (result.Blocked) Fail("установка остановлена: " + string.Join("; ", result.Blockers.ToArray()));
            if (result.Changes.Count == 0) Debug.Log(LogTag + " Нечего делать: UI уже установлен в этой сцене.");
        }

        // ------------------------------------------------------------------ основное

        /// <summary>Установить UI в сцену. Возвращает отчёт; при блокере файлы не меняются.</summary>
        public static UiInstallResult Install(string scenePath, bool dryRun)
        {
            var result = new UiInstallResult { ScenePath = scenePath, DryRun = dryRun };

            if (!File.Exists(scenePath))
            {
                result.Blocked = true;
                result.Blockers.Add("файл сцены не найден: " + scenePath);
                return result;
            }

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            catch (Exception exception)
            {
                result.Blocked = true;
                result.Blockers.Add("не удалось открыть сцену: " + exception.Message);
                return result;
            }

            try
            {
                SceneManager.SetActiveScene(scene);

                // В проекте УЖЕ есть пауза (City.Pause). Дублировать её нельзя, поэтому
                // установщик не блокирует сцену, а переводит UI в режим «пауза занята»:
                // меню открывает существующий City.Pause, а наш экран — только его вид.
                Package pause = FindExistingPause(scene);
                if (pause != null)
                {
                    result.AlreadyDone.Add("пауза игры: " + pause.Hint + " — UI будет использовать её, второго меню не будет");
                }

                GameObject root = FindRoot(scene);
                if (root != null)
                {
                    result.AlreadyDone.Add("объект «" + RootObjectName + "» уже есть");
                }
                else
                {
                    result.Changes.Add("создан объект «" + RootObjectName + "» с UiBootstrap");
                }

                if (FindByType<UiBootstrap>(scene) == null)
                {
                    result.Changes.Add("добавлен компонент UiBootstrap");
                }
                else
                {
                    result.AlreadyDone.Add("компонент UiBootstrap уже есть");
                }

                foreach (Type screen in new[] { typeof(UiRouter), typeof(HudScreen), typeof(PhoneScreen), typeof(MapScreen), typeof(PauseMenuScreen) })
                {
                    if (FindByType(scene, screen) != null)
                    {
                        result.AlreadyDone.Add("экран " + screen.Name + " уже есть");
                        continue;
                    }
                    result.Changes.Add("создан экран " + screen.Name);
                }

                if (FindByType<QuarterLocationProvider>(scene) == null)
                {
                    result.Changes.Add("добавлен адаптер геопозиции доставки QuarterLocationProvider");
                }
                else
                {
                    result.AlreadyDone.Add("QuarterLocationProvider уже есть");
                }

                if (NeedsAdapterWiring(scene, root))
                {
                    result.Changes.Add("связаны сериализованные поля Inspector между UiBootstrap, мостами и сценой");
                }

                if (FindByType<UnityEngine.EventSystems.EventSystem>(scene) == null)
                {
                    result.Changes.Add("создан EventSystem (InputSystemUIInputModule / StandaloneInputModule)");
                }

                AddManualSteps(result);

                if (dryRun) return result;

                // Резервная копия файла сцены ДО изменений.
                string backup = BackupScene(scenePath);
                if (backup == null)
                {
                    result.Blocked = true;
                    result.Blockers.Add("не удалось создать резервную копию сцены — изменения отменены");
                    return result;
                }
                result.BackupDir = Path.GetDirectoryName(backup);

                if (result.Changes.Count > 0)
                {
                    if (root == null)
                    {
                        root = new GameObject(RootObjectName);
                        SceneManager.MoveGameObjectToScene(root, scene);
                    }

                    var bootstrap = root.GetComponent<UiBootstrap>() ?? root.AddComponent<UiBootstrap>();

                    // Мост к существующим системам: позиция игрока (камера) и, если в
                    // сцене есть City.Pause, — источник данных и передача действий.
                    var bridge = root.GetComponent<UiGameBridge>() ?? root.AddComponent<UiGameBridge>();
                    var pauseSnapshot = root.GetComponent<PauseUiSnapshotSource>() ?? root.AddComponent<PauseUiSnapshotSource>();
                    var deliveryBridge = root.GetComponent<DeliveryServiceBridge>() ?? root.AddComponent<DeliveryServiceBridge>();
                    var locationProvider = root.GetComponent<QuarterLocationProvider>() ?? root.AddComponent<QuarterLocationProvider>();
                    var menuActions = root.GetComponent<UiMenuActions>() ?? root.AddComponent<UiMenuActions>();

                    // UiBootstrap сам создаёт экраны в Awake, но если проект запускает
                    // интерфейс из кода (UiBootstrap.Ensure) — экраны всё равно нужны.
                    // Поэтому кладём их в сцену один раз, а не полагаемся на рантайм.
                    AddScreen<HudScreen>(root, "HUD");
                    AddScreen<PhoneScreen>(root, "Phone");
                    AddScreen<MapScreen>(root, "Map");
                    AddScreen<PauseMenuScreen>(root, "Pause Menu");

                    WireSceneReferences(scene, bootstrap, bridge, pauseSnapshot, deliveryBridge, locationProvider, menuActions);

                    if (FindByType<UnityEngine.EventSystems.EventSystem>(scene) == null &&
                        !HasEventSystemInScene(scene))
                    {
                        var eventSystem = new GameObject("EventSystem");
                        SceneManager.MoveGameObjectToScene(eventSystem, scene);
                        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                        AddPreferredInputModule(eventSystem);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                    {
                        result.Blocked = true;
                        result.Blockers.Add("не удалось сохранить сцену: " + scenePath);
                        return result;
                    }
                    AssetDatabase.Refresh();
                }

                result.Completed = true;
                return result;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ------------------------------------------------------------------ проверки

        private static void AddManualSteps(UiInstallResult result)
        {
            result.ManualSteps.Add(
                "UiMenuActions: для кнопок «Сохранить»/«Загрузить»/«Главное меню» назначить компоненты, " +
                "реализующие IUiGameAction. Без них кнопки НЕактивны (это правильно: без сохранения " +
                "кнопка «Сохранить» врала бы игроку).");
            result.ManualSteps.Add(
                "UiGameBridge → Player Input Behaviours: перетащить компоненты движения и камеры " +
                "(без них персонаж продолжит двигаться при открытом окне). " +
                "Tools → Life From Zero → Report input candidates показывает кандидатов.");
            result.ManualSteps.Add(
                "UiGameBridge → Player: если камера не от первого лица, указать трансформ игрока " +
                "(по умолчанию берётся камера).");
            result.ManualSteps.Add(
                "UiGameBridge → Gameplay HUD Roots: объекты игрового HUD, которые должны скрываться " +
                "при открытом UI (иначе будут видны поверх телефона и паузы).");
            result.ManualSteps.Add(
                "Телефон: реализовать IPhoneDataSource (ReadPhone/TryRun) и отдать его в UiBootstrap, " +
                "либо реализовать интерфейс на PauseUiSnapshotSource. Сейчас телефон показывает каркас.");
            result.ManualSteps.Add(
                "Заказы: написать наследника DeliveryServiceHost (CreateService() → ваш IDeliveryService) " +
                "и повесить на объект сцены. DeliveryServiceBridge сам найдёт его и подключит телефон.");
            result.ManualSteps.Add(
                "Если сцене нужен план района не из QuarterUnityAdapter — вызвать " +
                "UiBootstrap.SetDistrictPlan(plan) после генерации города.");
            result.ManualSteps.Add(
                "Проверить, что PauseKeyboardInput (если есть) включён ровно один раз: " +
                "UiBootstrap сам выключает чтение клавиш у UiRouter, когда находит City.Pause.");
        }

        /// <summary>Найденная в сцене существующая пауза проекта (City.Pause).</summary>
        private sealed class Package
        {
            public string Hint;
        }

        /// <summary>
        /// Ищет паузу, которая уже есть в сцене. Возвращает её описание или null.
        /// Типы ищутся по имени, а не по типу: установщик не должен ломаться, если
        /// проект переименует свой класс паузы.
        /// </summary>
        private static Package FindExistingPause(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    string name = behaviour.GetType().Name;
                    if (name == "PauseController") return new Package { Hint = "PauseController найден" };
                    if (name == "StandalonePauseIntegration") return new Package { Hint = "StandalonePauseIntegration найден" };
                    if (name == "PauseKeyboardInput") return new Package { Hint = "PauseKeyboardInput найден" };
                }
            }
            return null;
        }

        private static void AddPreferredInputModule(GameObject eventSystemGo)
        {
            Type inputSystemUiType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiType != null && typeof(Component).IsAssignableFrom(inputSystemUiType))
            {
                eventSystemGo.AddComponent(inputSystemUiType);
                return;
            }
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private static bool NeedsAdapterWiring(Scene scene, GameObject root)
        {
            if (root == null) return true;
            var bootstrap = root.GetComponent<UiBootstrap>();
            var delivery = root.GetComponent<DeliveryServiceBridge>();
            var location = root.GetComponent<QuarterLocationProvider>();
            if (bootstrap == null || delivery == null || location == null) return true;

            return IsPropertyNull(bootstrap, "worldAdapter") ||
                   IsPropertyNull(bootstrap, "snapshotSource") ||
                   IsPropertyNull(bootstrap, "deliverySystem") ||
                   IsPropertyNull(delivery, "locationProvider") ||
                   IsPropertyNull(delivery, "walletSource") ||
                   (FindInterfaceBehaviour<LifeFromZero.City.Quarter.IQuarterPlanSource>(scene) != null &&
                    IsPropertyNull(bootstrap, "districtSource"));
        }

        private static bool IsPropertyNull(UnityEngine.Object target, string propertyName)
        {
            if (target == null) return true;
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(propertyName);
            return prop != null && prop.objectReferenceValue == null;
        }

        private static void SetIfNull(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            if (value == null) return;
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null && prop.objectReferenceValue == null)
                prop.objectReferenceValue = value;
        }

        private static void WireSceneReferences(
            Scene scene,
            UiBootstrap bootstrap,
            UiGameBridge bridge,
            PauseUiSnapshotSource pauseSnapshot,
            DeliveryServiceBridge deliveryBridge,
            QuarterLocationProvider locationProvider,
            UiMenuActions menuActions)
        {
            MonoBehaviour districtSource = FindInterfaceBehaviour<LifeFromZero.City.Quarter.IQuarterPlanSource>(scene);
            var existingPause = FindByType<LifeFromZero.City.Pause.PauseIntegration>(scene);
            var pauseData = FindByType<LifeFromZero.City.Pause.PauseDataSource>(scene);
            var deliveryHost = FindByType<DeliveryServiceHost>(scene);
            MonoBehaviour clockAdapter = FindInterfaceBehaviour<LifeFromZero.Jobs.Delivery.IGameClockAdapter>(scene);
            MonoBehaviour payoutAdapter = FindInterfaceBehaviour<LifeFromZero.Jobs.Delivery.IPayoutAdapter>(scene);

            if (bootstrap != null)
            {
                var so = new SerializedObject(bootstrap);
                SetIfNull(so, "worldAdapter", bridge);
                SetIfNull(so, "snapshotSource", pauseSnapshot);
                SetIfNull(so, "deliverySystem", deliveryBridge);
                SetIfNull(so, "districtSource", districtSource);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (bridge != null)
            {
                var so = new SerializedObject(bridge);
                SetIfNull(so, "existingPause", existingPause);
                SetIfNull(so, "menuActions", menuActions);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (pauseSnapshot != null)
            {
                var so = new SerializedObject(pauseSnapshot);
                SetIfNull(so, "dataSource", pauseData);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (locationProvider != null)
            {
                var so = new SerializedObject(locationProvider);
                SetIfNull(so, "worldAdapter", bridge);
                SetIfNull(so, "districtSource", districtSource);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // ТЗ №045: поле phoneSystem в UiBootstrap было мёртвым — интегратор назначал
            // телефонный мост, но бутстрап его не читал, и телефон открывался пустым.
            // Теперь установщик сам кладёт найденный IPhoneDataSource (PhoneFacadeUiBridge).
            if (bootstrap != null)
            {
                MonoBehaviour phoneSource = FindInterfaceBehaviourExcept<IPhoneDataSource>(scene,
                    typeof(DeliveryServiceBridge));
                var soPhone = new SerializedObject(bootstrap);
                SetIfNull(soPhone, "phoneSystem", phoneSource);
                soPhone.ApplyModifiedPropertiesWithoutUndo();
            }

            if (deliveryBridge != null)
            {
                var so = new SerializedObject(deliveryBridge);
                SetIfNull(so, "locationProvider", locationProvider);
                SetIfNull(so, "walletSource", pauseSnapshot);
                SetIfNull(so, "districtSource", districtSource);
                SetIfNull(so, "deliveryServiceSource", deliveryHost);
                SetIfNull(so, "clockAdapter", clockAdapter);
                SetIfNull(so, "payoutAdapter", payoutAdapter);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static MonoBehaviour FindInterfaceBehaviourExcept<TInterface>(Scene scene, Type excluded)
            where TInterface : class
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    if (!(mb is TInterface)) continue;
                    if (excluded != null && excluded.IsInstanceOfType(mb)) continue;
                    return mb;
                }
            }
            return null;
        }

        private static MonoBehaviour FindInterfaceBehaviour<TInterface>(Scene scene) where TInterface : class
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb is TInterface) return mb;
                }
            }
            return null;
        }

        private static string BuildScenesVerificationReport(out int issueCount)
        {
            issueCount = 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Life From Zero — проверка компиляции и связей сцен (Unity)");
            sb.AppendLine();

            var candidatePaths = new List<string>();
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            if (buildScenes != null && buildScenes.Length > 0)
            {
                sb.AppendLine("## Build Settings");
                for (int i = 0; i < buildScenes.Length; i++)
                {
                    EditorBuildSettingsScene s = buildScenes[i];
                    sb.AppendLine("- [" + (s.enabled ? "x" : " ") + "] `" + s.path + "`");
                    if (s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path) &&
                        !candidatePaths.Contains(s.path))
                    {
                        candidatePaths.Add(s.path);
                    }
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("- [WARN] Build Settings пуст — переходы между Apartment67 и District750 требуют включения обеих сцен.");
                issueCount++;
            }

            string startScene;
            if (TryResolveScene(out startScene) && !candidatePaths.Contains(startScene))
                candidatePaths.Insert(0, startScene);

            string defaultDistrict = "Assets/_Project/Scenes/District750.unity";
            if (File.Exists(defaultDistrict) && !candidatePaths.Contains(defaultDistrict))
                candidatePaths.Add(defaultDistrict);

            for (int i = 0; i < candidatePaths.Count; i++)
            {
                string path = candidatePaths[i];
                sb.AppendLine("## Сцена `" + path + "`");
                if (!File.Exists(path))
                {
                    sb.AppendLine("- [FAIL] Файл сцены не найден.");
                    issueCount++;
                    continue;
                }

                Scene scene;
                try
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                catch (Exception ex)
                {
                    sb.AppendLine("- [FAIL] Ошибка открытия: " + ex.Message);
                    issueCount++;
                    continue;
                }

                try
                {
                    var uiBootstrap = FindByType<UiBootstrap>(scene);
                    var eventSystem = FindByType<UnityEngine.EventSystems.EventSystem>(scene);
                    var pauseCtrl = FindByType<LifeFromZero.City.Pause.PauseController>(scene);
                    var pauseInt = FindByType<LifeFromZero.City.Pause.PauseIntegration>(scene);
                    var pauseData = FindByType<LifeFromZero.City.Pause.PauseDataSource>(scene);
                    var district = FindInterfaceBehaviour<LifeFromZero.City.Quarter.IQuarterPlanSource>(scene);
                    Component districtLink = FindComponentByTypeName(scene, "District750Link");
                    Component homeAnchor = FindComponentByTypeName(scene, "PlayerHomeAnchor");

                    sb.AppendLine("- UiBootstrap: " + (uiBootstrap != null ? "есть" : "нет (нормально для вторичной сцены, если приходит через DontDestroyOnLoad из стартовой)"));
                    sb.AppendLine("- EventSystem: " + (eventSystem != null ? DescribeEventSystem(eventSystem) : "нет"));
                    sb.AppendLine("- PauseController / PauseIntegration: " +
                                  (pauseCtrl != null ? "PauseController есть" : "нет") + " / " +
                                  (pauseInt != null ? pauseInt.GetType().Name : "нет"));
                    sb.AppendLine("- PauseDataSource: " + (pauseData != null ? pauseData.GetType().Name : "нет (покажет честное «Нет данных»)"));
                    sb.AppendLine("- IQuarterPlanSource (район): " + (district != null ? district.GetType().Name : "нет"));
                    var phoneSource = FindInterfaceBehaviourExcept<IPhoneDataSource>(scene, typeof(DeliveryServiceBridge));
                    sb.AppendLine("- IPhoneDataSource (телефон): " +
                                  (phoneSource != null
                                      ? phoneSource.GetType().Name
                                      : "нет (телефон покажет неактивные плитки — подключите PhoneFacadeUiBridge)"));
                    sb.AppendLine("- Переходы сцен: District750Link=" + (districtLink != null ? "есть" : "нет") +
                                  ", PlayerHomeAnchor=" + (homeAnchor != null ? "есть" : "нет"));

                    // ТЗ №045: дефекты, которые в Play Mode выглядят как «M не открывает карту»
                    // (Graphic без CanvasRenderer) и как Missing Script в консоли.
                    var uiIssues = new StringBuilder();
                    int rendererProblems;
                    RepairGraphicRenderers(scene, uiIssues, false, out rendererProblems);
                    int missingScripts = ReportMissingScripts(scene, uiIssues);
                    int staleMapLayers = ReportStaleMapLayers(scene, uiIssues);

                    if (uiIssues.Length > 0)
                    {
                        sb.Append(uiIssues.ToString());
                    }

                    sb.AppendLine("- Слои карты (Graphic/CanvasRenderer): " +
                                  (rendererProblems == 0 ? "проблем нет" : "ПРОБЛЕМ " + rendererProblems));
                    sb.AppendLine("- Missing Script: " + (missingScripts == 0 ? "нет" : "ПРОБЛЕМ " + missingScripts));
                    sb.AppendLine("- Сохранённые слои карты (должны создаваться в рантайме): " +
                                  (staleMapLayers == 0 ? "нет" : "ПРОБЛЕМ " + staleMapLayers));
                    issueCount += rendererProblems + missingScripts + staleMapLayers;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string DescribeEventSystem(UnityEngine.EventSystems.EventSystem es)
        {
            var modules = es.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
            if (modules == null || modules.Length == 0) return "ЕСТЬ, но БЕЗ InputModule (!)";
            var names = new string[modules.Length];
            for (int i = 0; i < modules.Length; i++)
                names[i] = modules[i] != null ? modules[i].GetType().Name : "null";
            return string.Join(", ", names);
        }

        private static Component FindComponentByTypeName(Scene scene, string typeName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb != null && mb.GetType().Name == typeName) return mb;
                }
            }
            return null;
        }

        private static void AddScreen<T>(GameObject root, string name) where T : Component, new()
        {
            if (root.GetComponentInChildren<T>(true) != null) return;
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<T>();
        }

        private static bool HasEventSystemInScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null) return true;
            }
            return false;
        }

        private static GameObject FindRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == RootObjectName) return root;
            }
            return null;
        }

        private static T FindByType<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static Component FindByType(Scene scene, Type type)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component found = root.GetComponentInChildren(type, true);
                if (found != null) return found;
            }
            return null;
        }

        private static string BackupScene(string scenePath)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string dir = Path.Combine(projectRoot, "LifeFromZeroIntegration", "backups", stamp);
                Directory.CreateDirectory(dir);

                string target = Path.Combine(dir, Path.GetFileName(scenePath));
                File.Copy(scenePath, target, true);

                // Метаданные рядом: без них бэкап сцены бесполезен как ссылка.
                string meta = scenePath + ".meta";
                if (File.Exists(meta)) File.Copy(meta, target + ".meta", true);

                return target;
            }
            catch (Exception exception)
            {
                Debug.LogError(LogTag + " Резервная копия не создана: " + exception.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------ сцена и аргументы

        /// <summary>Стартовая сцена: отчёт детектора, затем единственная сцена в Build Settings.</summary>
        private static bool TryResolveScene(out string scenePath)
        {
            scenePath = null;

            string report = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "LifeFromZeroIntegration", "discovery.json");
            if (File.Exists(report))
            {
                try
                {
                    string text = File.ReadAllText(report);
                    const string key = "\"startScene\"";
                    int index = text.IndexOf(key, StringComparison.Ordinal);
                    if (index >= 0)
                    {
                        int colon = text.IndexOf(':', index);
                        int firstQuote = text.IndexOf('"', colon + 1);
                        int secondQuote = text.IndexOf('"', firstQuote + 1);
                        if (colon > 0 && firstQuote > colon && secondQuote > firstQuote)
                        {
                            string candidate = text.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                            if (File.Exists(candidate)) { scenePath = candidate; return true; }
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(LogTag + " Отчёт детектора не прочитан: " + exception.Message);
                }
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes != null)
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    if (!scenes[i].enabled) continue;
                    if (scenePath != null) return false; // больше одной — не угадываем
                    scenePath = scenes[i].path;
                }
            }

            return !string.IsNullOrEmpty(scenePath) && File.Exists(scenePath);
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        private static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void Fail(string message)
        {
            Debug.LogError(LogTag + " " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw new InvalidOperationException(message);
        }
    }
}
