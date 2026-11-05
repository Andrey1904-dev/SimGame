// Life From Zero — City/UnityIntegration/Editor/LifeFromZeroSetup.cs (ТЗ #038)
// Namespace: LifeFromZero.Setup
//
// Unity Editor-инструмент: создаёт тестовую сцену District750 + объект меню паузы
// в СУЩЕСТВУЮЩЕМ проекте LifeFromZero (Unity 6000.3.27f1, URP 17.3.0).
//
// Пункт меню:  Tools → Life From Zero → Setup District750 & Pause
// Из командной строки (batchmode):
//   Unity -batchmode -quit -projectPath "<проект>" \
//         -executeMethod LifeFromZero.Setup.LifeFromZeroSetup.SetupBatch
//
// Гарантии безопасности:
//   * сцена Assets/_Project/Scenes/District750IntegrationTest.unity НИКОГДА не
//     перезаписывается: если файл существует, инструмент останавливается;
//   * CityTest.unity, Apartment67 и другие сцены не открываются и не меняются:
//     новая сцена создаётся аддитивно и закрывается после сохранения;
//   * повторный запуск не создаёт дубликатов (второй запуск просто блокируется
//     существующей сценой);
//   * EventSystem создаётся только если его нет в новой сцене; Canvas меню паузы
//     создаёт сам PauseView в Awake, поэтому второй Canvas в сцене не появляется;
//   * фиктивный контроллер персонажа не создаётся — реальные компоненты
//     назначаются в Inspector (см. City/CODEX_EXECUTE.md, шаг 7).
//
// ВНИМАНИЕ: этот файл не компилировался в Unity в среде репозитория (Unity нет).
// Используются только публичные API, существующие в Unity 6000.3 / URP 17.3:
// CityBootstrap.Seed / LayoutId / GenerateOnStart / AddColliders,
// QuarterUnityAdapter.Seed / BuildOnStart / AddColliders,
// PauseController.Configure(...), StandalonePauseIntegration, PauseView (создаётся
// контроллером), UnityEngine.EventSystems.EventSystem.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using LifeFromZero.City.Pause;
using LifeFromZero.City.Quarter.UnityAdapter;

namespace LifeFromZero.Setup
{
    public static class LifeFromZeroSetup
    {
        public const string SceneFolder = "Assets/_Project/Scenes";
        public const string ScenePath = SceneFolder + "/District750IntegrationTest.unity";
        public const int Seed = 1904;
        public const string LayoutId = "district750";

        private const string LogTag = "[LFZ-SETUP]";

        [MenuItem("Tools/Life From Zero/Setup District750 && Pause", false, 0)]
        public static void SetupFromMenu()
        {
            Run(false);
        }

        /// <summary>Точка входа для Unity -executeMethod: ошибки дают ненулевой код выхода.</summary>
        public static void SetupBatch()
        {
            Run(true);
        }

        private static void Run(bool batch)
        {
            Debug.Log(LogTag + " Начало: создание " + ScenePath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || System.IO.File.Exists(ScenePath))
            {
                Debug.LogError(LogTag + " СТОП: сцена уже существует и НЕ будет перезаписана: " + ScenePath +
                               ". Если нужна новая сцена — создайте её вручную с другим именем; " +
                               "если нужно пересоздать — сначала удалите файл сцены осознанно.");
                if (batch) throw new Exception("Scene already exists: " + ScenePath);
                return;
            }

            EnsureFolder(SceneFolder);

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                BuildScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError(LogTag + " Не удалось сохранить сцену: " + ScenePath);
                    if (batch) throw new Exception("Scene save failed: " + ScenePath);
                    return;
                }
            }
            finally
            {
                if (previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.Refresh();
            Debug.Log(LogTag + " RESULT: OK scene=" + ScenePath + " seed=" + Seed + " layout=" + LayoutId);
            Debug.Log(LogTag + " Дальше (ручные шаги, без них интеграция не завершена):");
            Debug.Log(LogTag + "  1) объект Pause → StandalonePauseIntegration → Player Input Behaviours: " +
                               "перетащите реальные компоненты движения и камеры (Tools → Life From Zero → Report input candidates);");
            Debug.Log(LogTag + "  2) Pause → PauseKeyboardInput: Use Standalone Keyboard включён; выключите его, " +
                               "если в проекте уже есть свой роутер Esc;");
            Debug.Log(LogTag + "  3) для реальных данных добавьте наследника PauseDataSource и назначьте в Pause → Data Source.");
        }

        private static void BuildScene(Scene scene)
        {
            // 1. Город: CityBootstrap (генерация) + QuarterUnityAdapter (построение примитивов).
            var city = new GameObject("City");
            var adapter = city.AddComponent<QuarterUnityAdapter>();
            adapter.Seed = Seed;
            adapter.BuildOnStart = false; // генерацией управляет CityBootstrap — двойной генерации не будет
            adapter.AddColliders = true;

            var bootstrap = city.AddComponent<CityBootstrap>();
            bootstrap.Seed = Seed;
            bootstrap.LayoutId = LayoutId;
            bootstrap.GenerateOnStart = true;
            bootstrap.AddColliders = true;

            // 2. Камера обзора сверху (ортографическая, вся карта 750×750 м).
            var cameraObject = new GameObject("District Camera");
            cameraObject.transform.position = new Vector3(375f, 500f, 375f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 380f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f, 1f);
            cameraObject.AddComponent<AudioListener>();
            TrySetTag(cameraObject, "MainCamera");
            // URP добавляет UniversalAdditionalCameraData сам; если тип доступен — добавляем явно.
            TryAddComponentByTypeName(cameraObject, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");

            // 3. Направленный свет.
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.position = new Vector3(375f, 120f, 375f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = new Color(1f, 0.96f, 0.9f, 1f);
            light.shadows = LightShadows.Soft;

            // 4. Меню паузы: один корневой объект. Canvas и UI создаёт PauseView в Awake.
            var pauseObject = new GameObject("Pause");
            var integration = pauseObject.AddComponent<StandalonePauseIntegration>();
            var controller = pauseObject.AddComponent<PauseController>();
            SetObjectReference(controller, "integration", integration);

            // Только физические Esc и P, никаких I/M/J. Включается, если в проекте нет своего роутера.
            var keyboard = TryAddComponentByTypeName(pauseObject, "PauseKeyboardInput");
            if (keyboard != null)
            {
                SetBoolField(keyboard, "useStandaloneKeyboard", true);
            }
            else
            {
                Debug.LogWarning(LogTag + " Тип PauseKeyboardInput не найден (ожидался в Assembly-CSharp). " +
                                 "Esc настраивается вручную: см. шаг 7 в City/CODEX_EXECUTE.md.");
            }

            // 5. EventSystem: ровно один, только если его нет.
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                if (!TryAddComponentByTypeName(eventSystemObject,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule"))
                {
                    eventSystemObject.AddComponent<StandaloneInputModule>();
                }
            }

            Debug.Log(LogTag + " Сцена собрана: City (CityBootstrap + QuarterUnityAdapter), District Camera, " +
                              "Directional Light, Pause (PauseController + StandalonePauseIntegration" +
                              (keyboard != null ? " + PauseKeyboardInput" : "") + "), EventSystem.");
        }

        // ---------------- вспомогательные методы ----------------

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder) ?? "Assets";
            parent = parent.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            string leaf = System.IO.Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SetObjectReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(LogTag + " Поле '" + fieldName + "' не найдено в " + target.GetType().Name +
                               " — назначьте ссылку вручную в Inspector.");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBoolField(UnityEngine.Object target, string fieldName, bool value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning(LogTag + " Поле '" + fieldName + "' не найдено в " + target.GetType().Name +
                                 " — включите опцию вручную в Inspector.");
                return;
            }
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Component TryAddComponentByTypeName(GameObject host, string typeName)
        {
            Type type = FindType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return null;
            return host.AddComponent(type);
        }

        private static Type FindType(string typeName)
        {
            Type direct = Type.GetType(typeName + ", Assembly-CSharp") ?? Type.GetType(typeName);
            if (direct != null) return direct;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type candidate;
                try { candidate = assembly.GetType(typeName); }
                catch (Exception) { continue; }
                if (candidate != null) return candidate;
                if (typeName.IndexOf('.') < 0)
                {
                    // Короткое имя без namespace: ищем по простому имени типа.
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (Exception) { continue; }
                    foreach (var t in types)
                        if (t.Name == typeName) return t;
                }
            }
            return null;
        }

        private static void TrySetTag(GameObject target, string tag)
        {
            try { target.tag = tag; }
            catch (Exception) { Debug.LogWarning(LogTag + " Тег '" + tag + "' недоступен; камера останется без тега."); }
        }

        // ---------------- диагностика для шага 7 ----------------

        [MenuItem("Tools/Life From Zero/Report input candidates", false, 20)]
        public static void ReportInputCandidates()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                Debug.LogWarning(LogTag + " Нет активной сцены для анализа.");
                return;
            }

            string[] hints = { "move", "movement", "walk", "look", "camera", "input", "player", "controller", "motor", "character" };
            var found = new List<string>();
            foreach (GameObject root in active.GetRootGameObjects())
            {
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    string name = behaviour.GetType().Name.ToLowerInvariant();
                    bool candidate = false;
                    foreach (string hint in hints)
                        if (name.Contains(hint)) { candidate = true; break; }

                    if (candidate)
                    {
                        string path = PathOf(behaviour.transform);
                        found.Add("  " + path + "  →  " + behaviour.GetType().FullName +
                                  (behaviour.enabled ? "" : "  (disabled)"));
                    }
                }
            }

            if (found.Count == 0)
            {
                Debug.Log(LogTag + " Кандидатов не найдено в сцене '" + active.name + "'. Откройте игровую сцену " +
                                   "проекта и повторите, либо найдите компоненты вручную.");
                return;
            }

            Debug.Log(LogTag + " Кандидаты для Pause → StandalonePauseIntegration → Player Input Behaviours (" +
                              found.Count + "), сцена '" + active.name + "':\n" + string.Join("\n", found.ToArray()));
            Debug.Log(LogTag + " Назначьте компоненты, которые управляют движением и камерой. Не назначайте " +
                               "PauseController/PauseView/StandalonePauseIntegration/PauseKeyboardInput — они отвергаются.");
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }
            return string.Join("/", parts.ToArray());
        }
    }
}
