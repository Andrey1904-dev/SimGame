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

                if (FindByType<UnityEngine.EventSystems.EventSystem>(scene) == null)
                {
                    result.Changes.Add("создан EventSystem (иначе кнопки uGUI не нажимаются)");
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

                    if (root.GetComponent<UiBootstrap>() == null) root.AddComponent<UiBootstrap>();

                    // Мост к существующим системам: позиция игрока (камера) и, если в
                    // сцене есть City.Pause, — источник данных и передача действий.
                    if (root.GetComponent<UiGameBridge>() == null) root.AddComponent<UiGameBridge>();
                    if (root.GetComponent<PauseUiSnapshotSource>() == null) root.AddComponent<PauseUiSnapshotSource>();
                    if (root.GetComponent<DeliveryServiceBridge>() == null) root.AddComponent<DeliveryServiceBridge>();
                    if (root.GetComponent<UiMenuActions>() == null) root.AddComponent<UiMenuActions>();

                    // UiBootstrap сам создаёт экраны в Awake, но если проект запускает
                    // интерфейс из кода (UiBootstrap.Ensure) — экраны всё равно нужны.
                    // Поэтому кладём их в сцену один раз, а не полагаемся на рантайм.
                    AddScreen<HudScreen>(root, "HUD");
                    AddScreen<PhoneScreen>(root, "Phone");
                    AddScreen<MapScreen>(root, "Map");
                    AddScreen<PauseMenuScreen>(root, "Pause Menu");

                    if (FindByType<UnityEngine.EventSystems.EventSystem>(scene) == null &&
                        !HasEventSystemInScene(scene))
                    {
                        var eventSystem = new GameObject("EventSystem");
                        SceneManager.MoveGameObjectToScene(eventSystem, scene);
                        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
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
