// Life From Zero — City/UnityIntegration/Editor/LifeFromZeroIntegrateWindow.cs (ТЗ #039)
// Namespace: LifeFromZero.Setup
//
// Окно подтверждения интеграции: показывает отчёт обнаружения, даёт выбрать стартовую
// сцену руками (автоматический выбор по названию запрещён) и запустить интеграцию.
//
// Открывается из Tools → Life From Zero → Integrate Into Game
// или кодом: LifeFromZeroIntegrateWindow.ShowWindow()
//
// ВНИМАНИЕ: в среде репозитория Unity нет, файл не компилировался в Unity.

using UnityEditor;
using UnityEngine;

namespace LifeFromZero.Setup
{
    public sealed class LifeFromZeroIntegrateWindow : EditorWindow
    {
        private DiscoveryReport report;
        private string selectedScene = "";
        private Vector2 sceneScroll;
        private Vector2 reportScroll;
        private Vector2 resultScroll;
        private IntegrationPlan lastPlan;
        private bool showCandidates;
        private MigrationReport migration;
        private MigrationPlan lastMigration;
        private Vector2 migrationScroll;
        private bool showMigration;

        public static void ShowWindow()
        {
            var window = GetWindow<LifeFromZeroIntegrateWindow>(false, "Life From Zero", true);
            window.minSize = new Vector2(720f, 560f);
            window.Refresh();
            window.Show();
        }

        private void OnEnable()
        {
            if (report == null) Refresh();
        }

        private void Refresh()
        {
            try
            {
                report = LifeFromZeroDiscovery.Collect();
                if (string.IsNullOrEmpty(selectedScene))
                    selectedScene = report.ChosenScenePath ?? "";
            }
            catch (System.Exception e)
            {
                report = null;
                Debug.LogError("[LFZ-INTEGRATE] обнаружение не удалось: " + e.Message);
            }
        }

        /// <summary>Квартира №67 и переход Quarter250 → District750 (ТЗ #039, финальное дополнение).</summary>
        private void DrawHomeAndMigration()
        {
            EditorGUILayout.LabelField("Квартира №67 и миграция Quarter250 → District750", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Сцена квартиры (имя без .unity):", GUILayout.Width(240f));
                LifeFromZeroGameIntegration.ApartmentSceneName =
                    EditorGUILayout.TextField(LifeFromZeroGameIntegration.ApartmentSceneName, GUILayout.Width(260f));
            }
            EditorGUILayout.LabelField("Имя сцены квартиры не угадывается: пусто — переход в квартиру выключен.",
                                       EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Привязать квартиру №67 в сцене района"))
                    LifeFromZeroGameIntegration.BindHomeToDistrictScene(false);

                if (GUILayout.Button("Диагностика миграции"))
                {
                    migration = LifeFromZeroMigration.Diagnose();
                    showMigration = true;
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedScene)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("District750 = основной район (dry-run)"))
                        lastMigration = LifeFromZeroMigration.Migrate(selectedScene, true);

                    if (GUILayout.Button("Применить переход на District750"))
                    {
                        bool proceed = EditorUtility.DisplayDialog(
                            "Life From Zero — миграция",
                            "Переключить сцену на District750 как основной район:\n" + selectedScene +
                            "\n\nПеред изменением создаётся резервная копия сцены. Quarter250 не удаляется. Продолжить?",
                            "Переключить", "Отмена");
                        if (proceed) lastMigration = LifeFromZeroMigration.Migrate(selectedScene, false);
                    }
                }
            }

            if (migration != null && showMigration)
            {
                EditorGUILayout.HelpBox("Диагностика: рисков " + migration.Risks.Count +
                                        ", генераторов " + migration.Bootstraps.Count +
                                        ", совпадений в коде " + migration.CodeHits.Count +
                                        (string.IsNullOrEmpty(migration.ReportPath) ? "" : "\nОтчёт: " + migration.ReportPath),
                                        migration.Risks.Count > 0 ? MessageType.Warning : MessageType.Info);
            }

            if (lastMigration != null)
            {
                EditorGUILayout.HelpBox(lastMigration.Summary(), lastMigration.Blocked ? MessageType.Error : MessageType.Info);
                migrationScroll = EditorGUILayout.BeginScrollView(migrationScroll, GUILayout.Height(150f));
                for (int i = 0; i < lastMigration.Blockers.Count; i++)
                    EditorGUILayout.HelpBox("БЛОКЕР: " + lastMigration.Blockers[i], MessageType.Error);
                for (int i = 0; i < lastMigration.Changes.Count; i++)
                    EditorGUILayout.LabelField("• " + lastMigration.Changes[i], EditorStyles.wordWrappedMiniLabel);
                for (int i = 0; i < lastMigration.Findings.Count; i++)
                    EditorGUILayout.LabelField("· " + lastMigration.Findings[i], EditorStyles.wordWrappedMiniLabel);
                for (int i = 0; i < lastMigration.Actions.Count; i++)
                    EditorGUILayout.LabelField((i + 1) + ") " + lastMigration.Actions[i], EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(lastMigration.ReportPath))
                    EditorGUILayout.HelpBox("Отчёт: " + lastMigration.ReportPath, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Интеграция LIFE FROM ZERO в настоящую игру", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Тестовая сцена не является результатом: цель — обычный запуск игры.",
                                       EditorStyles.miniLabel);

            if (report == null)
            {
                EditorGUILayout.HelpBox("Обнаружение не выполнено.", MessageType.Error);
                if (GUILayout.Button("Повторить обнаружение")) Refresh();
                return;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Стартовая сцена (выберите подтверждённую вручную):", GUILayout.Width(340f));
                if (GUILayout.Button("Обновить обнаружение", GUILayout.Width(180f))) Refresh();
            }

            if (!string.IsNullOrEmpty(report.ChosenScenePath))
                EditorGUILayout.HelpBox("Обнаружено автоматически: " + report.ChosenScenePath + "\n" + report.ChosenReason,
                                        MessageType.Info);
            else
                EditorGUILayout.HelpBox("Автоматически определить стартовую сцену не удалось. Выберите её в списке ниже.",
                                        MessageType.Warning);

            for (int i = 0; i < report.Ambiguity.Count; i++)
                EditorGUILayout.HelpBox("Неопределённость: " + report.Ambiguity[i], MessageType.Warning);

            sceneScroll = EditorGUILayout.BeginScrollView(sceneScroll, GUILayout.Height(140f));
            for (int i = 0; i < report.Scenes.Count; i++)
            {
                SceneFacts facts = report.Scenes[i];
                var flags = new System.Text.StringBuilder();
                if (facts.IsPlayModeStart) flags.Append(" [PLAY-START]");
                if (facts.BuildIndex >= 0) flags.Append(" [build ").Append(facts.BuildIndex).Append(facts.BuildEnabled ? "]" : " off]");
                if (facts.InActiveProfile) flags.Append(" [profile ").Append(facts.ActiveProfileIndex).Append("]");
                if (facts.HasCamera) flags.Append(" [camera]");
                if (facts.HasCityBootstrap) flags.Append(" [city]");
                if (facts.HasPauseController) flags.Append(" [pause]");
                if (facts.IsTestScene) flags.Append(" [ТЕСТОВАЯ]");

                bool isSelected = string.Equals(selectedScene, facts.Path, System.StringComparison.OrdinalIgnoreCase);
                string label = facts.Path + flags;
                bool clicked = GUILayout.Toggle(isSelected, label, EditorStyles.toolbarButton);
                if (clicked != isSelected) selectedScene = clicked ? facts.Path : "";
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (string.IsNullOrEmpty(selectedScene))
            {
                EditorGUILayout.HelpBox("Сцена не выбрана.", MessageType.Warning);
            }
            else if (string.Equals(selectedScene, LifeFromZeroSetup.ScenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorGUILayout.HelpBox("Это тестовая сцена: интеграция в неё запрещена.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Будет изменена сцена: " + selectedScene +
                                        "\nПеред изменением автоматически создаются резервные копии вне Assets.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedScene)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Dry-run (ничего не меняет)", GUILayout.Height(26f)))
                        lastPlan = LifeFromZeroGameIntegration.Integrate(selectedScene, true);

                    if (GUILayout.Button("Интегрировать в игру", GUILayout.Height(26f)))
                    {
                        bool proceed = EditorUtility.DisplayDialog(
                            "Life From Zero",
                            "Интегрировать меню паузы и District750 в сцену:\n" + selectedScene +
                            "\n\nБудут созданы резервные копии изменяемых файлов. Продолжить?",
                            "Интегрировать", "Отмена");
                        if (proceed) lastPlan = LifeFromZeroGameIntegration.Integrate(selectedScene, false);
                    }
                }
            }

            EditorGUILayout.Space();
            DrawHomeAndMigration();

            showCandidates = EditorGUILayout.Foldout(showCandidates, "Отчёт обнаружения (полный)");
            if (showCandidates)
            {
                reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.Height(160f));
                EditorGUILayout.TextArea(LifeFromZeroDiscovery.Prettify(report), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }

            if (lastPlan != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(lastPlan.DryRun ? "Результат dry-run" : "Результат интеграции", EditorStyles.boldLabel);
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.Height(180f));
                if (lastPlan.Blockers.Count > 0)
                    for (int i = 0; i < lastPlan.Blockers.Count; i++)
                        EditorGUILayout.HelpBox("БЛОКЕР: " + lastPlan.Blockers[i], MessageType.Error);
                for (int i = 0; i < lastPlan.Changes.Count; i++)
                    EditorGUILayout.LabelField("• " + lastPlan.Changes[i], EditorStyles.wordWrappedMiniLabel);
                if (lastPlan.CandidatesForManualChoice.Count > 0)
                {
                    EditorGUILayout.LabelField("Кандидаты для выбора вручную:", EditorStyles.boldLabel);
                    for (int i = 0; i < lastPlan.CandidatesForManualChoice.Count; i++)
                        EditorGUILayout.LabelField("• " + lastPlan.CandidatesForManualChoice[i], EditorStyles.wordWrappedMiniLabel);
                }
                if (lastPlan.Actions.Count > 0)
                {
                    EditorGUILayout.LabelField("Действия после интеграции:", EditorStyles.boldLabel);
                    for (int i = 0; i < lastPlan.Actions.Count; i++)
                        EditorGUILayout.LabelField((i + 1) + ") " + lastPlan.Actions[i], EditorStyles.wordWrappedMiniLabel);
                }
                if (!string.IsNullOrEmpty(lastPlan.ReportPath))
                    EditorGUILayout.HelpBox("Отчёт: " + lastPlan.ReportPath, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
