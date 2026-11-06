// Compile-only stubs for UnityEditor API used by UI/Editor. Not shipped.
using System;
using UnityEngine;
namespace UnityEditor
{
    public class MenuItem : Attribute { public MenuItem(string path, bool validate = false, int priority = 0) {} }
    public static class EditorUtility {
        public static bool DisplayDialog(string title, string message, string ok) => true;
    }
    public static class AssetDatabase { public static void Refresh() {} }
    public class EditorBuildSettingsScene { public bool enabled; public string path; }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes => new EditorBuildSettingsScene[0]; }
    public static class EditorApplication { public static bool isBatchMode => true; public static void Exit(int code) {} }
}
namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
    public static class EditorSceneManager {
        public static Scene OpenScene(string path, OpenSceneMode mode) => new Scene();
        public static bool SaveScene(Scene scene, string path) => true;
        public static void MarkSceneDirty(Scene scene) {}
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => new Scene();
        public static bool CloseScene(Scene scene, bool removeScene) => true;
    }
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
}
