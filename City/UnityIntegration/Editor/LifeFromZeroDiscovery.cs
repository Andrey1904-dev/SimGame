// Life From Zero — City/UnityIntegration/Editor/LifeFromZeroDiscovery.cs (ТЗ #039)
// Namespace: LifeFromZero.Setup
//
// ТОЛЬКО ЧТЕНИЕ. Инструмент обнаружения настоящей стартовой сцены LIFE FROM ZERO.
// Ничего не изменяет: не открывает сцены, не сохраняет ассеты, не трогает настройки.
//
// Пункт меню: Tools → Life From Zero → Detect start scene
// Batch:
//   Unity -batchmode -quit -projectPath "<проект>" \
//         -executeMethod LifeFromZero.Setup.LifeFromZeroDiscovery.DetectBatch \
//         -logFile /tmp/lfz-detect.log
//
// Что собирается:
//   1. список всех сцен проекта (AssetDatabase);
//   2. EditorBuildSettings (порядок, enabled, индекс 0 = стартовая сцена сборки);
//   3. активный Build Profile (Unity 6) — через рефлексию, чтобы не зависеть от версии API;
//   4. EditorSceneManager.playModeStartScene — сцена, которая запускается при Play в редакторе;
//   5. переходы между сценами: ссылки в тексте сцен + вызовы SceneManager в коде;
//   6. существующие обработчики Esc и P в коде проекта;
//   7. есть ли в сцене камера, CityBootstrap, PauseController (по GUID скриптов).
//
// Стартовая сцена НЕ выбирается по названию: только по фактическим настройкам проекта.
// Если однозначного кандидата нет — печатаются кандидаты и причины неопределённости.
//
// ВНИМАНИЕ: в среде репозитория Unity нет, файл не компилировался в Unity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LifeFromZero.Setup
{
    public sealed class SceneFacts
    {
        public string Path;
        public string Name;
        public int BuildIndex = -1;          // -1 = не в Build Settings
        public bool BuildEnabled;
        public bool InActiveProfile;
        public int ActiveProfileIndex = -1;
        public bool IsPlayModeStart;
        public bool HasCamera;
        public bool HasCityBootstrap;
        public bool HasPauseController;
        public bool HasDistrictLink;
        public bool IsTestScene;
        public int SizeKb;
        public readonly List<string> ReferencedFrom = new List<string>();
        public readonly List<string> ReferencesTo = new List<string>();
        public readonly List<string> ReferencingScripts = new List<string>();
    }

    public sealed class ProfileScene
    {
        public string Path;
        public bool Enabled;
        public int Index;
    }

    public sealed class CodeFact
    {
        public string File;
        public int Line;
        public string Text;
        public override string ToString() { return File + ":" + Line + "  " + Text; }
    }

    public sealed class DiscoveryReport
    {
        public string ProjectRoot;
        public string Stamp;
        public readonly List<SceneFacts> Scenes = new List<SceneFacts>();
        public readonly List<ProfileScene> ActiveProfileScenes = new List<ProfileScene>();
        public string ActiveBuildProfileName;
        public string ActiveBuildProfileError;
        public string PlayModeStartScenePath;
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Ambiguity = new List<string>();
        public readonly List<CodeFact> EscapeHandlers = new List<CodeFact>();
        public readonly List<CodeFact> PhoneHandlers = new List<CodeFact>();
        public readonly List<CodeFact> SceneLoadCalls = new List<CodeFact>();
        public string ChosenScenePath;
        public string ChosenReason;
        public string ReportPath;

        public SceneFacts Find(string path)
        {
            for (int i = 0; i < Scenes.Count; i++)
                if (string.Equals(Scenes[i].Path, path, StringComparison.OrdinalIgnoreCase)) return Scenes[i];
            return null;
        }
    }

    public static class LifeFromZeroDiscovery
    {
        internal const string LogTag = "[LFZ-DISCOVERY]";
        public const string OutputFolder = "LifeFromZeroIntegration";
        public const string CityBootstrapScript = "Assets/City/UnityAdapter/CityBootstrap.cs";
        public const string PauseControllerScript = "Assets/City/PauseMenu/PauseController.cs";
        public const string DistrictLinkScript = "Assets/City/UnityIntegration/Runtime/District750Link.cs";

        [MenuItem("Tools/Life From Zero/Detect start scene", false, 20)]
        public static void DetectFromMenu()
        {
            var report = Collect();
            Debug.Log(LogTag + " " + Prettify(report));
            Debug.Log(LogTag + " Отчёт записан: " + report.ReportPath);
        }

        /// <summary>Точка входа для -executeMethod. Ничего не меняет, всегда завершается успешно.</summary>
        public static void DetectBatch()
        {
            var report = Collect();
            Debug.Log(LogTag + " " + Prettify(report));
            Debug.Log(LogTag + " RESULT: OK report=" + report.ReportPath);
        }

        // ===================== сбор данных =====================

        public static DiscoveryReport Collect()
        {
            var report = new DiscoveryReport
            {
                ProjectRoot = ProjectRoot(),
                Stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture),
            };

            List<string> scenePaths = AllScenePaths();
            var textByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string text = SafeRead(scenePaths[i]);
                textByPath[scenePaths[i]] = text ?? string.Empty;
            }

            // 1. Build Settings (классический список сборки).
            var buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene entry = buildScenes[i];
                if (entry == null || string.IsNullOrEmpty(entry.path)) continue;
                if (!textByPath.ContainsKey(entry.path))
                {
                    textByPath[entry.path] = SafeRead(entry.path) ?? string.Empty;
                    scenePaths.Add(entry.path);
                }
                SceneFacts facts = GetOrAdd(report, entry.path);
                facts.BuildIndex = i;
                facts.BuildEnabled = entry.enabled;
            }

            // 2. Активный Build Profile (Unity 6) и его список сцен.
            ReadActiveBuildProfile(report);
            for (int i = 0; i < report.ActiveProfileScenes.Count; i++)
            {
                ProfileScene scene = report.ActiveProfileScenes[i];
                if (string.IsNullOrEmpty(scene.Path)) continue;
                if (!textByPath.ContainsKey(scene.Path))
                {
                    textByPath[scene.Path] = SafeRead(scene.Path) ?? string.Empty;
                    scenePaths.Add(scene.Path);
                }
                SceneFacts facts = GetOrAdd(report, scene.Path);
                facts.InActiveProfile = true;
                facts.ActiveProfileIndex = scene.Index;
            }

            // 3. Сцена, запускаемая при Play в редакторе.
            try
            {
                SceneAsset playStart = EditorSceneManager.playModeStartScene;
                if (playStart != null)
                {
                    string path = AssetDatabase.GetAssetPath(playStart);
                    if (!string.IsNullOrEmpty(path)) report.PlayModeStartScenePath = path;
                }
            }
            catch (Exception e)
            {
                report.Notes.Add("playModeStartScene не прочитан: " + e.Message);
            }

            // 4. Факты по каждой сцене (по тексту файла, без открытия сцены).
            string cityGuid = ScriptGuid(CityBootstrapScript);
            string pauseGuid = ScriptGuid(PauseControllerScript);
            string linkGuid = ScriptGuid(DistrictLinkScript);
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                SceneFacts facts = GetOrAdd(report, path);
                string text = textByPath.TryGetValue(path, out string t) ? t : string.Empty;
                facts.IsPlayModeStart = string.Equals(path, report.PlayModeStartScenePath, StringComparison.OrdinalIgnoreCase);
                facts.HasCamera = Regex.IsMatch(text, @"(?m)^Camera:\s*$");
                facts.HasCityBootstrap = HasScript(text, cityGuid);
                facts.HasPauseController = HasScript(text, pauseGuid);
                facts.HasDistrictLink = HasScript(text, linkGuid);
                facts.IsTestScene = string.Equals(path, LifeFromZeroSetup.ScenePath, StringComparison.OrdinalIgnoreCase);
                facts.SizeKb = EstimateSizeKb(text);
            }

            // 5. Переходы между сценами (эвристика по тексту сцен).
            for (int i = 0; i < report.Scenes.Count; i++)
            {
                SceneFacts from = report.Scenes[i];
                string text = textByPath.TryGetValue(from.Path, out string t) ? t : string.Empty;
                if (string.IsNullOrEmpty(text)) continue;
                for (int j = 0; j < report.Scenes.Count; j++)
                {
                    SceneFacts to = report.Scenes[j];
                    if (ReferenceEquals(from, to)) continue;
                    if (to.Name.Length < 4) continue;
                    if (text.IndexOf("\"" + to.Name + "\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("m_SceneName: " + to.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        from.ReferencesTo.Add(to.Path);
                        to.ReferencedFrom.Add(from.Path);
                    }
                }
            }

            // 6. Код: SceneManager, Esc, P.
            ScanCode(report, scenePaths);

            // 7. Решение о стартовой сцене.
            ChooseStartScene(report);
            report.ReportPath = WriteReport(report);
            return report;
        }

        private static void ChooseStartScene(DiscoveryReport report)
        {
            SceneFacts playMode = report.Find(report.PlayModeStartScenePath);

            SceneFacts buildZero = null;
            for (int i = 0; i < report.Scenes.Count; i++)
                if (report.Scenes[i].BuildIndex == 0 && report.Scenes[i].BuildEnabled) { buildZero = report.Scenes[i]; break; }

            SceneFacts profileZero = null;
            for (int i = 0; i < report.Scenes.Count; i++)
                if (report.Scenes[i].InActiveProfile && report.Scenes[i].ActiveProfileIndex == 0) { profileZero = report.Scenes[i]; break; }

            if (playMode != null)
            {
                report.ChosenScenePath = playMode.Path;
                report.ChosenReason = "EditorSceneManager.playModeStartScene — именно это запускается по Play в редакторе";
            }
            else if (buildZero != null)
            {
                report.ChosenScenePath = buildZero.Path;
                report.ChosenReason = "индекс 0 в Build Settings (классический список сборки)";
            }
            else if (profileZero != null && !string.IsNullOrEmpty(profileZero.Path) && profileZero.InActiveProfile)
            {
                report.ChosenScenePath = profileZero.Path;
                report.ChosenReason = "индекс 0 в активном Build Profile '" + report.ActiveBuildProfileName + "'";
            }

            // Причины неопределённости: разные источники указывают на разные сцены.
            var strongSignals = new List<string>();
            if (playMode != null) strongSignals.Add("playModeStartScene: " + playMode.Path);
            if (buildZero != null) strongSignals.Add("Build Settings[0]: " + buildZero.Path);
            if (profileZero != null && !string.IsNullOrEmpty(profileZero.Path)) strongSignals.Add("Build Profile[0]: " + profileZero.Path);

            if (strongSignals.Count > 1)
            {
                bool conflict = false;
                for (int i = 1; i < strongSignals.Count; i++)
                    if (!string.Equals(strongSignals[i].Substring(strongSignals[i].IndexOf(':') + 2),
                                       strongSignals[0].Substring(strongSignals[0].IndexOf(':') + 2),
                                       StringComparison.OrdinalIgnoreCase)) conflict = true;
                if (conflict)
                {
                    for (int i = 0; i < strongSignals.Count; i++)
                        report.Ambiguity.Add("источники расходятся — " + strongSignals[i]);
                    report.Ambiguity.Add("выбрано по приоритету playModeStartScene → Build Settings[0] → Build Profile[0]; подтвердите сцену вручную");
                }
            }

            if (string.IsNullOrEmpty(report.ChosenScenePath))
            {
                report.Ambiguity.Add("нет ни playModeStartScene, ни сцен в Build Settings/активном Build Profile");
                report.Ambiguity.Add("выберите стартовую сцену в списке ниже вручную: автоматический выбор по названию запрещён");
            }

            SceneFacts chosen = report.Find(report.ChosenScenePath);
            if (chosen != null)
            {
                if (!chosen.HasCamera)
                    report.Notes.Add("в выбранной сцене нет Camera (по тексту сцены) — проверьте, кто рендерит картинку");
                if (chosen.HasCityBootstrap)
                    report.Notes.Add("в выбранной сцене уже есть CityBootstrap — город уже подключён к этой сцене");
                if (chosen.HasPauseController)
                    report.Notes.Add("в выбранной сцене уже есть PauseController — меню паузы уже добавлено");
                if (chosen.ReferencesTo.Count > 0)
                    report.Notes.Add("сцена ссылается на: " + string.Join(", ", chosen.ReferencesTo.ToArray()));
                if (chosen.ReferencedFrom.Count > 0)
                    report.Notes.Add("на сцену ссылаются: " + string.Join(", ", chosen.ReferencedFrom.ToArray()));
                if (chosen.ReferencedFrom.Count == 0 && chosen.ReferencesTo.Count == 0)
                    report.Notes.Add("связей с другими сценами в тексте не найдено — переходы выполняются из кода или отсутствуют");
            }

            if (report.EscapeHandlers.Count > 0)
                report.Notes.Add("в коде уже читается Esc (" + report.EscapeHandlers.Count +
                                 " мест) — при интеграции PauseKeyboardInput будет ВЫКЛЮЧЕН, чтобы не было двойной обработки");
            else
                report.Notes.Add("обработчиков Esc в коде проекта не найдено — можно включить PauseKeyboardInput");

            if (report.SceneLoadCalls.Count == 0)
                report.Notes.Add("вызовов SceneManager.LoadScene* в коде не найдено: переходы, возможно, реализованы иначе");
        }

        // ===================== Build Profile (рефлексия) =====================

        private static void ReadActiveBuildProfile(DiscoveryReport report)
        {
            try
            {
                Type profileType = LifeFromZeroSetup.FindType("UnityEditor.Build.Profile.BuildProfile");
                if (profileType == null)
                {
                    report.ActiveBuildProfileError = "тип UnityEditor.Build.Profile.BuildProfile не найден (Unity младше 6.0)";
                    return;
                }

                object profile = null;
                MethodInfo getActive = profileType.GetMethod("GetActiveBuildProfile", BindingFlags.Public | BindingFlags.Static);
                if (getActive != null) profile = getActive.Invoke(null, null);
                if (profile == null)
                {
                    PropertyInfo active = profileType.GetProperty("activeBuildProfile", BindingFlags.Public | BindingFlags.Static);
                    if (active != null) profile = active.GetValue(null);
                }
                if (profile == null)
                {
                    report.ActiveBuildProfileError = "активный Build Profile не выбран";
                    return;
                }

                PropertyInfo nameProperty = profileType.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                report.ActiveBuildProfileName = nameProperty != null ? nameProperty.GetValue(profile) as string : profileType.Name;

                PropertyInfo scenesProperty = profileType.GetProperty("scenes", BindingFlags.Public | BindingFlags.Instance);
                if (scenesProperty == null)
                {
                    report.ActiveBuildProfileError = "у Build Profile нет свойства scenes";
                    return;
                }
                var entries = scenesProperty.GetValue(profile) as System.Collections.IEnumerable;
                if (entries == null)
                {
                    report.ActiveBuildProfileError = "Build Profile.scenes == null";
                    return;
                }

                int index = 0;
                foreach (object entry in entries)
                {
                    if (entry == null) { index++; continue; }
                    report.ActiveProfileScenes.Add(new ProfileScene
                    {
                        Path = ReadScenePath(entry),
                        Enabled = ReadSceneEnabled(entry),
                        Index = index,
                    });
                    index++;
                }
            }
            catch (Exception e)
            {
                report.ActiveBuildProfileError = e.GetType().Name + ": " + e.Message;
            }
        }

        private static string ReadScenePath(object entry)
        {
            var settingsScene = entry as EditorBuildSettingsScene;
            if (settingsScene != null) return settingsScene.path;

            Type type = entry.GetType();
            PropertyInfo pathProperty = type.GetProperty("path", BindingFlags.Public | BindingFlags.Instance)
                                        ?? type.GetProperty("scenePath", BindingFlags.Public | BindingFlags.Instance);
            if (pathProperty != null)
            {
                string value = pathProperty.GetValue(entry) as string;
                if (!string.IsNullOrEmpty(value)) return value;
            }
            PropertyInfo guidProperty = type.GetProperty("guid", BindingFlags.Public | BindingFlags.Instance);
            if (guidProperty != null)
            {
                object guidValue = guidProperty.GetValue(entry);
                string guid = guidValue != null ? guidValue.ToString() : null;
                if (!string.IsNullOrEmpty(guid)) return AssetDatabase.GUIDToAssetPath(guid);
            }
            return null;
        }

        private static bool ReadSceneEnabled(object entry)
        {
            var settingsScene = entry as EditorBuildSettingsScene;
            if (settingsScene != null) return settingsScene.enabled;
            PropertyInfo enabledProperty = entry.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabledProperty == null) return true;
            object value = enabledProperty.GetValue(entry);
            return value is bool b ? b : true;
        }

        // ===================== код проекта =====================

        private static void ScanCode(DiscoveryReport report, List<string> scenePaths)
        {
            string assets = Application.dataPath;
            if (!Directory.Exists(assets)) return;

            string[] files;
            try { files = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories); }
            catch (Exception e)
            {
                report.Notes.Add("чтение Assets/*.cs не удалось: " + e.Message);
                return;
            }

            const int limitPerPattern = 40;
            for (int i = 0; i < files.Length; i++)
            {
                string full = files[i];
                string normalized = full.Replace('\\', '/');
                // Свои модули не считаем «существующим обработчиком»: пауза сама читает Esc.
                if (normalized.IndexOf("/Assets/City/", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string[] lines;
                try { lines = File.ReadAllLines(full); }
                catch (Exception) { continue; }

                string relative = "Assets/" + normalized.Substring(normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) + 8);
                for (int line = 0; line < lines.Length; line++)
                {
                    string text = lines[line];
                    if (text.IndexOf("//", StringComparison.Ordinal) >= 0)
                    {
                        int comment = text.IndexOf("//", StringComparison.Ordinal);
                        text = text.Substring(0, comment);
                    }
                    if (text.Trim().Length == 0) continue;

                    if (report.EscapeHandlers.Count < limitPerPattern && LooksLikeEscape(text))
                        report.EscapeHandlers.Add(NewFact(relative, line + 1, text));
                    if (report.PhoneHandlers.Count < limitPerPattern && LooksLikePhone(text))
                        report.PhoneHandlers.Add(NewFact(relative, line + 1, text));
                    if (report.SceneLoadCalls.Count < limitPerPattern && LooksLikeSceneLoad(text))
                        report.SceneLoadCalls.Add(NewFact(relative, line + 1, text));
                }
            }

            // В какой сцене лежит скрипт, выполняющий переход — подсказка для отчёта.
            // Каждая сцена читается один раз (а не по разу на каждый вызов LoadScene).
            if (report.SceneLoadCalls.Count == 0) return;
            for (int s = 0; s < scenePaths.Count; s++)
            {
                SceneFacts facts = report.Find(scenePaths[s]);
                if (facts == null) continue;
                string sceneText = SafeRead(facts.Path);
                if (string.IsNullOrEmpty(sceneText)) continue;
                for (int i = 0; i < report.SceneLoadCalls.Count; i++)
                {
                    string script = report.SceneLoadCalls[i].File;
                    if (facts.ReferencingScripts.Contains(script)) continue;
                    string guid = AssetDatabase.AssetPathToGUID(script);
                    if (!string.IsNullOrEmpty(guid) &&
                        sceneText.IndexOf("guid: " + guid, StringComparison.OrdinalIgnoreCase) >= 0)
                        facts.ReferencingScripts.Add(script);
                }
            }
        }

        private static CodeFact NewFact(string file, int line, string text)
        {
            return new CodeFact { File = file, Line = line, Text = text.Trim() };
        }

        private static bool LooksLikeEscape(string text)
        {
            return text.IndexOf("KeyCode.Escape", StringComparison.Ordinal) >= 0
                   || text.IndexOf("escapeKey", StringComparison.OrdinalIgnoreCase) >= 0
                   || text.IndexOf("\"Cancel\"", StringComparison.Ordinal) >= 0;
        }

        private static bool LooksLikePhone(string text)
        {
            return text.IndexOf("KeyCode.P", StringComparison.Ordinal) >= 0
                   || text.IndexOf("pKey", StringComparison.Ordinal) >= 0
                   || text.IndexOf("phoneKey", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool LooksLikeSceneLoad(string text)
        {
            return text.IndexOf("SceneManager.LoadScene", StringComparison.Ordinal) >= 0
                   || text.IndexOf("LoadSceneAsync(", StringComparison.Ordinal) >= 0;
        }

        // ===================== вспомогательное =====================

        public static List<string> AllScenePaths()
        {
            var result = new List<string>();
            string[] guids;
            try { guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }); }
            catch (Exception) { return result; }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                if (result.Contains(path)) continue;
                result.Add(path);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static SceneFacts GetOrAdd(DiscoveryReport report, string path)
        {
            SceneFacts existing = report.Find(path);
            if (existing != null) return existing;
            var facts = new SceneFacts
            {
                Path = path,
                Name = Path.GetFileNameWithoutExtension(path),
            };
            report.Scenes.Add(facts);
            return facts;
        }

        private static string ScriptGuid(string assetPath)
        {
            // assetPath задан относительно корня проекта ("Assets/..."), а File.Exists
            // зависит от текущего каталога процесса — работаем с абсолютным путём.
            string absolute = Path.Combine(ProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute)) return null;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return string.IsNullOrEmpty(guid) ? null : guid;
        }

        private static bool HasScript(string sceneText, string guid)
        {
            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(sceneText)) return false;
            return sceneText.IndexOf("guid: " + guid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception) { return null; }
        }

        private static int EstimateSizeKb(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Mathf.Max(1, text.Length / 1024);
        }

        public static string ProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        // ===================== отчёты =====================

        public static string Prettify(DiscoveryReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Обнаружение стартовой сцены LIFE FROM ZERO");
            sb.AppendLine("  проект: " + report.ProjectRoot);
            sb.AppendLine("  сцен в проекте: " + report.Scenes.Count);
            sb.AppendLine("  playModeStartScene: " + (string.IsNullOrEmpty(report.PlayModeStartScenePath) ? "не задано" : report.PlayModeStartScenePath));
            sb.AppendLine("  активный Build Profile: " +
                          (!string.IsNullOrEmpty(report.ActiveBuildProfileName) ? report.ActiveBuildProfileName : "нет") +
                          (string.IsNullOrEmpty(report.ActiveBuildProfileError) ? "" : " (" + report.ActiveBuildProfileError + ")"));

            sb.AppendLine("  Сцены:");
            for (int i = 0; i < report.Scenes.Count; i++)
            {
                SceneFacts facts = report.Scenes[i];
                var flags = new List<string>();
                if (facts.IsPlayModeStart) flags.Add("PLAY-START");
                if (facts.BuildIndex >= 0) flags.Add("build[" + facts.BuildIndex + "]" + (facts.BuildEnabled ? "" : " disabled"));
                if (facts.InActiveProfile) flags.Add("profile[" + facts.ActiveProfileIndex + "]");
                if (facts.HasCamera) flags.Add("camera");
                if (facts.HasCityBootstrap) flags.Add("CityBootstrap");
                if (facts.HasPauseController) flags.Add("PauseController");
                if (facts.HasDistrictLink) flags.Add("District750Link");
                if (facts.IsTestScene) flags.Add("ТЕСТОВАЯ-СЦЕНА");
                if (facts.ReferencedFrom.Count > 0) flags.Add("← " + facts.ReferencedFrom.Count);
                if (facts.ReferencesTo.Count > 0) flags.Add("→ " + facts.ReferencesTo.Count);
                sb.AppendLine("    " + facts.Path + "  [" + string.Join(", ", flags.ToArray()) + "]");
            }

            sb.AppendLine("  Стартовая сцена: " + (string.IsNullOrEmpty(report.ChosenScenePath) ? "НЕ ОПРЕДЕЛЕНА" : report.ChosenScenePath));
            if (!string.IsNullOrEmpty(report.ChosenReason)) sb.AppendLine("    причина: " + report.ChosenReason);
            for (int i = 0; i < report.Ambiguity.Count; i++) sb.AppendLine("    неопределённость: " + report.Ambiguity[i]);
            for (int i = 0; i < report.Notes.Count; i++) sb.AppendLine("    замечание: " + report.Notes[i]);

            sb.AppendLine("  Обработчики Esc в коде проекта: " + report.EscapeHandlers.Count);
            for (int i = 0; i < Mathf.Min(10, report.EscapeHandlers.Count); i++) sb.AppendLine("    " + report.EscapeHandlers[i]);
            sb.AppendLine("  Обработчики P (телефон): " + report.PhoneHandlers.Count);
            for (int i = 0; i < Mathf.Min(10, report.PhoneHandlers.Count); i++) sb.AppendLine("    " + report.PhoneHandlers[i]);
            sb.AppendLine("  Переходы SceneManager в коде: " + report.SceneLoadCalls.Count);
            for (int i = 0; i < Mathf.Min(10, report.SceneLoadCalls.Count); i++) sb.AppendLine("    " + report.SceneLoadCalls[i]);
            return sb.ToString();
        }

        public static string WriteReport(DiscoveryReport report)
        {
            try
            {
                string folder = Path.Combine(report.ProjectRoot, OutputFolder, "reports");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "discovery-" + report.Stamp + ".md");

                var sb = new StringBuilder();
                sb.AppendLine("# Обнаружение стартовой сцены — " + report.Stamp);
                sb.AppendLine();
                sb.AppendLine("* проект: `" + report.ProjectRoot + "`");
                sb.AppendLine("* сцен: " + report.Scenes.Count);
                sb.AppendLine("* playModeStartScene: " + (string.IsNullOrEmpty(report.PlayModeStartScenePath) ? "не задано" : "`" + report.PlayModeStartScenePath + "`"));
                sb.AppendLine("* активный Build Profile: " + (!string.IsNullOrEmpty(report.ActiveBuildProfileName) ? report.ActiveBuildProfileName : "нет") +
                              (string.IsNullOrEmpty(report.ActiveBuildProfileError) ? "" : " — " + report.ActiveBuildProfileError));
                sb.AppendLine();
                sb.AppendLine("## Стартовая сцена");
                sb.AppendLine();
                sb.AppendLine(string.IsNullOrEmpty(report.ChosenScenePath)
                    ? "**НЕ ОПРЕДЕЛЕНА.** Выберите вручную из списка ниже."
                    : "**" + report.ChosenScenePath + "** — " + report.ChosenReason);
                for (int i = 0; i < report.Ambiguity.Count; i++) sb.AppendLine("* неопределённость: " + report.Ambiguity[i]);
                for (int i = 0; i < report.Notes.Count; i++) sb.AppendLine("* " + report.Notes[i]);

                sb.AppendLine();
                sb.AppendLine("## Сцены проекта");
                sb.AppendLine();
                sb.AppendLine("| Сцена | Build | Profile | Play | Camera | CityBootstrap | PauseController | → переходы | ← ссылки |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
                for (int i = 0; i < report.Scenes.Count; i++)
                {
                    SceneFacts facts = report.Scenes[i];
                    sb.AppendLine("| `" + facts.Path + "` | " +
                                  (facts.BuildIndex >= 0 ? facts.BuildIndex + (facts.BuildEnabled ? "" : " off") : "—") + " | " +
                                  (facts.InActiveProfile ? facts.ActiveProfileIndex.ToString(CultureInfo.InvariantCulture) : "—") + " | " +
                                  (facts.IsPlayModeStart ? "да" : "—") + " | " +
                                  (facts.HasCamera ? "да" : "—") + " | " +
                                  (facts.HasCityBootstrap ? "да" : "—") + " | " +
                                  (facts.HasPauseController ? "да" : "—") + " | " +
                                  facts.ReferencesTo.Count + " | " + facts.ReferencedFrom.Count + " |");
                }

                sb.AppendLine();
                sb.AppendLine("## Переходы (эвристика по тексту сцен и коду)");
                sb.AppendLine();
                for (int i = 0; i < report.Scenes.Count; i++)
                {
                    SceneFacts facts = report.Scenes[i];
                    if (facts.ReferencesTo.Count == 0 && facts.ReferencingScripts.Count == 0) continue;
                    sb.AppendLine("* `" + facts.Path + "`");
                    for (int j = 0; j < facts.ReferencesTo.Count; j++) sb.AppendLine("  * ссылается на `" + facts.ReferencesTo[j] + "`");
                    for (int j = 0; j < facts.ReferencingScripts.Count; j++) sb.AppendLine("  * выполняет код перехода: `" + facts.ReferencingScripts[j] + "`");
                }

                sb.AppendLine();
                sb.AppendLine("## Обработчики Esc в коде проекта (" + report.EscapeHandlers.Count + ")");
                sb.AppendLine();
                for (int i = 0; i < report.EscapeHandlers.Count; i++) sb.AppendLine("* `" + report.EscapeHandlers[i] + "`");
                sb.AppendLine();
                sb.AppendLine("## Обработчики P / телефона (" + report.PhoneHandlers.Count + ")");
                sb.AppendLine();
                for (int i = 0; i < report.PhoneHandlers.Count; i++) sb.AppendLine("* `" + report.PhoneHandlers[i] + "`");
                sb.AppendLine();
                sb.AppendLine("## Вызовы SceneManager (" + report.SceneLoadCalls.Count + ")");
                sb.AppendLine();
                for (int i = 0; i < report.SceneLoadCalls.Count; i++) sb.AppendLine("* `" + report.SceneLoadCalls[i] + "`");

                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogTag + " не удалось записать отчёт: " + e.Message);
                return "(отчёт не записан)";
            }
        }
    }
}
