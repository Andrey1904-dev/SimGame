// Life From Zero — City/UnityIntegration/Editor/LifeFromZeroMigration.cs (ТЗ #039, финальное дополнение)
// Namespace: LifeFromZero.Setup
//
// ДИАГНОСТИКА ПЕРЕХОДА Quarter250 → District750 и БЕЗОПАСНОЕ включение District750
// как основного района. Ничего не удаляет, Quarter250 не трогает (он остаётся
// выбираемым значением Layout Id), числа 250 на 750 не подменяет.
//
// Пункты меню:
//   Tools → Life From Zero → Migration: diagnose Quarter250 → District750   (только чтение)
//   Tools → Life From Zero → Migration: make District750 the main district   (с бэкапом сцены)
//
// Batch:
//   -executeMethod LifeFromZero.Setup.LifeFromZeroMigration.DiagnoseBatch
//   -executeMethod LifeFromZero.Setup.LifeFromZeroMigration.MigrateBatch -lfzScene <Assets/...unity> [-lfzApply]
//   (без -lfzApply — dry-run: план без изменений)
//
// Результат в логе: "RESULT: OK report=<путь>" | "RESULT: DRY_RUN_OK" | "RESULT: BLOCKED".
// Отчёты и резервные копии — вне Assets: <проект>/LifeFromZeroIntegration/…
//
// ВНИМАНИЕ: в среде репозитория Unity нет, файл не компилировался в Unity.
// Используются реальные API: EditorSceneManager, SerializedObject, EditorBuildSettings,
// Camera, CityBootstrap/QuarterUnityAdapter (публичные свойства только для чтения плана).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LifeFromZero.City.Quarter.UnityAdapter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LifeFromZero.Setup
{
    /// <summary>Найденный в сцене CityBootstrap (параметры генерации).</summary>
    public sealed class BootstrapFacts
    {
        public string ScenePath;
        public string HierarchyPath;
        public string LayoutId = "";
        public int Seed;
        public bool GenerateOnStart;
        public bool AddColliders;
        public int AdapterOnSameObject;
        public bool AdapterBuildOnStart;
        public Vector3 Position;
        public int BootstrapCountInScene;
    }

    /// <summary>Камера, найденная в сцене (для проверки обзора района 750×750).</summary>
    public sealed class CameraFacts
    {
        public string ScenePath;
        public string HierarchyPath;
        public bool Orthographic;
        public float OrthographicSize;
        public float FarClip;
        public Vector3 Position;
    }

    /// <summary>Кандидат на точку спавна игрока (определяется по имени типа/объекта, проверяется вручную).</summary>
    public sealed class SpawnFacts
    {
        public string ScenePath;
        public string HierarchyPath;
        public string ComponentType = "";
        public Vector3 Position;
        public bool InsideDistrict;
    }

    /// <summary>Совпадение в коде проекта (кандидат на ручное решение, не авто-правку).</summary>
    public sealed class CodeHit
    {
        public string File;
        public int Line;
        public string Kind;
        public string Text;
        public override string ToString() =>
            File + ":" + Line.ToString(CultureInfo.InvariantCulture) + "  [" + Kind + "]  " + Text;
    }

    /// <summary>Результат диагностики перехода на District750 (только чтение).</summary>
    public sealed class MigrationReport
    {
        public string ProjectRoot;
        public string Stamp;
        public readonly List<string> Scenes = new List<string>();
        public readonly List<BootstrapFacts> Bootstraps = new List<BootstrapFacts>();
        public readonly List<CameraFacts> Cameras = new List<CameraFacts>();
        public readonly List<SpawnFacts> Spawns = new List<SpawnFacts>();
        public readonly List<CodeHit> CodeHits = new List<CodeHit>();
        public readonly List<string> BuildSettings = new List<string>();
        public readonly List<string> Findings = new List<string>();
        public readonly List<string> Risks = new List<string>();
        public readonly List<string> Actions = new List<string>();
        public string ReportPath;
    }

    /// <summary>План переключения сцены на District750.</summary>
    public sealed class MigrationPlan
    {
        public string ScenePath;
        public bool DryRun;
        public readonly List<string> Findings = new List<string>();
        public readonly List<string> Changes = new List<string>();
        public readonly List<string> Blockers = new List<string>();
        public readonly List<string> Actions = new List<string>();
        public string BackupDir;
        public string ReportPath;
        public string PreviousLayoutId = "";
        public bool Blocked;

        public string Summary()
        {
            string head = Blocked ? "BLOCKED" : (DryRun ? "DRY-RUN" : "OK");
            return head + " scene=" + (ScenePath ?? "—") +
                   " changes=" + Changes.Count.ToString(CultureInfo.InvariantCulture) +
                   " blockers=" + Blockers.Count.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static class LifeFromZeroMigration
    {
        internal const string LogTag = "[LFZ-MIGRATION]";
        public const string OutputFolder = "LifeFromZeroIntegration";
        public const string DistrictLayoutId = "district750";
        public const string OldLayoutId = "quarter250";
        private const float DistrictSize = 750f;

        // ===================== точки входа =====================

        [MenuItem("Tools/Life From Zero/Migration: diagnose Quarter250 to District750", false, 31)]
        public static void DiagnoseFromMenu()
        {
            MigrationReport report = Diagnose();
            EditorUtility.DisplayDialog("Life From Zero — миграция",
                "Диагностика завершена.\nОтчёт: " + (report.ReportPath ?? "не записан") +
                "\nНайдено рисков: " + report.Risks.Count.ToString(CultureInfo.InvariantCulture),
                "OK");
        }

        public static void DiagnoseBatch()
        {
            try
            {
                MigrationReport report = Diagnose();
                Debug.Log(LogTag + " RESULT: OK report=" + report.ReportPath);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError(LogTag + " RESULT: FAILED " + e.GetType().Name + ": " + e.Message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void MigrateBatch()
        {
            string scenePath = CommandLineValue("-lfzScene");
            bool apply = CommandLineHas("-lfzApply");
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                Debug.LogError(LogTag + " RESULT: FAILED нет -lfzScene <Assets/…unity> (сцена будущего основного района).");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            MigrationPlan plan = Migrate(scenePath, !apply);
            int code = plan.Blocked ? 3 : 0;
            Debug.Log(LogTag + " RESULT: " + (plan.Blocked ? "BLOCKED" : (plan.DryRun ? "DRY_RUN_OK" : "OK")) +
                      " report=" + plan.ReportPath);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ===================== диагностика (только чтение) =====================

        /// <summary>
        /// Собирает факты проекта: генераторы по сценам, риск двойной генерации, камеры,
        /// кандидаты спавна, границы мира, коллизии, старые константы 250 в коде,
        /// Build Settings. Ничего не изменяет; пишет отчёт вне Assets.
        /// </summary>
        public static MigrationReport Diagnose()
        {
            var report = new MigrationReport
            {
                ProjectRoot = LifeFromZeroDiscovery.ProjectRoot(),
                Stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            };

            CollectScenes(report);
            CollectCode(report);
            CollectBuildSettings(report);
            Analyse(report);
            WriteReport(report);

            Debug.Log(LogTag + " диагностика: сцен=" + report.Scenes.Count +
                      ", CityBootstrap=" + report.Bootstraps.Count +
                      ", камер=" + report.Cameras.Count +
                      ", кандидатов спавна=" + report.Spawns.Count +
                      ", совпадений в коде=" + report.CodeHits.Count +
                      ", рисков=" + report.Risks.Count + ".");
            return report;
        }

        private static void CollectScenes(MigrationReport report)
        {
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && !string.IsNullOrEmpty(s.path)) loaded.Add(s.path);
            }

            List<string> paths = LifeFromZeroDiscovery.AllScenePaths();
            report.Scenes.AddRange(paths);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                bool openedByUs = !loaded.Contains(path);
                Scene scene;
                try
                {
                    scene = openedByUs
                        ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                        : SceneManager.GetSceneByPath(path);
                }
                catch (Exception e)
                {
                    report.Findings.Add("сцена не открылась: " + path + " — " + e.Message);
                    continue;
                }

                try
                {
                    CollectScene(report, scene, path);
                }
                finally
                {
                    if (openedByUs && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void CollectScene(MigrationReport report, Scene scene, string path)
        {
            CityBootstrap[] bootstraps = CollectAll<CityBootstrap>(scene);
            QuarterUnityAdapter[] adapters = CollectAll<QuarterUnityAdapter>(scene);

            for (int i = 0; i < bootstraps.Length; i++)
            {
                CityBootstrap b = bootstraps[i];
                var so = new SerializedObject(b);
                var facts = new BootstrapFacts
                {
                    ScenePath = path,
                    HierarchyPath = LifeFromZeroGameIntegration.Describe(b),
                    LayoutId = Str(so, "layoutId"),
                    Seed = Int(so, "seed"),
                    GenerateOnStart = Bool(so, "generateOnStart"),
                    AddColliders = Bool(so, "addColliders"),
                    Position = b.transform.position,
                    BootstrapCountInScene = bootstraps.Length,
                };
                for (int a = 0; a < adapters.Length; a++)
                {
                    if (adapters[a].gameObject != b.gameObject) continue;
                    facts.AdapterOnSameObject++;
                    facts.AdapterBuildOnStart |= Bool(new SerializedObject(adapters[a]), "buildOnStart");
                }
                report.Bootstraps.Add(facts);
            }

            for (int i = 0; i < adapters.Length; i++)
            {
                if (FindBootstrapOn(adapters[i].gameObject)) continue;
                report.Findings.Add("отдельный QuarterUnityAdapter без CityBootstrap: " +
                    LifeFromZeroGameIntegration.Describe(adapters[i]) +
                    " (buildOnStart=" + Bool(new SerializedObject(adapters[i]), "buildOnStart") + ")");
            }

            Camera[] cameras = CollectAll<Camera>(scene);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                report.Cameras.Add(new CameraFacts
                {
                    ScenePath = path,
                    HierarchyPath = LifeFromZeroGameIntegration.Describe(cam),
                    Orthographic = cam.orthographic,
                    OrthographicSize = cam.orthographicSize,
                    FarClip = cam.farClipPlane,
                    Position = cam.transform.position,
                });
            }

            // Кандидаты на точку спавна игрока: по имени объекта/типа. Это НЕ автоматическая
            // привязка — список для проверки владельцем/Codex.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Transform[] all = CollectAll<Transform>(scene);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (!LooksLikeSpawn(t)) continue;
                if (!seen.Add(t.GetInstanceID().ToString(CultureInfo.InvariantCulture))) continue;
                Vector3 p = t.position;
                report.Spawns.Add(new SpawnFacts
                {
                    ScenePath = path,
                    HierarchyPath = LifeFromZeroGameIntegration.Describe(t),
                    Position = p,
                    InsideDistrict = p.x >= 0f && p.x <= DistrictSize && p.z >= 0f && p.z <= DistrictSize,
                });
            }
        }

        private static void CollectCode(MigrationReport report)
        {
            string assets = Path.Combine(LifeFromZeroDiscovery.ProjectRoot(), "Assets");
            if (!Directory.Exists(assets)) return;

            string[] files;
            try { files = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories); }
            catch (Exception e) { report.Findings.Add("код не прочитан: " + e.Message); return; }

            for (int f = 0; f < files.Length; f++)
            {
                string file = files[f];
                string relative = Relative(file);
                string[] lines4;
                try { lines4 = File.ReadAllLines(file); }
                catch { continue; }

                for (int i = 0; i < lines4.Length; i++)
                {
                    string line = lines4[i];
                    string kind = MatchCode(line);
                    if (kind == null) continue;
                    report.CodeHits.Add(new CodeHit
                    {
                        File = relative,
                        Line = i + 1,
                        Kind = kind,
                        Text = line.Trim(),
                    });
                }
            }
        }

        private static string MatchCode(string line)
        {
            if (line.Contains("QuarterLayoutIds.Quarter250")) return "layout-quarter250";
            if (line.Contains("QuarterLayoutIds.District750")) return "layout-district750";
            if (line.Contains("QuarterGenerator.Generate")) return "old-generator";
            if (line.Contains("DistrictMapGenerator")) return "new-generator";
            if (line.Contains("CityBootstrap")) return "bootstrap";
            if (line.Contains("250f") || line.Contains("= 250") || line.Contains("(250")) return "magic-250";
            if (line.Contains("125f") || line.Contains("= 125") || line.Contains("(125")) return "magic-125";
            return null;
        }

        private static void CollectBuildSettings(MigrationReport report)
        {
            EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
            for (int i = 0; i < build.Length; i++)
            {
                if (build[i] == null) continue;
                report.BuildSettings.Add("[" + i + "] " + (build[i].enabled ? "вкл " : "выкл ") + build[i].path);
            }
        }

        private static void Analyse(MigrationReport report)
        {
            // 1. Двойная генерация: два района в одной сцене или автозапуск адаптера вместе с bootstrap.
            bool anyDistrictMain = false;
            for (int i = 0; i < report.Bootstraps.Count; i++)
            {
                BootstrapFacts b = report.Bootstraps[i];
                if (b.BootstrapCountInScene > 1)
                    report.Risks.Add("ДВОЙНАЯ ГЕНЕРАЦИЯ: в сцене " + b.ScenePath + " " +
                        b.BootstrapCountInScene + " CityBootstrap — построятся два района.");
                if (b.AdapterBuildOnStart)
                    report.Risks.Add("ДВОЙНАЯ ГЕНЕРАЦИЯ: QuarterUnityAdapter на том же объекте имеет Build On Start = on " +
                        "(" + b.ScenePath + "): при старте построятся два города.");
                if (b.AdapterOnSameObject == 0)
                    report.Risks.Add("У CityBootstrap нет QuarterUnityAdapter на том же объекте (" + b.ScenePath + "): " +
                        "он будет добавлен автоматически в Awake — проверьте, что второй адаптер не висит отдельно.");
                if (string.Equals(b.LayoutId, DistrictLayoutId, StringComparison.OrdinalIgnoreCase)) anyDistrictMain = true;
                if (b.AddColliders == false)
                    report.Risks.Add("Коллизии выключены (Add Colliders = off) в " + b.ScenePath +
                        ": в районе не будет столкновений со стенами и бордюрами.");
                if (!b.GenerateOnStart)
                    report.Risks.Add("Generate On Start = off в " + b.ScenePath + ": район не появится сам — " +
                        "нужен вызов CityBootstrap.Generate() из существующего кода.");
                if (Math.Abs(b.Position.x) > 0.01f || Math.Abs(b.Position.z) > 0.01f)
                    report.Risks.Add("CityBootstrap смещён от начала координат (" + Format(b.Position) + ") в " +
                        b.ScenePath + ": границы района сдвинуты, проверьте спавн и камеру.");
            }

            if (!anyDistrictMain)
                report.Findings.Add("ни одна сцена не настроена на Layout Id = " + DistrictLayoutId +
                    " — сейчас игра строит другой район (см. Bootstraps).");

            // 2. Камера: район 750×750 требует orthographicSize ≥ 375 или достаточного far clip.
            for (int i = 0; i < report.Cameras.Count; i++)
            {
                CameraFacts c = report.Cameras[i];
                if (c.Orthographic && c.OrthographicSize < DistrictSize / 2f)
                    report.Risks.Add("Камера " + c.HierarchyPath + " (" + c.ScenePath + "): orthographicSize=" +
                        c.OrthographicSize.ToString("0.##", CultureInfo.InvariantCulture) +
                        " < 375 — район 750×750 не помещается в обзор целиком.");
                if (!c.Orthographic && c.FarClip < DistrictSize + 50f)
                    report.Risks.Add("Камера " + c.HierarchyPath + " (" + c.ScenePath + "): farClip=" +
                        c.FarClip.ToString("0.##", CultureInfo.InvariantCulture) +
                        " — дальние объекты района могут отсекаться.");
            }

            // 3. Спавн: позиции вне 0..750.
            for (int i = 0; i < report.Spawns.Count; i++)
            {
                SpawnFacts s = report.Spawns[i];
                if (!s.InsideDistrict)
                    report.Risks.Add("Кандидат спавна " + s.HierarchyPath + " (" + s.ScenePath + ") в точке " +
                        Format(s.Position) + " — вне границ района 0..750.");
            }

            // 4. Quarter250 и старые константы — только как решение для человека.
            int quarterHits = 0, magicHits = 0;
            for (int i = 0; i < report.CodeHits.Count; i++)
            {
                if (report.CodeHits[i].Kind == "layout-quarter250") quarterHits++;
                if (report.CodeHits[i].Kind.StartsWith("magic-", StringComparison.Ordinal)) magicHits++;
            }
            if (quarterHits > 0)
                report.Findings.Add("ссылок на QuarterLayoutIds.Quarter250 в коде: " + quarterHits +
                    " (это значение по умолчанию; District750 включается полем Layout Id, код не переписывается).");
            if (magicHits > 0)
                report.Findings.Add("чисел 250/125 в коде: " + magicHits +
                    " — НЕ заменять на 750 автоматически: в каждом месте нужно понять смысл (размер квартала, " +
                    "половина стороны, координата, лимит).");

            report.Actions.Add("Ничего не удалять: Quarter250 остаётся в проекте и доступен значением Layout Id = quarter250.");
            report.Actions.Add("Переключение основного района — Tools → Life From Zero → Migration: make District750 the main district " +
                "(бэкап сцены + EditorBuildSettings, dry-run по умолчанию).");
            report.Actions.Add("Спавн игрока: перечисленные кандидаты проверить в Play Mode и поставить в точку " +
                "player.home.exit района (см. отчёт интеграции) — без угадывания координат.");
            report.Actions.Add("Камеру и границы мира проверить после переключения: район 750×750, центры и размеры — в отчёте раскладки.");
        }

        // ===================== безопасное переключение сцены =====================

        /// <summary>
        /// Делает сцену основным районом District750: Layout Id = district750, Generate On Start = on,
        /// Add Colliders = on, ровно один CityBootstrap и один QuarterUnityAdapter (Build On Start = off).
        /// Без dryRun сначала создаёт резервные копии сцены и EditorBuildSettings; сохраняет сцену
        /// только если блокеров нет. Quarter250 не изменяется и не удаляется — откат возможен.
        /// </summary>
        public static MigrationPlan Migrate(string scenePath, bool dryRun)
        {
            var plan = new MigrationPlan
            {
                ScenePath = LifeFromZeroGameIntegration.NormalizeScenePath(scenePath),
                DryRun = dryRun,
            };

            string absolute = string.IsNullOrEmpty(plan.ScenePath) ? null : LifeFromZeroGameIntegration.AbsolutePath(plan.ScenePath);
            if (absolute == null || !File.Exists(absolute))
            {
                plan.Blockers.Add("сцена не найдена: " + (scenePath ?? "—"));
                return Finish(plan);
            }

            bool alreadyLoaded = SceneManager.GetSceneByPath(plan.ScenePath).IsValid() &&
                                 SceneManager.GetSceneByPath(plan.ScenePath).isLoaded;
            Scene scene = alreadyLoaded
                ? SceneManager.GetSceneByPath(plan.ScenePath)
                : EditorSceneManager.OpenScene(plan.ScenePath, OpenSceneMode.Additive);
            bool openedByUs = !alreadyLoaded;

            try
            {
                if (alreadyLoaded && scene.isDirty)
                {
                    plan.Blockers.Add("сцена открыта с несохранёнными изменениями — сохраните её (Cmd+S) и повторите, " +
                                      "иначе наши записи смешаются с вашими.");
                    return Finish(plan);
                }

                CityBootstrap[] bootstraps = CollectAll<CityBootstrap>(scene);
                if (bootstraps.Length > 1)
                {
                    plan.Blockers.Add("в сцене " + bootstraps.Length + " CityBootstrap — два района одновременно. " +
                                      "Оставьте один (какой — решает владелец), затем повторите.");
                    return Finish(plan);
                }

                CityBootstrap bootstrap = bootstraps.Length == 1 ? bootstraps[0] : null;
                if (bootstrap == null)
                {
                    plan.Findings.Add("CityBootstrap в сцене нет — будет создан объект City с CityBootstrap и QuarterUnityAdapter.");
                    if (dryRun)
                    {
                        plan.Changes.Add("создать объект City + CityBootstrap (layoutId=" + DistrictLayoutId + ", seed=1904) + QuarterUnityAdapter");
                        plan.Changes.Add("настроить Layout Id = " + DistrictLayoutId + ", Generate On Start = on, Add Colliders = on");
                        return Finish(plan);
                    }
                    var go = new GameObject("City");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<QuarterUnityAdapter>();
                    bootstrap = go.AddComponent<CityBootstrap>();
                    plan.Changes.Add("создан объект City с CityBootstrap и QuarterUnityAdapter");
                }

                var so = new SerializedObject(bootstrap);
                plan.PreviousLayoutId = Str(so, "layoutId");

                SetString(so, "layoutId", DistrictLayoutId, plan, "Layout Id");
                SetBool(so, "generateOnStart", true, plan, "Generate On Start");
                SetBool(so, "addColliders", true, plan, "Add Colliders");

                // Ровно один адаптер на том же объекте и без автозапуска.
                QuarterUnityAdapter[] sameObjectAdapters = bootstrap.GetComponents<QuarterUnityAdapter>();
                if (sameObjectAdapters.Length == 0)
                {
                    plan.Changes.Add("добавить QuarterUnityAdapter на объект CityBootstrap");
                    if (!dryRun) bootstrap.gameObject.AddComponent<QuarterUnityAdapter>();
                }
                else
                {
                    for (int i = 0; i < sameObjectAdapters.Length; i++)
                    {
                        var aso = new SerializedObject(sameObjectAdapters[i]);
                        SetBool(aso, "buildOnStart", false, plan, "Adapter Build On Start");
                        if (!dryRun) aso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                // Отдельные адаптеры с автозапуском в этой же сцене — риск двух городов.
                QuarterUnityAdapter[] all = CollectAll<QuarterUnityAdapter>(scene);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].gameObject == bootstrap.gameObject) continue;
                    if (!Bool(new SerializedObject(all[i]), "buildOnStart")) continue;
                    plan.Blockers.Add("отдельный QuarterUnityAdapter с Build On Start = on: " +
                                      LifeFromZeroGameIntegration.Describe(all[i]) +
                                      " — при старте построятся два района. Выключите его автозапуск.");
                }

                if (!dryRun)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(bootstrap);
                }

                // Пост-проверки ДО сохранения.
                if (!dryRun)
                {
                    var check = new SerializedObject(bootstrap);
                    if (!string.Equals(Str(check, "layoutId"), DistrictLayoutId, StringComparison.OrdinalIgnoreCase))
                        plan.Blockers.Add("после записи Layout Id не стал " + DistrictLayoutId + " — сцена не сохранена");
                    if (CollectAll<CityBootstrap>(scene).Length != 1)
                        plan.Blockers.Add("после записи в сцене не ровно один CityBootstrap — сцена не сохранена");
                }

                if (plan.Blockers.Count > 0)
                    return Finish(plan);

                if (dryRun)
                {
                    if (plan.Changes.Count == 0)
                        plan.Findings.Add("сцена уже настроена на District750 — изменений не требуется");
                    return Finish(plan);
                }

                // Бэкап сцены + EditorBuildSettings ДО сохранения изменений на диск.
                string error;
                plan.BackupDir = CreateBackup(absolute, out error);
                if (error != null)
                {
                    plan.Blockers.Add("резервная копия не создана (" + error + ") — сцена не сохранена");
                    return Finish(plan);
                }

                if (plan.Changes.Count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                        plan.Blockers.Add("не удалось сохранить сцену " + plan.ScenePath);
                }
            }
            finally
            {
                if (openedByUs && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            plan.Actions.Add("Проверить в Unity Client: Play → район District750 строится, спавн и камера в норме.");
            plan.Actions.Add("Откат: скопировать сцену из " + (plan.BackupDir ?? "<бэкап>") +
                " обратно и вернуть Layout Id = " + (string.IsNullOrEmpty(plan.PreviousLayoutId) ? OldLayoutId : plan.PreviousLayoutId) + ".");
            plan.Actions.Add("Quarter250 не удалён: Layout Id = quarter250 возвращает старый район в любой момент.");
            return Finish(plan);
        }

        // ===================== отчёты и бэкапы =====================

        private static void WriteReport(MigrationReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Диагностика перехода Quarter250 → District750");
            sb.AppendLine();
            sb.AppendLine("Проект: " + report.ProjectRoot);
            sb.AppendLine("Это только чтение: ни сцены, ни код, ни Build Settings не изменялись.");
            sb.AppendLine();

            sb.AppendLine("## Сцены проекта (" + report.Scenes.Count + ")");
            for (int i = 0; i < report.Scenes.Count; i++) sb.AppendLine("* " + report.Scenes[i]);
            sb.AppendLine();

            sb.AppendLine("## Генераторы района (" + report.Bootstraps.Count + ")");
            if (report.Bootstraps.Count == 0) sb.AppendLine("* CityBootstrap не найден ни в одной сцене");
            for (int i = 0; i < report.Bootstraps.Count; i++)
            {
                BootstrapFacts b = report.Bootstraps[i];
                sb.AppendLine("* " + b.HierarchyPath + " — сцена " + b.ScenePath);
                sb.AppendLine("  - layoutId=" + b.LayoutId + ", seed=" + b.Seed +
                              ", generateOnStart=" + b.GenerateOnStart + ", addColliders=" + b.AddColliders);
                sb.AppendLine("  - QuarterUnityAdapter на объекте: " + b.AdapterOnSameObject +
                              ", buildOnStart=" + b.AdapterBuildOnStart +
                              ", CityBootstrap в сцене: " + b.BootstrapCountInScene);
            }
            sb.AppendLine();

            sb.AppendLine("## Камеры (" + report.Cameras.Count + ")");
            for (int i = 0; i < report.Cameras.Count; i++)
            {
                CameraFacts c = report.Cameras[i];
                sb.AppendLine("* " + c.HierarchyPath + " — " + c.ScenePath + ": " +
                              (c.Orthographic ? "orthographic, size=" + c.OrthographicSize.ToString("0.##", CultureInfo.InvariantCulture)
                                              : "perspective, farClip=" + c.FarClip.ToString("0.##", CultureInfo.InvariantCulture)) +
                              ", позиция " + Format(c.Position));
            }
            sb.AppendLine();

            sb.AppendLine("## Кандидаты точки спавна игрока (" + report.Spawns.Count + ") — проверить вручную");
            if (report.Spawns.Count == 0) sb.AppendLine("* кандидатов нет: спавн задаётся кодом или спавн-объектом с другим именем");
            for (int i = 0; i < report.Spawns.Count; i++)
            {
                SpawnFacts s = report.Spawns[i];
                sb.AppendLine("* " + s.HierarchyPath + " (" + s.ScenePath + ") — " + Format(s.Position) +
                              (s.InsideDistrict ? " — внутри 0..750" : " — ВНЕ района 0..750"));
            }
            sb.AppendLine();

            sb.AppendLine("## Build Settings (" + report.BuildSettings.Count + ")");
            for (int i = 0; i < report.BuildSettings.Count; i++) sb.AppendLine("* " + report.BuildSettings[i]);
            sb.AppendLine();

            sb.AppendLine("## Совпадения в коде (" + report.CodeHits.Count + ")");
            for (int i = 0; i < report.CodeHits.Count; i++) sb.AppendLine("* " + report.CodeHits[i]);
            sb.AppendLine();

            sb.AppendLine("## Риски (" + report.Risks.Count + ")");
            if (report.Risks.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < report.Risks.Count; i++) sb.AppendLine("* " + report.Risks[i]);
            sb.AppendLine();

            sb.AppendLine("## Найдено");
            if (report.Findings.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < report.Findings.Count; i++) sb.AppendLine("* " + report.Findings[i]);
            sb.AppendLine();

            sb.AppendLine("## Действия (для Codex/владельца)");
            for (int i = 0; i < report.Actions.Count; i++) sb.AppendLine("* " + report.Actions[i]);

            try
            {
                string folder = Path.Combine(report.ProjectRoot, OutputFolder, "reports");
                Directory.CreateDirectory(folder);
                report.ReportPath = Path.Combine(folder, "migration-" + report.Stamp + ".md");
                File.WriteAllText(report.ReportPath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message);
            }

            Debug.Log(LogTag + " " + sb.ToString().Replace("\n", "\n" + LogTag + " "));
        }

        private static MigrationPlan Finish(MigrationPlan plan)
        {
            plan.Blocked = plan.Blockers.Count > 0;

            var sb = new StringBuilder();
            sb.AppendLine("Переключение основного района на District750" + (plan.DryRun ? " (dry-run)" : ""));
            sb.AppendLine();
            sb.AppendLine("Сцена: " + (plan.ScenePath ?? "—"));
            sb.AppendLine("Было Layout Id: " + (string.IsNullOrEmpty(plan.PreviousLayoutId) ? "—" : plan.PreviousLayoutId));
            sb.AppendLine("Резервные копии: " + (plan.BackupDir ?? (plan.DryRun ? "не создавались (dry-run)" : "не создавались")));
            sb.AppendLine();
            sb.AppendLine("## Изменения");
            if (plan.Changes.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Changes.Count; i++) sb.AppendLine("* " + plan.Changes[i]);
            sb.AppendLine();
            sb.AppendLine("## Блокеры");
            if (plan.Blockers.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Blockers.Count; i++) sb.AppendLine("* " + plan.Blockers[i]);
            sb.AppendLine();
            sb.AppendLine("## Найдено");
            if (plan.Findings.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Findings.Count; i++) sb.AppendLine("* " + plan.Findings[i]);
            sb.AppendLine();
            sb.AppendLine("## Действия");
            if (plan.Actions.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Actions.Count; i++) sb.AppendLine("* " + plan.Actions[i]);

            try
            {
                string folder = Path.Combine(LifeFromZeroDiscovery.ProjectRoot(), OutputFolder, "reports");
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                plan.ReportPath = Path.Combine(folder, "main-district-" + stamp + (plan.DryRun ? "-dryrun" : "") + ".md");
                File.WriteAllText(plan.ReportPath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message);
            }

            Debug.Log(LogTag + " " + sb.ToString().Replace("\n", "\n" + LogTag + " "));
            return plan;
        }

        private static string CreateBackup(string sceneAbsolutePath, out string error)
        {
            error = null;
            try
            {
                string projectRoot = LifeFromZeroDiscovery.ProjectRoot();
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string folder = Path.Combine(projectRoot, OutputFolder, "backups", stamp);
                Directory.CreateDirectory(folder);

                string relative = sceneAbsolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                    ? sceneAbsolutePath.Substring(projectRoot.Length).TrimStart('/', '\\')
                    : Path.GetFileName(sceneAbsolutePath);
                string target = Path.Combine(folder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);
                File.Copy(sceneAbsolutePath, target, true);
                if (Hash(sceneAbsolutePath) != Hash(target))
                {
                    error = "копия сцены не совпала по содержимому";
                    return folder;
                }

                var sb = new StringBuilder();
                sb.AppendLine("EditorBuildSettings до переключения основного района (ТЗ #039):");
                EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
                for (int i = 0; i < build.Length; i++)
                    if (build[i] != null)
                        sb.AppendLine("[" + i + "] " + (build[i].enabled ? "вкл " : "выкл ") + build[i].path);
                sb.AppendLine();
                sb.AppendLine("Откат: скопировать сцену обратно и вернуть Layout Id = " + OldLayoutId + ".");
                File.WriteAllText(Path.Combine(folder, "README.txt"), sb.ToString());
                return folder;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        // ===================== утилиты =====================

        private static T[] CollectAll<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                list.AddRange(roots[i].GetComponentsInChildren<T>(true));
            return list.ToArray();
        }

        private static bool FindBootstrapOn(GameObject go) => go != null && go.GetComponent<CityBootstrap>() != null;

        private static bool LooksLikeSpawn(Transform t)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("player") || n.Contains("spawn") || n.Contains("respawn") || n.Contains("start-point")) return true;
            Component[] components = t.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                string type = components[i].GetType().Name.ToLowerInvariant();
                if (type.Contains("player") || type.Contains("spawn") || type.Contains("respawn")) return true;
            }
            return false;
        }

        private static string Str(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null && p.propertyType == SerializedPropertyType.String ? (p.stringValue ?? "") : "";
        }

        private static int Int(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null && p.propertyType == SerializedPropertyType.Integer ? p.intValue : 0;
        }

        private static bool Bool(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            return p != null && p.propertyType == SerializedPropertyType.Boolean && p.boolValue;
        }

        private static void SetString(SerializedObject so, string field, string value, MigrationPlan plan, string label)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                plan.Blockers.Add("поле " + field + " не найдено у CityBootstrap — версия скрипта в проекте отличается");
                return;
            }
            if (p.stringValue == value) return;
            plan.Changes.Add(label + ": " + (string.IsNullOrEmpty(p.stringValue) ? "—" : p.stringValue) + " → " + value);
            p.stringValue = value;
        }

        private static void SetBool(SerializedObject so, string field, bool value, MigrationPlan plan, string label)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            if (p.boolValue == value) return;
            plan.Changes.Add(label + ": " + (p.boolValue ? "on → off" : "off → on"));
            p.boolValue = value;
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static string Relative(string absolute)
        {
            string root = LifeFromZeroDiscovery.ProjectRoot();
            return absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? absolute.Substring(root.Length).TrimStart('/', '\\')
                : absolute;
        }

        private static string Format(Vector3 v) =>
            "(" + v.x.ToString("0.##", CultureInfo.InvariantCulture) + "; " +
                   v.z.ToString("0.##", CultureInfo.InvariantCulture) + ")";

        private static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static bool CommandLineHas(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
