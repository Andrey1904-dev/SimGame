// Life From Zero — City/UnityIntegration/Editor/LifeFromZeroGameIntegration.cs (ТЗ #039)
// Namespace: LifeFromZero.Setup
//
// Безопасная интеграция меню паузы и District750 в НАСТОЯЩУЮ игру (не в тестовую сцену).
//
// Пункт меню: Tools → Life From Zero → Integrate Into Game (открывает окно подтверждения)
// Batch:
//   Unity -batchmode -quit -projectPath "<проект>" \
//         -executeMethod LifeFromZero.Setup.LifeFromZeroGameIntegration.IntegrateBatch \
//         -lfzStartScene "Assets/…/Start.unity" [-lfzDryRun] -logFile /tmp/lfz-integrate.log
//
// Принципы:
//   * стартовая сцена НЕ угадывается: её путь передаётся явно (аргумент или окно),
//     а инструмент сверяет его с отчётом LifeFromZeroDiscovery;
//   * перед любым изменением файлов сцены и ProjectSettings создаются резервные копии
//     в <проект>/LifeFromZeroIntegration/backups/<время>/ (вне Assets);
//   * ничего не удаляется и не заменяется: только добавление недостающих объектов;
//   * повторный запуск безопасен: уже существующие компоненты переиспользуются;
//   * если в стартовой сцене уже есть чужое меню паузы — СТОП (два меню хуже одного);
//   * District750 не встраивается в квартиру №67: город живёт в отдельной сцене,
//     а в стартовую сцену добавляется пассивный мост District750Link;
//   * -lfzDryRun показывает план и ничего не меняет.
//
// ВНИМАНИЕ: в среде репозитория Unity нет, файл не компилировался в Unity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LifeFromZero.City.Pause;
using LifeFromZero.City.Quarter.UnityAdapter;

namespace LifeFromZero.Setup
{
    public sealed class IntegrationPlan
    {
        public string StartScenePath;
        public bool DryRun;
        public string BackupDir;
        public bool Completed;
        public bool Blocked;

        public readonly List<string> Findings = new List<string>();
        public readonly List<string> Changes = new List<string>();
        public readonly List<string> Blockers = new List<string>();
        public readonly List<string> Actions = new List<string>();
        public readonly List<string> CandidatesForManualChoice = new List<string>();

        public string DistrictScenePath;
        public bool DistrictSceneCreated;
        public bool BuildSettingsChanged;
        public bool PauseCreated;
        public bool KeyboardEnabled;
        public bool InputAutoAssigned;
        public string ReportPath;

        public string Summary()
        {
            return "start=" + (StartScenePath ?? "—") +
                   " district=" + (DistrictScenePath ?? "—") +
                   " pauseCreated=" + PauseCreated +
                   " keyboard=" + KeyboardEnabled +
                   " inputAssigned=" + InputAutoAssigned +
                   " backups=" + (BackupDir ?? "нет") +
                   " changes=" + Changes.Count +
                   " blockers=" + Blockers.Count;
        }
    }

    public static class LifeFromZeroGameIntegration
    {
        public const string DistrictScenePath = "Assets/_Project/Scenes/District750.unity";
        public const string DistrictSceneName = "District750";
        public const string OutputFolder = "LifeFromZeroIntegration";
        internal const string LogTag = "[LFZ-INTEGRATE]";

        private static readonly string[] MovementHints =
        {
            "move", "movement", "walk", "run", "motor", "locomotion", "look", "camera", "orbit", "follow"
        };

        private static readonly string[] BroadInputHints =
        {
            "move", "movement", "walk", "run", "motor", "locomotion", "look", "camera", "orbit", "follow",
            "input", "player", "controller", "character"
        };

        private static readonly string[] DataHints =
        {
            "stats", "vitals", "needs", "health", "satiety", "hunger", "energy", "mood", "stress",
            "wallet", "money", "economy", "inventory", "clock", "timeofday", "daycycle", "daycounter",
            "location", "debt", "goals", "quest", "income"
        };

        // ===================== точки входа =====================

        [MenuItem("Tools/Life From Zero/Integrate Into Game", false, 10)]
        public static void OpenWindow()
        {
            LifeFromZeroIntegrateWindow.ShowWindow();
        }

        /// <summary>Точка входа для -executeMethod. Блокер = исключение = ненулевой код выхода Unity.</summary>
        public static void IntegrateBatch()
        {
            string startScene = CommandLineValue("-lfzStartScene");
            bool dryRun = CommandLineHas("-lfzDryRun");

            if (string.IsNullOrEmpty(startScene))
            {
                var discovery = LifeFromZeroDiscovery.Collect();
                string candidates = DescribeCandidates(discovery);
                Debug.LogError(LogTag + " BLOCKED: не передан -lfzStartScene. Сначала выполните DetectBatch и выберите " +
                               "стартовую сцену из отчёта.\n" + candidates);
                throw new Exception("Не передан -lfzStartScene. Кандидаты:\n" + candidates);
            }

            IntegrationPlan plan = Integrate(startScene, dryRun);

            if (plan.Blocked)
            {
                string text = string.Join("\n", plan.Blockers.ToArray());
                Debug.LogError(LogTag + " RESULT: BLOCKED\n" + text);
                throw new Exception("Интеграция заблокирована:\n" + text);
            }

            if (dryRun)
            {
                Debug.Log(LogTag + " RESULT: DRY_RUN_OK " + plan.Summary());
                return;
            }

            Debug.Log(LogTag + " RESULT: OK " + plan.Summary());
            for (int i = 0; i < plan.Actions.Count; i++)
                Debug.Log(LogTag + " ACTION " + plan.Actions[i]);
        }

        // ===================== основная логика =====================

        public static IntegrationPlan Integrate(string startScenePath, bool dryRun)
        {
            var plan = new IntegrationPlan { DryRun = dryRun };

            string scenePath = NormalizeScenePath(startScenePath);
            plan.StartScenePath = scenePath;

            // ---- 1. проверка сцены (без изменений) ----
            if (string.IsNullOrEmpty(scenePath))
            {
                plan.Blockers.Add("путь к стартовой сцене пуст или не внутри Assets: '" + startScenePath + "'");
                return Finish(plan);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                plan.Blockers.Add("сцена не найдена: " + scenePath);
                return Finish(plan);
            }
            if (string.Equals(scenePath, LifeFromZeroSetup.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                plan.Blockers.Add("нельзя интегрировать в тестовую сцену " + LifeFromZeroSetup.ScenePath +
                                  ": она не должна становиться стартовой. Укажите настоящую стартовую сцену игры.");
                return Finish(plan);
            }

            if (LifeFromZeroSetup.FindType("District750Link") == null)
            {
                plan.Blockers.Add("тип District750Link не найден в проекте: сначала выполните " +
                                  "install_to_unity.sh --apply (файл Assets/City/UnityIntegration/Runtime/District750Link.cs) " +
                                  "и дождитесь компиляции Unity");
                return Finish(plan);
            }

            DiscoveryReport discovery = LifeFromZeroDiscovery.Collect();
            plan.Findings.Add("отчёт обнаружения: " + discovery.ReportPath);
            plan.Findings.Add("обнаруженная стартовая сцена: " +
                              (string.IsNullOrEmpty(discovery.ChosenScenePath) ? "не определена" : discovery.ChosenScenePath));
            if (!string.IsNullOrEmpty(discovery.ChosenScenePath) &&
                !string.Equals(discovery.ChosenScenePath, scenePath, StringComparison.OrdinalIgnoreCase))
                plan.Findings.Add("ВНИМАНИЕ: указанная сцена отличается от обнаруженной автоматически — используется указанная, " +
                                  "убедитесь, что это осознанное решение");
            if (discovery.EscapeHandlers.Count > 0)
                plan.Findings.Add("Esc уже читается в коде проекта: " + discovery.EscapeHandlers.Count + " мест (см. " +
                                  discovery.ReportPath + ")");
            else
                plan.Findings.Add("обработчиков Esc в коде проекта не найдено");

            SceneFacts startFacts = discovery.Find(scenePath);
            bool startInBuildSettings = startFacts != null && startFacts.BuildIndex >= 0 && startFacts.BuildEnabled;
            bool anyBuildScenes = EditorBuildSettings.scenes != null && EditorBuildSettings.scenes.Length > 0;

            // ---- 2. открытие сцены (аддитивно, без сохранения чужих сцен) ----
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool alreadyOpen = scene.IsValid() && scene.isLoaded;
            bool openedByUs = false;
            bool alreadyDirty = alreadyOpen && scene.isDirty;

            if (alreadyDirty)
            {
                if (!EditorApplication.isBatchMode)
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "Life From Zero",
                        "В стартовой сцене есть несохранённые изменения. Они будут сохранены вместе с интеграцией.\n\nПродолжить?",
                        "Продолжить", "Отмена");
                    if (!proceed)
                    {
                        plan.Blockers.Add("отменено пользователем: в сцене были несохранённые изменения");
                        return Finish(plan);
                    }
                }
                else
                {
                    plan.Blockers.Add("стартовая сцена открыта с несохранёнными изменениями — сохраните или закройте её, " +
                                      "затем повторите (в batch-режиме изменения не будут записаны)");
                    return Finish(plan);
                }
            }

            if (!alreadyOpen)
            {
                try
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    openedByUs = true;
                }
                catch (Exception e)
                {
                    plan.Blockers.Add("не удалось открыть сцену " + scenePath + ": " + e.Message);
                    return Finish(plan);
                }
            }

            try
            {
                InspectScene(plan, scene, discovery);

                if (plan.Blockers.Count > 0) return Finish(plan);

                // ---- 3. резервные копии (до любых изменений) ----
                if (!dryRun)
                {
                    plan.BackupDir = CreateBackup(plan,
                        new[] { AbsolutePath(scenePath), AbsolutePath("ProjectSettings/EditorBuildSettings.asset") },
                        out string backupError);
                    if (!string.IsNullOrEmpty(backupError))
                    {
                        plan.Blockers.Add("резервная копия не создана, изменения отменены: " + backupError);
                        return Finish(plan);
                    }
                }

                // ---- 4. меню паузы ----
                ApplyPause(plan, scene, discovery, dryRun);

                // ---- 5. District750 ----
                ApplyDistrict(plan, scene, discovery, dryRun, startInBuildSettings, anyBuildScenes);

                // ---- 6. пост-проверки ДО сохранения: блокер не должен попасть на диск ----
                PostChecks(plan, scene, discovery);

                // ---- 7. сохранение (только если пост-проверки чисты) ----
                if (!dryRun && plan.Blockers.Count == 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                    {
                        plan.Blockers.Add("не удалось сохранить сцену " + scenePath + " (резервная копия: " +
                                          (plan.BackupDir ?? "нет") + ")");
                    }
                    else
                    {
                        plan.Changes.Add("сцена сохранена: " + scenePath);
                    }
                    AssetDatabase.SaveAssets();
                }
                else if (!dryRun)
                {
                    plan.Findings.Add("изменения НЕ сохранены: сначала устраните блокеры");
                }

                return Finish(plan);
            }
            finally
            {
                // Закрываем только ту сцену, которую открыли сами: чужие открытые сцены не трогаем.
                // Несохранённые изменения пользователя (сцена была открыта до нас) сюда не попадают:
                // openedByUs == true только для только что открытой чистой сцены.
                if (openedByUs && scene.IsValid())
                {
                    // Никогда не сохраняем из finally: частично применённые изменения не должны
                    // попадать на диск (резервные копии уже созданы, сцену просто закрываем).
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        // ===================== проверки без изменений =====================

        private static void InspectScene(IntegrationPlan plan, Scene scene, DiscoveryReport discovery)
        {
            List<MonoBehaviour> components = CollectComponents(scene);

            PauseController pause = FindFirst<PauseController>(scene);
            PauseIntegration integration = FindFirst<PauseIntegration>(scene);
            PauseDataSource dataSource = FindFirst<PauseDataSource>(scene);
            CityBootstrap bootstrap = FindFirst<CityBootstrap>(scene);
            Component districtLink = FindByTypeName(scene, "District750Link");

            plan.Findings.Add("объектов-скриптов в стартовой сцене: " + components.Count);
            plan.Findings.Add("PauseController: " + (pause != null ? "есть" : "нет") +
                              ", PauseIntegration: " + (integration != null ? integration.GetType().Name : "нет") +
                              ", PauseDataSource: " + (dataSource != null ? dataSource.GetType().Name : "нет"));
            plan.Findings.Add("CityBootstrap в стартовой сцене: " + (bootstrap != null ? "есть" : "нет") +
                              ", District750Link: " + (districtLink != null ? "есть" : "нет"));
            plan.Findings.Add("сцена в Build Settings: " + (IsInBuildSettings(scene.path) ? "да" : "нет"));

            // Чужое меню паузы — блокер: два меню на Esc одновременно недопустимы.
            var foreignPause = new List<string>();
            var menuLike = new List<string>();
            for (int i = 0; i < components.Count; i++)
            {
                Type type = components[i].GetType();
                if (!IsProjectScript(type)) continue;
                if (IsPauseInfrastructure(type)) continue;
                string name = type.Name.ToLowerInvariant();
                if (name.Contains("paus")) foreignPause.Add(Describe(components[i]));
                else if (name.Contains("menu")) menuLike.Add(Describe(components[i]));
            }
            if (foreignPause.Count > 0)
            {
                plan.Blockers.Add("в стартовой сцене уже есть собственная пауза: " + string.Join("; ", foreignPause.ToArray()));
                plan.Actions.Add("решение владельца: (а) отключить/удалить старое меню и запустить интеграцию снова, " +
                                 "либо (б) не добавлять наше меню, а реализовать PauseIntegration поверх старого");
            }
            if (menuLike.Count > 0)
                plan.Findings.Add("меню-подобные компоненты (не блокируют): " + string.Join("; ", menuLike.ToArray()));

            // Кандидаты управления.
            var strong = new List<Behaviour>();
            var broad = new List<Behaviour>();
            for (int i = 0; i < components.Count; i++)
            {
                MonoBehaviour behaviour = components[i];
                Type type = behaviour.GetType();
                if (!IsProjectScript(type)) continue;
                if (IsPauseInfrastructure(type)) continue;
                string name = type.Name.ToLowerInvariant();
                if (Matches(name, BroadInputHints)) broad.Add(behaviour);
                if (Matches(name, MovementHints)) strong.Add(behaviour);
            }

            plan.Findings.Add("кандидатов управления (движение/камера): " + broad.Count +
                              ", из них явных движение/камера: " + strong.Count);
            for (int i = 0; i < broad.Count; i++)
                plan.CandidatesForManualChoice.Add("управление: " + Describe(broad[i]));

            plan.InputAutoAssigned = false;
            if (broad.Count >= 1 && broad.Count <= 3 && strong.Count == broad.Count)
            {
                plan.InputAutoAssigned = true;
                plan.Findings.Add("автоназначение управления возможно: " + broad.Count +
                                  " компонент(ов), все явно движение/камера");
            }
            else if (broad.Count > 0)
            {
                plan.Findings.Add("автоназначение управления НЕ выполнено: кандидатов " + broad.Count +
                                  " (нужен выбор Codex/владельца по списку ниже)");
            }
            else
            {
                plan.Findings.Add("кандидатов управления не найдено: назначение только вручную после поиска компонентов");
                plan.Actions.Add("найти контроллер движения/камеры: Tools → Life From Zero → Report input candidates " +
                                 "в игровой сцене, затем назначить в Pause → StandalonePauseIntegration → Player Input Behaviours");
            }

            // Кандидаты источников данных.
            var dataCandidates = new List<string>();
            for (int i = 0; i < components.Count; i++)
            {
                Type type = components[i].GetType();
                if (!IsProjectScript(type) || IsPauseInfrastructure(type)) continue;
                if (Matches(type.Name.ToLowerInvariant(), DataHints)) dataCandidates.Add(Describe(components[i]));
            }
            if (dataCandidates.Count > 0)
            {
                plan.Findings.Add("возможные источники игровых данных: " + dataCandidates.Count);
                for (int i = 0; i < dataCandidates.Count && i < 20; i++)
                    plan.CandidatesForManualChoice.Add("данные: " + dataCandidates[i]);
                plan.Actions.Add("подключить данные: скопировать City/UnityIntegration/Templates/GamePauseDataSource.cs.txt " +
                                 "в Assets/City/PauseMenu/GamePauseDataSource.cs, повесить на объект Pause, " +
                                 "назначить в PauseController → Data Source и выставить делегаты из найденных компонентов");
            }
            else
            {
                plan.Findings.Add("источников игровых данных в стартовой сцене не найдено — меню покажет «Нет данных»");
            }
        }

        // ===================== меню паузы =====================

        private static void ApplyPause(IntegrationPlan plan, Scene scene, DiscoveryReport discovery, bool dryRun)
        {
            bool escapeAlreadyHandled = discovery.EscapeHandlers.Count > 0;
            plan.KeyboardEnabled = !escapeAlreadyHandled;

            PauseController controller = FindFirst<PauseController>(scene);
            PauseIntegration integration = FindFirst<PauseIntegration>(scene);
            Component keyboard = FindByTypeName(scene, "PauseKeyboardInput");

            if (controller != null)
            {
                plan.Findings.Add("PauseController уже есть на объекте '" + LifeFromZeroSetup.PathOf(controller.transform) +
                                  "' — переиспользуется, дубликат не создаётся");
            }
            else if (integration != null)
            {
                plan.Changes.Add("PauseController добавляется на существующий объект '" +
                                 LifeFromZeroSetup.PathOf(integration.transform) + "'");
                if (!dryRun) controller = integration.gameObject.AddComponent<PauseController>();
            }
            else
            {
                plan.PauseCreated = true;
                plan.Changes.Add("создан объект 'Pause' (PauseController + StandalonePauseIntegration" +
                                 (plan.KeyboardEnabled ? " + PauseKeyboardInput" : "") + ")");
                if (!dryRun)
                {
                    GameObject pauseObject = LifeFromZeroSetup.CreatePauseRoot(plan.KeyboardEnabled);
                    controller = pauseObject.GetComponent<PauseController>();
                    integration = pauseObject.GetComponent<PauseIntegration>();
                    keyboard = FindByTypeName(scene, "PauseKeyboardInput");
                }
            }

            if (!dryRun)
            {
                if (controller == null) controller = FindFirst<PauseController>(scene);
                if (integration == null) integration = FindFirst<PauseIntegration>(scene);
                if (controller != null && integration != null && LifeFromZeroSetup.FieldExists(controller, "integration"))
                {
                    LifeFromZeroSetup.SetObjectReference(controller, "integration", integration);
                    plan.Changes.Add("PauseController.integration → " + integration.GetType().Name);
                }
            }

            // Esc: двойная обработка недопустима.
            bool keyboardTypeExists = LifeFromZeroSetup.FindType("PauseKeyboardInput") != null;
            if (keyboard != null || (plan.PauseCreated && keyboardTypeExists))
            {
                plan.Changes.Add("PauseKeyboardInput.useStandaloneKeyboard = " + plan.KeyboardEnabled +
                                 (plan.KeyboardEnabled
                                     ? " (Esc открывает меню; других обработчиков Esc в коде нет)"
                                     : " (Esc уже обрабатывается проектом — наш обработчик выключен)"));
                if (!dryRun && keyboard != null)
                {
                    bool current = false;
                    LifeFromZeroSetup.TryGetBoolField(keyboard, "useStandaloneKeyboard", out current);
                    if (current != plan.KeyboardEnabled)
                        LifeFromZeroSetup.SetBoolField(keyboard, "useStandaloneKeyboard", plan.KeyboardEnabled);
                }
                if (!plan.KeyboardEnabled)
                    plan.Actions.Add("Esc уже обрабатывается проектом: направьте его в PauseController.Instance.HandleEscape() " +
                                     "(или отключите старый обработчик), иначе меню не откроется. Места Esc — в отчёте обнаружения");
            }
            else if (!keyboardTypeExists)
            {
                plan.Findings.Add("тип PauseKeyboardInput не найден в проекте (нет Assets/City/PauseInputSystem)");
                plan.Actions.Add("вызывать PauseController.Instance.HandleEscape() из существующего ввода проекта");
            }

            // Управление: назначаем только при однозначных кандидатах (см. InspectScene).
            var standalone = integration as StandalonePauseIntegration;
            bool willUseStandalone = standalone != null || (plan.PauseCreated && dryRun);
            if (willUseStandalone)
            {
                int size = standalone != null ? LifeFromZeroSetup.ArraySizeOf(standalone, "playerInputBehaviours") : 0;
                if (size > 0)
                {
                    plan.Findings.Add("playerInputBehaviours уже заполнен (" + size + ") — не изменяется");
                }
                else if (plan.InputAutoAssigned)
                {
                    List<Behaviour> candidates = FindInputCandidates(scene);
                    if (candidates.Count >= 1 && candidates.Count <= 3)
                    {
                        plan.Changes.Add("StandalonePauseIntegration.playerInputBehaviours ← " + candidates.Count +
                                         " компонент(ов): " + DescribeMany(candidates));
                        if (!dryRun && standalone != null)
                            LifeFromZeroSetup.SetBehaviourArray(standalone, "playerInputBehaviours", candidates.ToArray());
                    }
                }
                else
                {
                    plan.Actions.Add("назначить Player Input Behaviours вручную по списку кандидатов: без этого " +
                                     "CanOpenPause() = false и Esc ничего не делает");
                }
            }
            else if (integration != null)
            {
                plan.Findings.Add("используется существующий PauseIntegration (" + integration.GetType().Name +
                                  ") — его поля не изменяются");
            }
        }

        // ===================== District750 =====================

        private static void ApplyDistrict(IntegrationPlan plan, Scene startScene, DiscoveryReport discovery,
                                          bool dryRun, bool startInBuildSettings, bool anyBuildScenes)
        {
            CityBootstrap bootstrap = FindFirst<CityBootstrap>(startScene);
            if (bootstrap != null)
            {
                plan.Findings.Add("CityBootstrap уже находится в стартовой сцене — город уже подключён, отдельная сцена не создаётся");
                plan.DistrictScenePath = startScene.path;
                return;
            }

            plan.DistrictScenePath = DistrictScenePath;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DistrictScenePath) == null)
            {
                plan.Changes.Add("создана сцена района " + DistrictScenePath + " (CityBootstrap, камера, свет)");
                plan.DistrictSceneCreated = true;
                if (!dryRun) CreateDistrictScene();
            }
            else
            {
                plan.Findings.Add("сцена района уже существует и не изменяется: " + DistrictScenePath);
            }

            if (!dryRun) RegisterInBuildSettings(plan, startInBuildSettings, anyBuildScenes);

            // Пассивный мост в стартовой сцене: ничего не делает без вызова из существующего перехода.
            Component existingLink = FindByTypeName(startScene, "District750Link");
            if (existingLink == null)
            {
                plan.Changes.Add("в стартовую сцену добавлен District750Link (пассивный мост, без Update и клавиш)");
                if (!dryRun)
                {
                    var linkHost = new GameObject("Life From Zero — District750 Link");
                    Component link = LifeFromZeroSetup.TryAddComponentByTypeName(linkHost, "District750Link");
                    if (link == null)
                    {
                        plan.Blockers.Add("тип District750Link не найден в проекте: повторите установку " +
                                          "install_to_unity.sh --apply и перезапустите инструмент");
                        UnityEngine.Object.DestroyImmediate(linkHost);
                    }
                    else
                    {
                        LifeFromZeroSetup.SetStringField(link, "districtSceneName", DistrictSceneName);
                        LifeFromZeroSetup.SetStringField(link, "returnSceneName", Path.GetFileNameWithoutExtension(startScene.path));
                        LifeFromZeroSetup.SetEnumField(link, "loadMode", 0); // LoadSceneMode.Single
                    }
                }
            }
            else
            {
                plan.Findings.Add("District750Link уже есть в стартовой сцене — не дублируется");
            }

            plan.Actions.Add("подключить переход в город: в существующем переходе (дверь/портал/меню) вызвать " +
                             "District750Link.OpenDistrict через UnityEvent; если перехода нет — решает владелец игры");
            plan.Actions.Add("проверить переход из квартиры №67 в District750 и обратно (ReturnToStart) в Play Mode");
        }

        private static void CreateDistrictScene()
        {
            LifeFromZeroSetup.EnsureFolder(LifeFromZeroSetup.SceneFolder);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                LifeFromZeroSetup.CreateCityRoot();
                LifeFromZeroSetup.CreateOverviewCamera();
                LifeFromZeroSetup.CreateDirectionalLight();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, DistrictScenePath);
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.Refresh();
        }

        private static void RegisterInBuildSettings(IntegrationPlan plan, bool startInBuildSettings, bool anyBuildScenes)
        {
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            for (int i = 0; i < current.Length; i++)
                if (current[i] != null && string.Equals(current[i].path, DistrictScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Findings.Add("сцена района уже в Build Settings (индекс " + i + ") — порядок не меняется");
                    return;
                }

            if (!anyBuildScenes)
            {
                plan.Findings.Add("Build Settings пуст: список сцен проекта не изменяется");
                plan.Actions.Add("добавить " + DistrictScenePath + " в свою систему загрузки сцен вручную " +
                                 "(проект не использует Build Settings)");
                return;
            }

            if (!startInBuildSettings)
            {
                plan.Findings.Add("стартовая сцена не в Build Settings (проект загружает сцены иначе): Build Settings не изменяются");
                plan.Actions.Add("добавить " + DistrictScenePath + " в ту же систему сцен, где регистрируется стартовая сцена");
                return;
            }

            var updated = new List<EditorBuildSettingsScene>(current);
            updated.Add(new EditorBuildSettingsScene(DistrictScenePath, true));
            EditorBuildSettings.scenes = updated.ToArray();
            plan.BuildSettingsChanged = true;
            plan.Changes.Add("Build Settings: " + DistrictScenePath + " добавлена в конец (индекс " +
                             (updated.Count - 1) + ", стартовая сцена осталась на индексе 0)");
        }

        // ===================== пост-проверки и отчёт =====================

        private static void PostChecks(IntegrationPlan plan, Scene scene, DiscoveryReport discovery)
        {
            if (plan.Blocked) return;
            if (plan.DryRun)
            {
                plan.Actions.Add("после реального запуска проверить Play Mode стартовой сцены: управление, камера, Esc, переход в District750");
                return;
            }

            // Стартовая сцена не должна стать тестовой сценой.
            if (string.Equals(scene.path, LifeFromZeroSetup.ScenePath, StringComparison.OrdinalIgnoreCase))
                plan.Blockers.Add("стартовая сцена совпала с тестовой — это запрещено");

            int controllers = CountInScene<PauseController>(scene);
            if (controllers > 1)
                plan.Blockers.Add("в стартовой сцене " + controllers + " PauseController — дубликаты недопустимы");
            plan.Changes.Add("проверено: PauseController в стартовой сцене = " + controllers);

            if (IsInBuildSettings(LifeFromZeroSetup.ScenePath))
                plan.Findings.Add("ВНИМАНИЕ: тестовая сцена " + LifeFromZeroSetup.ScenePath +
                                  " есть в Build Settings — убедитесь, что она не стоит первой, иначе обычный запуск откроет тест");
            else
                plan.Findings.Add("тестовая сцена не в Build Settings — она не станет стартовой");

            plan.Actions.Add("проверить обычный запуск: открыть " + scene.path + " и нажать Play");
        }

        private static IntegrationPlan Finish(IntegrationPlan plan)
        {
            plan.Blocked = plan.Blockers.Count > 0;

            var sb = new StringBuilder();
            sb.AppendLine("Интеграция LIFE FROM ZERO в игру" + (plan.DryRun ? " (dry-run)" : ""));
            sb.AppendLine();
            sb.AppendLine("## Стартовая сцена");
            sb.AppendLine(plan.StartScenePath ?? "—");
            sb.AppendLine();
            sb.AppendLine("## Резервные копии");
            sb.AppendLine(plan.BackupDir ?? ("не создавались" + (plan.DryRun ? " (dry-run)" : "")));
            sb.AppendLine();
            sb.AppendLine("## Найдено");
            for (int i = 0; i < plan.Findings.Count; i++) sb.AppendLine("* " + plan.Findings[i]);
            sb.AppendLine();
            sb.AppendLine("## Изменения");
            if (plan.Changes.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Changes.Count; i++) sb.AppendLine("* " + plan.Changes[i]);
            sb.AppendLine();
            sb.AppendLine("## Кандидаты для выбора вручную");
            if (plan.CandidatesForManualChoice.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.CandidatesForManualChoice.Count; i++) sb.AppendLine("* " + plan.CandidatesForManualChoice[i]);
            sb.AppendLine();
            sb.AppendLine("## Блокеры");
            if (plan.Blockers.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Blockers.Count; i++) sb.AppendLine("* " + plan.Blockers[i]);
            sb.AppendLine();
            sb.AppendLine("## Действия Codex/владельца");
            if (plan.Actions.Count == 0) sb.AppendLine("* нет");
            for (int i = 0; i < plan.Actions.Count; i++) sb.AppendLine("* " + plan.Actions[i]);

            try
            {
                string folder = Path.Combine(LifeFromZeroDiscovery.ProjectRoot(), OutputFolder, "reports");
                Directory.CreateDirectory(folder);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                plan.ReportPath = Path.Combine(folder, "integration-" + stamp + (plan.DryRun ? "-dryrun" : "") + ".md");
                File.WriteAllText(plan.ReportPath, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogTag + " отчёт не записан: " + e.Message);
            }

            Debug.Log(LogTag + " " + sb.ToString().Replace("\n", "\n" + LogTag + " "));
            return plan;
        }

        // ===================== резервные копии =====================

        private static string CreateBackup(IntegrationPlan plan, string[] absolutePaths, out string error)
        {
            error = null;
            try
            {
                string projectRoot = LifeFromZeroDiscovery.ProjectRoot();
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string folder = Path.Combine(projectRoot, OutputFolder, "backups", stamp);
                Directory.CreateDirectory(folder);

                int copied = 0;
                for (int i = 0; i < absolutePaths.Length; i++)
                {
                    string source = absolutePaths[i];
                    if (string.IsNullOrEmpty(source) || !File.Exists(source)) continue;

                    string relative = source.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                        ? source.Substring(projectRoot.Length).TrimStart('/', '\\')
                        : Path.GetFileName(source);
                    string target = Path.Combine(folder, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? folder);

                    File.Copy(source, target, true);
                    if (Hash(source) == Hash(target)) copied++;
                    else
                    {
                        error = "копия не совпала по содержимому: " + relative;
                        return folder;
                    }
                }

                if (copied == 0)
                {
                    error = "нечего копировать: файлы для изменения не найдены";
                    return folder;
                }

                File.WriteAllText(Path.Combine(folder, "README.txt"),
                    "Резервные копии Life From Zero, созданные перед интеграцией (Tools → Life From Zero → Integrate Into Game).\n" +
                    "Как откатить: скопируйте файлы обратно в проект, сохранив относительные пути.\n" +
                    "Каталог вне Assets, Unity его не импортирует. Можно удалить после проверки.\n");
                plan.Changes.Add("резервные копии (" + copied + " файл(ов)): " + folder);
                return folder;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
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

        // ===================== утилиты =====================

        internal static bool IsProjectScript(Type type)
        {
            if (type == null) return false;
            string assembly = type.Assembly.GetName().Name;
            if (assembly == "UnityEngine" || assembly == "UnityEditor" || assembly == "mscorlib") return false;
            if (assembly.StartsWith("UnityEngine.", StringComparison.Ordinal)) return false;
            if (assembly.StartsWith("UnityEditor.", StringComparison.Ordinal)) return false;
            if (assembly.StartsWith("Unity.", StringComparison.Ordinal)) return false;
            if (assembly.StartsWith("Unity.RenderPipelines", StringComparison.Ordinal)) return false;
            return true;
        }

        internal static bool IsPauseInfrastructure(Type type)
        {
            if (type == null) return false;
            if (type == typeof(PauseController)) return true;
            if (type == typeof(PauseView)) return true;
            if (typeof(PauseIntegration).IsAssignableFrom(type)) return true;
            if (typeof(PauseDataSource).IsAssignableFrom(type)) return true;
            return type.Name == "PauseKeyboardInput" || type.Name == "District750Link";
        }

        private static bool Matches(string lowerName, string[] hints)
        {
            for (int i = 0; i < hints.Length; i++)
                if (lowerName.Contains(hints[i])) return true;
            return false;
        }

        private static List<Behaviour> FindInputCandidates(Scene scene)
        {
            var result = new List<Behaviour>();
            List<MonoBehaviour> components = CollectComponents(scene);
            for (int i = 0; i < components.Count; i++)
            {
                Type type = components[i].GetType();
                if (!IsProjectScript(type) || IsPauseInfrastructure(type)) continue;
                if (Matches(type.Name.ToLowerInvariant(), MovementHints)) result.Add(components[i]);
            }
            return result;
        }

        private static List<MonoBehaviour> CollectComponents(Scene scene)
        {
            var result = new List<MonoBehaviour>();
            if (!scene.IsValid()) return result;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                MonoBehaviour[] found = roots[i].GetComponentsInChildren<MonoBehaviour>(true);
                for (int j = 0; j < found.Length; j++)
                    if (found[j] != null) result.Add(found[j]);
            }
            return result;
        }

        internal static T FindFirst<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        internal static int CountInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            if (!scene.IsValid()) return count;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                count += roots[i].GetComponentsInChildren<T>(true).Length;
            return count;
        }

        internal static Component FindByTypeName(Scene scene, string simpleTypeName)
        {
            List<MonoBehaviour> components = CollectComponents(scene);
            for (int i = 0; i < components.Count; i++)
                if (components[i].GetType().Name == simpleTypeName) return components[i];
            return null;
        }

        internal static string Describe(Component component)
        {
            if (component == null) return "—";
            return "'" + LifeFromZeroSetup.PathOf(component.transform) + "' → " + component.GetType().FullName +
                   (component is Behaviour behaviour && !behaviour.enabled ? " (disabled)" : "");
        }

        private static string DescribeMany(List<Behaviour> items)
        {
            var parts = new List<string>();
            for (int i = 0; i < items.Count; i++) parts.Add(items[i].GetType().Name);
            return string.Join(", ", parts.ToArray());
        }

        internal static bool IsInBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i] != null && string.Equals(scenes[i].path, scenePath, StringComparison.OrdinalIgnoreCase))
                    return scenes[i].enabled;
            return false;
        }

        internal static string NormalizeScenePath(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string path = input.Trim().Replace('\\', '/');
            int assetsIndex = path.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex < 0) return null;
            path = path.Substring(assetsIndex);
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) return null;
            return path;
        }

        internal static string AbsolutePath(string projectRelative)
        {
            return Path.Combine(LifeFromZeroDiscovery.ProjectRoot(), projectRelative.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static string DescribeCandidates(DiscoveryReport report)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < report.Scenes.Count; i++)
            {
                SceneFacts facts = report.Scenes[i];
                sb.AppendLine("  " + facts.Path +
                              (facts.IsPlayModeStart ? "  [PLAY-START]" : "") +
                              (facts.BuildIndex >= 0 ? "  [build " + facts.BuildIndex + (facts.BuildEnabled ? "" : " off") + "]" : "") +
                              (facts.InActiveProfile ? "  [profile " + facts.ActiveProfileIndex + "]" : "") +
                              (facts.HasPauseController ? "  [pause]" : "") +
                              (facts.HasCityBootstrap ? "  [city]" : ""));
            }
            return sb.ToString();
        }

        private static string CommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool CommandLineHas(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
